# Revit 內嵌 AI 對話面板

## v0.6.0 操作調整（2026-10-27）

模型與 Codex 執行檔皆改為唯讀下拉選單，不能手動輸入。按「連線／取得模型」後依供應商實際回傳更新模型選項；Codex 路徑從本機存在的執行檔中選取。以下舊版提及手動輸入的步驟以本節為準。

啟動時先載入所有命令；MCP 服務隨 AI助手顯示／隱藏切換，Ribbon 按鈕與視窗 X 都適用。因此外部 MCP 用戶端也必須先開啟 AI助手才能連線。收合會拒絕尚未開始的工具，已派送的工具仍等待結果，避免把未知寫入誤認為失敗而重送。

內部版本新增兩項 Donald 個人工具，預設共 260 項。CAD 自動建牆的操作、限制及本次採用的私人書面授權見 [個人功能與授權](personal-features.md)。

## v0.5.2 Claude／API 穩定性與三版 MSI（2026-10-21）

- Claude 與 Gemini 共用 ApiTools 本機功能搜尋，先載入本回合相關工具；既有文件身分驗證、完整參數確認及不明寫入狀態保護不變。Claude 也顯示等待、接收串流、工具後續回應及等待秒數。
- Claude 模型清單讀完分頁，保留 API 有回傳的 Sonnet／Haiku／Opus 4、5 系列；有 lifecycle 時僅接受 active，排除 preview／beta／experimental。預設依 Sonnet、Haiku、Opus 排序；舊的不可用模型會在重新連線時更正，選單不提供手動填入不相容模型。
- HTTP 500／502／503／504／529 最多額外重試兩次，採遞增延遲並遵守短 Retry-After。429 僅在供應商明確提供 15 秒內 Retry-After 時重試；額度不足、金鑰失效、權限不足、模型不存在不會盲目重試。重試使用同一份雲端請求與 180 秒期限，不重送 MCP 寫入。
- 已開始的回應串流若中斷，不會自動重播；Claude 未完整完成的工具 JSON 不派送。服務仍忙碌、金鑰無效或額度不足時，程式可顯示原因，不能保證雲端恢復或補足額度。
- 三版 0.5.2 MSI 分別使用 Revit 2024、2025、2026 API 編譯；Windows 解除安裝名稱與安裝流程見 [Setup 說明](../TYBIM_MCP_Setup/README.md)。API Key／Codex 個人登入檔不會進入 MSI。

