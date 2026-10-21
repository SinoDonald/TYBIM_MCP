[CmdletBinding(SupportsShouldProcess=$true)]
param([ValidateSet('mcp-servers-for-revit','revit-mcp-study')][string]$Source)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$sources = if ($Source) { @($Source) } else { @('mcp-servers-for-revit','revit-mcp-study') }
foreach ($name in $sources) {
    $folder = Join-Path $root "third_party/$name"
    $manifest = Get-Content -LiteralPath (Join-Path $folder 'UPSTREAM.json') -Raw | ConvertFrom-Json
    foreach ($file in $manifest.files) {
        $snapshot = [IO.Path]::GetFullPath((Join-Path $folder $file.snapshotPath))
        $allowed = [IO.Path]::GetFullPath((Join-Path $folder 'original')) + [IO.Path]::DirectorySeparatorChar
        if (!$snapshot.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Export source is not an original upstream snapshot.' }
        if ((Get-FileHash -LiteralPath $snapshot).Hash -ne $file.originalSha256) { throw "Snapshot was modified: $($file.snapshotPath). Review provenance before exporting." }
    }
}
$output = Join-Path $root ('artifacts/upstream-export-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
if ($PSCmdlet.ShouldProcess($output,'Export ONLY verified third-party snapshots; exclude TYBIM source, adaptations, private code, binaries and credentials')) {
    New-Item -ItemType Directory -Path $output | Out-Null
    foreach ($name in $sources) {
        $folder = Join-Path $root "third_party/$name"
        $destination = Join-Path $output $name
        New-Item -ItemType Directory -Path $destination | Out-Null
        $manifest = Get-Content -LiteralPath (Join-Path $folder 'UPSTREAM.json') -Raw | ConvertFrom-Json
        # Copy individual manifest allowlist files, never a whole repository tree.
        foreach ($file in $manifest.files) {
            $relative = $file.snapshotPath.Substring('original/'.Length)
            $target = Join-Path $destination $relative
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $folder $file.snapshotPath) -Destination $target
        }
        foreach ($notice in 'LICENSE','NOTICE.md') { if (Test-Path -LiteralPath (Join-Path $folder $notice)) { Copy-Item -LiteralPath (Join-Path $folder $notice) -Destination $destination } }
        @("Repository: $($manifest.repository)","Commit: $($manifest.commit)") | Set-Content -LiteralPath (Join-Path $destination 'SOURCE.txt') -Encoding utf8
    }
    Write-Host "Local snapshot export: $output"
}
