# 驗證方式

## 建牆預設及門窗選擇 v0.7.1（2026-10-28）

柱、樑、板、牆保持獨立。建牆的原生及 MCP 預設皆不建柱、板、門窗；新增 placeDoors／placeWindows，各自明確啟用才嘗試放置。舊 placeOpenings=true 仍表示明確啟用門窗兩者，未要求兩者時 AI 指令要求保持 false。原生 OpeningSelection 過濾實際傳入放置器的候選，避免只要求門卻放窗，或只要求窗卻放門。

Node 57 項、私人 MCP 11 項及原生幾何／選擇規則 24 項通過，涵蓋原生預設只建牆、門／窗均未授權、門限定、窗限定與舊兩者開關相容。三版 Release 編譯通過，只有既有 Study 五項警告。實際 Revit 模型放置仍待驗收，未登入 AI 或修改使用者模型。

本次依 Donald 明確要求產出三版，後續腳本預設只製作 2025；全部指定年份 MSI 驗證成功後才清除同年份較舊 MSI，並重建保留檔案的校驗清單。清理限定 Release 直接檔案，不遞迴刪除原始碼或測試紀錄。三版最終 MSI 已通過全 CAB 雜湊、年度 manifest／API／框架／版本、兩種載入方式的 DWG 文字讀寫、相依檔／授權及解除安裝設定驗證。2024 共 3765 檔，2025／2026 各 3758 檔。建置完成後實際清除三個 0.7.0 MSI，Release 僅保留三個 0.7.1 MSI 並重建校驗清單。本次未重跑 Windows 安裝生命週期；0.7.0 的隔離安裝／升級／移除紀錄保留於下節。

## 個人自動建柱、樑、板、牆 v0.7.0（2026-10-28）

將 Donald 在 Sinotech_MCP 的個人 AutoBuild 演算法適配到既有私人命令入口，未修改兩份外來快照／適配程式，也未新增第二套 Socket 或 ExternalEvent。模型寫入仍由既有 UpstreamCommand 的文件檢查、operationId 去重與交易群組管理；完整參數經 gateway 確認，四個私人寫入皆採 180 秒原生期限及 185 秒 MCP 等待。

三版 Release（2024 net48、2025／2026 net8.0-windows）編譯通過，保留 Study 原有五項未使用變數／事件警告。Node 57 項通過；私人 MCP 11 項涵蓋工具名稱／分類、Int64 與重複圖層驗證、組合操作預設關閉、四種寫入拒絕確認時不派送、長期限、失敗回傳與中文搜尋。私人幾何 18 項通過，除舊有牆測試外，新增矩形／圓柱、重複柱輪廓、樑圖層排除標註、雙線中心線、RC 標註 cm 換算、鋼樑 mm 換算及過大間距拒絕。

新增 ACadSharp 3.8.0 NuGet 相依，封裝附來源與 MIT 聲明。隔離載入 fixture 透過正式 sibling resolver 寫入及讀回含 TEXT 尺寸標註的小型 DWG，避免只驗證 DLL 檔名卻沒驗證相依可執行。這不替代在 Revit 模型裡對齊連結 DWG 座標或讀取各種實際 CAD 版本／區塊的測試。

三版最終 MSI 均通過內嵌 CAB 全檔 SHA-256、年度 manifest、DLL 框架／API／版本、兩種載入方式的 DWG 文字讀寫回圈、第三方與私人授權檔、解除安裝清理及升級條件驗證。2024 共 3765 檔，2025／2026 各 3758 檔；三版實際 payload 探索皆回傳 263 工具，其中 5 項私人、27 upstream、220 Study。

2024 隔離安裝／移除通過（artifacts/qa/msi-uninstall-7c95fe34a0204d32b0d860963243e2a7/result.json）。2025 隔離安裝、模擬下一版 major upgrade、最後移除通過（artifacts/qa/msi-uninstall-933a6cde816e469799b57abb199eb612/result.json），含 Windows Apps 註冊、升級保留檔案／產生設定、移除清理、其他年份與外掛保留。測試使用獨立產品／元件 GUID 及 workspace 目錄，未替換正式安裝。2026 本次完成 MSI 封裝／載入與設定檢查，未重做其 Windows 安裝生命週期；同版腳本的前版 0.6.0 2026 安裝／移除紀錄保留如下。

