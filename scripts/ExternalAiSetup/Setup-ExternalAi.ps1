# Copyright 2026 拓源數位科技. All rights reserved.
[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [ValidateSet('2024','2025','2026')][string]$RevitVersion='2025',
    [ValidateSet('Codex','ClaudeCode','ClaudeDesktop')][string[]]$Clients,
    [string]$InstallDirectory,
    [switch]$ValidateUi
)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'ClientSetup.psm1') -Force

if($Clients) {
    $source=Get-ClientSetupSource -RevitVersion $RevitVersion -InstallDirectory $InstallDirectory
    $plan=@(New-ClientSetupPlan -Source $source -Clients $Clients)
    Invoke-ClientSetupPlan -Plan $plan -WhatIf:$WhatIfPreference
    if($WhatIfPreference) { Write-Host '模擬完成，未寫入任何設定。' }
    else { Test-ClientSetupApplied -Source $source -Plan $plan | Out-Null; Save-ClientSetupReceipt -Source $source -Plan $plan; Write-Host "設定完成。外部埠號：$($source.Port)。請重新啟動所選 AI；Revit 外部 AI 服務須開啟。未執行模型操作。" }
    exit 0
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$form=[Windows.Forms.Form]::new()
$form.Text='TYBIM MCP — AI 連線設定 v1.4.0'
$iconPath=Join-Path (Split-Path $PSScriptRoot -Parent) 'Pic/TYBIM.ico'
if(!(Test-Path -LiteralPath $iconPath)){$iconPath=Join-Path $PSScriptRoot '../../Pic/TYBIM.ico'}
if(Test-Path -LiteralPath $iconPath){$form.Icon=[Drawing.Icon]::new([IO.Path]::GetFullPath($iconPath))}
$form.StartPosition='CenterScreen'
$form.Size=[Drawing.Size]::new(780,660)
$form.MinimumSize=[Drawing.Size]::new(660,580)
$form.Font=[Drawing.Font]::new('Microsoft JhengHei UI',10)
$form.AutoScaleMode=[Windows.Forms.AutoScaleMode]::Dpi
$panel=[Windows.Forms.TableLayoutPanel]::new()
$panel.Dock='Fill';$panel.ColumnCount=1;$panel.RowCount=6;$panel.Padding=[Windows.Forms.Padding]::new(16)
foreach($height in @(106,42,46)) { $panel.RowStyles.Add([Windows.Forms.RowStyle]::new([Windows.Forms.SizeType]::Absolute,$height)) | Out-Null }
$panel.RowStyles.Add([Windows.Forms.RowStyle]::new([Windows.Forms.SizeType]::Percent,100)) | Out-Null
foreach($height in @(48,60)) { $panel.RowStyles.Add([Windows.Forms.RowStyle]::new([Windows.Forms.SizeType]::Absolute,$height)) | Out-Null }
$form.Controls.Add($panel)
$intro=[Windows.Forms.Label]::new();$intro.Dock='Fill'
$intro.Text="第一次使用、沒有任何 MCP 設定也可以直接建立。TYBIM 已內附 Node.js。`r`n1. 選擇要使用的 AI；完全結束這些 AI 程式（含系統匣）。`r`n2. 按「建立 TYBIM 連線」，完成後重新開啟 AI。`r`n不用安裝其他 Node MCP；既有帳號、其他 MCP 與偏好設定會保留。"
$panel.Controls.Add($intro,0,0)
$yearRow=[Windows.Forms.FlowLayoutPanel]::new();$yearRow.Dock='Fill'
$label=[Windows.Forms.Label]::new();$label.Text='Revit 版本';$label.AutoSize=$true;$label.Padding=[Windows.Forms.Padding]::new(0,7,8,0)
$year=[Windows.Forms.ComboBox]::new();$year.DropDownStyle='DropDownList';$year.Width=120
$installed=@(Get-InstalledTybimVersions -InstallDirectory $InstallDirectory -CurrentYear $RevitVersion)
$script:installedDirectories=@{}
foreach($item in $installed){$year.Items.Add($item.Year) | Out-Null;$script:installedDirectories[$item.Year]=$item.Directory}
if($script:installedDirectories.ContainsKey($RevitVersion)){$year.SelectedItem=$RevitVersion}
elseif($year.Items.Count){$year.SelectedIndex=0}
function Get-SelectedSetupDirectory {
    if(!$year.SelectedItem){throw '找不到已安裝 TYBIM MCP 的版本，請先安裝外掛。'}
    $script:installedDirectories[[string]$year.SelectedItem]
}
function Confirm-SetupRemoval([string]$Text) {
    $dialog=[Windows.Forms.Form]::new();$dialog.Text='清除 TYBIM 設定';$dialog.Icon=$form.Icon
    $dialog.Size=[Drawing.Size]::new(600,380);$dialog.StartPosition='CenterParent';$dialog.Font=$form.Font
    $dialog.MinimizeBox=$false;$dialog.MaximizeBox=$false;$dialog.ShowInTaskbar=$false
    $content=[Windows.Forms.TextBox]::new();$content.Multiline=$true;$content.ReadOnly=$true;$content.Text=$Text
    $content.ScrollBars='Vertical';$content.Dock='Fill'
    $row=[Windows.Forms.FlowLayoutPanel]::new();$row.Dock='Bottom';$row.Height=48;$row.FlowDirection='RightToLeft'
    $cancel=[Windows.Forms.Button]::new();$cancel.Text='取消';$cancel.DialogResult='Cancel';$cancel.Size=[Drawing.Size]::new(90,34)
    $yes=[Windows.Forms.Button]::new();$yes.Text='確認清除';$yes.DialogResult='Yes';$yes.Size=[Drawing.Size]::new(110,34)
    $row.Controls.AddRange(@($cancel,$yes));$dialog.Controls.Add($content);$dialog.Controls.Add($row)
    $dialog.CancelButton=$cancel;$dialog.AcceptButton=$cancel
    try{return ($dialog.ShowDialog($form) -eq [Windows.Forms.DialogResult]::Yes)}finally{$dialog.Dispose()}
}
$yearRow.Controls.Add($label);$yearRow.Controls.Add($year);$panel.Controls.Add($yearRow,0,1)
$choices=[Windows.Forms.FlowLayoutPanel]::new();$choices.Dock='Fill'
$checks=@{}
foreach($pair in @(@('Codex','Codex'),@('ClaudeCode','Claude Code（命令列／本機）'),@('ClaudeDesktop','Claude Desktop（聊天）'))) {
    $box=[Windows.Forms.CheckBox]::new();$box.Text=$pair[1];$box.AutoSize=$true;$box.Margin=[Windows.Forms.Padding]::new(0,6,18,0)
    $box.Checked=$pair[0] -in @('Codex','ClaudeCode');$checks[$pair[0]]=$box;$choices.Controls.Add($box)
}
$panel.Controls.Add($choices,0,2)
$output=[Windows.Forms.TextBox]::new();$output.Multiline=$true;$output.ReadOnly=$true;$output.ScrollBars='Both';$output.WordWrap=$false;$output.Dock='Fill'
$output.Text="預設建立 Codex、Claude Code 的 TYBIM 連線，可自行取消勾選。`r`n使用 Claude 桌面版一般聊天時，請另勾選 Claude Desktop。`r`n即使設定檔或資料夾不存在，也會自動建立。`r`n只會新增／更新 TYBIM，不會建立 node_repl 或舊 mcp-server-for-revit。"
$panel.Controls.Add($output,0,3)
$buttons=[Windows.Forms.FlowLayoutPanel]::new();$buttons.Dock='Fill';$buttons.FlowDirection='RightToLeft'
$close=[Windows.Forms.Button]::new();$close.Text='關閉';$close.Size=[Drawing.Size]::new(90,34);$close.Add_Click({$form.Close()})
$apply=[Windows.Forms.Button]::new();$apply.Text='建立 TYBIM 連線';$apply.Size=[Drawing.Size]::new(165,34);$apply.Enabled=$true
$check=[Windows.Forms.Button]::new();$check.Text='檢查設定';$check.Size=[Drawing.Size]::new(120,34)
$clear=[Windows.Forms.Button]::new();$clear.Text='清除 TYBIM 設定';$clear.Size=[Drawing.Size]::new(155,34)
$buttons.Controls.AddRange(@($close,$apply,$check,$clear));$panel.Controls.Add($buttons,0,4)
$footer=[Windows.Forms.Label]::new();$footer.Dock='Fill'
$footer.Text="套用後重新啟動 AI。Revit 的「AI助手」可保持關閉；「外部 AI 服務」須為開。`r`n不需要在此填 API Key；AI 帳號仍由各 AI 程式登入。檢查設定不等於 Revit 已連線。"
$panel.Controls.Add($footer,0,5)
$script:setupPlan=$null
$script:setupSource=$null
$invalidate={ $apply.Enabled=($year.Items.Count -gt 0);$script:setupPlan=$null }
$year.Add_SelectedIndexChanged($invalidate)
foreach($box in $checks.Values) { $box.Add_CheckedChanged($invalidate) }
$check.Add_Click({
    try {
        $apply.Enabled=$false
        $selected=@($checks.Keys | Where-Object {$checks[$_].Checked} | Sort-Object)
        $script:setupSource=Get-ClientSetupSource -RevitVersion ([string]$year.SelectedItem) -InstallDirectory (Get-SelectedSetupDirectory)
        $script:setupPlan=@(New-ClientSetupPlan -Source $script:setupSource -Clients $selected)
        $lines=@("檔案檢查通過。外部服務：127.0.0.1:$($script:setupSource.Port)","安裝目錄：$($script:setupSource.Directory)",'','將更新下列設定（僅 TYBIM MCP 項目）：')
        foreach($item in $script:setupPlan) { $lines+=$item.Client+' → '+$item.Path }
        $lines+=@('','既有設定變更前會在原檔旁建立 .before-tybim-時間戳.bak。','請確認要設定的 AI 程式已完全結束，再按「建立 TYBIM 連線」。')
        $output.Text=$lines -join "`r`n";$apply.Enabled=$true
    } catch { $script:setupPlan=$null;$output.Text='檢查未通過：'+$_.Exception.Message }
})
$apply.Add_Click({
    try {
        $apply.Enabled=$false;$check.Enabled=$false;$clear.Enabled=$false
        $selected=@($checks.Keys | Where-Object {$checks[$_].Checked} | Sort-Object)
        $script:setupSource=Get-ClientSetupSource -RevitVersion ([string]$year.SelectedItem) -InstallDirectory (Get-SelectedSetupDirectory)
        $script:setupPlan=@(New-ClientSetupPlan -Source $script:setupSource -Clients $selected)
        $result=@(Invoke-ClientSetupPlan -Plan $script:setupPlan)
        Test-ClientSetupApplied -Source $script:setupSource -Plan $script:setupPlan | Out-Null
        $lines=@('TYBIM 註冊已寫入，所有設定檔讀回驗證通過。請重新啟動所選 AI。','AI 是否載入工具，仍需在重新啟動後確認本機 MCP 清單。','')
        try { Save-ClientSetupReceipt -Source $script:setupSource -Plan $script:setupPlan }
        catch { $lines+='用戶端設定已套用，但無法記錄完成狀態；下次 Revit 可能再次提示：'+$_.Exception.Message }
        foreach($item in $result) { $lines+=$item.Client+'：'+$(if($item.Changed){'已更新'}else{'已是相同設定'});$lines+='設定檔：'+$item.Path;if($item.Backup){$lines+='備份：'+$item.Backup} }
        $output.Text=($lines+@('','正在檢查 Revit 外部服務連線……')) -join "`r`n";$output.Refresh()
        $connection=Test-RevitExternalConnection -Source $script:setupSource
        $lines+=@('',$connection.Message,'','沒有發送付費 AI 對話或修改模型。重新開啟 AI 後可輸入：','「讀取目前 Revit 專案資訊與樓層，先不要修改模型。」','Codex：設定 → 外掛程式 → MCP；Claude Code：/mcp。','Claude Desktop：設定 → 開發者 → 本機 MCP 伺服器。')
        $output.Text=$lines -join "`r`n"
    } catch { $output.Text='設定未完成：'+$_.Exception.Message;$apply.Enabled=$true }
    finally {$check.Enabled=$true;$clear.Enabled=$true}
    $script:setupPlan=$null
})
$clear.Add_Click({
    try {
        $null=Get-SelectedSetupDirectory
        $selected=@($checks.Keys | Where-Object {$checks[$_].Checked} | Sort-Object)
        $selectedYear=[string]$year.SelectedItem
        $identity=[pscustomobject]@{Year=$selectedYear;Name=('tybim_revit_'+$selectedYear+'_external')}
        $plan=@(New-ClientSetupPlan -Source $identity -Clients $selected -Remove)
        $changed=@($plan | Where-Object Changed)
        if(!$changed.Count){$output.Text='所選 AI 沒有此版本的 TYBIM 設定，無須清除。';return}
        $details=($changed | ForEach-Object {$_.Client+' → '+$_.Path}) -join "`r`n"
        if(!(Confirm-SetupRemoval "清除 Revit $selectedYear 在所選 AI 的 TYBIM 連線？`r`n$details`r`n`r`n會先備份；保留其他 MCP、其他年份、AI 帳號及偏好。Revit 外部服務開關不變。`r`n請先完全結束要清除設定的 AI 程式。")){return}
        $result=@(Invoke-ClientSetupPlan -Plan $plan)
        $remaining=@(New-ClientSetupPlan -Source $identity -Clients $selected -Remove)
        if(@($remaining | Where-Object Changed).Count){throw '清除後讀回驗證不一致，請先完全結束 AI 再重試。'}
        $lines=@('所選 AI 的本年度 TYBIM 設定已清除，讀回驗證通過。','請完全結束並重新開啟 AI，才會更新工具清單。','需要恢復連線時，可直接按「建立 TYBIM 連線」。','')
        foreach($item in $result){if($item.Backup){$lines+='備份：'+$item.Backup}}
        $output.Text=$lines -join "`r`n";$apply.Enabled=$true;$script:setupPlan=$null
    } catch {$output.Text='清除未完成：'+$_.Exception.Message}
})
if(!$year.Items.Count){$apply.Enabled=$false;$check.Enabled=$false;$clear.Enabled=$false;$output.Text='找不到已安裝 TYBIM MCP 的版本。請先安裝對應 Revit 年份的 TYBIM MCP。'}
if($ValidateUi) {
    $form.CreateControl();$form.PerformLayout();$panel.PerformLayout()
    if($output.Height -lt 80 -or $buttons.Bottom -gt $panel.ClientSize.Height -or $footer.Bottom -gt $panel.ClientSize.Height) { throw '視窗配置超出可用範圍。' }
    if($null -eq $form.Icon){throw 'Missing TYBIM window icon.'}
    if($script:installedDirectories.ContainsKey($RevitVersion) -and $year.SelectedItem -ne $RevitVersion){throw 'Current Revit year was not selected.'}
    Write-Output ('Detected installed TYBIM years: '+($year.Items -join ',')+'; selected: '+$year.SelectedItem)
    Write-Output 'PASS: GUI controls fit the default window; configuration writes were not invoked.'
    $form.Dispose();exit 0
}
$form.Add_Shown({
    $form.WindowState=[Windows.Forms.FormWindowState]::Normal
    $form.TopMost=$true
    $form.Activate()
    $form.TopMost=$false
})
[void]$form.ShowDialog()
$form.Dispose()
