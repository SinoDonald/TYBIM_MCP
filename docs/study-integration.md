# REVIT_MCP_study 整合 v0.4.0

來源：使用者本機 `C:\Prj\Revit\REVIT_MCP_study`，commit `9a6ebf52958f2c5c69bcf570ca1d5ff5e2a88f68`。沒有修改或上傳原參考專案。授權聲明與來源紀錄見 `third_party/revit-mcp-study/NOTICE.md`、`UPSTREAM.json`。

## 實際範圍

依原始碼完整工具註冊流程擷取 221 個宣告，接通其中有原生命令實作的 **220 個工具**，前綴為 `revit_study_`。`check_sanitary_fixture_requirements` 缺少實作，沒有註冊。原 TYBIM 與第一套上游共 38 個工具繼續保留，預設總數 **258**；將來私人工具另計。

包含專案／元素查詢、視圖與圖紙、尺寸標註、房間、MEP、結構、碰撞檢查、相機、帷幕牆、地形整地、CAD 與材料等。完整清單在 `third_party/revit-mcp-study/catalog.json`。這是程式實作與入口的整合，不代表每一功能已在你的實際模型驗證成功；族型、模型狀態與 Revit 版本仍會影響結果。

沿用單一 TYBIM addin、AI 登入及 token bridge（2025 為 17825）。不另啟動原 study 的 8964 服務。原相機面板獨立初始化，使用新的 pane GUID，按鈕在「拓源數位 → MCP → 相機管理」。AI 助手按鈕維持原 32px 圖示。沒有移植原 Node server 的 MCP HTML App 呈現，但對應資料命令仍可呼叫。

相機管理按鈕使用 Pic/相機管理.png（32×32），說明預覽使用 Pic/原圖/相機管理(原圖).png，並提供【功能介紹】與【使用步驟】。載入時將顯示 DPI 正規化為 96，不改動你的原圖。v0.4.3 移除標題中的明確換行，顯示一列「相機管理」。

Revit 啟動時相機面板預設隱藏，AI助手顯示。v0.4.2 只在早期閒置套用一次，實機回報相機仍顯示；v0.4.3 改在 ApplicationInitialized、ViewActivated 與相機 WPF 面板恢復可見時標記待處理，再由 Idling 使用有效 UIApplication 隱藏。沒有待處理工作時不查詢或修改面板；AI助手只初始化顯示一次，不持續強制展開。

需要時點「相機管理」開啟，按重新整理載入相機。按鈕成功開啟後標記使用者主動操作，解除本次啟動的可見性防護，不再自動隱藏。下次啟動 Revit 才重新收起。處理記錄與例外寫入 %LOCALAPPDATA%/TYBIM/McpLogs/pane-layout.log，供實機驗收排查。

## 程式碼位置

| 用途 | 位置 |
|---|---|
| 原始碼快照，只供來源比對 | `third_party/revit-mcp-study/original/` |
| 實際編譯的外來適配程式 | `integrations/revit-mcp-study/revit/` |
| 命令主分派 | `integrations/revit-mcp-study/revit/Core/CommandExecutor.cs` 及同目錄 partial 檔 |
| 相機面板 | `integrations/revit-mcp-study/revit/UI/` |
| TYBIM 原生接入與命令表 | `src/Tybim.Revit.Study/` |
| 統一文件／操作狀態管理 | `src/Tybim.Revit.Commands/UpstreamCommands.cs` |
| MCP 工具宣告與轉接 | `server/src/integrations/revit-mcp-study/` |

Visual Studio 開啟 `TYBIM.MCP.sln`，展開 `Tybim.Revit.Study → AdaptedStudy` 即可看到實際編譯的外來程式。不要將新私人功能寫進原始快照；請參考 `source-ownership.md` 的私人入口。

## 操作與交易

1. 安裝完整 0.4.0 套件後重開 Revit。開啟 AI 助手，連線／取得模型並開始新對話；既有登入資料保留。外部 MCP 用戶端要重啟連線，重新取得工具。
2. 可先要求「使用 Study 工具讀取目前專案資訊及目前選取元素」，再測試所需功能。查詢先取得 TYBIM 的 documentId。
3. Study 寫入使用完整參數確認卡，不冒稱已做幾何試放。每次寫入有唯一 operationId；逾時先查 `revit_upstream_operation_status`，不要盲目重做。
4. Study 保留各命令原有交易，不強加跨文件 TransactionGroup，以支援開啟／儲存文件。失敗回傳 `failed`、`partialChangesMayRemain`；可能已有部分模型變更，檔案輸出或文件操作也無法靠目前模型 Undo 完整還原。依結果決定如何處理，不能宣稱整批都已回復。
5. `open_document`／`list_open_documents` 在沒有開啟模型時使用 `documentId: application`；有模型時仍使用目前模型 ID。保存、關閉、匯出等需明確要求並確認。
6. 擷取視圖影像會回傳 MCP image block，介面可直接顯示；AI 是否能分析影像亦取決於所選模型的影像能力。

原相機設定／記錄隔離在 `%APPDATA%\TYBIM\McpStudy\2025`（或 2024）。不改寫原 study 的設定。部分 CAD 轉換功能另外需要 Python、ezdxf；DWG 轉 DXF 需原功能支援的 ODA File Converter。這些是選用依賴，未擅自安裝；一般查詢、模型操作與對話不需要它們。

## 建置與更新

執行 `npm.cmd run build`（server 目錄），然後 `dotnet build src/Tybim.Revit.Addin/Tybim.Revit.Addin.csproj -c Release -p:RevitVersion=2025 --ignore-failed-sources`。完整包使用 `scripts/Package-Chat.ps1 -RevitVersion 2025`；安裝包自帶安裝腳本。Revit 2024 將版本參數改成 2024。

更新外來來源請先使用 `scripts/Compare-Upstream.ps1`，再人工合併適配版、重跑測試。不要直接覆蓋 integrations 或 private。`server/scripts/build-study-catalog.mjs` 只適合審查後重新產生工具與原生命令對應表；`scripts/Record-UpstreamSnapshots.mjs` 記錄既有快照，不會自動抓取新版，也不要用它掩蓋未審查的快照異動。