實際 Revit 中的柱／樑／牆建模、暫時房間回復、樓板內孔、裁剪範圍、族群尺寸與 Undo 尚待使用者於測試模型驗收。本次沒有讀取 API Key、登入真實 AI 或修改使用者模型。

## 私人建牆、面板服務與 MSI v0.6.0（2026-10-27）

三版 Revit Addin Release 編譯成功。Node 測試 57 項、私人 MCP schema／確認／分類測試 5 項、私人 CAD 幾何測試 9 項通過。WPF ChatPanelSmoke 在 .NET 8 與 .NET Framework 4.8 各 52 項通過，涵蓋唯讀選單、有效執行檔候選、模型回退與既有裁切／登入回歸案例。

PaneServiceRegression 連結實際 Dispatcher 與 BridgeListener，以 Revit stub 驗證兩框架各 14 項：預載一次命令、服務啟停、同埠重開、停止時清除未執行佇列，以及保留連線至明確拒絕／結果送出。這不是 Revit 宿主視窗事件的端到端測試。Node 另驗證 MCP_SERVICE_DISABLED 的已知未執行寫入不會永久進入結果不明鎖定；一般斷線仍保留不明寫入保護。

最終三版 MSI 通過 CAB 全檔雜湊、五個原生 DLL、框架／API 年份、相對 manifest 路徑、隔離 LoadFile／LoadFrom、解除安裝註冊與 RemoveFile 範圍／升級條件檢查。2024 共 3762 檔，2025／2026 各 3755 檔。2026 最終 payload 探索回傳 260 工具（個人 2、upstream 含狀態 27、Study 220）；沒有向 AI 發出推論或修改模型。

安裝生命週期測試使用全新 Product／Upgrade／Package／Component GUID 與 workspace 隔離路徑，未替換使用者正式外掛。2024 全移除測試通過（artifacts/qa/msi-uninstall-3abb22499bf348e9a13d7dffe6ceec36/result.json）；2025 真實 major upgrade 後全移除通過（artifacts/qa/msi-uninstall-2b0546ea9a4640f88fda2fceee266bc8/result.json），確認升級保留新檔案及 bridge 設定，完整移除清除產生檔並保留其他年份／外掛。上述兩項以首次 0.6.0 包驗證，最終包後續只更新服務／gateway payload，MSI 清理規則相同。

2026 最終 MSI 隔離安裝／解除安裝通過，確認 Windows Apps 註冊、清除該年份產生檔與資料夾、保留其他外掛及年份；紀錄：artifacts/qa/msi-uninstall-4e412f5fe8284f59aca6344742674be7/result.json。

私人原始碼與測試由 Git ignore 排除，內部 MSI 含編譯後功能及著作權聲明。本次依 Donald 選擇未加入執行時授權。仍待實際 Revit 2024／2025／2026 驗收 Ribbon 與 X 的服務切換，以及真實 DWG 的建牆、Undo 與模型結果；獨立幾何測試不能替代模型驗收。

## 確認按鈕裁切修正 v0.5.5（2026-10-22）

原 Grid 使用三個 Auto 列顯示設定、確認區與輸入區；高度總和超過 dockable pane 時，對話 Star 列縮成零仍無法容納，確認與送出按鈕被裁切。新版使用上方 Star 列的 ScrollViewer 顯示設定／紀錄／完整參數，將 approvalActions 放進下方操作區；下方 ScrollViewer 依實際高度限制在可用高度一半內。取消 300 DIP 最小寬度並讓設定標籤換行。確認完成及取消會一併隱藏參數與操作區。

`ChatPanelSmoke` 新增 300 個元素 ID 確認案例，在 300x820、300x600、260x400、300x240 及 150%／200% 等比例縮放檢查執行／取消按鈕完整落在主視窗及 footer viewport；捲動 footer 後送出／停止亦完整可見。確認所有參數保留、上方內容可捲動、關閉確認會隱藏操作區。離屏渲染沒有 PresentationSource，因此可見性檢查採元素到祖先的 Visibility 鏈，另用 TransformBounds 驗證實際幾何位置。