參考：[Claude API 錯誤與重試](https://platform.claude.com/docs/en/api/errors)、[Claude 模型生命週期](https://platform.claude.com/docs/en/api/models/list)。

## v0.5.1 Gemini 修正（Revit 2025，2026-10-21）

- Gemini 模型選單只列出 live models API 有回傳的 `gemini-3.8-flash`、`gemini-3.5-flash-lite`（依建議順序）。排除 TTS、影像、Live、preview 及新專案受限的 2.5 系列；舊設定在重新連線時改為清單中的第一個建議模型。模型列出不代表推論權限或配額保證。
- 等待區顯示目前階段與本回合經過秒數，包含等待 AI、接收串流、執行工具與參數確認。不顯示或假造模型的內部推理。收到第一段實際回答時清除等待提示。
- Gemini 改為 `thinkingConfig.thinkingLevel=low`，常用工具及與本回合相關的最多八項工具先載入，其餘由本機 `tybim_search_revit_tools` 按需求發現。工具執行仍走原 gateway、文件檢查及確認；所有 258 個工具仍可搜尋。新對話會重設已載入工具。
- 建立多樓層優先使用 `revit_upstream_create_level` 的批次 data；先讀目前樓層，標高採 mm，350 cm = 3500 mm。是否建立平面圖需核對確認參數。
- 每次 AI 生成請求仍為 180 秒上限。逾時會顯示供應商、實際模型及已完成工具結果的注意事項，不自動重送寫入。Codex 的 app-server 與 Gemini 的 REST adapter 是不同流程；這些修正處理已確認的請求過大與進度不明問題，不能保證雲端延遲或免費配額。

Google 參考：[模型](https://ai.google.dev/gemini-api/docs/models)、[低思考強度 REST 設定](https://ai.google.dev/gemini-api/docs/generate-content/thinking)。

此版本在 Revit 的「拓源數位 → MCP → AI助手」提供可停駐於右側的對話面板。
v0.3.0 已整合上游 26 項功能；新增工具以 `revit_upstream_` 開頭，沿用相同 AI 引擎與登入。新增寫入工具顯示完整參數確認卡片，原 TYBIM 放門保留幾何試放預覽。詳見 [上游功能整合說明](upstream-integration.md)。

v0.4.0 另加入 220 個 `revit_study_` 工具，預設總數 258；相機管理在同一 MCP panel。重開 Revit 後重新連線、開始新對話即可取得新清單，既有登入保留。Study 原生交易及失敗可能部分變更的差異見 [Study 說明](study-integration.md)，私人開發入口見 [來源分類](source-ownership.md)。
與 TYBIM 共用「拓源數位」頁籤；已有 MCP 面板時直接共用，沒有時自行建立。按鈕使用獨立內部 ID，不依安裝先後建立不同頁籤。同一註冊流程不重複新增此按鈕。圖示與 250 像素寬的原圖提示嵌入 DLL，安裝時不依賴原始 Pic 路徑；懸停顯示【功能介紹】及【使用步驟】。
v0.2.3 修正圖示 DPI：原 32×32 PNG 帶有 300 DPI，載入時轉為 96 DPI 的 BitmapSource，維持原像素並以 32×32 WPF 單位顯示，不另外指定 16×16 Image。程式位置：Tybim.Revit.Addin 專案的 RibbonUi.cs（按鈕與圖片），Application.cs（啟動呼叫），csproj（嵌入 Pic 資源）。
支援 Codex（官方 ChatGPT 登入）、Claude API Key、Gemini API Key。三家共用原有 TYBIM MCP 工具。
不必另外開啟 Codex／Claude 桌面視窗。需要網路連線；首次 Codex 授權會開啟瀏覽器。

## 建置與安裝（先以 Revit 2025 使用）

開發環境已有 Node.js 24、.NET SDK 與本機 Revit API，執行：

```powershell
cd C:\Prj\Revit\TYBIM_MCP
./scripts/Build.ps1 -RevitVersion 2025
./scripts/Package-Chat.ps1 -RevitVersion 2025
```

關閉 Revit 並儲存模型後安裝：

```powershell
& 'C:\Prj\Revit\TYBIM_MCP\artifacts\packages\2025\Install-RevitAddin.ps1' -RevitVersion 2025
```

安裝程式複製到：

```text
%APPDATA%\Autodesk\Revit\Addins\
├─ 2025\TYBIM.Mcp.addin
└─ TYBIM_MCP\2025\
   ├─ Tybim.Revit.Addin.dll
   ├─ Tybim.Revit.Commands.dll
   ├─ Tybim.Mcp.Contracts.dll
   ├─ Newtonsoft.Json.dll
   ├─ 其他編譯相依檔案
   ├─ bridge.config.json
   ├─ chat.config.json
   └─ runtime\
      ├─ package.json
      ├─ build\
      └─ node_modules\
```

完整套件位於 `artifacts/packages/2025`。不可只複製單一 DLL，對話面板還需要 runtime 與 Node.js。
Revit 2025 的 DPAPI 支援由 Windows Desktop .NET 8 runtime 提供，安裝包不另外複製該 framework DLL。
Installer 會更新既有同名 manifest、保留已安裝的 bridge token 與 chat.config.json，並在旁邊建立 `.addin.backup`。manifest 的 Assembly 為 `..\TYBIM_MCP\2025\Tybim.Revit.Addin.dll`。
不修改其他外掛或 Codex 全域設定。若以前另外建立過其他名稱的 TYBIM MCP manifest，請移除重複註冊。

如果不使用安裝腳本，可將整個 `Tybim.Mcp` 資料夾放到上述 Addins 目錄，再將既有 `TYBIM.Mcp.addin` 的 Assembly 改為該資料夾內 DLL 的絕對路徑；AddInId／FullClassName 保持原值。

Revit 2024 使用相同命令並將年份改成 2024；其外掛為 .NET Framework 4.8，必須使用 2024 套件。
安裝到其他電腦時仍需安裝 Node.js 24，並更新 `chat.config.json` 的 nodeExecutable 路徑。

## Codex 登入

1. 確認有可使用的 `codex.exe`。本機可由已安裝 Codex 的執行檔自動找到；也可在面板「連線設定」指定完整路徑。
2. 開啟 Revit，點選「拓源數位 → MCP → AI助手」。
3. 選擇「Codex · ChatGPT 登入」，按「登入 Codex」。首次會由官方 app-server 開啟瀏覽器授權。
4. 以有 Codex 使用權限的 ChatGPT 帳號登入；回到面板看到登入完成後，按「連線／取得模型」。
5. 選擇清單中的模型，再送出訊息。

Codex 的登入狀態保存在 `%LOCALAPPDATA%\TYBIM\McpChat\2025\codex`，與桌面版／CLI 全域設定分開。
不複製 `%USERPROFILE%\.codex\auth.json` 或其他應用程式的 token。即使桌面版已登入，此面板首次仍可能需要獨立登入。
官方 app-server 負責儲存及更新其憑證；其模型清單是目錄，實際模型權限仍以推論結果為準。
目前以本機 Codex CLI `0.163.0-alpha.10.2` 的 schema 開發，dynamic tools 為 experimental；更新 CLI 後須重跑相容性驗收。
按「登出／清除金鑰」可登出此面板的 Codex 帳號；不影響桌面版／CLI 的全域登入。

## Claude 連線

1. 選擇「Claude · API Key」，按「取得 API Key」開啟 Claude 開發者平台。
2. 建立自己的 API Key，貼到面板的密碼欄位。
3. 按「連線／取得模型」，選擇有權限的模型，再輸入訊息。

此版本使用 Claude Messages API，不會取用 Claude Code／Claude 桌面版的登入 token，也不提供 claude.ai 訂閱登入按鈕。
官方 Agent SDK 文件對未經核准的第三方產品提供 claude.ai 登入設有限制，因此採用獨立 API Key 方式；API 用量依平台帳戶計費。

## Gemini 連線

1. 選擇「Gemini · API Key」，按「取得 API Key」開啟 Google AI Studio。
2. 建立 Gemini API Key，貼入密碼欄位，按「連線／取得模型」。
3. 選擇支援 generateContent 的模型，再輸入訊息。

本版本使用 Gemini API 的串流 Function Calling，保留模型回傳的 thought signatures 以完成後續工具回合。
Google 帳號或一般 Gemini 聊天訂閱不會自動變成此面板的 API Key；請依自己的 API 專案權限／配額使用。

## 日常使用

先以唯讀工具確認連線：

> 請確認 Revit 版本，再讀取目前專案名稱與樓層清單。

再測試：

> 在 Revit 顯示 Hello World 視窗。

模型修改範例：

> 在目前模型每道適用牆的起點起算 2/3 處放置一個門。使用目前已載入的單開門，先列出預覽。

AI 會查詢工具、建立預覽。面板顯示模型、門型、位置比例、可建立與略過數量，按「執行此計畫」才真正修改模型。
工具結果可展開檢查。`completed` 及 `createdCount` 才代表已建立；`rolledBack` 表示該批回復，不能當作成功。
停止會取消後續 AI／工具動作；已送入 Revit 的交易仍會等待實際結果。停止不等於撤銷，請依 Revit Undo 處理已提交的修改。
寫入若逾時，先查原 planId 的 operation_status。介面會阻擋結果不明時的盲目重試或新計畫。
面板不自動儲存或同步模型。修改完成請自行檢查並儲存。

切換引擎或模型會開始新對話；三家不共用其原生 conversation ID。對話執行中請先停止並等待完成，再切換。
目前顯示文字與可展開的工具結果，不包含影像附件、語音或完整 Markdown 渲染。

## 憑證及背景程序

- Claude／Gemini Key 以 Windows DPAPI CurrentUser 加密儲存於 `%LOCALAPPDATA%\TYBIM\McpChat\2025\ui.settings.json`；同一 Windows 使用者可使用。
- API Key 只透過本機子程序 stdin 傳入，不放在命令列、不寫入套件或聊天紀錄。
- 選擇 Claude／Gemini 後，按「登出／清除金鑰」可移除該供應商的本機加密金鑰。
- Codex app-server、Node 對話 runtime 與 MCP Server 均在背景執行；關閉 Revit 時會要求停止並清理。
- 官方 AI 服務會接收你送出的訊息及工具回傳的模型資料，請依專案資料規範使用。

## 問題排查

v0.2.1 的門預覽會在可回復的交易群組中暫時試放，逐牆提交以取得 Revit 接合檢查結果，最後回復整組試放，不保留任何新門。正式放置仍須按確認。`ready` 是試放檢查結果，正式執行若失敗仍會整批回復。
衝突牆會在 `targets[].diagnostics` 列出牆 ID、失敗原因與相關元素；試放門的暫時 ID 在回復後不再有效。程式不自動解除接合或偏移指定位置。

更新 v0.2.1：儲存模型、關閉所有 Revit，執行 `artifacts/packages/2025/Install-RevitAddin.ps1 -RevitVersion 2025`，再開啟模型。此更新包含 C# DLL，單獨執行 Update-ChatRuntime.ps1 不足以載入新版放門診斷。登入資料保留；更新後請在面板重新連線並開始新對話。

| 狀況 | 處理 |
|---|---|
| 找不到 AI 助手按鈕 | 確認完整套件已安裝，重啟 Revit，允許載入 TYBIM 外掛 |
| 找不到 runtime／Node | 檢查 chat.config.json；runtimeScript 相對於外掛目錄，nodeExecutable 必須存在 |
| 找不到 codex.exe | 指定真正的 Windows codex.exe 路徑；不要填 codex.cmd |
| Codex 未登入 | 按登入，完成瀏覽器授權後重新取得模型 |
| `code-mode host is disabled` | 舊版啟動設定誤關閉工具主機。更新修正版；可等待回合結束後執行下方 runtime 修復，無須關閉 Revit或重新登入 |
| API 401／403／配額錯誤 | 檢查對應供應商金鑰、模型權限及 API 額度 |
| ECONNREFUSED | 確認 Revit bridge 已啟動及 bridge.config.json 的 port；同版本 Revit 只能一個行程佔用該 port |
| Revit timeout | 關閉模態視窗後重試唯讀工具；寫入先查原操作狀態 |
| 工作已完成但沒有門 | 展開工具結果，確認是否 rolledBack／skipped；模型範圍與階段是否正確 |

## 官方參考

### Codex 工具主機修復（2026-10-21）

修正 `features.code_mode_host=true`；保留 shell 工具停用與模型修改預覽確認流程。
已安裝完整聊天面板時，可在回合結束後執行：

```powershell
& 'C:\Prj\Revit\TYBIM_MCP\scripts\Update-ChatRuntime.ps1' -RevitVersion 2025 -RestartChat
```

只替換已安裝的 `codex-provider.js`（保留備份），結束由 Revit 啟動的該面板背景程序樹。
Revit 與模型保持開啟，登入資料保留。接著按「連線／取得模型」及「新對話」，再查詢專案與樓層。
未執行中的已安裝面板可省略 `-RestartChat`。若有模型寫入進行中，先等待其工具結果，勿中止背景程序。

唯讀實機驗證（使用面板既有登入與 Codex 額度，需開啟模型）：

```powershell
node server/scripts/smoke-codex-revit.mjs "$env:APPDATA/Autodesk/Revit/Addins/2025/Tybim.Mcp"
```

此驗證必須收到三個真正工具的成功回傳才算通過，不以文字回覆或 thread/start 成功代替。

## 官方參考資料

- Codex app-server：https://learn.chatgpt.com/docs/app-server
- Codex 認證：https://learn.chatgpt.com/docs/auth
- Claude Agent SDK 認證限制：https://code.claude.com/docs/en/agent-sdk/overview
- Claude Messages 串流：https://platform.claude.com/docs/en/build-with-claude/streaming
- Gemini Function Calling：https://ai.google.dev/gemini-api/docs/function-calling

實作：Revit WPF 面板 → 本機 Node JSONL runtime → AI adapter → MCP Client → 既有 Revit bridge／ExternalEvent。
Codex 經 dynamic tool callback 使用同一 gateway；Claude／Gemini 經 API function calling 使用該 gateway，沒有把三家工具執行規則分散到 UI。
