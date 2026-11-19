# TYBIM_MCP 更新狀態

## v0.12.0 三版 MSI 已交付

依使用者明確要求，同步製作 Revit 2024／2025／2026 v0.12.0。三版 Release 編譯、打包前回歸、266 項 MCP 工具清單（私人 8 項）、設定精靈四檔一致性、MSI CAB 逐檔雜湊、相對安裝路徑、無共用權杖、解除安裝登錄及五個 TYBIM DLL 的隔離依賴載入均通過。2024 共 3772 個安裝檔案，2025／2026 各 3765 個；2024 使用 net48，其餘 net8.0-windows。本機 Revit 2026 未安裝，使用對應 API 參考編譯，未宣稱實機操作通過。未代為安裝 MSI，也未改寫目前用戶端設定或模型。

安裝檔在 `TYBIM_MCP_Setup/Release`，版本皆為 0.12.0；SHA256 記錄於 checksums-0.12.0.sha256。全部驗證成功後已刪除 2024／2026 的 0.8.2、2025 的 0.11.0 舊 MSI。打包後清理 29 個產物目錄、12436 個檔案，再釋放 868.99 MiB；三版 MSI 與完整部署套件保留。

已移除 5 個過時 PowerShell 腳本並同步操作文件，見 [腳本整理](script-audit.md)；正式 MSI／設定／驗證腳本保留。設定精靈僅登錄勾選的 AI 用戶端；重新啟動後，Codex 在「設定 → 外掛程式 → MCP」、Claude Code 在 `/mcp` 查看本年度 TYBIM 項目。Claude Desktop 是另一份設定，須另行勾選才會出現在開發者頁。清單列出或 stdio Connected 不等於 Revit 已連線，仍需開啟本年度外部服務並成功執行唯讀查詢。

## v0.12.0：前置設定整合與容量清理（先前階段，當時未重製 MSI）

新增 ExternalAiSetup.cs：首次啟用外部服務時開啟獨立設定精靈，成功記錄後不反覆提示；失效設定重新提供。新增「AI連線設定」Ribbon 按鈕，與 listener／AI助手開關分開。精靈透過 Package-Chat／Prepare-Payload 納入未來 MSI；不自動安裝或登入 AI，也不關閉用戶端審批。2025 Release 編譯、27 項 PowerShell 設定檔測試、8 項首次使用狀態檢查、GUI 配置及既有打包前回歸通過。實際新 Revit Ribbon／精靈互動仍待部署驗收，原 v0.11.0 MSI 未變更。

實際 Codex 設定入口已以 UI 確認：「設定 → 外掛程式 → MCP → tybim_revit_2025 右側齒輪」，「連線」頁管理遠端裝置。清理只移除可重建產物，保留必要測試原始碼；細節與指令見 [容量清理](workspace-cleanup.md)。MSI／DLL 驗證成功後改為自動清理暫存，可用 KeepDiagnostics 保留。

實際清理完成：40 個輸出／快取資料夾、142,902 個檔案，共 11,016.87 MiB（約 10.76 GiB）。包含歷次 QA、安裝 staging／產生專案、各 C# 專案 bin／obj、Copilot 索引；保留 VS 個人設定、所有程式與測試原始碼、離線參考／套件依賴、目前 2025 部署套件及既有三年 MSI。結果記錄 `artifacts/workspace-cleanup.json`。新套件有 3765 個明確安裝檔案連結（比舊版多四個精靈檔案），只完成 payload 驗證後再清除 staging，未建置 MSI。v0.12.0 的隔離 DLL 載入檢查通過，並確認本次檢查的暫存已自動清除。

## 2026-11-18：獨立外部 AI 設定工具 v1.0.0

新增可雙擊使用的 PowerShell 設定視窗＋CMD 啟動器，勾選 Codex／Claude Code／Claude Desktop，依已安裝 Revit MCP 設定本年度外部服務。預設 2025，支援原有自訂埠及用戶端設定目錄；合併保留其他設定、備份、檢查後變更偵測、失敗復原與模擬模式。來源 `scripts/ExternalAiSetup`，封裝 早期獨立設定 ZIP（已由 MSI 內附精靈取代並清除）。25 項 PowerShell 5.1 回歸測試、視窗配置檢查與本機唯讀預檢通過。未套用目前用戶端設定，未修改 Revit 模型，未製作任何新的 Revit MSI，主程式仍為 v0.11.0。