.NET Framework 4.8 與 .NET 8 各 46 項檢查全部通過。`artifacts/chat-panel-confirmation-300x820.png` 已人工檢視，四個按鈕皆完整顯示。三版 Release 編譯成功，仍保留外來程式既有 5 項警告。此次不涉及 Node 行為變更，僅更新版本並重新編譯 runtime；未重跑與版面無關的 Node 測試。實際 Revit 宿主仍須使用者安裝後驗證。

0.5.5 三版 MSI 已完成並通過內嵌 CAB 全檔 SHA-256、註冊檔相對路徑、DLL 版本／目標框架／Revit API 年份、LoadFile/LoadFrom 載入與解除安裝設定檢查；2024 共 3761 檔，2025/2026 各 3754 檔。Release 已更新安裝說明及 checksums-0.5.5.sha256。

## 登入按鈕重複累加修正 v0.5.4（2026-10-22）

原 `ChatPanel.HandleEvent(loginUrl)` 每次呼叫 `AddLink`，直接將新按鈕加入 `messages.Children`。改為連線區唯一 `reopenLogin` 控制項，更新其網址與可見狀態。相同網址事件不重開瀏覽器，待登入時重按登入只重開現有網址，啟動過程使用 `loginStarting` 防止重入。登入與重新開啟 Click 均設定 Handled，避免路由到宿主。

`CodexProvider` 轉送結構化 `loginCompleted` 事件，面板在成功／失敗、登出、切換供應商、重新 configure 或斷線時清除登入連結。原登入與 API Key 儲存方式不變。

WPF `ChatPanelSmoke` 改為依 RevitVersion 使用實際目標框架，.NET Framework 4.8 與 .NET 8 各 27 項檢查通過：20 次重複網址事件、40 次登入／重開 Click，不新增對話按鈕，重開使用最新網址、Click 不向外冒泡、登入完成／切換供應商／斷線清除連結。測試以瀏覽器替身運作，不實際登入。2024 測試須先編譯再直接執行 net48/ChatPanelSmoke.exe；此環境透過 dotnet run 啟動 net48 時，Node 測試背景程序會提前結束，直接執行通過。

三版 Release 編譯成功（各保留原有外來程式 5 項警告），Node 56 項測試通過。登入按鈕問題仍需在實際 Revit 宿主確認；獨立 WPF 測試不等同登入真實帳號或驗收完整 Revit。

0.5.4 三版 MSI 均完成重建：2024 共 3761 檔、2025/2026 各 3754 檔；內嵌 CAB 全檔 SHA-256、註冊檔相對路徑、Contracts 等五個 DLL 版本／框架／Revit API 年份、LoadFile/LoadFrom 正反測試與 Windows 解除安裝設定檢查全部通過。Setup 產生器亦改為暫存檔寫完後原子替換 .vdproj，避開已開啟專案的記憶體對應檔覆寫錯誤。

## 啟動相依 DLL 修正 v0.5.3（2026-10-22）

Revit 2024 回報 `Tybim.Mcp.Contracts, Version=0.5.2.0` 找不到。原封裝含正確 Contracts DLL，但入口直接依賴 Contracts/Study，且沒有同目錄相依解析。新增不含上述外部相依型別欄位的 `Application` bootstrap，以禁止內嵌的方法延後建立 `RevitApplication`，先啟用相依解析再啟動。

新增 `tests/AddinLoadRegression` 無專案相依的獨立 host 與 `tests/LoadFixture`，從 host app base 以外的目錄 LoadFile 載入。未安裝 resolver 時必須重現找不到 Contracts；安裝正式 resolver 後必須成功建立使用 Newtonsoft.Json 的 BridgeRequest。另驗證 LoadFrom、五個 TYBIM DLL 身分版本與不接管其他 host 的要求。測試不會初始化 Revit API；不能替代在 Revit 中的完整 OnStartup/Ribbon/模型操作測試。

MSI 驗證從實際內嵌 CAB 解出內容，逐檔比對 payload-index 的 SHA-256，再用解出的 DLL 執行上述測試。三個年份各自編譯與封裝，原 per-user 安裝路徑、升級識別及 Windows 解除安裝註冊檢查保留。

