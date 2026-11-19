# CAD 建牆自動選型與接合修正 — v0.9.1

## 0.9.1 實機回饋修正

0.9.0 安裝後的 DWG 實測仍因 CannotKeepJoined 回滾，新增 0 道牆；已核對安裝 DLL 是 0.9.0，不能將問題歸咎於未更新。
Revit 2025 本機 RevitAPI.xml 的 Transaction.Start 說明明定：每次啟動交易都會重設 FailureHandlingOptions。
0.9.0 將前處理器設定在 Start 之前，導致修復邏輯失效。本版統一透過 CadWallFailureHandler.StartTransaction，先 Start，再設定前處理器與 ClearAfterRollback。
回歸測試新增「Start 清除先前設定」的負向重現及正式 helper 的正向檢查，共 20 項。
0.9.0 為未通過實機驗收的版本，原安裝包會於 0.9.1 驗證完成後清除。以下為沿用的選型與限定解除接合設計。

0.9.1 已完成 2025 Release 編譯、20 項選型／接合生命週期檢查、CAD 幾何檢查；MSI 3760 個檔案雜湊／路徑、相依 DLL 載入、程式與功能的移除註冊驗證通過。264 個 MCP 工具可正常載入；沒有再次執行模型建牆。0.9.0 安裝包已清除。

2026-11-17；本次只交付 Revit 2025 MSI。程式屬拓源數位科技自有開發，放在 Git 忽略的 private/revit/AutoBuild，並非 Study。

## 行為

使用者明確指定 wallTypeId／wallTypeName 時，仍使用指定型別。
沒有指定時，不再任意固定 RC 牆 15cm，也不再將無法判定的牆厚當作 200 mm：

1. 依 CAD 雙線距離取得牆厚，優先使用現有同厚度（±1 mm）的基本牆型。
2. 若缺少同厚度類型，選厚度最接近、可調整的單層且非垂直複合牆型，複製並保留材料／層功能，將新型別厚度設定至 CAD 的 mm 精度。
3. 新型別名稱例如「TYBIM CAD 100mm - RC 牆 12cm」；同一次執行重用同厚度型別，不逐牆複製。
4. 不修改既有型別，不任意縮放多層飾面／複合牆。沒有安全的單層來源時才選最近型別，結果會明確回報 thicknessMismatches。

選型只依幾何厚度，CAD 沒有提供材料資訊時不代表已辨識正確材料或防火性能；複製牆型沿用來源設定，應由使用者檢查。
原有 CAD 輪廓配對、樓層與指定圖層、數量限制、門窗明確選用規則維持。

## 「無法保持接合元素」處理

原版只刪除警告，接合 Error 進入通用交易保護後整筆回滾。
本版在建牆交易的失敗前處理器中，比對 Revit 的 CannotKeepJoined 識別碼，僅對本次新建牆相關的接合、且 Revit 提供及允許 DetachElements 時，套用解除接合。
不透過刪除元素、改牆厚或搬動牆來隱藏錯誤；不解除既有模型元素的接合。一般可正常接合的牆保持原行為。
同一組元素最多嘗試一次，避免無限處理；未解決或其他 Error 繼續交由原有保護回滾。

完成結果新增：

- createdWallTypes：因 CAD 厚度新建的類型。
- recoveredJoinElementIds：已套用問題接合處理的新牆 ID 組。接角可能顯示接縫，應檢查。
- thicknessMismatches：未能匹配 ±1 mm、使用最近型別的牆數。

牆體與中心線保留不表示所有交接都已達到出圖需求；請檢查接角、牆厚、材料及牆數。

## 程式與測試

- private/revit/AutoBuild/CadWallTypeSelector.cs：同厚度優先、安全複製與快取。
- private/revit/AutoBuild/CadWallFailureHandler.cs：限定失敗類型、限定新牆與明確解除接合，防止重複嘗試。
- private/revit/AutoBuild/CadWallBuilder.cs：先準備類型再建立個別牆，交易提交後回報。
- private/revit/AutoBuild/Models/CadWallInfo.cs：新增結果欄位。
- server/src/private/register.ts：工具提示 AI 未指定牆型時保留自動選型。
- private/tests/WallCreationRegression：18 項針對選型及錯誤處理決策的模擬 API 回歸檢查。

完整 2025 編譯驗證真實 Revit API 簽章；模擬 API 測試不能替代 Revit 的幾何接合驗收。此版尚未在使用者 DWG 上重跑建模。

交付驗證：Revit 2025 Release 編譯、18 項新牆型／接合檢查、既有 CAD 幾何檢查、67 項 Node MCP／AI 測試均通過。
MSI 內 3760 個檔案與 payload 雜湊、相依 DLL 隔離載入、2025 相對 addin 路徑、Windows 程式名稱與移除設定皆通過驗證。
同年舊版 TYBIM_MCP_Revit2025_0.8.2.msi 已於新版驗證後移除；2024／2026 未更新或刪除。

## 安裝及測試

1. 儲存模型並關閉所有 Revit，再執行 TYBIM_MCP_Revit2025_0.9.0.msi。既有 2024／2026 不更新。
2. 開啟 LinkCAD_2025，切換 FL1，確認目前沒有上次測試留下的重複新牆。
3. 使用內建 AI助手，或開啟外部AI服務後重新載入 Codex／Claude 的 MCP。兩邊使用同一份新 DLL。
4. 輸入：「依 A1_Test.dwg 的 WALL、WALL2、WALL3 建牆，FL1 到 FL2；依 CAD 厚度自動選型，不指定固定牆型，只建牆。」
5. 核對確認參數沒有 wallTypeId／wallTypeName，也沒有啟用柱、門窗或樓板。
6. 檢查實際牆厚 100／150 mm、新建類型與接合處理的 ID。不要只看 AI 文字成功，要確認工具 status=completed 及實際建立數。
7. 如仍回滾，保留工具完整 failures 與 operationId，勿盲目重複建立。成功後可 Undo，模型不會自動儲存。
