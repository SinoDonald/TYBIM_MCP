param([ValidateSet('2024','2025','2026','2027')][string]$RevitVersion = '2025')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    & dotnet run --project tests/StudyCatalogRegression/StudyCatalogRegression.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Study catalog initialization regression failed.' }
    & dotnet build src/Tybim.Revit.Addin/Tybim.Revit.Addin.csproj -c Release "-p:RevitVersion=$RevitVersion"
    if ($LASTEXITCODE -ne 0) { throw 'Revit add-in build failed.' }
    Push-Location (Join-Path $root 'server')
    try {
        & npm.cmd ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
        & npm.cmd test
        if ($LASTEXITCODE -ne 0) { throw 'MCP server tests failed.' }
    } finally { Pop-Location }
} finally { Pop-Location }

