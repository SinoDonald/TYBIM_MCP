$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot '../../scripts/ExternalAiSetup/ClientSetup.psm1') -Force
$fixture=Join-Path $PSScriptRoot ('fixture-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
$script:checks=0
function Check($ok,[string]$text) { if(!$ok){throw "FAIL: $text"};$script:checks++;Write-Host "PASS: $text" }
function Write-Text([string]$path,[string]$text) { [IO.Directory]::CreateDirectory((Split-Path $path -Parent)) | Out-Null;[IO.File]::WriteAllText($path,$text,[Text.UTF8Encoding]::new($false)) }
function Expect-Failure([scriptblock]$action,[string]$text) { $failed=$false;try { &$action | Out-Null } catch {$failed=$true};Check $failed $text }
try {
    $install=Join-Path $fixture '安裝位置 [MCP]'
    $node=Join-Path $install 'runtime/node/node.exe';$scriptPath=Join-Path $install 'runtime/build/index.js'
    Write-Text $node 'test fixture, never executed';Write-Text $scriptPath 'test fixture, never executed'
    $bridge=Join-Path $install 'external.bridge.config.json';Write-Text $bridge '{"port":21825,"token":"test-only-not-for-packaging"}'
    Write-Text (Join-Path $install 'bridge.config.json') '{"port":17825}'
    $export=[pscustomobject]@{mcpServers=[pscustomobject]@{tybim_revit_2025_external=[pscustomobject]@{command=$node;args=@($scriptPath);env=[pscustomobject]@{TYBIM_MCP_CONFIG=$bridge}}}}
    Write-Text (Join-Path $install 'external-ai.mcp.json') ($export | ConvertTo-Json -Depth 20)
    $source=Get-ClientSetupSource -InstallDirectory $install
    Check ($source.Port -eq 21825 -and $source.Entry.command -eq $node) 'installed export resolves paths containing spaces, Unicode and brackets'
    Check (!($source.Entry | ConvertTo-Json -Depth 20).Contains('test-only-not-for-packaging')) 'bridge token is not copied into client configuration'
    $roaming=Join-Path $fixture 'desktop/Roaming'
    $local=Join-Path $fixture 'desktop/Local'
    $legacy=Join-Path $roaming 'Claude/claude_desktop_config.json'
    $detected=@(Get-ClaudeDesktopConfigPaths -RoamingAppData $roaming -LocalAppData $local)
    Check ($detected.Count -eq 1 -and $detected[0] -eq $legacy) 'fresh legacy Desktop uses the standard Roaming location'
    $modern=Join-Path $local 'Claude-Data/claude_pzs8sxrjxfjjc/claude_desktop_config.json'
    Write-Text $modern '{"preferences":{"keep":true}}'
    $detected=@(Get-ClaudeDesktopConfigPaths -RoamingAppData $roaming -LocalAppData $local)
    Check ($detected.Count -eq 1 -and $detected[0] -eq $modern) 'Store migrated user data is discovered when Claude is closed'
    Write-Text $legacy '{"mcpServers":{"other":{"command":"other.exe"}}}'
    $enterprise=Join-Path $local 'Claude-Data/Claude_enterprise/claude_desktop_config.json'
    [IO.Directory]::CreateDirectory((Split-Path $enterprise -Parent)) | Out-Null
    $virtual=Join-Path $local 'Packages/Claude_test/LocalCache/Roaming/Claude/claude_desktop_config.json'
    Write-Text $virtual '{}'
    $custom=Join-Path $fixture '自訂 [Desktop]/claude_desktop_config.json'
    $desktopPaths=@(Get-ClaudeDesktopConfigPaths -RoamingAppData $roaming -LocalAppData $local -UserDataDirectories @((Split-Path $custom -Parent),(Split-Path $modern -Parent)))
    Check ($desktopPaths.Count -eq 5 -and $desktopPaths -contains $legacy -and $desktopPaths -contains $modern -and $desktopPaths -contains $enterprise -and $desktopPaths -contains $virtual -and $desktopPaths -contains $custom) 'coexisting legacy, Store, enterprise, virtualized and explicit custom profiles are deduplicated'
    Write-Text (Join-Path (Split-Path $virtual -Parent) '.user-data-left.claude_test') ''
    $migrated=@(Get-ClaudeDesktopConfigPaths -RoamingAppData $roaming -LocalAppData $local)
    Check ($migrated -notcontains $virtual) 'abandoned MSIX virtualized directories are skipped after migration'
    $multiPaths=@{ClaudeDesktop=$desktopPaths}
    $multi=@(New-ClientSetupPlan -Source $source -Clients ClaudeDesktop -Paths $multiPaths)
    Check ($multi.Count -eq 5) 'one Desktop selection includes every discovered install profile'
    Invoke-ClientSetupPlan $multi | Out-Null
    Test-ClientSetupApplied -Source $source -Plan $multi | Out-Null
    Check (([IO.File]::ReadAllText($legacy) | ConvertFrom-Json).mcpServers.other.command -eq 'other.exe') 'multi-profile merge preserves unrelated MCP servers'
    Write-Text $modern '{}'
    Expect-Failure { Test-ClientSetupApplied -Source $source -Plan $multi } 'read-back catches a missing registration in an earlier Desktop profile'
    Invoke-ClientSetupPlan @(New-ClientSetupPlan -Source $source -Clients ClaudeDesktop -Paths $multiPaths) | Out-Null
    $multiReceipt=Join-Path $fixture 'desktop/receipt.json'
    Save-ClientSetupReceipt -Source $source -Plan $multi -ReceiptPath $multiReceipt
    Check (([IO.File]::ReadAllText($multiReceipt) | ConvertFrom-Json).clients.Count -eq 5) 'receipt records every Desktop path, including repeated client names'
    Invoke-ClientSetupPlan @(New-ClientSetupPlan -Source $source -Clients ClaudeDesktop -Paths $multiPaths -Remove) | Out-Null
    $remaining=@(New-ClientSetupPlan -Source $source -Clients ClaudeDesktop -Paths $multiPaths -Remove)
    Check (@($remaining | Where-Object Changed).Count -eq 0) 'clear removes TYBIM from all discovered Desktop profiles'
    Write-Text $modern '{broken'
    $beforeMulti=[IO.File]::ReadAllText($legacy)
    Expect-Failure { $bad=@(New-ClientSetupPlan -Source $source -Clients ClaudeDesktop -Paths $multiPaths);Invoke-ClientSetupPlan $bad } 'one corrupt Desktop profile rejects the entire multi-profile update'
    Check ([IO.File]::ReadAllText($legacy) -ceq $beforeMulti) 'multi-profile preflight failure preserves earlier profiles'
    $paths=@{Codex=(Join-Path $fixture 'clients/config.toml');ClaudeCode=(Join-Path $fixture 'clients/.claude.json');ClaudeDesktop=(Join-Path $fixture 'clients/desktop.json')}
    $original=@"
model = "keep-this-model"
# preserve unrelated settings
[mcp_servers.other]
command = "other.exe"
[mcp_servers."tybim_revit_2025"]
command = "old.exe"
[mcp_servers."tybim_revit_2025".env]
OLD = "obsolete"
[projects.'C:/other']
trust_level = "trusted"
"@
    Write-Text $paths.Codex $original
    Write-Text $paths.ClaudeCode '{"preferences":{"keep":true},"mcpServers":{"other":{"command":"other.exe"}},"projects":{"C:/existing":{"keep":true}}}'
    Write-Text $paths.ClaudeDesktop '{"preferences":{"keep":true},"mcpServers":{"other":{"command":"other.exe"}}}'
    $plan=@(New-ClientSetupPlan -Source $source -Clients Codex,ClaudeCode,ClaudeDesktop -Paths $paths)
    $result=@(Invoke-ClientSetupPlan -Plan $plan -WhatIf)
    Check ([IO.File]::ReadAllText($paths.Codex) -ceq $original) 'WhatIf does not modify existing configuration'
    $result=@(Invoke-ClientSetupPlan -Plan $plan)
    Check ($result.Count -eq 3 -and @($result | Where-Object Changed).Count -eq 3) 'selected three clients are configured'
    Check (@($result | Where-Object {Test-Path -LiteralPath $_.Backup}).Count -eq 3) 'each existing file has an independent backup'
    Check ([IO.File]::ReadAllText($result[0].Backup) -ceq $original) 'backup preserves original Codex content'
    $toml=[IO.File]::ReadAllText($paths.Codex)
    Check ($toml.Contains('keep-this-model') -and $toml.Contains('[mcp_servers.other]') -and $toml.Contains("[projects.'C:/other']")) 'Codex unrelated model, server and project tables survive'
    Check (!$toml.Contains('old.exe') -and !$toml.Contains('obsolete') -and ([regex]::Matches($toml,'\[mcp_servers.tybim_revit_2025\]')).Count -eq 1) 'own quoted tables are replaced without duplicate definitions'
    Check ($toml.Contains('tool_timeout_sec = 240') -and $toml.Contains('external.bridge.config.json')) 'Codex uses external bridge and a modeling timeout'
    foreach($client in @('ClaudeCode','ClaudeDesktop')) {
        $json=[IO.File]::ReadAllText($paths[$client]) | ConvertFrom-Json
        Check ($json.preferences.keep -and $json.mcpServers.other.command -eq 'other.exe' -and $json.mcpServers.tybim_revit_2025_external.command -eq $node) "$client merges its server and preserves unrelated fields"
    }
    $again=@(New-ClientSetupPlan -Source $source -Clients Codex,ClaudeCode,ClaudeDesktop -Paths $paths)
    Check (@($again | Where-Object Changed).Count -eq 0) 'repeated configuration is idempotent'
    $removePaths=@{Codex=(Join-Path $fixture 'remove/config.toml');ClaudeCode=(Join-Path $fixture 'remove/.claude.json');ClaudeDesktop=(Join-Path $fixture 'remove/desktop.json')}
    foreach($name in $paths.Keys){Write-Text $removePaths[$name] ([IO.File]::ReadAllText($paths[$name]))}
    [IO.File]::AppendAllText($removePaths.Codex,"`r`n[mcp_servers.node_repl]`r`ncommand = 'keep-node.exe'`r`n[mcp_servers.tybim_revit_2024]`r`ncommand = 'keep-2024.exe'`r`n")
    $removePlan=@(New-ClientSetupPlan -Source $source -Clients Codex,ClaudeCode -Paths $removePaths -Remove)
    $desktopBefore=[IO.File]::ReadAllText($removePaths.ClaudeDesktop)
    $removed=@(Invoke-ClientSetupPlan -Plan $removePlan)
    $cleared=[IO.File]::ReadAllText($removePaths.Codex)
    Check (!$cleared.Contains('[mcp_servers.tybim_revit_2025') -and $cleared.Contains('[mcp_servers.node_repl]') -and $cleared.Contains('[mcp_servers.tybim_revit_2024]') -and $cleared.Contains('keep-this-model')) 'clear removes only own year and preserves other servers, years and Codex preferences'
    $clearedCode=[IO.File]::ReadAllText($removePaths.ClaudeCode) | ConvertFrom-Json
    Check ($null -eq $clearedCode.mcpServers.PSObject.Properties['tybim_revit_2025_external'] -and $clearedCode.mcpServers.other.command -eq 'other.exe' -and $clearedCode.preferences.keep) 'clear preserves unrelated Claude settings'
    Check ([IO.File]::ReadAllText($removePaths.ClaudeDesktop) -ceq $desktopBefore) 'clear does not change an unselected AI'
    Check (@($removed | Where-Object {Test-Path -LiteralPath $_.Backup}).Count -eq 2) 'clear backs up every changed client'
    Check (@(New-ClientSetupPlan -Source $source -Clients Codex,ClaudeCode -Paths $removePaths -Remove | Where-Object Changed).Count -eq 0) 'repeated clear is idempotent'
    $absent=@{ClaudeDesktop=(Join-Path $fixture 'never-created/config.json')}
    Invoke-ClientSetupPlan @(New-ClientSetupPlan -Source $source -Clients ClaudeDesktop -Paths $absent -Remove) | Out-Null
    Check (!(Test-Path -LiteralPath $absent.ClaudeDesktop)) 'clear never creates an absent config file'
    $installedRoot=Join-Path $fixture 'installed'
    foreach($installedYear in @('2024','2025')){
        foreach($file in @('Tybim.Revit.Addin.dll','runtime/build/index.js','client-setup/Setup-ExternalAi.ps1')){Write-Text (Join-Path $installedRoot "$installedYear/$file") 'fixture'}
    }
    Write-Text (Join-Path $installedRoot '2026/external.bridge.config.json') '{}'
    $versions=@(Get-InstalledTybimVersions -AddinsRoot $installedRoot)
    Check (($versions.Year -join ',') -eq '2024,2025') 'version list contains only installed TYBIM payloads in ascending order, ignoring leftover settings'
    Check ($versions[1].Directory -eq [IO.Path]::GetFullPath((Join-Path $installedRoot '2025'))) 'each version resolves to its own installation directory'
    [IO.File]::Delete((Join-Path $installedRoot '2024/Tybim.Revit.Addin.dll'))
    Check ((@(Get-InstalledTybimVersions -AddinsRoot $installedRoot).Year -join ',') -eq '2025') 'uninstalled version disappears from the list'
    $receiptPath=Join-Path $fixture 'state/completed.json'
    Save-ClientSetupReceipt -Source $source -Plan $again -ReceiptPath $receiptPath
    $receipt=[IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json
    Check ($receipt.schema -eq 2 -and $receipt.year -eq '2025' -and $receipt.clients.Count -eq 3) 'completion receipt records the year and selected client paths with Desktop path detection schema'
    Check (![IO.File]::ReadAllText($receiptPath).Contains('test-only-not-for-packaging')) 'completion receipt contains no bridge token'
    Save-ClientSetupReceipt -Source $source -Plan @($again[0]) -ReceiptPath $receiptPath
    $updatedReceipt=[IO.File]::ReadAllText($receiptPath) | ConvertFrom-Json
    Check ($updatedReceipt.clients.Count -eq 1 -and $updatedReceipt.clients[0].client -eq 'Codex') 'existing completion receipt can be rewritten after reconfiguring selected clients'
    $one=@(New-ClientSetupPlan -Source $source -Clients Codex -Paths $paths)
    Check ($one.Count -eq 1 -and $one[0].Client -eq 'Codex') 'single selection does not include unselected clients'
    $before=[IO.File]::ReadAllText($paths.Codex)
    Write-Text $paths.ClaudeDesktop '{ invalid JSON with secret-example-value'
    Expect-Failure { $bad=@(New-ClientSetupPlan -Source $source -Clients Codex,ClaudeDesktop -Paths $paths);Invoke-ClientSetupPlan $bad } 'corrupt JSON blocks the entire plan'
    Check ([IO.File]::ReadAllText($paths.Codex) -ceq $before) 'preflight failure does not partially write Codex'
    Write-Text $paths.Codex 'mcp_servers = { other = { command = "other.exe" } }'
    Expect-Failure { New-ClientSetupPlan -Source $source -Clients Codex -Paths $paths } 'inline TOML is refused without overwriting unrelated servers'
    Write-Text $paths.Codex "model = `"`"`"multi`nline`"`"`""
    Expect-Failure { New-ClientSetupPlan -Source $source -Clients Codex -Paths $paths } 'multiline TOML is refused conservatively'
    Write-Text $paths.Codex 'model = "original"'
    $stale=@(New-ClientSetupPlan -Source $source -Clients Codex -Paths $paths)
    Write-Text $paths.Codex 'model = "changed-by-client"'
    Expect-Failure { Invoke-ClientSetupPlan $stale } 'concurrent client edit is detected'
    Check ([IO.File]::ReadAllText($paths.Codex).Contains('changed-by-client')) 'concurrent client edit is preserved'
    $blocked=Join-Path $fixture 'blocked-parent';Write-Text $blocked 'a file, not a directory'
    $rollbackPaths=@{Codex=$paths.Codex;ClaudeDesktop=(Join-Path $blocked 'config.json')}
    $rollback=@(New-ClientSetupPlan -Source $source -Clients Codex,ClaudeDesktop -Paths $rollbackPaths)
    $before=[IO.File]::ReadAllText($paths.Codex)
    Expect-Failure { Invoke-ClientSetupPlan $rollback } 'second file write failure propagates'
    Check ([IO.File]::ReadAllText($paths.Codex) -ceq $before) 'earlier successful write is restored after later failure'
    $fresh=@{Codex=(Join-Path $fixture 'fresh/config.toml')}
    Invoke-ClientSetupPlan @(New-ClientSetupPlan -Source $source -Clients Codex -Paths $fresh) | Out-Null
    Check ((Test-Path -LiteralPath $fresh.Codex) -and [IO.File]::ReadAllText($fresh.Codex).Contains('enabled = true')) 'new users without a Codex config are supported'
    $freshAll=@{Codex=(Join-Path $fixture 'new-user/config.toml');ClaudeCode=(Join-Path $fixture 'new-user/.claude.json');ClaudeDesktop=(Join-Path $fixture 'new-user/desktop.json')}
    Invoke-ClientSetupPlan @(New-ClientSetupPlan -Source $source -Clients Codex,ClaudeCode,ClaudeDesktop -Paths $freshAll) | Out-Null
    $freshToml=[IO.File]::ReadAllText($freshAll.Codex)
    $freshPlan=@(New-ClientSetupPlan -Source $source -Clients Codex,ClaudeCode,ClaudeDesktop -Paths $freshAll)
    Check (Test-ClientSetupApplied -Source $source -Plan $freshPlan).Verified 'fresh client settings pass read-back verification'
    Write-Text $freshAll.Codex ''
    Expect-Failure {Test-ClientSetupApplied -Source $source -Plan $freshPlan} 'removed registration fails read-back verification'
    Check ($freshToml.Contains('[mcp_servers.tybim_revit_2025]') -and !$freshToml.Contains('node_repl')) 'fresh Codex needs only TYBIM registration, not node_repl'
    foreach($freshClient in @('ClaudeCode','ClaudeDesktop')) {
        $freshJson=[IO.File]::ReadAllText($freshAll[$freshClient]) | ConvertFrom-Json
        Check (@($freshJson.mcpServers.PSObject.Properties).Count -eq 1 -and $freshJson.mcpServers.tybim_revit_2025_external.command -eq $node) "$freshClient configures from zero without the old Revit MCP"
    }
    Write-Text $bridge '{"port":8964}'
    Expect-Failure { Get-ClientSetupSource -InstallDirectory $install } 'disallowed external port is rejected'
    Write-Text $bridge '{"port":17825}'
    Expect-Failure { Get-ClientSetupSource -InstallDirectory $install } 'internal service port cannot be used accidentally'
    Write-Text $bridge '{"port":22825}'
    Check ((Get-ClientSetupSource -InstallDirectory $install).Port -eq 22825) 'custom external port is preserved'
    foreach($scenario in @('ok','wrong-id','wrong-year','busy')) {
        $ready=Join-Path $fixture ($scenario+'.port')
        $job=Start-Job -ArgumentList $ready,$scenario -ScriptBlock {
            param($ready,$scenario)
            $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$listener.Start()
            [IO.File]::WriteAllText($ready,[string]$listener.LocalEndpoint.Port)
            try {
                $socket=$listener.AcceptTcpClient();$stream=$socket.GetStream();$stream.ReadTimeout=5000
                $reader=[IO.StreamReader]::new($stream,[Text.Encoding]::UTF8,$false,1024,$true)
                $request=$reader.ReadLine() | ConvertFrom-Json
                if($request.method -ne 'revit.health' -or $request.token -ne ('t'*40) -or $request.version -ne 1){throw 'Invalid health request'}
                $reply=@{version=1;id=$request.id;success=$true;data=@{revitVersion='2025';hasActiveDocument=$true}}
                if($scenario -eq 'wrong-id'){$reply.id='incorrect'}
                if($scenario -eq 'wrong-year'){$reply.data.revitVersion='2024'}
                if($scenario -eq 'busy'){$reply.success=$false}
                $bytes=[Text.Encoding]::UTF8.GetBytes(($reply | ConvertTo-Json -Compress)+"`n");$stream.Write($bytes,0,$bytes.Length)
                $socket.Dispose()
            } finally {$listener.Stop()}
        }
        try {
            $deadline=[DateTime]::UtcNow.AddSeconds(15)
            while(!(Test-Path -LiteralPath $ready)){if([DateTime]::UtcNow -gt $deadline){throw 'Stub startup timeout'};Start-Sleep -Milliseconds 100}
            $port=[int][IO.File]::ReadAllText($ready)
            Write-Text $bridge (@{port=$port;token=('t'*40)} | ConvertTo-Json)
            $health=Test-RevitExternalConnection -Source $source -TimeoutMs 3000
            Check ($health.Connected -eq ($scenario -eq 'ok')) "health response validation: $scenario"
            Check (!(($health | ConvertTo-Json).Contains(('t'*40)))) 'health result never exposes token'
            $job | Wait-Job -Timeout 5 | Out-Null
            Receive-Job $job -ErrorAction Stop | Out-Null
        } finally {$job | Stop-Job;$job | Remove-Job -Force}
    }
    $health=Test-RevitExternalConnection -Source $source -TimeoutMs 300
    Check (!$health.Connected) 'offline Revit returns unconfirmed without undoing saved settings'
    Write-Host "Passed $script:checks setup checks; actual user settings and Revit model were not modified."
} finally {
    $resolved=[IO.Path]::GetFullPath($fixture)
    $expected=[IO.Path]::GetFullPath($PSScriptRoot)+[IO.Path]::DirectorySeparatorChar
    if(!$resolved.StartsWith($expected,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture cleanup path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