**v0.11.0 / Revit 2025：建牆提交後預設自動逐組接合。** 先嘗試牆端，失敗再試幾何接合；失敗僅回復該組，保留牆體及先前成功接合。可用 autoJoin=false 只建不接合。新增私人 revit_private_auto_join，支援柱、樑、牆、板的接合／取消接合與明確的樓板切割順序；失敗提供元素 ID、mm 座標及可取得的 PNG／JSON 診斷。見 [接合與定位說明](wall-two-stage.md)。

交付：`TYBIM_MCP_Setup/Release/TYBIM_MCP_Revit2025_0.11.0.msi`。2025 Release 編譯、70 項 Node 測試、12 項私人 MCP 契約測試、24 項接合測試、9 項提交後隔離測試、既有建牆／選型與幾何測試皆通過。封裝 MCP 清單共 266 項（私人 8 項）；MSI 3761 個檔案的 CAB 雜湊、隔離 DLL 載入、年度相對路徑與「TYBIM_MCP for Revit 2025」解除安裝登錄檢查通過。2025 舊版 0.10.0 MSI 已清除，2024／2026 未重製。未部署至正在執行的 Revit、未修改實際模型；實際 DWG 接角與圖形匯出仍待安裝後驗收。私人來源仍受 Git ignore 排除，未強制納入公開追蹤。

## v0.10.0 / Revit 2025

依使用者要求改為兩階段：CAD 自動選型建牆後保持兩端不接合，另透過私人 join_walls 做指定牆的幾何／牆端接合。此工具由原生端自行管理每組交易，通用 wrapper 對它不再使用全組回滾；其他私人建模仍保留原子交易保護。失敗保留牆與先前成功接合，operationId 狀態會標示部分變更可能保留。診斷包含有效牆 ID、座標、原生 failure ID、PNG 與 JSON 報告，截圖不成功會明確說明。具體操作與限制見 docs/wall-two-stage.md。

## 2026-11-17 / v0.9.1

0.9.0 安裝後的實際建牆仍回滾；確認安裝目錄 DLL 為 0.9.0。根因之一已明確定位為 Transaction.Start 重設先前設定的 FailureHandlingOptions，讓接合前處理器未生效。已改為先啟動再安裝前處理器，補負向重現與正向檢查。需安裝新版後再實測 DWG，未宣稱實機成功。

## 2026-11-17 / v0.9.0

本次修正私人 CAD 自動建牆：同厚度優先、缺少時複製可安全調寬的單層類型；保留既有類型與材料。新牆 CannotKeepJoined 錯誤只允許 Revit DetachElements 解決方式，不刪牆、不移動、不解除既有元素；不支援的錯誤維持回滾。提供新建類型、問題接合元素 ID 及厚度不符統計。包含 11/13 的內外部獨立服務與 11/16 的連線設定教學。此次只製作 2025 安裝檔，沒有替換正在執行的 Revit 外掛，也沒有重跑實際模型建牆。

驗收與限制：[自動選型與接合修正](cad-wall-auto-types.md)。下方保留舊版歷程。

記錄日期：2026-11-04（Asia/Taipei）

最新專案組織調整：VS 連結路徑已對齊 TYBIM_MCP 根目錄下的實體路徑，包含私人、Study、Upstream、測試來源與圖片。10 個專案 270 個 MSBuild 連結項目核對通過；2025 外掛編譯成功。新增 .vs 排除並停止追蹤兩個既有索引快取，本機檔案保留；私人來源仍排除。本次未製作新 MSI，既有 0.8.2 安裝檔保持原樣。

Revit 2024／2025／2026 的 0.8.2 安裝檔均已完成：包含回合結束與下一步提示、2024 UTF-8 BOM 通訊修正，以及既有忙碌保護、設定收合和圖片／文字附件。未自動安裝或替換已載入外掛。

前次已清理自有 CAD 建模的來源適配文字與來源雜湊紀錄，不變更建模演算法，當時未編譯或製作新安裝檔。後續未指定年份時，只製作 Revit 2025，其他版次須由使用者明確要求。
最新程式版本：**0.8.2**。維持四種獨立建模，建牆預設只建牆，門／窗需明確要求且可分別啟用；本次更新 CAD 向量／圖層演算法與拓源數位科技著作權紀錄；三版 MSI 已完成並通過封裝與相依載入驗證。測試未操作實際 Revit 模型。

## 後續安裝檔製作規則（拓源數位科技，2026-10-28）

