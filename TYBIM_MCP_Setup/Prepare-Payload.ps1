param([ValidateSet('2024','2025')][string]$RevitVersion='2025')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$package=Join-Path $root "artifacts/packages/$RevitVersion/Tybim.Mcp"
$payload=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot "payloads/$RevitVersion"))
$allowed=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'payloads'))+[IO.Path]::DirectorySeparatorChar
if (!$payload.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) {throw 'Unsafe payload path'}
if (Test-Path -LiteralPath $payload) {
 if((Get-Item -LiteralPath $payload).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Payload cannot be a junction'}
 Remove-Item -LiteralPath $payload -Recurse -Force
}
New-Item -ItemType Directory -Path $payload -Force | Out-Null
Get-ChildItem -LiteralPath $package -Force | Where-Object { $_.Name -ne 'bridge.config.json' -and $_.Extension -ne '.pdb' } | Copy-Item -Destination $payload -Recurse -Force
$nodeDirectory=Join-Path $payload 'runtime/node'
New-Item -ItemType Directory -Path $nodeDirectory -Force | Out-Null
$nodePath=(Get-Command node.exe -ErrorAction Stop).Source
$nodeVersion=(& $nodePath -p 'process.versions.node').Trim()
if($nodeVersion -notmatch '^\d+\.\d+\.\d+$' -or [version]$nodeVersion -lt [version]'24.0.0'){throw 'Node 24 or later is required'}
if((& $nodePath -p 'process.arch').Trim() -ne 'x64'){throw 'An x64 Node runtime is required'}
Copy-Item -LiteralPath $nodePath -Destination (Join-Path $nodeDirectory 'node.exe')
$license=Join-Path $root "artifacts/node-runtime-LICENSE-$nodeVersion.txt"
if(!(Test-Path -LiteralPath $license)){
 $initialLicense=Join-Path $root 'artifacts/node-runtime-LICENSE.txt'
 if($nodeVersion -eq '24.19.0' -and (Test-Path -LiteralPath $initialLicense)){Copy-Item -LiteralPath $initialLicense -Destination $license}
 else {Invoke-WebRequest "https://raw.githubusercontent.com/nodejs/node/v$nodeVersion/LICENSE" -OutFile $license}
}
Copy-Item -LiteralPath $license -Destination (Join-Path $nodeDirectory 'LICENSE.txt')
@{nodeExecutable='runtime/node/node.exe';runtimeScript='runtime/build/chat/index.js';codexExecutable='';stateDirectory="%LOCALAPPDATA%\TYBIM\McpChat\$RevitVersion"} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'chat.config.json') -Encoding utf8
& node (Join-Path $PSScriptRoot 'Generate-Setup.mjs') $RevitVersion
if($LASTEXITCODE -ne 0){throw 'Setup generation failed'}
