# 原始碼來源、私人功能與上游更新

本專案整體以**私人專案／內部使用**管理。兩份上游是共同開發的外來程式，不代表 TYBIM_MCP 全部程式都可公開。分類記錄在根目錄 OWNERSHIP.json；上游原有授權與告示繼續保留，分類不變更其授權。

0.7.0 的 Donald 個人 CAD 建模包含自動建柱、樑、板、牆及圖層分析，來源為其 Sinotech_MCP 私人擴充，放在 `private/revit/AutoBuild` 與 `server/src/private/register.ts`，不放入 Study 或上游目錄。授權聲明為 `private/LICENSE.md`，內部安裝附 `personal-features/LICENSE.md`。依本次選擇只要求書面授權，沒有執行時啟用／序號驗證。這些原始碼已忽略 Git，請另外私人備份；操作與其他授權方案見 [個人功能與授權](personal-features.md)。第三方 ACadSharp 解析函式庫另列在 `third_party/acadsharp`，原授權隨 MSI 提供。

## 找程式與修改位置

| 類別 | 位置 | 用法 |
|---|---|---|
| 第一份上游原樣快照 | third_party/mcp-servers-for-revit/original | 保持原樣，用來比對新版本，不直接改功能 |
| Study 原樣快照 | third_party/revit-mcp-study/original | 同上；不混入客戶資料、自訂模型或 AI 憑證 |
| 第一份上游 C# 移植修改 | integrations/mcp-servers-for-revit/revit | 這裡才是實際編譯的修改版 |
| Study C# 移植修改 | integrations/revit-mcp-study/revit | 同上；含相機管理生命週期適配，不啟動原 WebSocket 服務 |
| 兩份上游 Node 適配 | server/src/integrations/各來源名稱 | 工具參數、catalog、bridge 與結果適配 |
| TYBIM 自有框架／現有功能 | src、server/src/chat、server/src/bridge.ts、server/src/tools.ts | 原 ribbon、AI助手、通訊、放門等自有邏輯；不要當成上游公開程式 |
| 新增私人 Revit 功能 | private/revit | 已排除 Git；在 Commands 專案的 PrivateFeatures 下編輯 |
| 新增私人 MCP 工具 | server/src/private/register.ts | 已排除 Git；編譯後由可選 loader 載入 |

Visual Studio 的 Tybim.Revit.Upstream／Tybim.Revit.Study 只是各來源的編譯與適配入口；真正的外來修改版在它們連結的 AdaptedUpstream／AdaptedStudy 資料夾。TYBIM 自有業務功能請優先放 private，而不是塞進上游 CommandExecutor。

原始碼快照與修改版的 SHA256、來源 commit、對照路徑在各來源 UPSTREAM.json。Study 的本機版本沒有獨立 LICENSE 檔，已保留其 README 與 package.json 的 MIT 聲明並記錄於 NOTICE.md；未代替作者創作版權文字。

## 寫自己的功能

private/revit/PrivateRegistration.cs 提供一個 partial 掛接點，範例如下（示範唯讀回應，不建立模型）：

0.7.0 的現有掛接點已註冊 CAD 圖層與四種建模，共五個功能；新增功能時請加入同一個 `RegisterPrivateCommands` 方法，不要另建第二份同名 partial 實作。下面是註冊方式範例，不要整段覆蓋現有功能。

```csharp
namespace Tybim.Revit.Commands
{
    public sealed partial class CommandRegistry
    {
        partial void RegisterPrivateCommands()
        {
            UpstreamCommand.RegisterExtension("private.my_feature", true,
                (app, p) => new { title = app.ActiveUIDocument.Document.Title });
        }
    }
}
```

對應的 server/src/private/register.ts：

```typescript
import { z } from 'zod';
import type { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import type { RevitBridge } from '../bridge.js';
export function registerPrivateTools(server: McpServer, bridge: RevitBridge) {
  server.registerTool('revit_private_my_feature', {
    description: '我的自訂專案查詢',
    inputSchema: { documentId: z.string().min(1) },
    annotations: { readOnlyHint: true }
  }, async args => ({ content: [{ type: 'text', text:
    JSON.stringify(await bridge.call('upstream.private.my_feature', args)) }] }));
}
```

寫入工具將 RegisterExtension 的 readOnly 設為 false，MCP schema 另要求 operationId（32 碼小寫十六進位），annotations.readOnlyHint 亦設為 false。revit_private_ 名稱會走既有完整參數確認與共用交易／操作狀態流程；在 action 內仍須自行開啟 Transaction。先測試 Undo、失敗回復與 operationId 重送，勿把未驗證的動作標成唯讀。

`dotnet build ... -p:IncludePrivateFeatures=false` 可略過 private/revit 的 C# 掛接點；`TYBIM_INCLUDE_PRIVATE=false` 可停用 Node 私人工具載入。這些不是「一鍵公開版」開關：原 TYBIM 核心仍有你的自有程式碼。每次 Node build 會先清理舊私人 JS；打包與安裝亦會替換產生的 runtime/build，避免刪掉私人原碼後仍留下舊執行檔。

目前套件標記 INTERNAL_PRIVATE，供你內部使用。DLL 可被反編譯，不能把「沒有附原始碼」當成客製功能可以對外分享。Git ignore 只排除尚未追蹤的檔案，不會撤回已追蹤／已發布的內容；整個 TYBIM 專案的 Git 儲存庫應維持私人，私人功能與必要資料另做私人備份。

## 上游更新

重新下載上游後，先執行唯讀比較：

```powershell
./scripts/Compare-Upstream.ps1 -Source mcp-servers-for-revit -SourceRoot C:/Prj/Revit/mcp-servers-for-revit
./scripts/Compare-Upstream.ps1 -Source revit-mcp-study -SourceRoot C:/Prj/Revit/REVIT_MCP_study
```

報告在 artifacts/upstream-reports，列出新增／刪除／修改；不會改寫任何快照、移植版或私人程式。確認差異後才更新來源快照、對照 integrations 的既有修改、重新產生 catalog 與來源雜湊，再執行編譯及測試。不要整個資料夾覆蓋到 TYBIM src 或 private。

若日後需要取出外來原始快照，可先預覽：

```powershell
./scripts/Export-UpstreamSnapshots.ps1 -WhatIf
```

這支腳本只能輸出 manifest 中雜湊驗證成功的原始快照與上游告示，不含 TYBIM src、移植修改、private、DLL、模型或登入資料；只產生本機 artifacts 目錄，不上傳、不提交 Git、不建立 PR。自行對外傳送前仍須核對內容與原授權。
