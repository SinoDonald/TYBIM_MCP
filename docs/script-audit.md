# PowerShell 腳本整理

2026-11-19 再次盤點：剩餘 16 個 .ps1 約 84 KB，7 個供建置／維護、7 個供安裝製作與驗證、1 個為安裝後的 AI 連線精靈、1 個為封裝前的設定回歸測試。仍有實際引用，均保留。容量精簡改清除可重建副本、依賴快取與測試輸出，不刪除執行流程。

本次製作 Revit 2024／2025／2026 v0.12.0 時，已確認正式建置、安裝及設定流程的引用，移除五個被取代的腳本：

| 已移除 | 原用途 | 現在使用 |
|---|---|---|
| scripts/Prepare-Package.ps1 | 早期不含聊天 runtime 的核心部署 | Package-Chat.ps1／Build-Msi.ps1 |
| scripts/Update-ChatRuntime.ps1 | 舊安裝路徑的單檔 Codex 熱修 | 完整新版 MSI；開發時完整部署套件 |
| scripts/Update-TutorialDebug.ps1 | 限定 2025 教學指令的臨時 Debug 部署 | 正式建置與部署流程；教學原始碼仍保留 |
| scripts/Connect-ExternalAiClients.ps1 | 固定 2025、要求既有 Codex 項目的舊設定器 | 內附 AI 連線設定精靈 |
| scripts/Package-ExternalAiSetup.ps1 | 早期獨立設定 ZIP 打包 | 設定精靈隨主程式 MSI 打包 |

保留 scripts 中的 Build、Check-Prerequisites、Package-Chat、Install-RevitAddin、Clean-Workspace、Compare-Upstream、Export-UpstreamSnapshots；分別供正式建置、三年份參考檢查、完整套件、開發部署、清理及來源維護使用。

TYBIM_MCP_Setup 內的 Build-Msi、Prepare-Payload、Generate-Setup、Set-MsiRegistration、Export-MsiCabinet、Verify-Msi、Test-PayloadAssembly 與 Test-MsiUninstall 是安裝製作及驗證工具，不是測完就能丟棄的暫存腳本。scripts/ExternalAiSetup 的 Setup-ExternalAi.ps1／ClientSetup.psm1 是安裝後會使用的精靈，tests/ExternalAiSetup 的測試也由 Package-Chat 執行，均須保留。

已同步移除操作文件中指向舊腳本的執行指令。此整理不會刪除 AI 用戶端設定、Revit 模型或私人 API 程式碼。
