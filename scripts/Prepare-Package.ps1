param([ValidateSet('2024','2025')][string]$RevitVersion = '2025', [int]$Port = 0)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ($Port -eq 0) { $Port = if ($RevitVersion -eq '2024') { 17824 } else { 17825 } }
if ($Port -lt 1024 -or $Port -gt 65535) { throw 'Port must be 1024..65535.' }
$source = Join-Path $root "src/Tybim.Revit.Addin/bin/$RevitVersion/Release"
$framework = if ($RevitVersion -eq '2024') { 'net48' } else { 'net8.0-windows' }
$source = Join-Path $source $framework
if (!(Test-Path (Join-Path $source 'Tybim.Revit.Addin.dll'))) { throw 'Build the add-in first.' }
$package = Join-Path $root "artifacts/$RevitVersion/Tybim.Mcp"
New-Item -ItemType Directory -Path $package -Force | Out-Null
Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Extension -in '.dll','.pdb','.json' -and $_.Name -notlike 'RevitAPI*' } | Copy-Item -Destination $package -Force
$configPath = Join-Path $package 'bridge.config.json'
if (Test-Path $configPath) {
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    if ($config.port -ne $Port) { throw 'Existing package uses another port; choose its port or a separate package directory.' }
} else {
    $random = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($random) } finally { $rng.Dispose() }
    @{ port = $Port; token = [Convert]::ToBase64String($random) } | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding utf8
}
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>TYBIM MCP</Name>
    <Assembly>$([System.Security.SecurityElement]::Escape((Join-Path $package 'Tybim.Revit.Addin.dll')))</Assembly>
    <AddInId>A405238F-64C6-4FE2-A3C8-B698E159A6C1</AddInId>
    <FullClassName>Tybim.Revit.Addin.Application</FullClassName>
    <VendorId>TYBM</VendorId>
    <VendorDescription>TYBIM local MCP bridge</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
$manifest | Set-Content -LiteralPath (Join-Path $root "artifacts/$RevitVersion/TYBIM.Mcp.addin") -Encoding utf8
@{ mcpServers = @{ "tybim-revit-$RevitVersion" = @{ command = (Get-Command node.exe).Source; args = @((Join-Path $root 'server/build/index.js')); env = @{ TYBIM_MCP_CONFIG = $configPath } } } } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root "artifacts/$RevitVersion/mcp-client.example.json") -Encoding utf8
Write-Host "Package prepared: $package"
Write-Host "Install only the manifest into the user's Revit Addins/$RevitVersion directory. Keep this package path stable."
