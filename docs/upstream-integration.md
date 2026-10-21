# mcp-servers-for-revit 整合 v0.3.0

來源為使用者下載的 `C:\Prj\Revit\mcp-servers-for-revit`，commit `86cf7057785f2423fc0d0892c9a5d9c93deb1aa3`（2026-10-21 匯入）。MIT 授權、來源檔案雜湊與修改說明保留於 `third_party/mcp-servers-for-revit`；安裝套件包含 LICENSE 與來源紀錄。

## 實際整合範圍

已匯入 107 個 C# 原始檔（包含上游模型定義），接通 **23 個 Revit 指令、3 個本機 SQLite 工具**。保留原有 11 個 TYBIM 工具，再加入 1 個整合操作狀態工具，合計 **38 個 MCP 工具**。未新增第二套 addin、WebSocket 服務、ribbon 或 AI 登入。

以下名稱都加上 `revit_upstream_` 前綴，例如 `get_current_view_info` 在 AI 工具清單中為 `revit_upstream_get_current_view_info`。

| 名稱（不含前綴） | 功能 |
|---|---|
| get_current_view_info | 目前視圖資訊 |
| get_current_view_elements | 目前視圖的模型／註釋元素 |
| get_available_family_types | 族型查詢與篩選 |
| get_selected_elements | 選取元素資訊 |
| get_material_quantities | 材料數量統計 |
| ai_element_filter | 依類別、條件查詢元素 |
| analyze_model_statistics | 模型元素統計 |
| export_room_data | 房間屬性資料讀取（回傳資料，不自動寫出檔案） |
| create_point_based_element | 點式元件，例如門窗、家具、柱；依上游類別與族型支援 |
| create_line_based_element | 線式元件，例如牆、樑、管線；依上游類別支援 |
| create_surface_based_element | 面式元件，例如樓板、天花、屋頂；依上游類別支援 |
| create_grid | 軸網 |
| create_level | 樓層 |
| create_room | 房間 |
| create_dimensions | 尺寸標註 |
| create_structural_framing_system | 結構樑系統 |
| delete_element | 刪除指定元素 |
| operate_element | 選取、變色、隱藏、隔離等操作 |
| color_elements | 依參數值著色 |
| tag_all_walls | 牆標籤 |
| tag_all_rooms | 房間標籤 |
| send_code_to_revit | 進階 C# 程式執行；保留 auto／none 交易模式 |
| say_hello | 連線測試對話框；需要手動關閉 |
| store_project_data | 本機專案資料存取 |
| store_room_data | 本機房間資料存取 |
| query_stored_data | 查詢本機已儲存資料（不是即時 Revit 模型） |

`revit_upstream_operation_status` 另外提供整合寫入操作的狀態與結果。

原始碼中的 `modify_element.ts`、`search_modules.ts`、`use_module.ts` 為空檔案，沒有登錄成可用工具。只有 Models 但沒有命令與工具入口的樓梯、欄杆、視圖等類別亦不當成新增功能。

## 在 Visual Studio 中開發

開啟 TYBIM.MCP.sln，目前另有第五個 Tybim.Revit.Study 專案；以下為第一套整合的四個原有專案：

- Tybim.Revit.Addin：原 ribbon、AI助手及既有 ExternalEvent 佇列。
- Tybim.Revit.Commands：原 TYBIM 功能；UpstreamCommands.cs 負責整合命令、文件核對、交易群組、錯誤回復與 operationId 去重。
- Tybim.Revit.Upstream：編譯 integrations/mcp-servers-for-revit/revit 中的適配 C# 原始碼；third_party 保留未改動快照；UpstreamCatalog.cs 列出 23 個指令。ExternalEventCompatibility.cs 在既有 Revit API 執行緒同步呼叫 handler，避免再 Raise＋Wait 造成死鎖。
- Tybim.Mcp.Contracts：原本的通訊協定。

Node 工具在 `server/src/integrations/mcp-servers-for-revit/tools`，原參數 schema 保留。`register.ts` 統一新增前綴、documentId、寫入 operationId、錯誤判讀；`utils/ConnectionManager.ts` 接現有 TYBIM token bridge，不使用原專案的 8080 port。重新下載外部參考專案不會自動覆蓋這裡，後續更新需比對來源紀錄、套用適配並重新驗證。

執行時不依賴外部 `mcp-servers-for-revit` 目錄，不需要另裝原專案 addin 或 npm server。

## 使用與行為差異

1. 儲存模型、關閉 Revit，安裝本專案產生的完整 v0.3.0 套件。
2. 重開 Revit，使用「拓源數位 → MCP → AI助手」，重新連線並開始新對話；原登入資料保留。
   若使用外部 Codex 或其他 MCP 用戶端，亦須重新啟動該 MCP 連線，才能重新取得新增工具清單。
3. 可以說「讀取目前視圖的牆與門族型」、「統計材料數量」、「列出房間資料」或「建立指定名稱與高程的樓層」。AI 會先取得目前 documentId。
4. 原 TYBIM 放門仍走既有試放預覽／確認；新增上游寫入工具顯示**完整參數確認**，並非幾何試放。確認後才執行。一般模型變更以外層交易群組整合為一筆 Undo；失敗或 Revit failure 時回復該群組並回傳原生結果與涉及元素。
5. 寫入呼叫帶唯一 32 碼 operationId。逾時先查同 documentId／operationId 的狀態；同 ID 重送同參數回原結果，換參數會拒絕。記錄只在本次 Revit 執行階段（資料庫寫入在本次 Node 程序）有效，重啟後查不到不代表先前沒有執行。外部 MCP 用戶端的確認由該用戶端負責。

進階 C# 工具只在使用者明確要求時使用，確認卡片會顯示完整 code 與參數。Revit 交易無法回復程式造成的檔案、網路或其他外部副作用；選取等 UI 狀態亦不等同模型 Undo。不要把交易群組視為程式沙箱。

建立幾何所用的尺寸、座標與參數意義沿用各工具 schema（多數為毫米）；材料量及其他結果的單位請依結果欄位判讀。部分上游指令保留 Int32 元素 ID，尚未全面改為 TYBIM 的字串 UniqueId。

本機資料庫使用 Node 24 內建 SQLite，無須另外安裝資料庫或 better-sqlite3。面板啟動的路徑為 `%LOCALAPPDATA%\TYBIM\McpChat\2025\upstream-data.db`；外部 MCP 程序未提供 TYBIM_CHAT_STATE 時預設於 `%LOCALAPPDATA%\TYBIM\McpChat\upstream-data.db`。可用 TYBIM_UPSTREAM_DB 明確指定；僅第一次存取資料時建立，不放在 DLL 安裝目錄。

## 驗證範圍

兩版 C# Release 編譯、Node MCP 真正協定／模擬 bridge 測試、實際 SQLite 存取，以及 WPF 確認卡片已驗證。新增 Revit 幾何功能尚未逐項在 Revit 實機驗證，不能由編譯或 mock 測試推論全部建模操作皆成功。

安裝後先在測試模型驗收：讀取視圖／族型／材料／房間 → 建一個樓層並檢查 Undo → 測試指定族型及宿主的單一元件 → 測試錯誤、回復與 operationId 重送 → 其餘工具逐一測試。原上游演算法的模型與族限制仍會影響結果。

## v0.4.0 後續整合

此文件的 38 個工具是第一套整合範圍；加入 Study 後預設總數為 258。來源與私人程式分類請見 source-ownership.md；Study 功能、交易差異與安裝請見 study-integration.md。