本次結果：三版 Release 編譯成功（既有外來程式 5 項未使用警告）；Node 56 項測試通過。MSI 解出內容全檔雜湊核對成功（2024：3761 檔，2025/2026：各 3754 檔，含註冊檔）；三版均通過負向重現、正式解析器 LoadFile/LoadFrom、.NET 目標與 Revit API 年份檢查。2024 測試副本實際安裝至隔離根目錄，確認相依 DLL 存在及載入、Windows 註冊、解除安裝成功，紀錄在 `artifacts/qa/msi-uninstall-5ddacbf0385c4a54a8b7df18c288c36b/result.json`。未在各版 Revit 執行實際 UI 啟動驗收。

## Claude／API 錯誤處理與三版 MSI v0.5.2（2026-10-21）

- Revit 2024、2025、2026 分版 Release 編譯全部成功，各保留既有 5 項外來程式未使用變數／事件警告。
- Node 56 項測試、WPF 面板 19 項檢查及 StudyCatalog 初始化 4 項檢查通過。包括 Claude 模型生命週期／分頁、舊模型更正、共用按需工具載入、Claude/Gemini 經 MCP 與確認 gateway 的五層批次建立模擬。
- 驗證暫時性 HTTP 過載最多額外兩次重試、沿用請求與期限、遵守短 Retry-After、金鑰／權限／額度錯誤不盲目重試，以及取消等待、串流中斷不重播工具。
- 三版 MSI：2024 為 3761 個檔案；2025／2026 各 3754。獨立 ProductCode／UpgradeCode、每使用者安裝、內嵌 CAB、正確年份 .addin、可移除註冊屬性均納入核對；各 payload 的內建 Node 成功發現 258 個 MCP 工具。
- 隔離的 2026 MSI 副本實際安裝測試發現 VSI 漏出 TYBIMREGISTERDIR 的 Directory 記錄（2727）；後處理補齊，驗證程式新增根目錄存在性檢查。另以全新產品／元件識別及 artifacts/qa 根目錄重新測試，Windows 產品顯示名稱、版本、解除安裝命令，以及移除後的檔案／註冊清理均通過。
- 成功隔離測試紀錄：`artifacts/qa/msi-uninstall-558fd0531d0349e0b209e69d519f00aa/result.json`。沒有取代或解除安裝現有正式外掛，沒有讀取或使用真實 Claude／Gemini 金鑰，也沒有呼叫實際 Revit API。
- 正式顯示名稱為 `TYBIM_MCP for Revit 2024/2025/2026`。舊 0.5.0 本機存在另一筆 `TYBIM MCP Revit 2025` 註冊（不含底線），跨安裝範圍升級前建議先移除舊版。

## Gemini 連線與等待提示 v0.5.1（Revit 2025，2026-10-21）

- Revit 2025 Release 編譯成功：0 錯誤、5 項既有外來程式未使用變數／事件警告。
- Node 48 項測試、WPF 面板 18 項檢查及 StudyCatalog 初始化 4 項檢查通過。
- 使用真正的 MCP Client／Server 與參數 schema，模擬 Gemini 串流走完「讀取專案 → 讀取樓層 → 確認五層批次參數 → native bridge 派送 → 回覆」。測試沒有呼叫 Google 推論或實際 Revit API。
- 全部 258 項工具均可透過本機搜尋載入；樓層案例的工具 inputSchema JSON 從 228221 bytes 降至 11188 bytes，約減少 95.1%，非整個 HTTP 請求或實際 token 數。
- 已安裝至 `%APPDATA%\Autodesk\Revit\Addins\TYBIM_MCP\2025`。DLL 為 0.5.1.0，核對 52 個 runtime/build 檔案雜湊與來源一致；2025 的 .addin 相對路徑可解析至正確 DLL。
- 已安裝套件用本身 Node 啟動 MCP 工具清單檢查，發現 258 項（27 upstream、220 study、11 原有工具）。未開啟 Revit、未進行雲端生成或修改模型；實際 Gemini 建立樓層仍需重開 Revit 驗收。
- 安裝保留既有 bridge.config.json、chat.config.json；未清除或搬移登入及 API Key 資料。本輪沒有重建或替換先前的 0.5.0 MSI。

## Setup Project／MSI v0.5.0（2026-10-21）

