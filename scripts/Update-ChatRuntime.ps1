param([ValidateSet('2024','2025')][string]$RevitVersion = '2025', [switch]$RestartChat)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Join-Path $root 'server/build/chat/codex-provider.js'
$installed = Join-Path $env:APPDATA "Autodesk/Revit/Addins/$RevitVersion/Tybim.Mcp"
$target = Join-Path $installed 'runtime/build/chat/codex-provider.js'
if (!(Test-Path -LiteralPath $source) -or !(Test-Path -LiteralPath $target)) { throw 'Build server and install the chat package first.' }
if (!(Get-Content -LiteralPath $source -Raw).Contains('features.code_mode_host=true')) { throw 'Source runtime is not the corrected build.' }
# Only restart this installed chat runtime when owned directly by Revit.
# No DLL, bridge configuration, login, or model files are replaced.
$entry = [IO.Path]::GetFullPath((Join-Path $installed 'runtime/build/chat/index.js'))
$processes = @(Get-CimInstance Win32_Process)
# Resolve parent IDs separately to avoid nested pipeline variable ambiguity.
$chatProcesses = @($processes | Where-Object { $_.Name -eq 'node.exe' -and $_.CommandLine -and $_.CommandLine.Contains('"' + $entry + '"') } | Where-Object {
    $parentId = $_.ParentProcessId
    @($processes | Where-Object { $_.ProcessId -eq $parentId -and $_.Name -eq 'Revit.exe' }).Count -eq 1
})
if ($chatProcesses.Count -and !$RestartChat) { throw 'Chat is running. Wait for its turn to finish, then rerun with -RestartChat, or close Revit.' }
$backup = $target + '.before-code-mode-fix'
if (!(Test-Path -LiteralPath $backup)) { Copy-Item -LiteralPath $target -Destination $backup }
Copy-Item -LiteralPath $source -Destination $target -Force
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { throw 'Runtime hash verification failed.' }
foreach ($chatProcess in $chatProcesses) {
    # Recheck process identity immediately before ending its owned child tree.
    $current = Get-CimInstance Win32_Process -Filter "ProcessId = $($chatProcess.ProcessId)"
    if ($current -and $current.ParentProcessId -eq $chatProcess.ParentProcessId -and $current.CommandLine -eq $chatProcess.CommandLine) {
        & "$env:SystemRoot/System32/taskkill.exe" /PID $chatProcess.ProcessId /T /F
        if ($LASTEXITCODE -ne 0) { throw 'Unable to restart chat. Close Revit before retrying.' }
    }
}
Write-Host 'Codex tool host fixed. Revit can remain open. Click Connect / Get Models, then New Conversation in the AI panel.'
