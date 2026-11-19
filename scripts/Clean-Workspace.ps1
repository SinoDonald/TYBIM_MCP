[CmdletBinding(SupportsShouldProcess=$true)]
param([switch]$IncludeBuildOutputs,[switch]$IncludePackages)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$prefix=$root+[IO.Path]::DirectorySeparatorChar
$relative=@('artifacts/qa','artifacts/setup-probe','artifacts/codex-schema','TYBIM_MCP_Setup/payloads','TYBIM_MCP_Setup/generated','.vs/TYBIM.MCP/CopilotIndices')
if($IncludePackages){$relative+='artifacts/packages'}
if($IncludeBuildOutputs){
    foreach($sourceRoot in @('src','tests','private/tests')) {
        # Project files locate build roots without traversing node_modules or
        # third-party source. Keep every test/source/dependency and release MSI.
        foreach($project in Get-ChildItem -LiteralPath (Join-Path $root $sourceRoot) -Filter '*.csproj' -File -Recurse){
            foreach($name in @('bin','obj')) {$relative+=(Join-Path $project.DirectoryName $name).Substring($prefix.Length)}
        }
    }
}
$removed=[Collections.Generic.List[object]]::new()
foreach($target in @($relative | Select-Object -Unique)) {
    $path=[IO.Path]::GetFullPath((Join-Path $root $target))
    if(!$path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or $path -eq $root) {throw 'Cleanup target escapes workspace'}
    if(!(Test-Path -LiteralPath $path)){continue}
    $item=Get-Item -LiteralPath $path -Force
    if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Cleanup target is a link: $path"}
    $links=@(Get-ChildItem -LiteralPath $path -Directory -Recurse -Force -ErrorAction Stop | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint})
    if($links.Count){throw "Cleanup target contains directory links: $path"}
    $tracked=@(& git -C $root ls-files -- $target)
    if($LASTEXITCODE -ne 0 -or $tracked.Count){throw "Refusing to remove tracked source: $target"}
    $files=@(Get-ChildItem -LiteralPath $path -File -Recurse -Force)
    $bytes=($files | Measure-Object Length -Sum).Sum
    if($PSCmdlet.ShouldProcess($path,"Delete reproducible output ($($files.Count) files, $([math]::Round($bytes/1MB,2)) MB)")) {
        try {Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction Stop;$removed.Add([pscustomobject]@{path=$target;files=$files.Count;bytes=$bytes})}
        catch {Write-Warning "Some output is in use; source was not touched: $path — $($_.Exception.Message)"}
    }
}
$summary=[pscustomobject]@{removedFolders=$removed.Count;removedFiles=($removed | Measure-Object files -Sum).Sum;freedMB=[math]::Round(($removed | Measure-Object bytes -Sum).Sum/1MB,2);folders=@($removed)}
if(!$WhatIfPreference){
    $report=Join-Path $root 'artifacts/workspace-cleanup.json'
    [IO.Directory]::CreateDirectory((Split-Path $report -Parent)) | Out-Null
    [IO.File]::WriteAllText($report,($summary | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
}
$summary
