# TYBIM_MCP 更新狀態

記錄日期：2026-10-28（Asia/Taipei）
最新程式版本：**0.7.1**。維持四種獨立建模，建牆預設只建牆，門／窗需明確要求且可分別啟用；本次依要求完成三版 MSI 與封裝／載入驗證，已清除被取代的舊安裝檔。測試未操作實際 Revit 模型。

## 後續安裝檔製作規則（Donald，2026-10-28）

- 後續修改預設只製作 **Revit 2025** 安裝檔，先交由 Donald 測試。
- Donald 確認 2025 沒問題並明確要求後，才製作 **Revit 2024**；2026 等其他年份也須另有要求，不再預設三版一起產出。
- 新版安裝檔成功產生並通過驗證後，刪除輸出資料夾中被新版取代的同年份舊版安裝檔，避免持續累積容量。其他年份保留各自最新可用版，直到該年份新版成功取代。
- 同步更新該輸出資料夾的校驗清單與說明連結，避免指向已刪除檔案；不刪除原始碼或驗證紀錄。
- 0.7.1 起 Build-Msi.ps1 預設只製作 2025，全部指定年份驗證成功後才清除被取代的舊 MSI。本次 Donald 另有明確要求 2024～2026，為三版交付例外。

## 目前交付範圍

- 已製作 Revit **2024、2025、2026** 各自獨立的 x64 MSI；2027 尚未發布 MSI。
- 2024 使用 .NET Framework 4.8；2025／2026 使用 .NET 8，DLL 按對應 Revit API 分版編譯，不能混用年份。
- Revit「拓源數位 → MCP」提供「AI助手」及「相機管理」。兩個按鈕可開啟／收合各自視窗；相機管理的啟動預設為隱藏，AI助手預設開啟。
- AI助手整合 Codex 帳號登入、Claude API Key、Gemini API Key。不同 AI 供應商共用同一套 Revit 工具與確認流程。
- 原有工具為 **258 項**：TYBIM 原有工具 11、upstream 工具 27、Study 工具 220；個人 CAD 工具共 5 項，內部預設共 **263 項**。私人擴充會影響實際清單，新增功能後須重新查詢。

## 版本更新摘要

| 版本 | 已完成內容 | 驗證／回報界線 |
|---|---|---|
| 0.7.1 | 建牆只建牆；門／窗獨立明確啟用；AI 提示、原生選擇規則及安裝輸出清理。 | Node 57、私人 MCP 11、幾何／選擇規則 24 項通過；三版 MSI 封裝／載入通過，0.7.0 舊 MSI 已清除；實際 Revit 待驗收。 |
| 0.7.0 | 加入個人自動建柱、樑、板，更新牆／門窗選項與 CAD 分析；DWG 樑標註採 ACadSharp 3.8.0。 | 三版編譯、Node 57、私人 MCP 11、幾何 18 項通過；三版 MSI、263 工具探索及 DWG 文字讀寫載入通過；2024 隔離安裝／移除通過，2025 隔離升級與移除通過。實際 Revit 建模待驗收。 |
| 0.6.0 | 模型／Codex 路徑唯讀選單；預設載入命令，服務隨 AI助手顯示／收合；新增 Donald 個人 CAD 建牆與書面授權聲明；解除安裝清理及升級保護。 | Node 57 項、私人 MCP 5 項、私人幾何 9 項，兩框架 WPF 各 52 項及服務測試各 14 項通過。2024／2026 移除清理與 2025 隔離升級及移除已通過；實際 Revit 建牆與視窗事件待驗收。 |
| 0.5.5 | 修正確認、送出等下方按鈕被裁切。設定、對話及完整參數可捲動；執行／取消移到下方操作區；面板過矮時，下方區域亦可捲動；取消固定最小寬度並讓標籤換行。 | 兩種 WPF 框架各 46 項檢查通過，包含大量 ID、窄／矮面板與 150%／200% 等比例縮放；已人工檢視渲染圖。尚待使用者回報新版在 Revit 內的最終結果。 |
| 0.5.4 | 登入頁按鈕改為固定單一控制項；重複網址不新增按鈕、不自動重複開瀏覽器；待登入時重按登入只重開目前網址；處理 Click 路由及登入狀態清理。 | 重複網址與點擊回歸測試通過。使用者後續回報確認區按鈕被擋住，已於 0.5.5 處理。 |
| 0.5.3 | 修正啟動時相依 DLL 的解析。新增 bootstrap 與同目錄解析器，避免啟動程式碼先載入 Contracts 等相依型別而失敗；MSI 加入實際 CAB 內容及載入檢查。 | 可重現未啟用解析器時找不到 Contracts，啟用後通過。2024 隔離安裝／載入／解除安裝通過；使用者已在 2024、2025 實測並回報後續登入按鈕問題。 |
| 0.5.2 | Claude 模型清單與分頁、Claude／Gemini 共用按需工具載入、暫時性過載重試、錯誤分類與取消／串流保護；補齊三版 MSI 與解除安裝註冊檢查。 | Node 56 項測試通過；未以真實 Claude API Key 完成端到端推論驗收。 |
| 0.5.1 | Gemini 模型篩選、模型更正、等待／工具進度與逾時提示，縮減單輪傳送的工具 schema。 | 使用者曾確認 Gemini Flash Lite 可建立樓層；Flash 的過載／額度等錯誤屬供應商回應，不能保證任一模型隨時可用。 |
| 0.5.0 及以前 | Setup Project、年度安裝目錄、相機視窗與 Ribbon、StudyCatalog 啟動修正、兩份外來工具整合、內嵌多 AI 對話、專案／樓層／選取查詢與放門流程。 | 詳細歷史、案例與限制保留在既有操作及驗證文件。 |

