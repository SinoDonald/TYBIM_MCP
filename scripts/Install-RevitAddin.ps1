param(
    [ValidateSet('2024','2025','2026','2027')][string]$RevitVersion = '2025',
    [string]$PackageRoot = $PSScriptRoot
)
$ErrorActionPreference = 'Stop'
$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
if (!(Test-Path -LiteralPath (Join-Path $PackageRoot 'Tybim.Mcp/Tybim.Revit.Addin.dll'))) {
    $candidate = Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts/packages/$RevitVersion"
    if (Test-Path -LiteralPath (Join-Path $candidate 'Tybim.Mcp/Tybim.Revit.Addin.dll')) { $PackageRoot = [IO.Path]::GetFullPath($candidate) }
    else { throw 'Cannot find the complete TYBIM chat package. Run Package-Chat.ps1 first.' }
}
$metadata = Get-Content -LiteralPath (Join-Path $PackageRoot 'package.json') -Raw | ConvertFrom-Json
if ($metadata.revitVersion -ne $RevitVersion) { throw 'Package Revit version does not match requested installation.' }
if (Get-Process -Name Revit -ErrorAction SilentlyContinue) { throw 'Save your models and close Revit before installation.' }
$source = Join-Path $PackageRoot 'Tybim.Mcp'
$addinDirectory = Join-Path $env:APPDATA "Autodesk/Revit/Addins/$RevitVersion"
$destination = Join-Path (Split-Path $addinDirectory -Parent) "TYBIM_MCP/$RevitVersion"
$dll = Join-Path $destination 'Tybim.Revit.Addin.dll'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
New-Item -ItemType Directory -Path $addinDirectory -Force | Out-Null
# Check our DLLs before copying so a locked add-in cannot leave a partially updated set.
foreach ($file in Get-ChildItem -LiteralPath $source -File -Filter '*.dll') {
    $target = Join-Path $destination $file.Name
    if (Test-Path -LiteralPath $target) { $handle = [IO.File]::Open($target,'Open','ReadWrite','None'); $handle.Dispose() }
}
$existingBridge = Join-Path $destination 'bridge.config.json'
$savedBridge = if (Test-Path -LiteralPath $existingBridge) { Get-Content -LiteralPath $existingBridge -Raw } else { $null }
$legacyBridge = Join-Path $addinDirectory 'Tybim.Mcp/bridge.config.json'
if (!$savedBridge -and (Test-Path -LiteralPath $legacyBridge)) { $savedBridge = Get-Content -LiteralPath $legacyBridge -Raw }
$existingChat = Join-Path $destination 'chat.config.json'
$savedChat = if (Test-Path -LiteralPath $existingChat) { Get-Content -LiteralPath $existingChat -Raw } else { $null }
# The child runtime never stores AI keys or user conversations in this installation folder.
$generatedRuntime = [IO.Path]::GetFullPath((Join-Path $destination 'runtime/build'))
$expectedRuntime = [IO.Path]::GetFullPath((Join-Path (Split-Path $addinDirectory -Parent) "TYBIM_MCP/$RevitVersion/runtime/build"))
if ($generatedRuntime -ne $expectedRuntime) { throw 'Unexpected installed runtime path.' }
if (Test-Path -LiteralPath $generatedRuntime) {
    if ((Get-Item -LiteralPath $generatedRuntime).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Installed runtime must not be a junction.' }
    Remove-Item -LiteralPath $generatedRuntime -Recurse -Force
}
Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $destination -Recurse -Force
if ($savedBridge) { [IO.File]::WriteAllText($existingBridge, $savedBridge, [Text.UTF8Encoding]::new($false)) }
if ($savedChat) { [IO.File]::WriteAllText($existingChat, $savedChat, [Text.UTF8Encoding]::new($false)) }
foreach ($file in Get-ChildItem -LiteralPath $source -File -Filter '*.dll') {
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $destination $file.Name)).Hash) { throw "Installed DLL verification failed: $($file.Name)" }
}
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns><AddIn Type="Application">
<Name>TYBIM MCP</Name>
<Assembly>..\TYBIM_MCP\$RevitVersion\Tybim.Revit.Addin.dll</Assembly>
<AddInId>A405238F-64C6-4FE2-A3C8-B698E159A6C1</AddInId>
<FullClassName>Tybim.Revit.Addin.Application</FullClassName>
<VendorId>TYBM</VendorId><VendorDescription>TYBIM Revit AI Chat</VendorDescription>
</AddIn></RevitAddIns>
"@
$manifestPath = Join-Path $addinDirectory 'TYBIM.Mcp.addin'
if (Test-Path -LiteralPath $manifestPath) { Copy-Item -LiteralPath $manifestPath -Destination ($manifestPath + '.backup') -Force }
[IO.File]::WriteAllText($manifestPath, $manifest, [Text.UTF8Encoding]::new($false))
Write-Host "Installed: $destination"
Write-Host 'Start Revit and choose the TYBIM tab > MCP > AI assistant.'

