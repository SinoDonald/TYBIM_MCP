# 外部 AI 服務（2026-11-13）

## v0.12.1：重複設定與全新用戶端驗證

重現 Windows PowerShell 5.1／PowerShell 7 將 File.Replace 第三參數的 $null 繫結為空字串，使已存在的 completed.json 無法更新。改用明確的暫時備份路徑，替換成功後清除暫存；新增重複設定與三種完全空白用戶端設定測試，兩種 PowerShell 的 31 項測試通過。

TYBIM MSI 自帶自己的 runtime/node/node.exe，設定精靈直接以它啟動 runtime/build/index.js。Codex 的 node_repl 是另一個工具執行環境，舊 mcp-server-for-revit 也是另一套 MCP，兩者都不是 TYBIM 的前置條件，精靈不會新增／還原它們。新電腦只需對應版本 Revit、已安裝並登入的 AI 客戶端與 TYBIM MSI，再由外部服務精靈登錄 TYBIM 即可；不需預先註冊任何「Node MCP」。

## v0.12.0：三版 MSI 內附首次使用設定精靈

外部服務啟用後，若沒有有效完成紀錄，自動開啟內附的設定精靈；v0.13.0 起取消不關閉服務，再次啟用外部服務仍可重開精靈，執行中不重複開啟。成功套用後檢查 Codex／Claude 項目與外部設定一致，之後不反覆提示；紀錄損壞、設定項目移除或 Codex 停用時重新提供。另有獨立 Ribbon「AI連線設定」可隨時重開，該按鈕只準備本機設定並開啟精靈，不切換 listener。

Package-Chat 會將四個精靈檔案放入安裝內容的 client-setup，Prepare-Payload 及 Generate-Setup 自動納入，Verify-Msi 強制確認精靈檔案存在。本次已製作 Revit 2024／2025／2026 v0.12.0 MSI，均含設定精靈；未代為安裝或修改目前用戶端設定。三版完成 API 編譯、MSI 逐檔雜湊、依賴載入及解除安裝登錄驗證；實機安裝與 Ribbon 操作由使用者測試。

本機介面實查：Codex「設定 → 外掛程式 → MCP → tybim_revit_2025 齒輪」；不再指引不存在於這個版本的獨立 MCP Servers 選單。首次設定可省去手動編輯設定檔，但不會略過帳號登入、信任專案或工具授權提示。

## 2026-11-18 獨立設定工具

新增 `scripts/ExternalAiSetup`：雙擊 `Start-Setup.cmd` 開啟設定視窗，預設 2025，可選 Codex／Claude Code／Claude Desktop，先檢查再套用。不需 Codex CLI，不額外安裝 Node，不重製 Revit MCP MSI。完整解壓縮 早期獨立設定 ZIP（已由 MSI 內附精靈取代並清除） 後使用。

從已安裝外掛匯出的 external-ai.mcp.json 取得路徑，保留其他用戶端設定與 MCP，備份後原子寫入；先檢查全部設定，寫入失敗會復原本次已完成的檔案。支援既有自訂外部埠號、CODEX_HOME／CLAUDE_CONFIG_DIR，拒絕 8964、誤用內部埠、無效 JSON／不支援的特殊 TOML；不寫入橋接權杖或 AI API Key，不更動用戶端審批。請先完全結束所選 AI 程式，套用後再重開；Revit 外部服務需開啟，AI助手可保持關閉。

驗證：Windows PowerShell 5.1 的 25 項獨立設定測試與視窗配置檢查通過；本機安裝／三用戶端設定唯讀預檢通過（外部埠 21825），未實際套用、未修改使用者設定或模型。ZIP 白名單與逐檔 SHA256 比對通過。詳細操作與備份復原見 [設定工具說明](../scripts/ExternalAiSetup/README.md)。獨立 ZIP 為早期交付；其打包腳本現已移除，改由 Package-Chat 隨主程式打包。

## 2026-11-16 本機連線設定及實機驗證

已備份並將此電腦的外部用戶端切換至安裝目錄的 external.bridge.config.json（21825）：

