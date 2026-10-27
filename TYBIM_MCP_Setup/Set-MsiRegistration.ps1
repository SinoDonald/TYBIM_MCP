param([ValidateSet('2024','2025','2026')][string]$RevitVersion='2025')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$version=(Get-Content (Join-Path $root 'server/package.json') -Raw | ConvertFrom-Json).version
$msi=Join-Path $PSScriptRoot "Release/TYBIM_MCP_Revit${RevitVersion}_$version.msi"
$installer=New-Object -ComObject WindowsInstaller.Installer
$database=$installer.OpenDatabase($msi,1)
function Sql([string]$query){$view=$database.OpenView($query);try{[void]$view.Execute()}finally{[void]$view.Close();[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)}}
# VSI can emit the registration component/custom action while dropping its
# custom root directory (observed on the newly generated 2026 setup project).
$directoryView=$database.OpenView('SELECT `Directory` FROM `Directory` WHERE `Directory`=''TYBIMREGISTERDIR''')
[void]$directoryView.Execute();$directoryRecord=$directoryView.Fetch()
[void]$directoryView.Close();[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($directoryView)
if($null -eq $directoryRecord){
 Sql 'INSERT INTO `Directory` (`Directory`,`Directory_Parent`,`DefaultDir`) VALUES (''TYBIMREGISTERDIR'',''TARGETDIR'',''.'')'
}
# Use standard MSI registration. No parallel hand-written Uninstall registry key.
foreach($name in @('ARPSYSTEMCOMPONENT','ARPNOREMOVE','ALLUSERS')){
 Sql "DELETE FROM ``Property`` WHERE ``Property``='$name'"
}
foreach($entry in @{ARPNOMODIFY='1';ARPNOREPAIR='1';MSIINSTALLPERUSER='1'}.GetEnumerator()){
 Sql "DELETE FROM ``Property`` WHERE ``Property``='$($entry.Key)'"
 Sql "INSERT INTO ``Property`` (``Property``,``Value``) VALUES ('$($entry.Key)','$($entry.Value)')"
}
# Remove runtime-generated files only on a real uninstall, never while an old
# product is being removed by a major upgrade after the new files were installed.
# No wildcards are authored for Addins/<year> or the shared TYBIM_MCP root.
function ReadRows([string]$query,[string[]]$columns){
 $view=$database.OpenView($query);[void]$view.Execute()
 try {while($null -ne ($row=$view.Fetch())){
  $item=@{};for($i=0;$i -lt $columns.Count;$i++){$item[$columns[$i]]=$row.StringData($i+1)}
  [pscustomobject]$item
 }}finally{[void]$view.Close();[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)}
}
$directories=@{};foreach($row in (ReadRows 'SELECT `Directory`,`Directory_Parent`,`DefaultDir` FROM `Directory`' @('Id','Parent','Name'))){$directories[$row.Id]=$row}
$nativeFile=ReadRows 'SELECT `Component_`,`FileName` FROM `File`' @('Component','Name') | Where-Object {($_.Name.Split('|') | Select-Object -Last 1) -eq 'Tybim.Revit.Addin.dll'} | Select-Object -First 1
if(!$nativeFile){throw 'Missing native component for cleanup'}
try {Sql 'CREATE TABLE `RemoveFile` (`FileKey` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255) LOCALIZABLE, `DirProperty` CHAR(72) NOT NULL, `InstallMode` SHORT NOT NULL PRIMARY KEY `FileKey`)'} catch {
 # Existing VSI RemoveFile table is retained; verify it can actually be queried.
 $null=ReadRows 'SELECT `FileKey` FROM `RemoveFile`' @('Key')
}
foreach($row in (ReadRows 'SELECT `FileKey` FROM `RemoveFile`' @('Key'))){
 if($row.Key.StartsWith('TYBIM_Files') -or $row.Key.StartsWith('TYBIM_Dir')){Sql "DELETE FROM ``RemoveFile`` WHERE ``FileKey``='$($row.Key)'"}
}
foreach($row in (ReadRows 'SELECT `Action` FROM `CustomAction`' @('Action'))){
 if($row.Action.StartsWith('TYBIM_SetClean')){
  Sql "DELETE FROM ``InstallExecuteSequence`` WHERE ``Action``='$($row.Action)'"
  Sql "DELETE FROM ``CustomAction`` WHERE ``Action``='$($row.Action)'"
 }
}
$index=0
foreach($directory in $directories.Values){
 $parts=@();$id=$directory.Id;$seen=@{}
 while($id -ne 'TARGETDIR' -and $id -ne 'TYBIMREGISTERDIR' -and $directories.ContainsKey($id)){
  if($seen[$id]){throw 'Circular MSI directory'};$seen[$id]=$true
  $entry=$directories[$id];$name=($entry.Name.Split(':')[0].Split('|') | Select-Object -Last 1)
  if($name -ne '.'){$parts=@($name)+$parts};$id=$entry.Parent
 }
 if($id -ne 'TARGETDIR' -or !$parts.Count -or $parts[0] -ne $RevitVersion){continue}
 $index++;if($index -gt 1600){throw 'Cleanup action sequence would overlap RemoveFiles'}
 $property="TYBIMCLEAN$index";$action="TYBIM_SetClean$index";$component=$nativeFile.Component
 Sql "INSERT INTO ``CustomAction`` (``Action``,``Type``,``Source``,``Target``) VALUES ('$action',51,'$property','[$($directory.Id)]')"
 $condition='REMOVE~="ALL" AND NOT UPGRADINGPRODUCTCODE'
 Sql "INSERT INTO ``InstallExecuteSequence`` (``Action``,``Condition``,``Sequence``) VALUES ('$action','$condition',$([int](1800+$index)))"
 Sql "INSERT INTO ``RemoveFile`` (``FileKey``,``Component_``,``FileName``,``DirProperty``,``InstallMode``) VALUES ('TYBIM_Files$index','$component','*','$property',2)"
 Sql "INSERT INTO ``RemoveFile`` (``FileKey``,``Component_``,``DirProperty``,``InstallMode``) VALUES ('TYBIM_Dir$index','$component','$property',2)"
}
if($index -eq 0){throw 'No owned year directory for cleanup'}
[void]$database.Commit()
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer)
Write-Host "MSI registration enabled: TYBIM_MCP for Revit $RevitVersion ($version)"
