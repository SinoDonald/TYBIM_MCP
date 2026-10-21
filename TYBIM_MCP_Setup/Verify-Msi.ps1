param([ValidateSet('2024','2025')][string[]]$RevitVersion=@('2024','2025'))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$version=(Get-Content (Join-Path $root 'server/package.json') -Raw | ConvertFrom-Json).version
$installer=New-Object -ComObject WindowsInstaller.Installer
foreach($year in $RevitVersion){
 $msi=Join-Path $PSScriptRoot "Release/TYBIM_MCP_Revit${year}_$version.msi"
 if(!(Test-Path -LiteralPath $msi)){throw "Missing MSI: $msi"}
 $db=$installer.OpenDatabase($msi,0) # Read-only: does not install/register anything.
 function Rows([string]$sql,[string[]]$columns){
  $view=$db.OpenView($sql);[void]$view.Execute()
  try {while($null -ne ($record=$view.Fetch())){
   $data=[ordered]@{};for($i=0;$i -lt $columns.Count;$i++){$data[$columns[$i]]=$record.StringData($i+1)}
   [pscustomobject]$data
  }}finally{[void]$view.Close()}
 }
 $properties=@{};foreach($r in (Rows 'SELECT `Property`,`Value` FROM `Property`' @('Name','Value'))){$properties[$r.Name]=$r.Value}
 if($properties.ProductVersion -ne $version){throw 'Wrong product version'}
 if($properties.ALLUSERS -eq '1'){throw 'MSI must install for the current user'}
 $actions=@(Rows 'SELECT `Source`,`Target` FROM `CustomAction`' @('Source','Target'))
 $payloadRoot="[AppDataFolder]Autodesk\Revit\Addins\TYBIM_MCP"
 $registrationRoot="[AppDataFolder]Autodesk\Revit\Addins\$year"
 if(!($actions | Where-Object {$_.Source -eq 'TARGETDIR' -and $_.Target -eq $payloadRoot})){throw 'Incorrect payload root'}
 if(!($actions | Where-Object {$_.Source -eq 'TYBIMREGISTERDIR' -and $_.Target -eq $registrationRoot})){throw 'Incorrect registration root'}
 $directories=@{};foreach($r in (Rows 'SELECT `Directory`,`Directory_Parent`,`DefaultDir` FROM `Directory`' @('Id','Parent','Name'))){$directories[$r.Id]=$r}
 $components=@{};foreach($r in (Rows 'SELECT `Component`,`Directory_` FROM `Component`' @('Id','Directory'))){$components[$r.Id]=$r.Directory}
 function RelativeDirectory([string]$id){
  $parts=@();$seen=@{}
  while($id -ne 'TARGETDIR' -and $id -ne 'TYBIMREGISTERDIR'){
   if($seen.ContainsKey($id) -or !$directories.ContainsKey($id)){throw "Invalid directory chain: $id"}
   $seen[$id]=$true;$d=$directories[$id];$name=($d.Name.Split(':')[0].Split('|') | Select-Object -Last 1)
   if($name -ne '.'){$parts=@($name)+$parts};$id=$d.Parent
  }
  return @{root=$id;relative=($parts -join '/')}
 }
 $files=@(Rows 'SELECT `File`,`Component_`,`FileName` FROM `File`' @('Id','Component','Name'))
 $index=Get-Content (Join-Path $PSScriptRoot "generated/$year/payload-index.json") -Raw | ConvertFrom-Json
 $actual=@{}
 foreach($file in $files){
  $name=($file.Name.Split('|') | Select-Object -Last 1)
  if($name -match '^(RevitAPI.*\.dll|bridge.config.json|auth.json)$'){throw "Forbidden MSI file: $name"}
  $dir=RelativeDirectory $components[$file.Component]
  $relative=(@($dir.relative,$name) | Where-Object {$_}) -join '/'
  if($dir.root -eq 'TYBIMREGISTERDIR'){
   if($relative -ne 'TYBIM.Mcp.addin'){throw 'Unexpected registration file'}
  }else{$actual[$relative]=$true}
 }
 if($files.Count -ne $index.files.Count+1){throw 'MSI file count differs from reviewed payload'}
 foreach($file in $index.files){if(!$actual.ContainsKey("$year/$($file.relative)")){throw "MSI missing payload file: $($file.relative)"}}
 foreach($required in @('Tybim.Revit.Addin.dll','Tybim.Revit.Commands.dll','Tybim.Revit.Study.dll','Tybim.Revit.Upstream.dll','runtime/node/node.exe','runtime/build/chat/index.js')){
  if(!$actual.ContainsKey("$year/$required")){throw "Missing runtime dependency: $required"}
 }
 $media=@(Rows 'SELECT `Cabinet` FROM `Media`' @('Cabinet'))
 if($media | Where-Object { !$_.Cabinet.StartsWith('#') }){throw 'MSI requires external cabinet files'}
 [xml]$manifest=Get-Content (Join-Path $PSScriptRoot "registration/$year/TYBIM.Mcp.addin") -Raw
 if($manifest.RevitAddIns.AddIn.Assembly -ne "..\TYBIM_MCP\$year\Tybim.Revit.Addin.dll"){throw 'Incorrect relative Assembly path'}
 if($properties.UpgradeCode -eq $lastUpgrade){throw 'Revit versions must have different upgrade identities'}
 $lastUpgrade=$properties.UpgradeCode
 [pscustomobject]@{RevitVersion=$year;Msi=$msi;FileCount=$files.Count;Version=$properties.ProductVersion;PerUser=$true;EmbeddedCabinet=$true;PathsVerified=$true;NoSharedToken=$true} | ConvertTo-Json
}

