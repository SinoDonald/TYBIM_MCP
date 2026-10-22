param([ValidateSet('2024','2025','2026')][string]$RevitVersion='2026')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$version=(Get-Content (Join-Path $root 'server/package.json') -Raw | ConvertFrom-Json).version
$qaParent=[IO.Path]::GetFullPath((Join-Path $root 'artifacts/qa'))+[IO.Path]::DirectorySeparatorChar
$qa=[IO.Path]::GetFullPath((Join-Path $qaParent ('msi-uninstall-'+[Guid]::NewGuid().ToString('N'))))
if(!$qa.StartsWith($qaParent,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe QA path'}
New-Item -ItemType Directory -Path $qa -Force | Out-Null
$testMsi=Join-Path $qa 'test-only.msi'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Release/TYBIM_MCP_Revit${RevitVersion}_$version.msi") -Destination $testMsi
$productCode='{'+[Guid]::NewGuid().ToString().ToUpperInvariant()+'}'
$upgradeCode='{'+[Guid]::NewGuid().ToString().ToUpperInvariant()+'}'
$displayName="TYBIM_MCP QA Revit $RevitVersion $productCode"
$payloadRoot=Join-Path $qa 'Addins/TYBIM_MCP'
$registrationRoot=Join-Path $qa "Addins/$RevitVersion"
$installer=New-Object -ComObject WindowsInstaller.Installer
$database=$installer.OpenDatabase($testMsi,1)
function Sql([string]$query){$view=$database.OpenView($query);try{[void]$view.Execute()}finally{[void]$view.Close();[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)}}
function Literal([string]$value){return $value.Replace("'","''")}
foreach($entry in @{ProductCode=$productCode;UpgradeCode=$upgradeCode;ProductName=$displayName}.GetEnumerator()){
 Sql "UPDATE ``Property`` SET ``Value``='$(Literal $entry.Value)' WHERE ``Property``='$($entry.Key)'"
}
Sql 'DELETE FROM `Upgrade`'
Sql "UPDATE ``CustomAction`` SET ``Target``='$(Literal $payloadRoot)' WHERE ``Source``='TARGETDIR'"
Sql "UPDATE ``CustomAction`` SET ``Target``='$(Literal $registrationRoot)' WHERE ``Source``='TYBIMREGISTERDIR'"
# Isolate component identities as well, so tests cannot change production refs.
$components=@();$view=$database.OpenView('SELECT `Component` FROM `Component`');[void]$view.Execute()
while($null -ne ($record=$view.Fetch())){$components+= $record.StringData(1)}
[void]$view.Close();[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
foreach($component in $components){
 $componentCode='{'+[Guid]::NewGuid().ToString().ToUpperInvariant()+'}'
 Sql "UPDATE ``Component`` SET ``ComponentId``='$componentCode' WHERE ``Component``='$(Literal $component)'"
}
$summary=$database.SummaryInformation(1)
[void]$summary.GetType().InvokeMember('Property',[Reflection.BindingFlags]::SetProperty,$null,$summary,@(9,('{'+[Guid]::NewGuid().ToString().ToUpperInvariant()+'}')))
[void]$summary.Persist();[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($summary)
[void]$database.Commit();[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer)
[GC]::Collect();[GC]::WaitForPendingFinalizers()
@{testOnly=$true;productCode=$productCode;path=$qa;payloadRoot=$payloadRoot;registrationRoot=$registrationRoot} | ConvertTo-Json | Set-Content (Join-Path $qa 'test-plan.json')
$attempted=$false;$uninstalled=$false;$registered=$false;$registrationPaths=@()
function ProductRegistryPaths([string]$code){
 $hex=$code.Trim('{}').Replace('-','')
 $packed=''
 foreach($part in @($hex.Substring(0,8),$hex.Substring(8,4),$hex.Substring(12,4))){$chars=$part.ToCharArray();[array]::Reverse($chars);$packed+= -join $chars}
 for($index=16;$index -lt 32;$index+=2){$packed+=$hex[$index+1].ToString()+$hex[$index]}
 @("HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/$code","HKLM:/Software/Microsoft/Windows/CurrentVersion/Uninstall/$code")
 # MSI per-user ARP metadata may live in Installer/UserData rather than HKCU/Uninstall.
 foreach($sidRoot in Get-ChildItem 'HKLM:/Software/Microsoft/Windows/CurrentVersion/Installer/UserData'){
  Join-Path $sidRoot.PSPath "Products/$packed/InstallProperties"
 }
}
try {
 $attempted=$true
 $install=Start-Process -FilePath "$env:WINDIR/System32/msiexec.exe" -ArgumentList @('/i',('"'+$testMsi+'"'),'/qn','/norestart','/L*v',('"'+(Join-Path $qa 'install.log')+'"')) -Wait -PassThru -WindowStyle Hidden
 if($install.ExitCode -notin @(0,3010)){throw "QA MSI install failed: $($install.ExitCode). See $qa/install.log"}
 $registrationPaths=@(ProductRegistryPaths $productCode | Where-Object {Test-Path -LiteralPath $_})
 $registration=$registrationPaths | ForEach-Object {Get-ItemProperty -LiteralPath $_} | Where-Object {$_.DisplayName -eq $displayName} | Select-Object -First 1
 if($registration.DisplayName -ne $displayName -or $registration.DisplayVersion -ne $version -or $registration.SystemComponent){throw 'Windows Apps registration is absent/hidden/incorrect'}
 if($registration.UninstallString -notmatch '(?i)msiexec' -or $registration.UninstallString -notmatch [regex]::Escape($productCode)){throw 'Uninstall command does not target test product'}
 if(!(Test-Path -LiteralPath (Join-Path $payloadRoot "$RevitVersion/Tybim.Revit.Addin.dll")) -or !(Test-Path -LiteralPath (Join-Path $registrationRoot 'TYBIM.Mcp.addin'))){throw 'QA package was not installed into isolated roots'}
 foreach($name in @('Tybim.Mcp.Contracts.dll','Tybim.Revit.Commands.dll','Tybim.Revit.Study.dll','Tybim.Revit.Upstream.dll')){
  if(!(Test-Path -LiteralPath (Join-Path $payloadRoot "$RevitVersion/$name"))){throw "QA install omitted dependency: $name"}
 }
 & (Join-Path $PSScriptRoot 'Test-PayloadAssembly.ps1') -RevitVersion $RevitVersion -PayloadPath (Join-Path $payloadRoot $RevitVersion)
 $registered=$true
} finally {
 if($attempted){
  $remove=Start-Process -FilePath "$env:WINDIR/System32/msiexec.exe" -ArgumentList @('/x',$productCode,'/qn','/norestart','/L*v',('"'+(Join-Path $qa 'uninstall.log')+'"')) -Wait -PassThru -WindowStyle Hidden
  if($remove.ExitCode -notin @(0,3010,1605)){throw "QA MSI uninstall failed: $($remove.ExitCode). Test product: $productCode"}
  foreach($registryPath in $registrationPaths){if(Test-Path -LiteralPath $registryPath){throw 'QA Windows Apps entry remains after uninstall'}}
  if((Test-Path -LiteralPath (Join-Path $payloadRoot "$RevitVersion/Tybim.Revit.Addin.dll")) -or (Test-Path -LiteralPath (Join-Path $registrationRoot 'TYBIM.Mcp.addin'))){throw 'QA add-in files remain after uninstall'}
  $uninstalled=$true
 }
}
$result=@{RevitVersion=$RevitVersion;Version=$version;IsolatedTest=$true;WindowsAppsRegistered=$registered;UninstallVerified=$uninstalled;Logs=$qa}
$result | ConvertTo-Json | Tee-Object -FilePath (Join-Path $qa 'result.json')
