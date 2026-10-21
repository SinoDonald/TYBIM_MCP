# 沿牆指定比例放門

2026-10-19：新增 `revit_preview_doors_along_walls` 與 `revit_execute_doors_along_walls`。
預覽額外必填 `positionFraction`（嚴格介於 0 與 1 之間），以 Revit 牆定位曲線 endpoint 0 起算，朝 endpoint 1 沿牆長量測；`2.0/3.0` 表示 2/3 處。
比例存入 plan，執行不得另外更改。直線／圓弧使用同一長度比例；門底標高仍是牆底。
預覽及提交前驗證兩端剩餘距離皆須大於門寬一半加 50 mm，避免比例偏向端點時門超出牆端。
原本名稱含 `at_wall_centers` 的工具保留固定 0.5，供既有呼叫使用；以下為共用流程及舊中點工具說明。
Undo 名稱更新為 `TYBIM MCP - Doors along walls`。

回歸檢查：`dotnet run --project tests/DoorPositionRegression/DoorPositionRegression.csproj`（獨立 .NET 10 測試程式，不載入 Revit API），以及 `server` 目錄內的 `npm test`。

在目前文件內各指定牆的定位曲線長度中點放置一個牆宿主門；門底高度為牆基準樓層加基準偏移，不是牆高度中點。
以 Revit 預設方向建立，未指定門扇左右開、內外開；不會自動翻轉門扇。

## 呼叫流程

1. `revit_get_door_placement_context`：`scope=all/view/selection`，分頁取得牆的 UniqueId 及已載入門族型、尺寸、wallHosted。all 不含連結模型。
2. 選擇 wallHosted 為 true 且可辨識正數寬高的門型。
3. `revit_preview_doors_at_wall_centers`：必填 documentId、doorTypeUniqueId、wallUniqueIds（每批 1–100 道、不得重複）。預覽不修改模型，回傳 10 分鐘有效的 planId、位置（mm）與略過原因。
4. `revit_execute_doors_at_wall_centers`：必填 documentId、planId。依原預覽建立；同一 planId 再次執行回傳原結果，不重複建門。
5. 若逾時，以 `revit_get_door_operation_status` 查詢同一 documentId、planId，不能直接建立新 plan 後重送。伺服器結果狀態為 completed／rolledBack，應檢查 createdCount 與 error。

每批一個 Transaction，Undo 名稱為 `TYBIM MCP - Doors at wall centers`。任何建立錯誤或提交警告會回復該批，回傳 failures，不會自動略過警告。
多批各自提交，若後面批次失敗，前面已提交批次仍存在；須依結果逐批處理，不宣稱全模型原子交易。
全部被略過時不啟動 Transaction，也不啟用門型。

## 放置邊界

- 支援垂直基本牆的水平直線／圓弧定位曲線。
- 不處理帷幕牆、疊層牆及子牆、斜牆、漸變牆、群組、設計選項中的牆。
- 牆須存在於目前視圖階段，並可辨識基準樓層；不會在該階段已拆除、尚未建立的牆上放門。
- 牆長至少須大於門寬加 100 mm；門高不可超過牆 bounding box 上界。
- 同一牆中點已有門時略過；同一牆既有門窗／開口的 bounding box 投影與新門重疊時略過，可能保守略過部分可放置的案例。
- 複雜編輯輪廓、牆接合、族型切割與專案約束由 Revit Transaction 最後驗證；預覽不是幾何碰撞的完整保證。
- 執行前重新核對門型 VersionGuid、牆／牆型／樓層及既有 inserts 的 VersionGuid 與階段，變更時要求重新預覽。
- 建立後核對宿主及位置（2 mm 容差）。不儲存模型、不同步中央模型。

plan/result 保留於 Revit 目前行程記憶體（最多 1000 份），重新啟動後舊 plan 無效。重啟後先重新查詢模型，確認已有門再決定是否重新預覽。

## 文件識別修正

2026-10-19：改用 Revit `Document.Equals` 的原生文件相等語意，不再以 managed wrapper 的物件參考作索引。
Autodesk 的本機 RevitAPI.xml 明確指出，同一開啟文件可能由相等的不同 Document instance 表示。
因此跨 MCP 呼叫的 documentId 會維持一致，關閉文件後清除舊對應；寫入工具強制要求 documentId。

## 本機呼叫

```powershell
node server/scripts/invoke.mjs revit_get_door_placement_context '{"scope":"all","limit":1000}'
```

此 CLI 透過官方 MCP Client 呼叫 stdio server，不繞過工具輸入 schema 或橋接權杖。
預設讀取本專案 Revit 2025 設定，也可用 TYBIM_MCP_CONFIG 指向其他已備妥的橋接設定。