- Revit 2024／2025 外掛分版編譯成功，保留 5 項外來未使用變數／事件警告；Node 41 項測試、C# 命令表初始化 4 項及 WPF 13 項檢查通過。WPF 新增驗證相對 Node 路徑解析。
- Visual Studio Installer Projects 已實際建置兩版 MSI：2024 為 3758 個檔案，2025 為 3751 個檔案，CAB 內嵌於 MSI。
- Windows Installer API 唯讀核對目錄、File／Component／CustomAction／Media 表、版本與升級識別。兩版 payload 的內建 Node 各自啟動真正 MCP Client／Server，皆發現 258 個工具。未呼叫 AI 推論或修改模型。
- 未執行 MSI 安裝、解除安裝或兩版 Revit 實機操作；尚需使用者重開 Revit 驗收。相機切換、Node／Codex 執行檔偵測及安裝後首次產生權杖，仍屬實機驗收範圍。
- 2026／2027 的參考設定尚未發布；2027 API 編譯有未處理差異，不宣稱相容。

## 相機標題與延後還原面板 v0.4.3（2026-10-21）

- 相機按鈕文字由明確換行改為單列「相機管理」。
- v0.4.2 實機回報面板仍顯示；改監聽 ApplicationInitialized、ViewActivated、相機 WPF IsVisibleChanged，將處理延後到可用的 Idling，支援 UIApplication 與 Application sender。
- 使用者成功點選相機管理後解除防護，避免手動開啟又被隱藏。AI助手僅首次顯示，之後尊重手動關閉。保留 pane-layout.log 診斷實機處理與例外。
- Revit 2024／2025 編譯通過；實機啟動配置仍需重開驗收。

## 相機按鈕與啟動面板配置 v0.4.2（2026-10-21）

- 新增相機管理圖示與原圖的 embedded resources，LargeImage 沿用 AI助手的 96-DPI 載入方式；提供功能介紹、使用步驟及 tooltip 原圖。
- 相機面板 VisibleByDefault=false，AI助手=true；啟動後一次套用隱藏相機／顯示 AI 的配置以處理 Revit 既有記憶。成功後解除事件，失敗最多重試三次，不持續改動使用者的面板操作。
- 實際 Revit 中圖示大小與啟動配置需重開驗收；不以編譯結果代替實機結果。

## 啟動修正 v0.4.1（2026-10-21）

- 已以執行實際 StudyCatalog.cs 的 C# 測試重現 0.4.0 例外：query_elements 與 query_elements_with_filter 對應同一 native command，Dictionary initializer 重複 Add query_elements，導致 TypeInitializationException；這發生在建立 Dispatcher 命令註冊時，尚未建立 ribbon。
- 產生器改為依原生命令去重，保留 220 個 MCP 工具入口、219 個唯一原生命令。若別名權限不同，採需要寫入確認的分類，不放寬權限。
- 修正後 C# 初始化回歸 4 項與既有 Node 41 項通過；Build 與 Package 腳本都會檢查 C# 初始化，避免僅編譯／Node discovery 通過卻發布無法啟動的命令表。
- 啟動失敗訊息會顯示內層根因，完整 exception chain 保存在 %LOCALAPPDATA%/TYBIM/McpLogs/startup-*.log。
- 真正 Revit 啟動與按鈕呈現仍需重新開啟驗收，不將獨立測試當作 Revit 內的驗收。

## Study 與來源分類 v0.4.0（2026-10-21）

- Revit 2024／2025 完整 Addin Release 編譯成功，各保留 5 個外來程式未使用變數／事件警告，0 錯誤。
- Node 41 項測試通過：既有 bridge／放門確認／SQLite，加上 Study 實作對應、文件及操作 ID、參數驗證、原生 failed 狀態、影像回傳、Study／私人功能確認、快照雜湊、Claude／Gemini 影像格式。
- WPF 12 項檢查通過：原加密憑證與對話確認，加上 Study 結果直接顯示影像、避免 base64 填滿文字；已輸出並檢視畫面。
- C# 原生 dispatch 適配 5 項、放門位置規則 11 項回歸通過。
- 從兩版完整包啟動真正 MCP Client／Server，各發現 258 個工具，其中 Study 220 個。這是入口與封裝驗證，沒有呼叫 AI 推論或改動模型。
- 已安裝至 Revit 2025 Addins，確認 Addin DLL 版本 0.4.0.0；從已安裝 runtime 再啟動真正 MCP Client／Server，確認同樣發現 258 個工具。原 bridge 設定與獨立 AI 登入狀態目錄保留。2024 僅完成套件，未安裝。
- 兩份原參考目錄與已記錄快照比對差異均為 0；僅執行快照輸出腳本的 WhatIf，未輸出／上傳私人原碼或建立 Git 發布。
- 未在真正 Revit 裡逐項執行 220 個新功能，亦未驗證選用的 Python／ezdxf／ODA 轉換環境。開啟模型後仍需依實際工作流程驗收。

