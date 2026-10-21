# TYBIM MCP MSI（0.5.0）

本次正式製作範圍是 **Revit 2024、2025**。2026／2027 僅保留後續開發設定與參考檔，沒有發布 MSI，不能把 2025 DLL 複製過去當作支援。

## 安裝與路徑

安裝檔在 Release：

- TYBIM_MCP_Revit2024_0.5.0.msi
- TYBIM_MCP_Revit2025_0.5.0.msi

兩版是獨立的 x64、目前使用者安裝包，具有不同 ProductCode／UpgradeCode，可以並存。升版只取代相同 Revit 年份的 MSI。請先儲存模型並關閉 Revit，再以將實際使用 Revit 的 Windows 帳號執行對應 MSI；不是部署給另一個帳號的全機安裝。

Application Folder 的 DefaultLocation 已改成：

```text
[AppDataFolder]Autodesk\Revit\Addins\TYBIM_MCP
```

各年度部署結果（以 2025 為例）：

```text
%APPDATA%\Autodesk\Revit\Addins\
  2025\
    TYBIM.Mcp.addin
  TYBIM_MCP\
    2025\
      Tybim.Revit.Addin.dll
      Tybim.Revit.Commands.dll
      Tybim.Revit.Study.dll
      Tybim.Revit.Upstream.dll
      Tybim.Mcp.Contracts.dll
      Newtonsoft.Json.dll、ClosedXML.dll、其他必要相依 DLL
      chat.config.json
      runtime\build\...
      runtime\node_modules\...
      runtime\node\node.exe
      third_party\...
```

`.addin` 必須留在 Revit 掃描的 `Addins\2025`；不能只放在 `Addins\TYBIM_MCP\2025`。註冊檔內容如下，2024 包只替換年份：

```xml
<RevitAddIns>
  <AddIn Type="Application">
    <Name>TYBIM MCP</Name>
    <Assembly>..\TYBIM_MCP\2025\Tybim.Revit.Addin.dll</Assembly>
    <AddInId>A405238F-64C6-4FE2-A3C8-B698E159A6C1</AddInId>
    <FullClassName>Tybim.Revit.Addin.Application</FullClassName>
    <VendorId>TYBM</VendorId>
    <VendorDescription>TYBIM Revit AI Chat</VendorDescription>
  </AddIn>
</RevitAddIns>
```

採用相對路徑，不含 Donald 或開發機的絕對路徑。另一個自訂安裝資料夾 `TYBIMREGISTERDIR` 固定為 `[AppDataFolder]Autodesk\Revit\Addins\2025`，專門放置註冊檔。為維持兩個目錄的相對位置，安裝精靈不提供更改目的資料夾的畫面；若要改整體部署配置，修改產生器並重新驗證。

## Runtime、登入與舊版升級

MSI 內含 Node.js 24.19.0 與其授權告示，不依賴使用者另裝 Node 或你的 C:\Program Files\nodejs 路徑。chat.config.json 的 nodeExecutable 使用相對路徑 `runtime/node/node.exe`。

- Codex 模式仍需該使用者電腦已安裝可用的 Codex CLI／原生執行檔；介面會偵測 PATH 及 Codex 的使用者安裝位置，也可指定路徑，再按「登入 Codex」。不把你的 Codex 可執行檔位置或登入狀態複製給其他人。
- Claude／Gemini 在面板輸入各使用者自己的 API Key。
- AI 偏好、金鑰與登入狀態仍在 `%LOCALAPPDATA%\TYBIM\McpChat\<年份>`，升級 MSI 不會移除這些資料。
- bridge.config.json **不放入 MSI**。Revit 首次啟動會建立該使用者的隨機權杖；若找到原 `Addins\<年份>\Tybim.Mcp\bridge.config.json`，會沿用既有連線設定。
- 舊手動部署目錄可能仍存在；只有 `Addins\<年份>\TYBIM.Mcp.addin` 指向的新目錄會載入。確認新版本正常後再自行處理舊目錄，不會在安裝時刪除未由 MSI 管理的檔案。
- 外部 MCP 用戶端改指向新目錄的 `runtime/build/index.js` 與 `bridge.config.json`。內嵌 AI助手會使用新路徑，不必手改。
- 2024 的主外掛使用 .NET Framework 4.8／Revit 2024 API；2025 使用 .NET 8／Revit 2025 API。通用相依庫可能相同，Revit 外掛 DLL 仍分版建置。

本包仍是 INTERNAL_PRIVATE 內部使用產物，包含你的自有功能編譯結果；不是可公開發布全部來源的上游專案。

## 在 Visual Studio 與命令列製作

1. 安裝 Visual Studio 2022 與 **Microsoft Visual Studio Installer Projects** 擴充套件。
2. 從專案根目錄執行：

```powershell
./TYBIM_MCP_Setup/Build-Msi.ps1 -RevitVersion 2024,2025
```

腳本會編譯各版本、產生完整 runtime 套件、準備 payload、產生 .vdproj／隔離的 setup solution，再呼叫 devenv.com 建立 MSI。Microsoft 的 DisableOutOfProcBuild 工具會在 VS 目錄執行，設定目前使用者的 `EnableOutOfProcBuild=0`，避免命令列建置的 8000000A 錯誤。

若 DLL 與 artifacts/packages 套件已是最新，可用 `-SkipBuild`；仍會重新準備 payload、建置及核對 MSI。

- 主方案中的 `TYBIM_MCP_Setup.vdproj` 預設是 **2025**，File System 中可查看全部明確檔案連結。Visual Studio 若顯示檔案已變更，請重新載入。
- 2024 使用 `generated/2024/TYBIM_MCP_2024.sln`；2025 使用 `generated/2025/TYBIM_MCP_2025.sln`。這些隔離方案只有 Setup Project，避免 MSI 建置時將各年份 DLL 重新編成同一個版本。
- File SourcePath 連至 `payloads/<年份>`。Prepare-Payload.ps1 從完整套件複製相依檔，不使用容易取錯版本的「Primary output」自動推斷。
- 原 Setup 範本保存在 Setup.Template.vdproj。UI 共用設定改範本；目錄、檔案連結與版本識別改 Generate-Setup.mjs。生成的 .vdproj 下次執行會重建，不直接編輯生成內容作為永久設定。
- 修改程式碼後先重建／打包，再產生 payload；不要只重建舊的 .vdproj，就以為已包含最新 DLL。
- MSI 未簽署；如需組織憑證簽署，應在完成建置與驗證後另外處理。

## 已執行驗證與實機驗收

Verify-Msi.ps1 用 Windows Installer API **唯讀**開啟 MSI，核對檔案表／目錄表、兩個根目錄、依賴檔、版本、獨立升級識別及內嵌 CAB；拒絕 RevitAPI*.dll、共用 bridge.config.json 或 auth.json。從 payload 內建 Node 啟動真正 MCP Client／Server，兩版均列出 258 個工具，不呼叫 AI 推論或修改模型。

```powershell
./TYBIM_MCP_Setup/Verify-Msi.ps1 -RevitVersion 2024,2025
```

這些驗證沒有在你的目前使用者帳號執行 MSI 安裝／解除安裝，也沒有取代實際 Revit 驗收。建議先安裝所用版本並重開 Revit，確認按鈕、AI 登入與查詢；「相機管理」按鈕現在依 IsShown 切換 Hide／Show，再次點擊即可收合。沿用先前的啟動隱藏邏輯。
