param([ValidateSet('2024','2025')][string[]]$RevitVersion=@('2024','2025'),[switch]$SkipBuild)
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
  & $devenv $solution /Rebuild Release /Out $log
  if($LASTEXITCODE -ne 0){throw "MSI build failed: $log"}
  $version=(Get-Content server/package.json -Raw | ConvertFrom-Json).version
  if(!(Test-Path -LiteralPath (Join-Path $PSScriptRoot "Release/TYBIM_MCP_Revit${year}_$version.msi"))){throw "Visual Studio skipped the setup project. Inspect $log"}
  & (Join-Path $PSScriptRoot 'Verify-Msi.ps1') -RevitVersion $year
 }
}finally{Pop-Location}
