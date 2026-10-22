param([ValidateSet('2024','2025','2026')][string]$RevitVersion='2024', [string]$PayloadPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$PayloadPath){$PayloadPath=Join-Path $PSScriptRoot "payloads/$RevitVersion"}
$PayloadPath=[IO.Path]::GetFullPath($PayloadPath)
$framework=if($RevitVersion -eq '2024'){'net48'}else{'net8.0-windows'}
Push-Location $root
try {
 foreach($project in @('AddinLoadRegression','LoadFixture')){
  & dotnet build "tests/$project/$project.csproj" -c Release "-p:RevitVersion=$RevitVersion" --ignore-failed-sources -v:quiet
  if($LASTEXITCODE -ne 0){throw "Cannot build $project for $RevitVersion"}
 }
 $folder=Join-Path $root ("artifacts/qa/assembly-load-$RevitVersion-"+[Guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory -Path $folder -Force | Out-Null
 Get-ChildItem -LiteralPath $PayloadPath -Filter '*.dll' -File | Copy-Item -Destination $folder
 Copy-Item -LiteralPath "tests/LoadFixture/bin/$RevitVersion/Release/$framework/LoadFixture.dll" -Destination $folder
 $entry=Join-Path $folder 'Tybim.Revit.Addin.dll'
 $expected=(Get-Content server/package.json -Raw | ConvertFrom-Json).version+'.0'
 foreach($file in Get-ChildItem -LiteralPath $folder -Filter 'Tybim*.dll'){
  if([Reflection.AssemblyName]::GetAssemblyName($file.FullName).Version.ToString() -ne $expected){throw "Stale assembly: $($file.Name)"}
 }
 # The host has no Revit/Contracts/Newtonsoft references or copies in its app base.
 $hostExe=Join-Path $root "tests/AddinLoadRegression/bin/$RevitVersion/Release/$framework/AddinLoadRegression.exe"
 & $hostExe $folder baseline
 if($LASTEXITCODE -ne 0){throw 'Negative control did not reproduce missing Contracts'}
 & $hostExe $folder
 if($LASTEXITCODE -ne 0){throw "Revit $RevitVersion dependency loading regression"}
 & $hostExe $folder loadfrom
 if($LASTEXITCODE -ne 0){throw "Revit $RevitVersion default/load-from dependency regression"}
} finally {Pop-Location}