- 後續修改預設只製作 **Revit 2025** 安裝檔，先交由 拓源數位科技 測試。
- 拓源數位科技 確認 2025 沒問題並明確要求後，才製作 **Revit 2024**；2026 等其他年份也須另有要求，不再預設三版一起產出。
- 新版安裝檔成功產生並通過驗證後，刪除輸出資料夾中被新版取代的同年份舊版安裝檔，避免持續累積容量。其他年份保留各自最新可用版，直到該年份新版成功取代。
- 同步更新該輸出資料夾的校驗清單與說明連結，避免指向已刪除檔案；不刪除原始碼或驗證紀錄。
- 0.7.1 起 Build-Msi.ps1 預設只製作 2025，全部指定年份驗證成功後才清除被取代的舊 MSI。本次 拓源數位科技 另有明確要求 2024～2026，為三版交付例外。

## 目前交付範圍

- 已製作 Revit **2024、2025、2026** 各自獨立的 x64 MSI；2027 尚未發布 MSI。
- 2024 使用 .NET Framework 4.8；2025／2026 使用 .NET 8，DLL 按對應 Revit API 分版編譯，不能混用年份。
- Revit「拓源數位 → MCP」提供「AI助手」及「相機管理」。兩個按鈕可開啟／收合各自視窗；相機管理的啟動預設為隱藏，AI助手預設開啟。
- AI助手整合 Codex 帳號登入、Claude API Key、Gemini API Key。不同 AI 供應商共用同一套 Revit 工具與確認流程。
- 原有工具為 **258 項**：TYBIM 原有工具 11、upstream 工具 27、Study 工具 220；個人 CAD 工具共 5 項，內部預設共 **263 項**。私人擴充會影響實際清單，新增功能後須重新查詢。

## 版本更新摘要

| 版本 | 已完成內容 | 驗證／回報界線 |
|---|---|---|
| 0.8.2 | 對話歷史及狀態區顯示回合完成、停止、未完成／工具錯誤與下一步；修正 2024 UTF-8 BOM。三版 MSI 更新。 | Node 67、2024／2025 WPF 通過；三版完整 CAB／相依載入／路徑／登錄驗證通過，舊版 MSI 已清除。未重跑 Windows 安裝生命週期，實際 Revit 待驗收。 |
| 0.8.1 | Revit 忙碌未開始保護、停止 AI 重複讀取、設定完整收合及圖片／文字附件；僅製作 2025。 | Node 65 項、排隊服務及 WPF 測試通過；2025 MSI 3759 檔、CAB 雜湊／相依載入／註冊設定通過。未重跑本版 Windows 安裝生命週期，實際 Revit 待驗收。 |
| 0.8.0 | 同步 CAD 雙線／牆網建模、柱支撐與獨立板流程；著作權名稱改拓源數位科技且不再詢問產品授權。 | Node 57、私人 MCP 12、原生幾何 31 項通過；三版 MSI 封裝／相依載入通過，0.7.1 舊 MSI 已清除；實際 Revit 待驗收。 |
| 0.7.1 | 建牆只建牆；門／窗獨立明確啟用；AI 提示、原生選擇規則及安裝輸出清理。 | Node 57、私人 MCP 11、幾何／選擇規則 24 項通過；三版 MSI 封裝／載入通過，0.7.0 舊 MSI 已清除；實際 Revit 待驗收。 |
| 0.7.0 | 加入個人自動建柱、樑、板，更新牆／門窗選項與 CAD 分析；DWG 樑標註採 ACadSharp 3.8.0。 | 三版編譯、Node 57、私人 MCP 11、幾何 18 項通過；三版 MSI、263 工具探索及 DWG 文字讀寫載入通過；2024 隔離安裝／移除通過，2025 隔離升級與移除通過。實際 Revit 建模待驗收。 |
| 0.6.0 | 模型／Codex 路徑唯讀選單；預設載入命令，服務隨 AI助手顯示／收合；新增 拓源數位科技 個人 CAD 建牆與書面授權聲明；解除安裝清理及升級保護。 | Node 57 項、私人 MCP 5 項、私人幾何 9 項，兩框架 WPF 各 52 項及服務測試各 14 項通過。2024／2026 移除清理與 2025 隔離升級及移除已通過；實際 Revit 建牆與視窗事件待驗收。 |
| 0.5.5 | 修正確認、送出等下方按鈕被裁切。設定、對話及完整參數可捲動；執行／取消移到下方操作區；面板過矮時，下方區域亦可捲動；取消固定最小寬度並讓標籤換行。 | 兩種 WPF 框架各 46 項檢查通過，包含大量 ID、窄／矮面板與 150%／200% 等比例縮放；已人工檢視渲染圖。尚待使用者回報新版在 Revit 內的最終結果。 |
| 0.5.4 | 登入頁按鈕改為固定單一控制項；重複網址不新增按鈕、不自動重複開瀏覽器；待登入時重按登入只重開目前網址；處理 Click 路由及登入狀態清理。 | 重複網址與點擊回歸測試通過。使用者後續回報確認區按鈕被擋住，已於 0.5.5 處理。 |
| 0.5.3 | 修正啟動時相依 DLL 的解析。新增 bootstrap 與同目錄解析器，避免啟動程式碼先載入 Contracts 等相依型別而失敗；MSI 加入實際 CAB 內容及載入檢查。 | 可重現未啟用解析器時找不到 Contracts，啟用後通過。2024 隔離安裝／載入／解除安裝通過；使用者已在 2024、2025 實測並回報後續登入按鈕問題。 |
| 0.5.2 | Claude 模型清單與分頁、Claude／Gemini 共用按需工具載入、暫時性過載重試、錯誤分類與取消／串流保護；補齊三版 MSI 與解除安裝註冊檢查。 | Node 56 項測試通過；未以真實 Claude API Key 完成端到端推論驗收。 |
| 0.5.1 | Gemini 模型篩選、模型更正、等待／工具進度與逾時提示，縮減單輪傳送的工具 schema。 | 使用者曾確認 Gemini Flash Lite 可建立樓層；Flash 的過載／額度等錯誤屬供應商回應，不能保證任一模型隨時可用。 |
| 0.5.0 及以前 | Setup Project、年度安裝目錄、相機視窗與 Ribbon、StudyCatalog 啟動修正、兩份外來工具整合、內嵌多 AI 對話、專案／樓層／選取查詢與放門流程。 | 詳細歷史、案例與限制保留在既有操作及驗證文件。 |

