# 兩階段建牆、接合與問題定位 — v0.11.0

本次只交付 Revit 2025。所有 AutoBuild 與新接合原生程式在 Git 忽略的 private/revit，Node 註冊與截圖讀取在 server/src/private；皆為拓源數位科技自有功能，非 Study。

## 先建牆

先查 CAD，再確認圖層／上下樓層。未指定牆型仍依 CAD 厚度選型，缺少同厚度時可複製單層牆型。
新建牆後立即停用兩端接合，再設定牆約束並提交整批牆。建立交易群組結束後，移除建立階段的全組回滾處理器，才開始逐組接合。
預設 autoJoin=true：只對本次 wallIds，先嘗試牆端；未形成接合或交易失敗時復原，再嘗試幾何接合；兩者都失敗保留牆與先前成功接合。autoJoin=false 可只建不接合。預設不建立柱、門窗或板。
建立完成 status=completed 代表牆已提交，不代表所有接合成功；核對 autoJoin.joinedPairs、failedPairs、issues。joinsDeferred=true 表示停用接合或仍有失敗待處理。後處理意外中止時另回報 postProcessingError 及 modelChangesCommitted=true，不得重新建同一批牆。

測試句：「依 DWG 的 WALL、WALL2、WALL3 建立 FL1 到 FL2 的牆，依 CAD 厚度自動選型，不指定固定牆型，只建牆，先不要接合。」

停用接合後仍需檢查牆厚、接縫／重疊及房間邊界；不能視為所有出圖細節都已完成。其他幾何錯誤仍可能使建牆失敗，不承諾每份 CAD 都能建立。

## 獨立牆接合

工具 revit_private_join_walls 必須指定 2～500 個有效且不重複的基本牆 ID，不默認處理整個模型。使用建牆結果的 wallIds；使用者另指定既有牆時也可處理。

- mode=auto（預設）：先試牆端，失敗回復後再試幾何接合。
- mode=geometry：對候選牆做幾何接合，不啟用牆端；不適用的幾何配對會回報失敗。
- mode=wallEnds：只對鄰近牆軸的可用端點恢復牆端接合，並查驗是否真的接到該牆；未形成接合就回復該組設定。若附近包含未指定的牆，保守略過，避免範圍外變更。
- toleranceMm 預設 5、範圍 0～50，僅用於候選範圍，不會把牆移動 5 mm 或填補空隙。
- maxPairs 預設 1000，上限 2000；超過先拒絕，不先處理一半再停止。
- captureLimit 預設 3，上限 5；0 表示不產生截圖但仍回報所有問題。

每組獨立交易，整批以 TransactionGroup.Assimilate 合併成一筆接合復原紀錄。失敗只回復該組，原牆與先前成功的組別保留。建模與後續接合各有一筆 Revit Undo；不自動儲存模型。
只要有失敗，操作可能回傳 status=failed、nativeTransactions=true、partialChangesMayRemain=true，並不代表牆或成功接合被全部回復。
必須核對 joinedPairs／failedPairs／issues，逾時查同一 operationId，不可盲目重試。

## 自有自動接合／取消接合

新增 revit_private_auto_join（原生 private.auto_join），程式在 private/revit/AutoBuild/AutoJoinCommand.cs，不在 Study；共用 JoinPairTransaction.cs 的逐組失敗隔離。

兩種範圍擇一，不能混用，也不預設整個模型：

- elementIds：2～500 個明確元素 ID，支援柱（含結構柱）、樑、牆、板，只處理所選集合內配對。
- hostCategory＋targetCategories：目前視圖的主體／目標品類，值為 columns、beams、walls、floors。最多 500 個相關元素，配對去重；maxPairs 預設 1000、上限 2000，超限先拒絕不修改。

候選由包圍盒重疊找出，不等於保證有實體交集；Revit 無法接合時回復該組並回報。toleranceMm 不會移動元素或填補空隙。
action=join 接合；unjoin 只取消幾何接合，不停用牆端接合。floorPriority 預設 preserve 保持既有順序；明確要求時用 floorCuts 或 otherCutsFloor，檢查切割元素後才切換，不盲目反轉。
結果 completedPairs／alreadyJoinedPairs／failedPairs／issues；部分失敗時其他成功操作仍保留。本次成功操作合併為一筆 Undo，原牆／其他元素不刪除，不自動儲存。

