# TYBIM 外部 AI 連線設定工具 v1.4.0

v1.4.0 的 Claude Desktop 設定依實際資料位置偵測。涵蓋傳統 `%APPDATA%/Claude`、新版 `%LOCALAPPDATA%/Claude-Data/claude_*`、MSIX 的 `LocalCache/Roaming/Claude` 與執行中 `--user-data-dir` 指定的位置。已搬移的 MSIX 目錄透過 `.user-data-left*` 標記略過。Windows 官網安裝程式、Microsoft Store 與企業 MSIX 可能使用相同封裝與資料結構，無法單憑 WindowsApps 路徑判定安裝來源。

偵測到多份 Desktop 資料目錄時，每份設定都會列入檢查、備份、建立、清除與逐檔讀回驗證，保留各自的其他 MCP 及偏好。尚未產生資料目錄的新使用者預設採傳統路徑；請先啟動並登入 Claude 一次，再完全結束後執行精靈，讓新版安裝完成資料目錄初始化。舊 Desktop 完成紀錄會要求重新設定一次。Claude Code 的本機 CLI／IDE 使用 user scope `.claude.json`，繼續支援 `CLAUDE_CONFIG_DIR`。

設定寫入與 Revit health 成功會分別顯示。完成後仍須重啟 AI，在該用戶端確認 TYBIM 本機 MCP 已載入並實際查詢 Revit，才能驗收端到端連線。此 Windows 精靈服務於同機 Revit，不設定瀏覽器／手機端的遠端連接器。

v1.3.0 已接入 TYBIM MCP 的首次外部服務啟用流程。MSI 會內附本資料夾；Revit 首次啟用外部服務自動開啟精靈，成功套用後記錄完成狀態，不再反覆提示。可用「拓源數位 → MCP → AI連線設定」隨時重開，開啟精靈不會自動切換外部服務。取消精靈時保持服務運作；再次啟用外部服務時可重新設定，執行中的精靈不重複開啟。

完成狀態在 `%LOCALAPPDATA%\TYBIM\McpClientSetup\<年份>\completed.json`，只記錄安裝位置與已設定用戶端的設定檔路徑，不包含權杖或 API Key。外部服務會檢查已設定項目是否仍存在、可用；失效時再提供精靈。

已確認此電腦目前介面入口：Codex「設定 → 外掛程式 → MCP → tybim_revit_2025 右側齒輪」。這個版本不使用獨立的「MCP Servers」設定選單。「程式設計 → 連線」是遠端裝置管理，與 MCP 不同。Claude Desktop 的開發者頁是管理畫面，不是每次使用前必填的設定提醒；帳號登入、信任工作資料夾與工具操作授權，仍依各 AI 用戶端規則處理，本工具不會關閉這些提示。

本工具已隨 TYBIM MCP 安裝，不需要另外下載設定 ZIP。不需要另外安裝 PowerShell 7、Node、Codex CLI 或以管理員執行；使用 Windows 內建 PowerShell 5.1 與既有 Revit MCP runtime。

## 使用

1. 安裝 TYBIM MCP，無須先建立任何 MCP 設定或安裝其他 Node MCP。
2. 先安裝對應年度的 TYBIM MCP，在 Revit 啟用「外部 AI 服務」一次，讓外掛產生本機設定。
3. 完全結束要設定的 Codex／Claude（包含系統匣），避免它們同時儲存設定。
4. 首次啟用自動開啟精靈，預設勾選 Codex、Claude Code；使用桌面聊天可另勾 Claude Desktop。亦可用「AI連線設定」開啟。
5. 直接按「建立 TYBIM 連線」；「檢查設定」為選用。完成後自動讀回驗證並以唯讀 health 請求檢查 Revit 連線。
6. 重新啟動所選 AI。Revit「外部 AI 服務」需保持開啟，「AI助手」可以關閉。輸入「讀取目前 Revit 專案資訊與樓層，先不要修改模型」。

檔案檢查只確認設定與 runtime 存在，不代表 Revit 已開啟或服務已連線；需由 AI 成功呼叫 Revit 讀取工具確認。MCP 入口使用本機 stdio 程序再連接外部 TCP 服務，不是雲端 HTTP 連接器。

## 寫入位置