## 目前安裝檔（2026-10-29）

| Revit | 安裝檔 |
|---|---|
| 2024 | [TYBIM_MCP Revit 2024 0.8.2](../TYBIM_MCP_Setup/Release/TYBIM_MCP_Revit2024_0.8.2.msi) |
| 2025 | [TYBIM_MCP Revit 2025 0.8.2](../TYBIM_MCP_Setup/Release/TYBIM_MCP_Revit2025_0.8.2.msi) |
| 2026 | [TYBIM_MCP Revit 2026 0.8.2](../TYBIM_MCP_Setup/Release/TYBIM_MCP_Revit2026_0.8.2.msi) |

三版已通過完整 CAB 雜湊、年度 manifest／API／框架／版本、LoadFile／LoadFrom 相依解析及 DWG 文字讀寫、著作權告示與解除安裝登錄設定驗證。2024 共 3767 檔，2025／2026 各 3760 檔。被取代的 2024／2026 0.8.0 與 2025 0.8.1 MSI 已清除；校驗清單為 Release/checksums-0.8.2.sha256。未重跑本版 Windows 安裝／移除生命週期，未操作使用者 Revit 模型；產物已排除 Git。

## 安裝配置

先儲存模型並關閉 Revit，再由實際使用 Revit 的 Windows 帳號安裝對應 MSI。

```text
%APPDATA%\Autodesk\Revit\Addins\
  <年份>\TYBIM.Mcp.addin
  TYBIM_MCP\<年份>\
    Tybim.Revit.Addin.dll
    Tybim.Mcp.Contracts.dll
    Tybim.Revit.Commands.dll
    Tybim.Revit.Study.dll
    Tybim.Revit.Upstream.dll
    其他相依 DLL、runtime、chat.config.json
```

`.addin` 的 Assembly 為 `..\TYBIM_MCP\<年份>\Tybim.Revit.Addin.dll`，入口為 `Tybim.Revit.Addin.Application`。年度註冊檔與共用根目錄內的年度 DLL 資料夾各有用途，不可只搬其中一邊。

三版有各自的產品／升級識別，可並存。Windows 解除安裝名稱為 `TYBIM_MCP for Revit <年份>`。目前使用者安裝與過去全機安裝的範圍不同，舊版處理及建置方式見 [Setup 說明](../TYBIM_MCP_Setup/README.md)。

## 後續修改程式的位置

