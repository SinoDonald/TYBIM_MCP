# Copyright 2026 拓源數位科技. All rights reserved.
Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

function Read-SetupText([string]$Path) {
    [IO.File]::ReadAllText($Path, [Text.UTF8Encoding]::new($false, $true))
}
function Get-SetupHash([string]$Text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text))) }
    finally { $sha.Dispose() }
}
function Read-SetupObject([string]$Text) {
    try { $object = ConvertFrom-Json -InputObject $Text -ErrorAction Stop }
    catch { throw 'JSON 格式無效，請先修正或還原設定檔；未套用任何設定。' }
    if ($null -eq $object -or $object -isnot [pscustomobject]) { throw 'JSON 最外層必須是物件，未修改任何設定。' }
    $object
}
function Get-SetupProperty($Object, [string]$Name) {
    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property) { $property.Value }
}
function Get-ClaudeDesktopConfigPaths {
    param([string]$RoamingAppData=$env:APPDATA,[string]$LocalAppData=$env:LOCALAPPDATA,
        [string[]]$UserDataDirectories=@())
    # New Windows clients migrate their Electron user data out of AppData/Roaming.
    # Discover persistent profiles as well as an explicit --user-data-dir while running.
    $directories=[Collections.Generic.List[string]]::new()
    foreach($directory in $UserDataDirectories){if($directory){$directories.Add($directory)}}
    $profiles=Join-Path $LocalAppData 'Claude-Data'
    if(Test-Path -LiteralPath $profiles){
        foreach($profile in Get-ChildItem -LiteralPath $profiles -Directory){
            if($profile.Name -match '^claude(?:[_-]|$)'){$directories.Add($profile.FullName)}
        }
    }
    $packages=Join-Path $LocalAppData 'Packages'
    if(Test-Path -LiteralPath $packages){
        foreach($package in Get-ChildItem -LiteralPath $packages -Directory -Filter 'Claude_*'){
            $directory=Join-Path $package.FullName 'LocalCache/Roaming/Claude'
            if(Test-Path -LiteralPath $directory){
                $moved=@(Get-ChildItem -LiteralPath $directory -Force -File -Filter '.user-data-left*')
                if(!$moved.Count){$directories.Add($directory)}
            }
        }
    }
    $legacy=Join-Path $RoamingAppData 'Claude'
    if((Test-Path -LiteralPath $legacy) -or !$directories.Count){$directories.Add($legacy)}
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($directory in $directories){
        $path=[IO.Path]::GetFullPath((Join-Path $directory 'claude_desktop_config.json'))
        if($seen.Add($path)){$path}
    }
}
function Get-ClientSetupPaths {
    $codexRoot = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $env:USERPROFILE '.codex' }
    $claudeRoot = if ($env:CLAUDE_CONFIG_DIR) { $env:CLAUDE_CONFIG_DIR } else { $env:USERPROFILE }
    $userDataDirectories=@()
    try {
        $processes=Get-CimInstance Win32_Process -Filter "name='claude.exe'" -ErrorAction Stop
        foreach($process in $processes){
            if($process.CommandLine -match '--user-data-dir(?:=|\s+)(?:"([^"]+)"|(\S+))'){
                $userDataDirectories+=($Matches[1]+$Matches[2])
            }
        }
    } catch { } # Closed clients are discovered through their persistent profile directories.
    @{
        Codex = Join-Path $codexRoot 'config.toml'
        ClaudeCode = Join-Path $claudeRoot '.claude.json'
        ClaudeDesktop = @(Get-ClaudeDesktopConfigPaths -UserDataDirectories $userDataDirectories)
    }
}
function Get-ClientSetupSource {
    param([ValidateSet('2024','2025','2026')][string]$RevitVersion='2025', [string]$InstallDirectory)
    if (!$InstallDirectory) { $InstallDirectory = Join-Path $env:APPDATA "Autodesk\Revit\Addins\TYBIM_MCP\$RevitVersion" }
    $directory = [IO.Path]::GetFullPath($InstallDirectory)
    $exportPath = Join-Path $directory 'external-ai.mcp.json'
    if (!(Test-Path -LiteralPath $exportPath -PathType Leaf)) {
        throw "尚未找到 $exportPath`r`n請先安裝對應版本 TYBIM MCP，在 Revit 點選「外部 AI 服務」啟用一次，再回來檢查。"
    }
    $export = Read-SetupObject (Read-SetupText $exportPath)
    $name = "tybim_revit_${RevitVersion}_external"
    $servers = Get-SetupProperty $export 'mcpServers'
    $entry = if ($servers) { Get-SetupProperty $servers $name }
    if (!$entry) { throw "匯出設定內缺少 $name。請重新啟用 Revit 外部 AI 服務。" }
    $command = Get-SetupProperty $entry 'command'
    $arguments = @(Get-SetupProperty $entry 'args')
    $environment = Get-SetupProperty $entry 'env'
    $bridgePath = if ($environment) { Get-SetupProperty $environment 'TYBIM_MCP_CONFIG' }
    if (!$command -or ![IO.Path]::IsPathRooted($command) -or [IO.Path]::GetFileName($command) -ine 'node.exe' -or !(Test-Path -LiteralPath $command -PathType Leaf)) {
        throw '找不到匯出設定中的 node.exe，請修復 TYBIM MCP 安裝。'
    }
    $expectedScript = [IO.Path]::GetFullPath((Join-Path $directory 'runtime\build\index.js'))
    $expectedBridge = [IO.Path]::GetFullPath((Join-Path $directory 'external.bridge.config.json'))
    if ($arguments.Count -ne 1 -or ![IO.Path]::IsPathRooted($arguments[0]) -or [IO.Path]::GetFullPath($arguments[0]) -ine $expectedScript -or
        !$bridgePath -or ![IO.Path]::IsPathRooted($bridgePath) -or [IO.Path]::GetFullPath($bridgePath) -ine $expectedBridge -or
        !(Test-Path -LiteralPath $expectedScript -PathType Leaf) -or !(Test-Path -LiteralPath $expectedBridge -PathType Leaf)) {
        throw '匯出的 runtime／外部服務設定路徑不符安裝目錄，或檔案不存在；未套用。'
    }
    $bridge = Read-SetupObject (Read-SetupText $expectedBridge)
    $port = Get-SetupProperty $bridge 'port'
    if ($null -eq $port -or $port -is [string] -or [double]$port -ne [int]$port -or $port -lt 1 -or $port -gt 65535 -or $port -eq 8964) { throw '外部服務埠號無效。' }
    $internalPath = Join-Path $directory 'bridge.config.json'
    if (Test-Path -LiteralPath $internalPath -PathType Leaf) {
        $internal = Read-SetupObject (Read-SetupText $internalPath)
        if ((Get-SetupProperty $internal 'port') -eq $port) { throw '內外部服務埠號相同，請先修復 Revit 外部服務設定。' }
    }
    [pscustomobject]@{ Year=$RevitVersion; Directory=$directory; Port=[int]$port; Name=$name
        Entry=[pscustomobject]@{command=[string]$command;args=@($expectedScript);env=[pscustomobject]@{TYBIM_MCP_CONFIG=$expectedBridge}} }
}
function Get-InstalledTybimVersions {
    param([string]$InstallDirectory,[string]$CurrentYear='2025',[string]$AddinsRoot=(Join-Path $env:APPDATA 'Autodesk/Revit/Addins/TYBIM_MCP'))
    $candidates=@{}
    foreach($year in @('2024','2025','2026')){$candidates[$year]=Join-Path $AddinsRoot $year}
    if($InstallDirectory){
        $current=[IO.Path]::GetFullPath($InstallDirectory)
        $parent=Split-Path $current -Parent
        foreach($year in @('2024','2025','2026')){$sibling=Join-Path $parent $year;if(Test-Path -LiteralPath $sibling){$candidates[$year]=$sibling}}
        $candidates[$CurrentYear]=$current
    }
    foreach($year in @($candidates.Keys | Sort-Object)){
        $path=$candidates[$year]
        if((Test-Path -LiteralPath (Join-Path $path 'Tybim.Revit.Addin.dll') -PathType Leaf) -and
           (Test-Path -LiteralPath (Join-Path $path 'runtime/build/index.js') -PathType Leaf) -and
           (Test-Path -LiteralPath (Join-Path $path 'client-setup/Setup-ExternalAi.ps1') -PathType Leaf)){
            [pscustomobject]@{Year=$year;Directory=[IO.Path]::GetFullPath($path)}
        }
    }
}
function Update-CodexSetupText([string]$Text, $Source,[bool]$Remove=$false) {
    # Conservative, line-preserving edit of our own TOML tables only. Refuse
    # ambiguous multiline/inline forms rather than risk rewriting unrelated data.
    if ($Text.Contains('"""') -or $Text.Contains("'''")) { throw 'Codex TOML 含多行字串，請先人工檢查；工具不會自動改寫此格式。' }
    $name = 'tybim_revit_' + $Source.Year
    $escaped = [regex]::Escape($name)
    if ($Text -match ('(?m)^\s*(?:mcp_servers\s*=|(?:mcp_servers\s*\.\s*)?(?:"?' + $escaped + '"?)\s*=)') -or
        $Text -match '(?m)^\s*\[\[?\s*["'']mcp_servers["'']') {
        throw 'Codex 使用行內或特殊 MCP 表格格式，請先人工檢查；其他設定保持原樣。'
    }
    $pattern = '(?ms)^[ \t]*\[\s*mcp_servers\s*\.\s*(?:' + $escaped + '|"' + $escaped + '"|''' + $escaped + ''')\s*(?:\.[^\]\r\n]+)?\][^\r\n]*\r?\n.*?(?=^[ \t]*\[|\z)'
    $textWithout = [regex]::Replace(($Text.TrimEnd()+"`r`n"), $pattern, '')
    if($Remove){if(![regex]::IsMatch(($Text.TrimEnd()+"`r`n"),$pattern)){return $Text};return $textWithout}
    function Toml-Value([string]$Value) { ConvertTo-Json -InputObject $Value -Compress }
    $block = "[mcp_servers.$name]`r`ncommand = $(Toml-Value $Source.Entry.command)`r`nargs = [$(Toml-Value $Source.Entry.args[0])]`r`nenabled = true`r`nstartup_timeout_sec = 20`r`ntool_timeout_sec = 240`r`n`r`n[mcp_servers.$name.env]`r`nTYBIM_MCP_CONFIG = $(Toml-Value $Source.Entry.env.TYBIM_MCP_CONFIG)`r`n"
    $textWithout.TrimEnd()+"`r`n`r`n"+$block
}
function New-ClientSetupPlan {
    param($Source, [ValidateSet('Codex','ClaudeCode','ClaudeDesktop')][string[]]$Clients, [hashtable]$Paths=(Get-ClientSetupPaths),[switch]$Remove)
    if (!$Clients -or !$Clients.Count) { throw '請至少選擇一個 AI 用戶端。' }
    foreach ($client in @($Clients | Select-Object -Unique)) {
      if(!$Paths.ContainsKey($client) -or !@($Paths[$client]).Count){throw "找不到 $client 的設定位置。"}
      foreach($candidate in @($Paths[$client] | Select-Object -Unique)) {
        if([string]::IsNullOrWhiteSpace($candidate)){throw "$client 的設定位置無效。"}
        $path = [IO.Path]::GetFullPath($candidate)
        $exists = Test-Path -LiteralPath $path -PathType Leaf
        if ($exists -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw '設定檔是符號連結，請先人工確認實體位置；工具不會取代此連結。'
        }
        $original = if ($exists) { Read-SetupText $path } else { '' }
        if ($client -eq 'Codex') { $updated = Update-CodexSetupText $original $Source ([bool]$Remove) }
        else {
            $config = if ($exists) { Read-SetupObject $original } else { [pscustomobject]@{} }
            $servers = Get-SetupProperty $config 'mcpServers'
            if ($null -ne $servers -and $servers -isnot [pscustomobject]) { throw "$path 的 mcpServers 不是物件，未套用。" }
            if($Remove){
                $updated=$original
                if($null -ne $servers -and $null -ne $servers.PSObject.Properties[$Source.Name]){
                    $servers.PSObject.Properties.Remove($Source.Name)
                    $updated=($config | ConvertTo-Json -Depth 100)+"`r`n"
                }
            } else {
            if ($null -eq $servers) { $servers=[pscustomobject]@{}; $config | Add-Member -NotePropertyName mcpServers -NotePropertyValue $servers -Force }
            $entry = $Source.Entry | ConvertTo-Json -Depth 20 | ConvertFrom-Json
            if ($client -eq 'ClaudeCode') { $entry | Add-Member -NotePropertyName type -NotePropertyValue stdio }
            $servers | Add-Member -NotePropertyName $Source.Name -NotePropertyValue $entry -Force
            $updated = ($config | ConvertTo-Json -Depth 100)+"`r`n"
            }
        }
        [pscustomobject]@{ Client=$client;Path=$path;Exists=$exists;Original=$original;Contents=$updated;Changed=($original -cne $updated);Backup=$null }
      }
    }
}
function Invoke-ClientSetupPlan {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param([object[]]$Plan)
    $completed = [Collections.Generic.List[object]]::new()
    $stamp = (Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8)
    try {
        foreach ($item in $Plan) {
            if (!$item.Changed -or !$PSCmdlet.ShouldProcess($item.Path, '備份並設定 TYBIM 外部 AI')) { continue }
            $existsNow=Test-Path -LiteralPath $item.Path -PathType Leaf
            if ($existsNow -ne $item.Exists -or ($existsNow -and (Get-SetupHash (Read-SetupText $item.Path)) -ne (Get-SetupHash $item.Original))) {
                throw "設定在檢查後已變更，請重新檢查：$($item.Path)"
            }
            $parent=Split-Path $item.Path -Parent
            [IO.Directory]::CreateDirectory($parent) | Out-Null
            $temp=Join-Path $parent ('.tybim-'+[Guid]::NewGuid().ToString('N')+'.tmp')
            try {
                [IO.File]::WriteAllText($temp,$item.Contents,[Text.UTF8Encoding]::new($false))
                if($item.Exists) {
                    $item.Backup=$item.Path+'.before-tybim-'+$stamp+'.bak'
                    [IO.File]::Replace($temp,$item.Path,$item.Backup)
                } else { [IO.File]::Move($temp,$item.Path) }
                $completed.Add($item)
            } finally { if(Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force } }
        }
    } catch {
        $reason=$_.Exception.Message
        $rollbackErrors=[Collections.Generic.List[string]]::new()
        for($i=$completed.Count-1;$i -ge 0;$i--) {
            $item=$completed[$i]
            try {
                if((Get-SetupHash (Read-SetupText $item.Path)) -ne (Get-SetupHash $item.Contents)) { throw '檔案又被外部程式修改，保留現況與備份，請人工復原。' }
                if($item.Exists) { [IO.File]::Copy($item.Backup,$item.Path,$true) }
                else { [IO.File]::Delete($item.Path) }
            } catch { $rollbackErrors.Add($item.Path+'：'+$_.Exception.Message) }
        }
        if($rollbackErrors.Count) { throw ($reason+"`r`n復原未完成："+($rollbackErrors -join "`r`n")) }
        throw ($reason+"`r`n本次已套用的設定已復原。")
    }
    foreach($item in $Plan) { [pscustomobject]@{Client=$item.Client;Path=$item.Path;Changed=($completed.Contains($item));Backup=$item.Backup} }
}
function Test-ClientSetupApplied {
    param($Source,[object[]]$Plan)
    if(!$Plan -or !$Plan.Count){throw '沒有要驗證的 AI 用戶端。'}
    foreach($item in $Plan){
        $paths=@{};$paths[$item.Client]=$item.Path
        $readBack=@(New-ClientSetupPlan -Source $Source -Clients $item.Client -Paths $paths)
        if(@($readBack | Where-Object Changed).Count){throw "用戶端設定讀回不一致，請先完全結束 AI 再重試：$($item.Path)"}
    }
    [pscustomobject]@{Verified=$true;Clients=@($Plan | ForEach-Object {$_.Client})}
}
function Test-RevitExternalConnection {
    param($Source,[int]$TimeoutMs=5000)
    $client=$null
    try {
        if($TimeoutMs -lt 200 -or $TimeoutMs -gt 10000){throw 'Invalid timeout'}
        $bridge=Read-SetupObject (Read-SetupText $Source.Entry.env.TYBIM_MCP_CONFIG)
        $port=Get-SetupProperty $bridge 'port';$token=Get-SetupProperty $bridge 'token'
        if(!$port -or $port -lt 1024 -or $port -gt 65535 -or $port -eq 8964 -or !$token -or $token.Length -lt 32){throw 'Invalid bridge settings'}
        $client=[Net.Sockets.TcpClient]::new()
        if(!$client.ConnectAsync('127.0.0.1',[int]$port).Wait($TimeoutMs)){throw 'Connection timeout'}
        $stream=$client.GetStream();$stream.ReadTimeout=$TimeoutMs;$stream.WriteTimeout=$TimeoutMs
        $id=[Guid]::NewGuid().ToString('N')
        $request=@{version=1;id=$id;token=$token;method='revit.health';parameters=@{};expiresAt=([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()+$TimeoutMs)}
        $bytes=[Text.Encoding]::UTF8.GetBytes(($request | ConvertTo-Json -Compress)+"`n")
        $stream.Write($bytes,0,$bytes.Length)
        $buffer=[Collections.Generic.List[byte]]::new()
        while($true){$value=$stream.ReadByte();if($value -lt 0){throw 'Connection closed'};if($value -eq 10){break};$buffer.Add([byte]$value);if($buffer.Count -gt 65536){throw 'Response too large'}}
        $response=Read-SetupObject ([Text.Encoding]::UTF8.GetString($buffer.ToArray()))
        if($response.version -ne 1 -or $response.id -cne $id){return [pscustomobject]@{Connected=$false;Message='設定已儲存，但服務回應識別不符，請重新開啟外部服務。'}}
        if(!$response.success){return [pscustomobject]@{Connected=$false;Message='設定已儲存，但 Revit 尚未就緒。請結束目前指令／關閉對話框，再重試唯讀查詢。'}}
        if($response.data.revitVersion -ne $Source.Year){return [pscustomobject]@{Connected=$false;Message='設定已儲存，但連到不同年份的 Revit，請核對外部服務。'}}
        [pscustomobject]@{Connected=$true;Message=('已確認 Revit '+$Source.Year+' 外部服務連線。');RevitVersion=$Source.Year;HasActiveDocument=[bool]$response.data.hasActiveDocument}
    } catch { [pscustomobject]@{Connected=$false;Message='用戶端設定已儲存；尚未確認 Revit 連線。請開啟對應版本 Revit 的「外部 AI 服務」後再查詢。'} }
    finally {if($null -ne $client){$client.Dispose()}}
}
function Save-ClientSetupReceipt {
    param($Source,[object[]]$Plan,[string]$ReceiptPath)
    if(!$ReceiptPath) { $ReceiptPath=Join-Path $env:LOCALAPPDATA "TYBIM\McpClientSetup\$($Source.Year)\completed.json" }
    if(!$Plan -or !$Plan.Count) { throw '沒有完成的用戶端設定，無法記錄完成狀態。' }
    $receipt=[ordered]@{schema=2;year=$Source.Year;installDirectory=$Source.Directory;completedAtUtc=[DateTime]::UtcNow.ToString('o')
        clients=@($Plan | ForEach-Object { [ordered]@{client=$_.Client;path=$_.Path} })}
    [IO.Directory]::CreateDirectory((Split-Path $ReceiptPath -Parent)) | Out-Null
    $temporary=$ReceiptPath+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'
    try {
        [IO.File]::WriteAllText($temporary,($receipt | ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
        if([IO.File]::Exists($ReceiptPath)) {
            # PowerShell can bind $null as an empty string for this .NET overload.
            # Use an explicit sibling backup so repeated setup works in PS 5.1/7.
            $previous=$ReceiptPath+'.'+[Guid]::NewGuid().ToString('N')+'.previous'
            [IO.File]::Replace($temporary,$ReceiptPath,$previous)
            [IO.File]::Delete($previous)
        }
        else { [IO.File]::Move($temporary,$ReceiptPath) }
    } finally { if([IO.File]::Exists($temporary)){[IO.File]::Delete($temporary)} }
}
Export-ModuleMember -Function Get-ClaudeDesktopConfigPaths,Get-InstalledTybimVersions,Get-ClientSetupPaths,Get-ClientSetupSource,New-ClientSetupPlan,Invoke-ClientSetupPlan,Test-ClientSetupApplied,Test-RevitExternalConnection,Save-ClientSetupReceipt
