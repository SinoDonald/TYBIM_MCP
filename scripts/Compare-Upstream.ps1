param(
    [ValidateSet('mcp-servers-for-revit','revit-mcp-study')][string]$Source,
    [Parameter(Mandatory=$true)][string]$SourceRoot
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content -LiteralPath (Join-Path $root "third_party/$Source/UPSTREAM.json") -Raw | ConvertFrom-Json
$upstreamRoot = [IO.Path]::GetFullPath($SourceRoot)
$report = @()
foreach ($file in $manifest.files) {
    $candidate = [IO.Path]::GetFullPath((Join-Path $upstreamRoot $file.sourcePath))
    if (!$candidate.StartsWith($upstreamRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Source path escapes repository.' }
    $state = if (!(Test-Path -LiteralPath $candidate)) { 'removed' }
        elseif ((Get-FileHash -LiteralPath $candidate).Hash -ne $file.originalSha256) { 'changed' } else { 'same' }
    if ($state -ne 'same') { $report += [pscustomobject]@{ sourcePath=$file.sourcePath; state=$state; snapshot=$file.snapshotPath } }
}
$known = @{}; foreach ($file in $manifest.files) { $known[$file.sourcePath.Replace('\','/')] = $true }
foreach ($scope in $manifest.scopes) {
    $directory = Join-Path $upstreamRoot $scope.directory
    if (!(Test-Path -LiteralPath $directory)) { continue }
    Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object { $_.Extension -in $scope.extensions -and $_.FullName -notmatch '[\\/](bin|obj|node_modules|build)[\\/]' } | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($upstreamRoot,$_.FullName).Replace('\','/')
        if (!$known.ContainsKey($relative)) { $report += [pscustomobject]@{ sourcePath=$relative; state='added'; snapshot=$null } }
    }
}
$reportDir = Join-Path $root 'artifacts/upstream-reports'
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$path = Join-Path $reportDir "$Source-comparison.json"
@{ source=$Source; baselineCommit=$manifest.commit; differences=@($report); overwroteAnySource=$false } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $path -Encoding utf8
Write-Host "Differences: $($report.Count). Report: $path"
Write-Host 'Read-only comparison completed. No snapshots, adaptations or private code were overwritten.'