| 功能／分類 | 位置 |
|---|---|
| Ribbon 按鈕、圖片與提示 | `src/Tybim.Revit.Addin/RibbonUi.cs`、`Pic` |
| AI助手版面、登入頁按鈕、確認操作區 | `src/Tybim.Revit.Addin/Chat/ChatPanel.cs` |
| 外掛啟動與相依解析 | `src/Tybim.Revit.Addin/Bootstrap.cs`、`DependencyResolver.cs`；原啟動實作為 `Application.cs` 中的 `RevitApplication` |
| AI 連線、模型策略、重試與工具確認 | `server/src/chat` |
| TYBIM 原有 Revit 命令 | `src/Tybim.Revit.Commands` |
| 外來原始快照，保持原樣供更新比對 | `third_party/mcp-servers-for-revit/original`、`third_party/revit-mcp-study/original` |
| 實際編譯的外來適配程式 | `integrations/<來源>/revit`、`src/Tybim.Revit.Upstream`、`src/Tybim.Revit.Study` |
| 外來 MCP 工具適配 | `server/src/integrations/<來源>` |
| 新增私人業務功能 | `private/revit`、`server/src/private`，已排除 Git |
| 安裝檔產生及驗證 | `TYBIM_MCP_Setup/Build-Msi.ps1`、`Generate-Setup.mjs`、`Verify-Msi.ps1` |

兩份外來來源與 TYBIM 自有程式分開管理；更新上游先比較，再合併至適配版。完整規則見 [原始碼分類](source-ownership.md)。

## 驗證現況與待辦

已完成的建置／測試結果以 [validation.md](validation.md) 為準；本次執行三版編譯、封裝及相關回歸測試。

- 0.7.0：三版編譯、MSI 封裝與 DWG 文字讀寫載入、263 工具探索通過；Node 57、私人 MCP 11、私人幾何 18 項通過；2024 隔離安裝／移除及 2025 升級／移除通過。實際 Revit 建模待驗收。
- 0.6.0：三版 Release 編譯與 MSI 驗證通過；Node 57、私人工具 5、私人幾何 9 項，WPF 兩框架各 52 項、服務兩框架各 14 項通過。
- MSI 驗證涵蓋內嵌 CAB 全檔雜湊、`.addin` 路徑、必備 DLL、框架與 API 年份、隔離載入、解除安裝設定。0.7.0 的 2024 為 3765 檔，2025／2026 各 3758 檔。
- **待使用者驗收 0.8.0**：在 Revit 2024／2025／2026 內確認唯讀選單、Ribbon／X 收合後服務關閉及重開、CAD 自動建牆，以及既有長參數／登入版面。尚未收到最新版的完整實機驗收回報。
- **待實機驗證**：Claude 真實 API Key 的登入設定、模型選擇、讀取與需確認的寫入流程；既有測試包含模擬，不等同雲端實測。
- **持續事項**：新增／更新工具後，驗證參數、確認、Undo、失敗結果及操作狀態；外來來源持續更新時使用既有比對流程。
- Revit 2027 尚未交付 MSI，也不能以其他年份 DLL 代替。

## 登入與資料保護紀錄

個人設定與登入資料使用 `%LOCALAPPDATA%\TYBIM\McpChat\<年份>`，API Key 使用目前 Windows 使用者的 DPAPI 加密。Codex 使用獨立的本機登入設定；MSI 不複製開發者的登入檔。過去檢查未發現有效憑證被納入版本控制；本次未重新執行憑證稽核，亦未讀取實際登入檔或金鑰。

Git ignore 不會移除已追蹤或已公開的秘密，也不是保密保證；私人原始碼須另行私人備份。整體專案與內部套件的公開範圍依 [原始碼分類](source-ownership.md) 管理。

## 本次個人功能與授權

自動建柱、樑、板、牆歸在 Git 忽略的 `private/revit/AutoBuild` 與 `server/src/private/register.ts`，不放入 Study。0.8.0 起著作權／授權名稱為拓源數位科技，只保留文字及註解紀錄，AI／MCP 不詢問產品授權；仍無執行時序號／授權檔驗證。現有三專案中未找到可直接沿用的產品授權系統；線上／離線方案與操作見 [個人功能與授權](personal-features.md)。

AI助手關閉後，外部 MCP 用戶端亦無法連線；重新開啟後才恢復。移除只清理該年度已安裝 addin 目錄，其他外掛／年份與 LocalAppData 登入資料保留。

## 接續工作入口

先讀本檔，再依需求閱讀：