| 用戶端 | 設定檔 | TYBIM 項目名稱 |
|---|---|---|
| Codex | `%USERPROFILE%\.codex\config.toml`，有 `CODEX_HOME` 時使用該目錄 | `tybim_revit_2025` |
| Claude Code | `%USERPROFILE%\.claude.json`，有 `CLAUDE_CONFIG_DIR` 時使用該目錄內的 `.claude.json` | `tybim_revit_2025_external` |
| Claude Desktop | `%APPDATA%\Claude\claude_desktop_config.json` | `tybim_revit_2025_external` |

其他年度替換名稱中的年份；不會刪除其他年份。Claude Code 寫入 user scope，若特定專案另有同名的 local／project scope 覆寫，需在該專案用 `/mcp` 核對；本工具不改寫專案設定。Desktop 僅限本機一般聊天，雲端 Code／瀏覽器聊天不適用。

安裝位置預設 `%APPDATA%\Autodesk\Revit\Addins\TYBIM_MCP\<年份>`。讀取該處 `external-ai.mcp.json`，檢查 `external.bridge.config.json` 的外部埠號；支援既有自訂埠，不另開服務、不改埠，拒絕 8964 或與內部服務相同的埠。不將權杖／API Key 複製到用戶端設定。

只更新所選 AI 的本年度 TYBIM 項目；保留其他 MCP、模型與登入設定。Codex 本項目工具逾時為 240 秒，適用較長的建模操作，逾時仍應查詢原 operationId，避免重複執行。工具不安裝 AI 程式、不替使用者登入，也不更改工具審批規則。

## 備份與復原

每份有變更的既有設定在原檔旁自動備份為 `原檔名.before-tybim-時間戳-識別碼.bak`。新建檔案沒有舊檔備份。先規劃全部檔案；任何檔案格式無效時全都不寫入。套用期間失敗會復原本次已寫入內容；若偵測到其他程式同時修改則停止並列出需人工復原的檔案。

要自行復原時，先完全結束相關 AI，將 `.bak` 複製回原檔名，再重新啟動。備份包含原本完整設定，可能含其他服務憑證，請留在自己的電腦，不要上傳或放入分享 ZIP。對於含多行字串、行內 MCP 表格等特殊 TOML，工具會拒絕自動改寫，避免破壞設定。

## PowerShell 自動化（可選）

```powershell
# 請在解壓縮資料夾內執行；先模擬，不修改任何設定
.\Setup-ExternalAi.ps1 -RevitVersion 2025 -Clients Codex,ClaudeCode -WhatIf
# 確認後套用
.\Setup-ExternalAi.ps1 -RevitVersion 2025 -Clients Codex,ClaudeCode
# 非預設安裝目錄
.\Setup-ExternalAi.ps1 -RevitVersion 2025 -Clients ClaudeDesktop -InstallDirectory 'D:\TYBIM\2025'
```

CMD 啟動器只對這次 PowerShell 程序使用 ExecutionPolicy Bypass，不永久更改系統執行原則。公司原則若禁止腳本，請請管理員核准工具；不需要關閉安全軟體。

格式依據：[Claude Code MCP](https://code.claude.com/docs/en/mcp)、[Claude Desktop 本機設定](https://modelcontextprotocol.io/docs/develop/connect-local-servers)。Codex 格式依本機 `codex mcp add --help` 及既有使用中的 MCP 設定確認。這些客戶端需使用者自己的帳號與可用方案，與 Revit 內建助手 API Key 設定分開。

## 清除設定與版本選單（v1.3.0）

版本由目前使用者已安裝的 TYBIM MCP 檔案偵測，只列 2024–2026 中實際具有外掛 DLL、MCP runtime 與精靈的版本，依年份遞增排列。由 Revit 開啟時預選相同年份，切換後使用該年份目錄；只有殘留設定、未安裝 TYBIM 的年份不列出。其他年份若尚未產生外部服務設定，請先在該年份 Revit 啟用一次外部 AI 服務。

先完全結束所選 AI，在精靈選擇年份與用戶端後按「清除 TYBIM 設定」。確認視窗列出實際檔案，確認後備份並只移除該年份 TYBIM 項目與其 Codex 子表格，不刪帳號、node_repl、其他 MCP、其他年份、外掛或外部服務權杖，不變更 Revit 服務開關。缺少的設定不會為了清除而新建。清除後讀回驗證，再重啟 AI；可隨時重新建立連線。

精靈與清除確認使用安裝目錄 Pic/TYBIM.ico，原始圖示維持專案 Pic/TYBIM.ico。
