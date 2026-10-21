param([ValidateSet('2024','2025','2026','2027')][string]$RevitVersion = '2025')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& dotnet run --project (Join-Path $root 'tests/StudyCatalogRegression/StudyCatalogRegression.csproj')
if ($LASTEXITCODE -ne 0) { throw 'Study catalog cannot initialize. Refusing to package an add-in that would fail at startup.' }
$framework = if ($RevitVersion -eq '2024') { 'net48' } elseif ($RevitVersion -eq '2027') { 'net10.0-windows' } else { 'net8.0-windows' }
$source = Join-Path $root "src/Tybim.Revit.Addin/bin/$RevitVersion/Release/$framework"
$server = Join-Path $root 'server'
if (!(Test-Path -LiteralPath (Join-Path $source 'Tybim.Revit.Addin.dll'))) { throw 'Build the add-in first.' }
if (!(Test-Path -LiteralPath (Join-Path $server 'build/chat/index.js'))) { throw 'Run npm ci and npm run build in server first.' }
$bundle = Join-Path $root "artifacts/packages/$RevitVersion"
$addin = Join-Path $bundle 'Tybim.Mcp'
$runtime = Join-Path $addin 'runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Extension -in '.dll','.pdb','.json' -and $_.Name -notlike 'RevitAPI*' } | Copy-Item -Destination $addin -Force
$generatedBuild = [IO.Path]::GetFullPath((Join-Path $runtime 'build'))
$expectedBuild = [IO.Path]::GetFullPath((Join-Path $root "artifacts/packages/$RevitVersion/Tybim.Mcp/runtime/build"))
if ($generatedBuild -ne $expectedBuild) { throw 'Unexpected package runtime path.' }
if (Test-Path -LiteralPath $generatedBuild) {
    if ((Get-Item -LiteralPath $generatedBuild).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Package runtime must not be a junction.' }
    Remove-Item -LiteralPath $generatedBuild -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $server 'build') -Destination $runtime -Recurse -Force
Copy-Item -LiteralPath (Join-Path $server 'node_modules') -Destination $runtime -Recurse -Force
Copy-Item -LiteralPath (Join-Path $server 'package.json') -Destination $runtime -Force
$notices = Join-Path $addin 'third_party/mcp-servers-for-revit'
New-Item -ItemType Directory -Path $notices -Force | Out-Null
foreach ($name in 'LICENSE','catalog.json','UPSTREAM.json') {
    Copy-Item -LiteralPath (Join-Path $root "third_party/mcp-servers-for-revit/$name") -Destination $notices -Force
}
$studyNotices = Join-Path $addin 'third_party/revit-mcp-study'
New-Item -ItemType Directory -Path $studyNotices -Force | Out-Null
foreach ($name in 'NOTICE.md','catalog.json','UPSTREAM.json') { Copy-Item -LiteralPath (Join-Path $root "third_party/revit-mcp-study/$name") -Destination $studyNotices -Force }
Copy-Item -LiteralPath (Join-Path $root 'third_party/revit-mcp-study/original/README.md') -Destination (Join-Path $studyNotices 'UPSTREAM-README.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'third_party/revit-mcp-study/original/MCP-Server/package.json') -Destination (Join-Path $studyNotices 'UPSTREAM-package.json') -Force
Copy-Item -LiteralPath (Join-Path $root 'integrations/revit-mcp-study/ezdxf_worker.py') -Destination $addin -Force
$bridge = Join-Path $addin 'bridge.config.json'
if (!(Test-Path -LiteralPath $bridge)) {
    $existing = Join-Path $root "artifacts/$RevitVersion/Tybim.Mcp/bridge.config.json"
    if (Test-Path -LiteralPath $existing) { Copy-Item -LiteralPath $existing -Destination $bridge }
    else {
        $bytes = New-Object byte[] 32
        $random = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $random.GetBytes($bytes) } finally { $random.Dispose() }
        @{ port = 15800 + [int]$RevitVersion; token = [Convert]::ToBase64String($bytes) } | ConvertTo-Json | Set-Content -LiteralPath $bridge -Encoding utf8
    }
}
$codex = Get-Command codex.exe -ErrorAction SilentlyContinue
@{
    nodeExecutable = (Get-Command node.exe -ErrorAction Stop).Source
    runtimeScript = 'runtime/build/chat/index.js'
    codexExecutable = if ($codex) { $codex.Source } else { '' }
    stateDirectory = "%LOCALAPPDATA%\TYBIM\McpChat\$RevitVersion"
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $addin 'chat.config.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-RevitAddin.ps1') -Destination (Join-Path $bundle 'Install-RevitAddin.ps1') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs/embedded-chat.md') -Destination (Join-Path $bundle '使用說明.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs/upstream-integration.md') -Destination (Join-Path $bundle 'upstream-integration.md') -Force
foreach ($name in 'study-integration.md','source-ownership.md') { Copy-Item -LiteralPath (Join-Path $root "docs/$name") -Destination $bundle -Force }
Copy-Item -LiteralPath (Join-Path $root 'OWNERSHIP.json') -Destination $bundle -Force
@{ revitVersion = $RevitVersion; product = 'TYBIM Revit AI Chat'; version = (Get-Content -LiteralPath (Join-Path $server 'package.json') -Raw | ConvertFrom-Json).version; distribution = 'INTERNAL_PRIVATE'; sourceOwnership = 'See OWNERSHIP.json; this bundle is not a public-source release' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $bundle 'package.json') -Encoding utf8
Write-Host "Chat package ready: $bundle"
Write-Host "After closing Revit, run: & '$bundle/Install-RevitAddin.ps1' -RevitVersion $RevitVersion"





