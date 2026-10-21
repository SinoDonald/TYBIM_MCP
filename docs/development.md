# 擴充與架構規則

## 新增唯讀工具

1. 在 `src/Tybim.Revit.Commands` 新增實作 `IRevitCommand` 的類別，例如 `revit.rooms`。
2. 在 `CommandRegistry.CreateDefault()` 註冊命令。大量命令時依 Architecture／Structure／MEP／DataExtraction 切分子 registry 或專案。
3. 在 `server/src/tools.ts` 新增 MCP 名稱、bridge method、zod 輸入 schema、清楚的說明與 annotations。工具多了可分檔，再由 index 統一註冊。
4. 所有 Revit API 存取維持在 command Execute 內，輸出 DTO；不要把 Document／Element 物件或 lazy LINQ iterator 傳給網路執行緒。
5. 加入工具呼叫測試及實機驗收案例，編譯 2024、2025，再更新部署套件。

## 未來寫入工具的完成條件

- 工具描述提供必要參數，不讓 AI 輸入任意 C#／Python 程式。
- 要求 `documentId`、確認模型與範圍、最大修改數量；server 與 Revit command 各驗證一次。
- preview 產生可 review 的變更計畫；execute 明確指定已確認的目標與參數。需求確定後再設計 preview token，而非先加入無實作的寫入介面。
- 一個 command 管理 Transaction／TransactionGroup；Exception 與失敗狀態必須 rollback，不回傳假成功。
- 結果包含成功／失敗 element IDs、實際單位及 warning，必要時讓使用者能 Undo。
- 要求 ID 用於通訊關聯，不是寫入冪等鍵。execute 須另設 operationId／結果保存，逾時後先查狀態再決定是否重試。
- 有執行時間長的工作時，將 dispatcher 改為分批 ExternalEvent／progress／cancel；目前只支援短小唯讀命令。

## 通訊契約 v1

bridge 不是另一個 MCP Server；stdio MCP 由官方 SDK 處理。
每次 command 建立一條 TCP 連線，一個 UTF-8 JSON request 加換行，一個 response 加換行，然後關閉。

Request 欄位：`version=1`、`id` UUID、`token`、`method`、`parameters` object、`expiresAt` Unix milliseconds。
Response 欄位：`version=1`、相同 `id`、`success` boolean、`data` 或 `error`。

- C# request 最大 64 KiB，JSON depth 32；Node response 最大 4 MiB。
- 同時連線最多 8，Revit 佇列最多 64；讀取 frame 最長 5 秒，MCP command 預設總逾時 15 秒。
- 不自動重試；關閉 Revit 時清除佇列及連線；晚到回應丟棄。
- 目前工具清單明確註冊於 C# 與 TypeScript。更大規模時新增單一工具契約來源或 capability discovery，避免兩邊命名及 schema 漂移。
- `documentId` 是開啟文件期間的 GUID；重新啟動或重新開啟模型後，重新查詢專案資訊。
- 只記錄基本診斷到 .NET Trace，第一階段未實作持久化 audit log；正式写入功能應新增結構化日誌，排除 token、API key 和敏感模型資料。

## 建置與相依

使用同一份原始碼、`RevitVersion` 指定 target framework 與安裝版 API。輸出及 obj 都按年份隔離。
RevitAPI／RevitAPIUI 屬於 host 提供的組件，`Private=false`，不複製或解析整串 Revit 主程式的相依 DLL 作為部署資產。
跨大版 API 差異應集中到 adapter；不要在每個命令散落大量條件編譯。
NuGet Newtonsoft.Json 固定 13.0.3，Node 使用 package-lock.json。升級 SDK 時跑 stdio contract test，再進 Revit 驗收。

本專案不會在編譯時修改 AppData、AI 用戶端全域設定或原 TYBIM 外掛。
部署在重啟 Revit 後才生效；關閉 Revit 再更新 DLL。移除 Addins 中本專案 manifest 即停用本外掛。