## 上游功能整合 v0.3.0（2026-10-21）

- 參考資料為本機下載的 mcp-servers-for-revit commit `86cf7057785f2423fc0d0892c9a5d9c93deb1aa3`；MIT 與來源雜湊已保留。匯入 107 個 C# 檔案，接通 23 個實際 Revit 指令＋3 個 SQLite 工具；3 個原始空 TS 模組未登錄。總 MCP 工具為 38 個。
- Revit 2024／2025 Release 均成功，0 warnings、0 errors；新增編譯專案 Tybim.Revit.Upstream 已加入同一方案。沒有依賴外部參考專案路徑或再啟動它的 plugin server。
- Node 30 項測試全部通過：所有 23 個 C# 指令與 TS caller 名稱一致、38 工具發現、schema 驗證、bridge 參數轉換、原生錯誤不被格式文字掩蓋、確認／取消、逾時阻擋與狀態查詢；含實際 SQLite 專案／房間存取、operationId 去重。
- WPF smoke 10 項通過；確認新增操作卡片顯示專案、工具及完整 code 參數。已檢視 artifacts/chat-panel-upstream-preview.png。
- UpstreamDispatchRegression 5 項通過：handler 恰好執行一次、在原執行緒執行、不等待另一個 ExternalEvent、釋放 wait handle、原生錯誤向外傳遞。此測試模擬 API 介面，不驗證 Revit 幾何。
- 本次尚未在 Revit 實機逐項驗證新增建模工具。所有新功能的幾何行為、Undo、錯誤回復及族型適用範圍仍須在測試模型驗收；不可稱為全部 Revit 功能實測通過。
- 已在 Revit 關閉時安裝至 Revit 2025 Addins；核對 DLL 0.3.0.0、Upstream DLL、Roslyn DLL 的雜湊及 MIT 授權。從已安裝 runtime 實際啟動 MCP Client／Server，確認發現 38 個工具；未呼叫 AI 推論或修改模型。

功能與開發位置詳見 [upstream-integration.md](upstream-integration.md)。

## Ribbon 圖示 DPI 修正 v0.2.3（2026-10-21）

- 使用者 32×32 圖示含約 300 DPI；舊載入方式保留 DPI，WPF Width/Height 實測僅 10.24002，因此 Ribbon 圖示顯著過小。v0.2.2 只驗證 PixelWidth/Height，未驗證 WPF 顯示尺寸，驗收不足。
- 對照 TYBIM_2025 的 HBitmap 轉換方式，將記憶體內 BitmapSource 統一為 96 DPI；不改寫原始 PNG、不縮放主圖，移除額外的 16×16 Image 設定。提示原圖仍按 250 像素寬載入。
- 直接反射呼叫新版 DLL 的 RibbonImages.LoadImage，驗證主圖 PixelWidth/Height 與 WPF Width/Height 均為 32×32、DPI=96；提示圖均為 250×250、DPI=96；兩者均 Freeze。逐位元組比較 Bgra32 像素，主圖像素完全一致。
- 按鈕與圖片程式均位於 src/Tybim.Revit.Addin/RibbonUi.cs，由 Application.OnStartup 呼叫；圖片嵌入設定在同專案 csproj。
- 尚需重新開啟 Revit 目視確認 Ribbon 圖示與鄰近按鈕顯示大小一致。

## Ribbon 整合 v0.2.2（2026-10-21）