- Codex：使用者 .codex/config.toml 中的 tybim_revit_2025。
- Claude Code：使用者 .claude.json 中的 tybim_revit_2025_external。
- Claude Desktop：AppData/Roaming/Claude/claude_desktop_config.json 中的 tybim_revit_2025_external；保留其他既有 Server。

各設定原檔旁有 before-external-時間戳.bak 備份（備份可能包含原設定的敏感資訊，不應上傳）。
當時使用的一次性連線設定腳本已移除；現在使用「AI連線設定」精靈，支援新使用者、三個年份、選擇用戶端與備份復原。
不修改 Revit 內部 bridge.config.json 或 AI 登入資料。已啟動的用戶端需重新載入 MCP／重新啟動才會使用新設定，Revit 不必重開。

實機測試使用官方 MCP SDK，依 external-ai.mcp.json 啟動與外部客戶端相同的 stdio Server：
AI助手面板已收合、17825 未監聽、21825 的 revit_health 成功（Revit PID 8948）；
讀取專案「磚牆排列_2025」、視圖 {3D}，樓層 FL1=0 mm、FL2=4000 mm。未修改模型。
可透過 server/scripts/read-external-revit.mjs 加上 external-ai.mcp.json 絕對路徑重跑唯讀測試。
此測試驗證實際外部 MCP 工具連線，沒有提交 Claude／Codex 的付費 AI 對話。
另以 codex mcp get 確認外部設定已載入；claude mcp get 顯示 User config、Status: Connected。

「拓源數位 → MCP → 外部AI／服務：關」切換外部服務；每次啟動 Revit 預設關閉。
成功後按鈕顯示「服務：開」，再次點選即可關閉。內建 AI助手的面板與服務開關維持原本行為，兩者互不影響。
外部操作不需要在內建面板登入 AI 或設定 API Key。

2025 預設埠為 **21825**（19800 + Revit 年份），僅監聽 `127.0.0.1`。
設定會拒絕 8964、內部相同埠或相同權杖。若埠被占用，啟用失敗，內部服務仍可使用。
先關閉外部服務，修改 external.bridge.config.json 的 port，再啟用並讓用戶端重連即可換埠。

## 部署 Revit 2024／2025／2026

先儲存模型並關閉 Revit，安裝對應年度 MSI。安裝目錄：

```text
%APPDATA%\Autodesk\Revit\Addins\TYBIM_MCP\<年份>
```

首次開啟外部服務會產生本機 external.bridge.config.json 與 external-ai.mcp.json，並開啟 AI 連線設定精靈。不需手動編輯用戶端設定。開發者使用完整部署套件與 Install-RevitAddin；舊教學 Debug 部署腳本已移除。

權杖保留在本機外部 bridge 設定，不上傳或放入 MSI。設定精靈不會修改內部 AI助手連線、AI 登入資料或審批規則。

下方 CLI／Desktop 手動步驟保留作為進階排錯參考；一般安裝使用設定精靈即可。

## Claude Code CLI

使用 Windows 本機 Claude Code（不是雲端工作階段），並以自己的 Claude 帳號登入。Revit 須開啟且外部服務為開。
以下在 **PowerShell** 執行一次，從產生的設定取得實際路徑：

```powershell
$configFile = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\TYBIM_MCP\2025\external-ai.mcp.json'
$config = Get-Content -LiteralPath $configFile -Raw | ConvertFrom-Json
$entry = $config.mcpServers.tybim_revit_2025_external
$nodeExecutable = $entry.command
$runtimeScript = $entry.args[0]
$bridgePath = $entry.env.TYBIM_MCP_CONFIG
claude mcp add --scope user --env "TYBIM_MCP_CONFIG=$bridgePath" --transport stdio tybim_revit_2025_external -- "$nodeExecutable" "$runtimeScript"
```

若已存在同名設定，先用 claude mcp get tybim_revit_2025_external 核對；需要更新才 remove 再 add。
重新啟動 claude，用 /mcp 查看狀態。可在專用資料夾啟動，不必把整個原始碼專案當工作範圍。

輸入：

> 請使用 tybim_revit_2025_external，先執行 revit_health，再讀取目前專案資訊與樓層。先不要修改模型。