## 目前 0.7.1 安裝檔

| Revit | 安裝檔 | 大小 |
|---|---|---|
| 2024 | [TYBIM_MCP Revit 2024 0.7.1](../TYBIM_MCP_Setup/Release/TYBIM_MCP_Revit2024_0.7.1.msi) | 54.5 MiB |
| 2025 | [TYBIM_MCP Revit 2025 0.7.1](../TYBIM_MCP_Setup/Release/TYBIM_MCP_Revit2025_0.7.1.msi) | 52.4 MiB |
| 2026 | [TYBIM_MCP Revit 2026 0.7.1](../TYBIM_MCP_Setup/Release/TYBIM_MCP_Revit2026_0.7.1.msi) | 52.4 MiB |

三個 0.7.0 舊 MSI 已在本次三版驗證成功後移除。SHA-256 見 Release/checksums-0.7.1.sha256；舊版本校驗清單已清理。Release、payload、generated 及 artifacts 為本機產物，已排除 Git。

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
- **待使用者驗收 0.7.1**：在 Revit 2024／2025／2026 內確認唯讀選單、Ribbon／X 收合後服務關閉及重開、CAD 自動建牆，以及既有長參數／登入版面。尚未收到最新版的完整實機驗收回報。
- **待實機驗證**：Claude 真實 API Key 的登入設定、模型選擇、讀取與需確認的寫入流程；既有測試包含模擬，不等同雲端實測。
- **持續事項**：新增／更新工具後，驗證參數、確認、Undo、失敗結果及操作狀態；外來來源持續更新時使用既有比對流程。
- Revit 2027 尚未交付 MSI，也不能以其他年份 DLL 代替。

## 登入與資料保護紀錄

個人設定與登入資料使用 `%LOCALAPPDATA%\TYBIM\McpChat\<年份>`，API Key 使用目前 Windows 使用者的 DPAPI 加密。Codex 使用獨立的本機登入設定；MSI 不複製開發者的登入檔。過去檢查未發現有效憑證被納入版本控制；本次未重新執行憑證稽核，亦未讀取實際登入檔或金鑰。

Git ignore 不會移除已追蹤或已公開的秘密，也不是保密保證；私人原始碼須另行私人備份。整體專案與內部套件的公開範圍依 [原始碼分類](source-ownership.md) 管理。

## 本次個人功能與授權

自動建柱、樑、板、牆歸在 Git 忽略的 `private/revit/AutoBuild` 與 `server/src/private/register.ts`，不放入 Study。依 Donald 選擇，只加私人著作權及書面授權聲明，沒有執行時序號／授權檔驗證。現有三專案中未找到可直接沿用的產品授權系統；線上／離線方案與操作見 [個人功能與授權](personal-features.md)。

AI助手關閉後，外部 MCP 用戶端亦無法連線；重新開啟後才恢復。移除只清理該年度已安裝 addin 目錄，其他外掛／年份與 LocalAppData 登入資料保留。

## 接續工作入口

先讀本檔，再依需求閱讀：

- [安裝檔與重建操作](../TYBIM_MCP_Setup/README.md)
- [詳細驗證及歷史結果](validation.md)
- [內嵌 AI 操作](embedded-chat.md)
- [上游與私人程式分類](source-ownership.md)
- [放門流程與限制](door-placement.md)

本次文件整理未建立 Git commit，亦未推送遠端。