範例：「將目前視圖的牆與柱、板自動接合，保留樓板原有切割順序。」或「將元素 ID … 取消幾何接合。」需要參數確認範圍，沒有額外著作權授權提問。

## 失敗定位與截圖

issues 每筆包含 stage、elementIds、wallIds（僅牆子集）、reason、locationMm、failureIds；能截圖時另有 screenshotPath。
每個接合問題的 wallIds 都是仍存在於模型的牆，可用 Revit「管理 → 依 ID 選取」定位；亦可要求 AI 依這些 ID 選取。
局部圖以問題交會位置為中心，牆以紅線顯示。截圖由暫時 3D 視圖輸出；建牆失敗且沒有有效新牆時，優先複製目前平面視圖顯示 CAD。
視圖交易提交後輸出圖，再將暫時交易群組回復，不在模型留下視圖／Undo 項目，不改使用者目前視圖。

檔案目錄：

```text
%LOCALAPPDATA%\TYBIM\McpDiagnostics\2025\<診斷識別碼>\
  issue-001*.png
  report.json
```

回傳 screenshotPaths 及 diagnosticReportPath。MCP 會把有效 PNG 以圖片區塊回傳，支援圖片的 Codex／Claude 與內建助手可直接顯示。
Node 只讀取此診斷目錄內的 PNG，檢查檔案簽章及 4 MiB 限制，不把任意路徑當圖片讀取。
截圖不是保證成功：無圖形模組、視圖匯出失敗、目錄無權限或超過截圖上限時，screenshotError／diagnosticNotes 會說明原因，ID 與座標仍回傳。
整筆建牆回滾時新牆不存在，不能拿暫時 ID 查詢；會改提供 CAD 候選牆段座標及上下文圖，並明示位置未必是唯一錯誤交點。

## 程式位置

- private/revit/AutoBuild/CadWallPlacement.cs：建立牆並立即停用兩端接合。
- private/revit/AutoBuild/CadWallJoinCommand.cs：候選範圍、逐組交易、實際接合驗證與失敗記錄。
- private/revit/AutoBuild/CadWallDiagnostics.cs：局部紅色定位圖與報告，暫時視圖清理。
- private/revit/AutoBuild/Models/CadWallInfo.cs：結果與問題欄位。
- private/revit/PrivateRegistration.cs、server/src/private/register.ts：原生／MCP 工具註冊。
- server/src/private/wall-evidence.ts：安全載入 PNG 並回傳 MCP image 區塊。
- src/Tybim.Revit.Commands/UpstreamCommands.cs：private.join_walls／private.auto_join 使用逐組交易契約，create_walls_from_cad 透過提交後回呼執行接合；保留其他指令原本的全組保護。

## 安裝驗收

儲存並關閉 Revit，安裝 TYBIM_MCP_Revit2025_0.11.0.msi。重新開啟模型及外部AI服務，外部客戶端重新載入 MCP，或在內建助手開新對話。
測試建牆時檢查 wallIds 與 autoJoin 統計；只想建不接合時明確要求 autoJoin=false。既有牆可獨立測試 join_walls，既有柱樑牆板可用 auto_join。遇到失敗請保留完整結果與 report.json／圖片。
自動測試以 API 替身驗證停用接合、失敗組回復、成功組保留、ID／座標與圖片讀取限制；完整編譯檢查真實 API 簽章。實際 DWG 與圖形匯出仍需在 Revit 驗收。

驗證涵蓋：2025 Release 編譯、22 項建牆／選型檢查、24 項逐組接合檢查、9 項提交後隔離檢查、31 項 CAD 幾何檢查、70 項 Node 測試。打包時執行 MSI 登錄／內嵌 CAB 逐檔雜湊與隔離載入驗證；實機幾何結果仍待新版安裝後驗收。

v0.10.0 歷史：為先保留牆體，預設只建不接合；v0.11.0 改為提交後預設嘗試接合，失敗保留原牆。