MCP 顯示 Connected 只代表 Server 啟動，以 revit_health 成功為 Revit 已連線的依據。

## Claude Desktop 一般聊天

1. 開啟 Revit 並啟用外部服務。
2. Claude Desktop 的 Settings → Developer → Edit Config，開啟 claude_desktop_config.json。Windows 通常位於 %APPDATA%\Claude\claude_desktop_config.json。
3. 開啟安裝目錄的 external-ai.mcp.json，把 mcpServers 內的 tybim_revit_2025_external 整個項目合併到 Claude 設定的 mcpServers。若沒有其他設定可使用整份內容；已有其他 Server 時不要覆蓋它們。
4. 儲存有效 JSON（注意項目間的逗號），完全結束 Claude Desktop（含系統匣）再啟動。
5. 在一般聊天中啟用 TYBIM 工具，使用上方唯讀測試句並依 Claude 提示核准工具使用。

CLI 與 Desktop 先測通一種即可。桌面 Code 分頁也可能讀取 Desktop／CLI 的設定，同名 Server 應保持內容一致。

## 內外部同時使用

兩個入口各有獨立 listener、dispatcher 與要求佇列，使用相同的指令實作。關閉一邊只拒絕該邊尚未執行的要求。
模型操作透過 ExternalEvent 在 Revit UI 執行緒依序執行，已開始的操作以實際結果為準。
兩邊不共享對話或預覽狀態。外部權限確認由外部用戶端處理，不會出現內建 AI助手的確認卡；工具自身的文件檢查與交易邏輯仍保留。
避免兩邊同時修改同一元素；另一邊變更模型後先重新查詢。

## 程式位置與驗證

- src/Tybim.Revit.Addin/Application.cs：內外部服務控制與清理。
- src/Tybim.Revit.Addin/BridgeEndpoint.cs：每個入口的服務生命週期與獨立佇列。
- src/Tybim.Revit.Addin/ExternalAiConfiguration.cs：設定載入、驗證與匯出。
- src/Tybim.Revit.Addin/ExternalAiCommand.cs、RibbonUi.cs：按鈕狀態、功能介紹與使用步驟。
- tests/PaneServiceRegression：loopback socket、服務獨立性、權杖隔離、埠占用與恢復、設定匯出測試。

2025 Debug 建置通過；服務回歸測試通過。Revit 真實 Ribbon 操作與 Claude 帳號連線尚待部署後實測。

官方參考：[Claude Code MCP](https://code.claude.com/docs/en/mcp)、[Claude Desktop／Code](https://code.claude.com/docs/en/desktop)、[本機 MCP 設定](https://modelcontextprotocol.io/docs/develop/connect-local-servers)。

## 2026-11-18：Revit 2025 v0.13.0 首次連線

以全新 Codex／Claude Code 沒有 MCP 設定為預設，安裝自帶 Node.js；啟用外部 AI 服務後精靈可直接建立 TYBIM 設定。Claude Desktop 為獨立選項，不依賴 node_repl 或舊 mcp-server-for-revit。寫入後讀回驗證，以唯讀 revit.health 核對連線年份。取消後再次啟用可重開精靈；首次啟用不再以 Revit 模態提示框阻擋連線檢查。用戶仍須登入 AI、重啟 AI 並依其規則確認信任與工具權限。

本次只產出 2025 安裝檔；42 項 PowerShell 隔離測試涵蓋空白設定、合併備份、異常與連線回應。實際新電腦 AI 用戶端安裝後操作由使用者測試；既有個人設定備份保留。

## 2026-11-19：v0.14.0 三年度安裝檔

AI 連線設定新增選定年份／用戶端的清除按鈕，備份後移除 TYBIM 項目、讀回驗證，保留其他設定；版本清單依已安裝外掛檔案偵測、遞增排列並預選啟動 Revit 年份。切換年份會切換安裝路徑。自有設定／確認、服務狀態、Hello World、相機預覽及記錄視窗採 Pic/TYBIM.ico；Revit 原生停靠框與依賴載入失敗的原生備援提示仍由 Revit 控制。服務狀態改用自有無模式視窗，避免擋住外部 MCP 查詢。
