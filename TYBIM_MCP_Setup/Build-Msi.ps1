param([ValidateSet('2024','2025','2026')][string[]]$RevitVersion=@('2025'),[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$vswhere='C:/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe'
$vs=& $vswhere -latest -property installationPath
$devenv=Join-Path $vs 'Common7/IDE/devenv.com'
$fix=Join-Path $vs 'Common7/IDE/CommonExtensions/Microsoft/VSI/DisableOutOfProcBuild/DisableOutOfProcBuild.exe'
if(!(Test-Path -LiteralPath $fix)){throw 'Install Microsoft Visual Studio Installer Projects extension first.'}
Push-Location $vs
try { & $fix } finally { Pop-Location }
if($LASTEXITCODE -ne 0){throw 'Failed to enable command-line setup builds.'}
Push-Location $root
try {
 foreach($year in $RevitVersion){
  if(!$SkipBuild){
   & dotnet build src/Tybim.Revit.Addin/Tybim.Revit.Addin.csproj -c Release "-p:RevitVersion=$year" --ignore-failed-sources
   if($LASTEXITCODE -ne 0){throw "Revit $year compilation failed"}
   Push-Location server
   try{& npm.cmd run build;if($LASTEXITCODE -ne 0){throw 'Node build failed'}}finally{Pop-Location}
   & ./scripts/Package-Chat.ps1 -RevitVersion $year
  }
  & (Join-Path $PSScriptRoot 'Prepare-Payload.ps1') -RevitVersion $year
  $solution=Join-Path $PSScriptRoot "generated/$year/TYBIM_MCP_$year.sln"
  $log=Join-Path $PSScriptRoot "generated/$year/build.log"
  & $devenv $solution /Rebuild Release /Out $log > (Join-Path $PSScriptRoot "generated/$year/console.log") 2>&1
  if($LASTEXITCODE -ne 0){throw "MSI build failed: $log"}
  $version=(Get-Content server/package.json -Raw | ConvertFrom-Json).version
  if(!(Test-Path -LiteralPath (Join-Path $PSScriptRoot "Release/TYBIM_MCP_Revit${year}_$version.msi"))){throw "Visual Studio skipped the setup project. Inspect $log"}
  # Isolate MSI writer COM handles before opening the same package for review.
  & (Get-Process -Id $PID).Path -NoProfile -File (Join-Path $PSScriptRoot 'Set-MsiRegistration.ps1') -RevitVersion $year
  if($LASTEXITCODE -ne 0){throw "MSI registration settings failed for $year"}
  & (Join-Path $PSScriptRoot 'Verify-Msi.ps1') -RevitVersion $year
 }
 # Retire only older installers for years successfully built in this run.
 # Resolve and check the output folder before any removal; never recurse.
 $release=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Release'))
 $expected=[IO.Path]::GetFullPath((Join-Path $root 'TYBIM_MCP_Setup/Release'))
 if($release -ne $expected -or ((Get-Item -LiteralPath $release).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Unsafe installer cleanup folder'}
 foreach($old in Get-ChildItem -LiteralPath $release -File -Filter 'TYBIM_MCP_Revit*.msi'){
  if($old.Name -match '^TYBIM_MCP_Revit(2024|2025|2026)_(\d+\.\d+\.\d+)\.msi$' -and $Matches[1] -in $RevitVersion -and [version]$Matches[2] -lt [version]$version){
   if([IO.Path]::GetDirectoryName($old.FullName) -ne $release){throw 'Installer cleanup escapes Release'}
   Remove-Item -LiteralPath $old.FullName -Force
   Write-Host "Removed superseded installer: $($old.Name)"
  }
 }
 $remaining=@(Get-ChildItem -LiteralPath $release -File -Filter 'TYBIM_MCP_Revit*.msi')
 foreach($checksum in Get-ChildItem -LiteralPath $release -File -Filter 'checksums-*.sha256'){
  if($checksum.Name -match '^checksums-(\d+\.\d+\.\d+)\.sha256$'){
   $checksumVersion=$Matches[1]
   $versionFiles=@($remaining | Where-Object {$_.Name.EndsWith("_$checksumVersion.msi")})
   if(!$versionFiles.Count){Remove-Item -LiteralPath $checksum.FullName -Force}
   else {$versionFiles | Sort-Object Name | ForEach-Object {"$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $($_.Name)"} | Set-Content -LiteralPath $checksum.FullName -Encoding ascii}
  }
 }
 $remaining | Sort-Object Name | ForEach-Object {"$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $($_.Name)"} | Set-Content -LiteralPath (Join-Path $release 'checksums.sha256') -Encoding ascii
 $remaining | Where-Object {$_.Name.EndsWith("_$version.msi")} | Sort-Object Name | ForEach-Object {"$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $($_.Name)"} | Set-Content -LiteralPath (Join-Path $release "checksums-$version.sha256") -Encoding ascii
}finally{Pop-Location}