- 依 TYBIM_2025/TYBIM_Button.cs 的顯示方式，將 AI助手放到「拓源數位」頁籤的「MCP」面板，加入功能介紹、使用步驟與原圖 ToolTipImage。
- 既有頁籤／面板會共用；內部按鈕 ID 為 `TYBIM.MCP.ShowAiAssistant.A405238F`，避免與其他 TYBIM 按鈕 ID 衝突，重複註冊亦不再新增。
- 圖示取自 Pic/AI助手.png（32／16 像素），提示圖取自 Pic/原圖/AI助手(原圖).png（250 像素寬），嵌入 DLL，沒有修改 TYBIM 原專案。
- 2024／2025 Release 均編譯成功，0 warnings、0 errors；以 WPF 解碼實際 DLL 的兩張嵌入圖，確認 32×32／250×250、可載入並 Freeze。兩版完整套件均已重新產生。
- 實際 Revit Ribbon 排列、展開提示、先後安裝兩種順序仍需重開 Revit 驗收；編譯成功不代表上述 UI 實機驗收已完成。

## 放門預覽診斷 v0.2.1（2026-10-21）

- 原因：使用者兩次實機操作（2/3 處及中心）均在交易提交時回報「插入物件與接合的牆衝突」，整批回復。
- 預覽改用 TransactionGroup 包住逐牆 Transaction：每面試放都 Commit，觸發 Revit 接合檢查；成功的試放保留到本輪結束以檢查門之間的交互影響；外層 finally 將全部試放 RollBack。
- 衝突牆標記 `REVIT_TRIAL_PLACEMENT_FAILED`；附錯誤等級、FailureDefinitionId、涉及元素與宿主牆資訊。暫建門的 ID 僅是試放當時的診斷資料，回復後不可當成現存門使用。
- 預覽 35 秒後未檢查者標記 `TRIAL_NOT_RUN_TIME_LIMIT_REDUCE_BATCH`，不得標為 ready；兩種預覽工具的 bridge 等待時間延長至 60 秒。
- 正式執行保留原整批交易與失敗回復。試放通過不保證批次交易必定成功；正式失敗同樣回傳詳細錯誤，`lastAttemptedWallId` 僅表示最後嘗試對象，不代表衝突源。
- 2024／2025 Release 均編譯成功（0 warnings、0 errors）；Node 20 項測試通過（含兩種預覽 60 秒 deadline）、門位置規則 11 項檢查通過。兩版完整套件已產生，DLL 0.2.1.0 與 runtime hashes 核對成功。
- 新版 DLL 尚未載入目前開啟的 Revit，故尚未實測該模型的逐牆診斷與試放回復；需關閉 Revit 安裝後驗收。

實機驗收：更新後開啟 FL1，先記錄門數與牆狀態，再執行中心位置預覽；檢查衝突牆 ID、略過原因、可放數，確認試放後沒有新增門且牆狀態不變。再確認執行，核對建立數與 Undo；另測預覽途中失敗／時間限制後仍無保留門。不得將編譯或 mock 測試當成此驗收已完成。

## 內嵌面板 v0.2.0（2026-10-20）

- Revit 2024／2025 Release 均編譯成功，0 warnings／0 errors。
- Node 自動測試 20 項通過：原 MCP 通訊、Claude／Gemini 串流工具回合、Gemini thought signature、取消、預覽確認、結果不明時禁止重試。
- WPF smoke 8 項通過：三家切換、供應商金鑰隔離、DPAPI 加解密、繁體中文／emoji C#→Node 傳輸，產生 `artifacts/chat-panel-preview.png` 並檢視版面。
- 已使用真正的本機 Codex CLI app-server 完成 initialize、account/read、model/list、含 dynamic tools 的 thread/start 及關閉清理；以原始 build 與打包後 runtime 分別驗證。
- 使用獨立暫存 Codex profile，回傳「尚未登入」符合預期，未要求登入、未讀取桌面版憑證、未呼叫 AI 推論。
- 完整 Revit 2024／2025 套件已產生於 `artifacts/packages/<year>`，驗證 DLL version 0.2.0.0、Node runtime hashes、MCP SDK 與橋接設定、PowerShell 腳本語法。
- 尚未將本次面板安裝至 AppData，也尚未完成使用者帳號登入後的雲端推論及 Revit DockablePane 實機驗收。

### Codex 工具主機修復實測（2026-10-21）

