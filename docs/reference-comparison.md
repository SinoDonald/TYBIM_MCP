# 兩個參考專案的選擇

此比較以 2026-10-13 本機原始碼為準，未以 GitHub 星數或 README 的支援宣稱當成實測結果。
參考路徑皆為外部讀取，本專案以自行撰寫的程式建立，未整份複製參考程式。

| 面向 | mcp-servers-for-revit | REVIT_MCP_study | TYBIM 採用方式 |
|---|---|---|---|
| 分工 | server／plugin／commandset 分開 | MCP Server／MCP 分開，功能集中於 CommandExecutor | 採三層，額外抽 contracts |
| 工具擴充 | 每工具 TypeScript 檔及 Command／Service | revit-tools.ts 與大型 switch executor | 明確 registry；日後按功能領域拆模組 |
| 版本 | csproj 設有 2020–2026 configurations | 主要 net48；2024 另專案，部分 Revit 路徑相對寫死 | 先驗證本機 2024、2025，使用可覆寫 RevitInstallDir |
| 通訊 | README 說 WebSocket，SocketService.cs 實際為 TCP 8080 | WebSocket，Node 客戶端使用 ws | stdio MCP + 本機 TCP 17824／17825，獨立 bridge v1 |
| 訊息邊界 | SocketService 以一次 Read 當要求；Node 嘗試解析整份 buffer | WebSocket 本身提供 frame 邊界 | UTF-8 JSON Lines，處理分段、限制大小、要求 ID |
| API 排程 | ExternalEvent／SDK 包裝、commandset handler | ExternalEvent handler 保留單一 _action | 用佇列避免新 action 覆蓋舊 action |
| 網路邊界 | TcpListener(IPAddress.Any, 8080) | 可依其設定調整 | 僅 127.0.0.1 + 每套件隨機 token |
| 額外功能 | SQLite、動態 C# 執行、UI、外部 SDK | 中文教學、domain 流程、聊天介面 | 先保留核心；資料庫及 AI UI 待需求加入 |
| 適用 | 多工具與可擴充架構的參考 | 學習 Revit MCP、繁體中文流程參考 | 長期產品開發，以前者分層為主、後者作教學參考 |

具體參考檔案：

- `mcp-servers-for-revit/mcp-servers-for-revit/plugin/Core/SocketService.cs`
- `mcp-servers-for-revit/mcp-servers-for-revit/server/src/utils/SocketClient.ts`
- `mcp-servers-for-revit/mcp-servers-for-revit/plugin/RevitMCPPlugin.csproj`
- `REVIT_MCP_study/REVIT_MCP_study/MCP/Core/ExternalEventManager.cs`
- `REVIT_MCP_study/REVIT_MCP_study/MCP/Core/CommandExecutor.cs`
- `REVIT_MCP_study/REVIT_MCP_study/MCP-Server/src/socket.ts`

建議採用第一個的分層概念，並非直接採用它的全部套件與部署方式。動態任意程式執行與資料庫目前對架構建立沒有必要，會增加部署及授權面。
第一個專案有 MIT LICENSE；第二個路徑未找到獨立 LICENSE 檔案，package.json 的 license 宣告不足以確認所有檔案授權。未來若直接移植第三方程式，仍需確認授權、保留其要求的聲明。