- [安裝檔與重建操作](../TYBIM_MCP_Setup/README.md)
- [詳細驗證及歷史結果](validation.md)
- [內嵌 AI 操作](embedded-chat.md)
- [上游與私人程式分類](source-ownership.md)
- [放門流程與限制](door-placement.md)

本次文件整理未建立 Git commit，亦未推送遠端。

## 2026-11-18：Revit 2025 v0.13.0 首次連線

以全新 Codex／Claude Code 沒有 MCP 設定為預設，安裝自帶 Node.js；啟用外部 AI 服務後精靈可直接建立 TYBIM 設定。Claude Desktop 為獨立選項，不依賴 node_repl 或舊 mcp-server-for-revit。寫入後讀回驗證，以唯讀 revit.health 核對連線年份。取消後再次啟用可重開精靈；首次啟用不再以 Revit 模態提示框阻擋連線檢查。用戶仍須登入 AI、重啟 AI 並依其規則確認信任與工具權限。

本次只產出 2025 安裝檔；42 項 PowerShell 隔離測試涵蓋空白設定、合併備份、異常與連線回應。實際新電腦 AI 用戶端安裝後操作由使用者測試；既有個人設定備份保留。

驗證完成：2025 v0.13.0 MSI（3,765 檔）通過 CAB 雜湊／路徑、五個 DLL 隔離依賴載入及解除安裝登錄檢查；42 項設定檢查在 Windows PowerShell 5.1 與 PowerShell 7 通過，8 項 C# 完成狀態檢查、精靈版面檢查與 MCP 266 工具套件檢查通過。舊 2025 v0.12.1 MSI 已移除；2024／2026 維持 v0.12.1。本次清理 869.32 MB 可重建輸出；VS 正在使用的索引保留。未安裝 MSI、未改動目前 AI 設定或 Revit 模型。

## 2026-11-19：v0.14.0 三年度安裝檔

AI 連線設定新增選定年份／用戶端的清除按鈕，備份後移除 TYBIM 項目、讀回驗證，保留其他設定；版本清單依已安裝外掛檔案偵測、遞增排列並預選啟動 Revit 年份。切換年份會切換安裝路徑。自有設定／確認、服務狀態、Hello World、相機預覽及記錄視窗採 Pic/TYBIM.ico；Revit 原生停靠框與依賴載入失敗的原生備援提示仍由 Revit 控制。服務狀態改用自有無模式視窗，避免擋住外部 MCP 查詢。

驗證完成：Revit 2024／2025／2026 v0.14.0 MSI 全數通過 CAB 逐檔內容、圖示資源、DLL 隔離載入與解除安裝登錄檢查；檔案數分別為 3773／3766／3766。51 項設定／清除／版本偵測檢查於 Windows PowerShell 5.1 及 PowerShell 7 通過，8 項首次使用完成狀態檢查與既有建模回歸通過。實際安裝目錄偵測顯示 2024、2025，預選 2025；三版均有 266 個 MCP 工具。舊版 2024、2025、2026 MSI 已移除，保留三個 v0.14.0。未代為安裝或修改現用 AI 設定、Revit 模型；實機 UI 與安裝測試由使用者執行。

## 2026-11-19：專案深度精簡

整個資料夾（含 Git）由約 585.37 MB 精簡至 278.55 MB，釋放約 306.82 MB，淨減少 15,181 個檔案。清除可重建的部署套件、Node 依賴與編譯 JS、重複 API 下載快取、舊測試輸出和 VS 索引；保留必要 API DLL 與授權、私人原始碼、回歸測試、Git 與 VS 個人設定。16 個必要 .ps1 合計約 84 KB 保留。三個 v0.14.0 MSI 的 SHA256 與清理前一致。Build-Msi 缺少 Node 依賴時會 npm ci 還原；開發 CLI 改要求明確指定已安裝橋接設定，不再依賴舊部署副本。未重新製作安裝檔，未改動已安裝外掛、AI 設定或模型。

## 2026-11-19：設定工具資料夾整併

外部 AI 設定原始檔已統一至 scripts/ExternalAiSetup，移除空的 tools 頂層目錄。Package-Chat、PowerShell 測試與操作文件均使用新路徑；CMD 啟動器、同目錄模組匯入及圖示相對路徑保持可用。安裝目錄仍為 client-setup，既有安裝檔與使用者設定不變。51 項設定測試、精靈版面／圖示／年份預選檢查、實際封裝複製區段及四檔雜湊驗證通過；Git 未排除新目錄，未重新製作安裝檔。