- 原設定 `features.code_mode_host=false` 導致文字可回覆、工具呼叫卻失敗；已改為 `true`。
- 新增 `server/scripts/smoke-codex-revit.mjs`，透過正式 CodexProvider、既有面板登入、真實 MCP Client 與 Revit bridge，限制僅提供 health／project_info／get_levels 三個唯讀工具。
- 使用 `gpt-6.1-sol` 實際推論，三個工具皆成功回傳：Revit 2025、模型「裝修牆測試_2025」、視圖 FL1；FL1～FL5 高程依序為 0、3500、7500、13500、17500 mm。
- Node 20 項自動測試再次全部通過。未修改模型；Claude／Gemini 雲端推論仍未實測。
- 新增 runtime 修復腳本，可保留 Revit、DLL、橋接設定與登入資料，僅更新 JS 並重新啟動聊天背景程序。

操作及安裝詳見 [embedded-chat.md](embedded-chat.md)。面板驗證命令：

```powershell
dotnet run --project tests/ChatPanelSmoke/ChatPanelSmoke.csproj -- C:\Prj\Revit\TYBIM_MCP
node server/scripts/smoke-chat.mjs '完整的 codex.exe 路徑'
```

實機驗收：安裝完整套件，重啟 Revit → 開啟 AI 助手 → Codex 登入／Claude、Gemini API Key 連線 → 選模型 → 查詢專案與樓層 → 預覽放門 → 取消一次確認模型未變更 → 再預覽並執行 → 驗證 Undo。停止測試須區分尚未送出與已在 Revit 執行中的交易。

## 本次建置結果（2026-10-13）

- Revit 2024 Release：成功，0 warnings／0 errors。
- Revit 2025 Release：成功，0 warnings／0 errors。
- TypeScript 編譯及 Node 測試：7 tests 通過。
- PowerShell 腳本語法、兩版 manifest 指向的 DLL 與設定有效性：通過。
- 2024／2025 部署套件已產生；2025 套件產生器重跑成功，保留既有 token。
- 尚未安裝到 Revit Addins，也未修改 MCP 用戶端全域設定；以下實機驗收尚未執行。

## 自動驗證

```powershell
dotnet build src/Tybim.Revit.Addin/Tybim.Revit.Addin.csproj -c Release -p:RevitVersion=2024
dotnet build src/Tybim.Revit.Addin/Tybim.Revit.Addin.csproj -c Release -p:RevitVersion=2025
cd server
npm ci
npm test
```

Node 測試涵蓋 TCP 分段／UTF-8、要求關聯 ID、不正確回應、錯誤回傳、斷線、逾時不重試與並行要求。
MCP 測試以官方 SDK Client 啟動真正的 stdio server，驗證 initialize、tools/list、tools/call、輸入 schema，再連到 mock Revit TCP endpoint。
Mock 回應不代表 Revit 實機 API 已驗證；C# build 驗證的是 API 相容與編譯。

## Revit 實機驗收（兩版各執行一次）

1. 準備套件、安裝 manifest、重啟對應 Revit；確認沒有啟動錯誤。
2. 未開模型時呼叫 health：應成功並回傳年份與 processId；project_info 應回 `NO_ACTIVE_DOCUMENT`。
3. 開啟非生產模型，project_info 回傳 title、view 與 documentId。
4. 選取少量元素，selection 的 ID／name 與 Revit 一致；空選取回空陣列，limit 過小時標記 truncated。
5. levels 的高程與 Revit 毫米顯示一致，offset／limit 分頁正確。
6. 開第二份模型並切換，傳第一份 documentId，應回 `DOCUMENT_CHANGED`。
7. 開啟模態視窗後送要求，應逾時；關閉視窗後重新呼叫應正常，過期要求不執行。
8. 同時送多個唯讀要求，回應各自對應正確 ID，沒有 action 覆蓋。
9. 使用不正確 token 應回 `UNAUTHORIZED`；關閉 Revit 後要求快速失敗。
10. 重新啟動 Revit／MCP 用戶端後恢復連線。相同年份多開時應提示 port 使用中，並且不能把要求送到不明行程。

第一階段不含寫入工具，沒有修改模型或 Transaction 驗收；新增寫入功能後另加 rollback、Undo、preview、冪等及工作共享案例。
