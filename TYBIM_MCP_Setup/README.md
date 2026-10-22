# TYBIM MCP MSI（0.5.5）

## 0.5.5 確認按鈕與窄面板版面修正（2026-10-22）

- 設定、對話與完整確認參數共用上方可捲動區，避免多個自動高度區塊把下方按鈕推出面板。
- 「執行此計畫／取消」移至下方操作區，顯示確認時優先保留操作空間。確認結束時，同時隱藏內容與操作按鈕。
- 下方區域在面板過矮時可另外捲動，讓送出／停止仍能到達；取消固定 300 最小寬度，標籤可換行。
- .NET Framework 4.8 與 .NET 8 各 46 項 WPF 檢查通過，涵蓋 300 個元素 ID、300x820、300x600、260x400、300x240，以及 150%／200% 等比例縮放。確認按鈕與送出／停止按鈕皆檢查完整落在對應可見區域。
- 此次未變更工具參數、確認規則、登入與金鑰儲存方式。請關閉 Revit 後安裝對應 0.5.5 MSI，再於實際宿主驗證。

## 0.5.4 登入按鈕重複累加修正（2026-10-22）

- 「開啟 Codex 登入頁」改為連線設定下方的固定單一按鈕，不再附加到對話紀錄。重複登入網址事件只更新同一個按鈕，相同網址不會自動重複開瀏覽器。
- 登入待完成時，再按「登入 Codex」重開現有登入頁，不重建登入請求；登入啟動中的重複點擊會被忽略。兩個登入按鈕會處理 Click 路由事件，避免傳到 Revit 外層。
- 登入成功或失敗、登出、切換引擎、重新設定連線與背景引擎斷線時，清除過期的登入頁按鈕及網址；若需重新啟動登入，請重新連線後再登入。
- WPF 回歸檢查模擬 20 次重複網址通知、40 次登入／重新開啟點擊、新網址替換、完成登入、切換引擎及斷線，瀏覽器使用測試替身，不會實際登入帳號。

## 0.5.3 啟動相依 DLL 修正（2026-10-22）

- 新增 `src/Tybim.Revit.Addin/Bootstrap.cs`，保留 `.addin` 入口 `Tybim.Revit.Addin.Application`；原啟動實作在 `Application.cs` 的 `RevitApplication`。
- `DependencyResolver.cs` 在載入 Contracts、JSON、Study 等程式碼前註冊同目錄相依解析。2024 使用 .NET Framework AssemblyResolve，2025/2026 同時支援獨立的 .NET 8 AssemblyLoadContext。拒絕其他外掛來源、Revit API、不同身分版本與跨年份目錄。
- 舊套件包含正確版本 Contracts；隔離 LoadFile 測試在未啟用解析時重現 FileNotFoundException，啟用正式解析器後通過。使用者截圖電腦的安裝內容尚未取得，仍須在該電腦重新安裝並啟動確認。
- `Verify-Msi.ps1` 解出內嵌 CAB，核對所有 payload 檔案 SHA-256，並針對 MSI 中的 DLL 執行正反載入測試；新增 Contracts 為明確必備項目。
- 測試不會啟動 Revit 或修改模型；實際 Ribbon、聊天面板及功能操作仍須在各版 Revit 驗證。

本次正式製作範圍是 **Revit 2024、2025、2026**，各自使用對應 Revit API 編譯。2027 不在本次發布範圍，不能把其他年份 DLL 複製過去當作支援。

## 安裝與路徑

安裝檔在 Release：

- TYBIM_MCP_Revit2024_0.5.5.msi
- TYBIM_MCP_Revit2025_0.5.5.msi
- TYBIM_MCP_Revit2026_0.5.5.msi

三版是獨立的 x64、目前使用者安裝包，具有不同 ProductCode／UpgradeCode，可以並存。同一安裝範圍的升版只取代相同 Revit 年份的 MSI。請先儲存模型並關閉 Revit，再以將實際使用 Revit 的 Windows 帳號執行對應 MSI；不是部署給另一個帳號的全機安裝。

## Windows 已安裝的應用程式／解除安裝

安裝後搜尋 `TYBIM_MCP`，顯示名稱為 `TYBIM_MCP for Revit 2024`、`TYBIM_MCP for Revit 2025`、`TYBIM_MCP for Revit 2026`，發行者為「拓源數位」，版本為 0.5.5。可在 Windows「設定 → 應用程式 → 已安裝的應用程式」或控制台「程式和功能」選擇解除安裝。

使用 Windows Installer 標準 RegisterProduct／PublishProduct 註冊，不手動建立另一筆重複的解除安裝資料。建置後確認未設定 ARPSYSTEMCOMPONENT／ARPNOREMOVE，設定 ARPNOMODIFY=1 讓系統提供直接移除入口。套件是目前使用者安裝，請用實際使用 Revit 的帳號一般執行，不要切換成另一個管理員帳號。

本機舊 0.5.0 版已有 Windows Installer 註冊，名稱是 `TYBIM MCP Revit 2025`（沒有底線），因此請用 `TYBIM` 搜尋。若舊版原本以全機方式安裝，升級前請先關閉 Revit、移除舊版，再安裝新版，避免跨安裝範圍留下兩筆。也可重新開啟原 MSI 的維護畫面選擇移除。單純執行開發用 Install-RevitAddin.ps1 只複製外掛，不會另外註冊成 Windows 應用程式。

解除安裝不會清除 `%LOCALAPPDATA%\TYBIM\McpChat\年份` 的個人設定與登入資料，也不會刪除其他 Revit 年份或 TYBIM 外掛。若要清除金鑰或登出，請先在 AI 助手操作「登出／清除金鑰」。

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
- 2024 的主外掛使用 .NET Framework 4.8／Revit 2024 API；2025、2026 使用 .NET 8／各自年份的 Revit API。通用相依庫可能相同，Revit 外掛 DLL 仍分版建置。

本包仍是 INTERNAL_PRIVATE 內部使用產物，包含你的自有功能編譯結果；不是可公開發布全部來源的上游專案。

## 在 Visual Studio 與命令列製作

1. 安裝 Visual Studio 2022 與 **Microsoft Visual Studio Installer Projects** 擴充套件。
2. 從專案根目錄執行：

```powershell
./TYBIM_MCP_Setup/Build-Msi.ps1 -RevitVersion 2024,2025,2026
```

腳本會編譯各版本、產生完整 runtime 套件、準備 payload、產生 .vdproj／隔離的 setup solution，再呼叫 devenv.com 建立 MSI。Microsoft 的 DisableOutOfProcBuild 工具會在 VS 目錄執行，設定目前使用者的 `EnableOutOfProcBuild=0`，避免命令列建置的 8000000A 錯誤。

若 DLL 與 artifacts/packages 套件已是最新，可用 `-SkipBuild`；仍會重新準備 payload、建置及核對 MSI。

- 主方案中的 `TYBIM_MCP_Setup.vdproj` 預設是 **2025**，File System 中可查看全部明確檔案連結。Visual Studio 若顯示檔案已變更，請重新載入。
- 2024／2025／2026 分別使用 `generated/年份/TYBIM_MCP_年份.sln`。這些隔離方案只有 Setup Project，避免 MSI 建置時將各年份 DLL 重新編成同一個版本。
- File SourcePath 連至 `payloads/<年份>`。Prepare-Payload.ps1 從完整套件複製相依檔，不使用容易取錯版本的「Primary output」自動推斷。
- 原 Setup 範本保存在 Setup.Template.vdproj。UI 共用設定改範本；目錄、檔案連結與版本識別改 Generate-Setup.mjs。生成的 .vdproj 下次執行會重建，不直接編輯生成內容作為永久設定。
- 修改程式碼後先重建／打包，再產生 payload；不要只重建舊的 .vdproj，就以為已包含最新 DLL。
- 建議一律使用 Build-Msi.ps1 完成發布。若直接在 Visual Studio 重建 Setup Project，之後仍必須在獨立 PowerShell 程序執行 `Set-MsiRegistration.ps1 -RevitVersion 年份`，再執行 Verify-Msi.ps1。此後處理設定解除安裝屬性，並補齊 VSI 在新 2026 專案中遺漏的 TYBIMREGISTERDIR 目錄記錄；只看 MSI 建置成功或檔案表不足以判定可安裝。
- MSI 未簽署；如需組織憑證簽署，應在完成建置與驗證後另外處理。

## 已執行驗證與實機驗收

Verify-Msi.ps1 用 Windows Installer API **唯讀**開啟 MSI，核對檔案表／目錄表、兩個根目錄、依賴檔、版本、獨立升級識別、內嵌 CAB、Windows 註冊動作及移除屬性；拒絕 RevitAPI*.dll、共用 bridge.config.json 或 auth.json。從 payload 內建 Node 啟動真正 MCP Client／Server，三版均須列出 258 個工具，不呼叫 AI 推論或修改模型。

```powershell
./TYBIM_MCP_Setup/Verify-Msi.ps1 -RevitVersion 2024,2025,2026
```

另執行 Test-MsiUninstall.ps1 -RevitVersion 2026：它複製 MSI，換成全新的 ProductCode／UpgradeCode／PackageCode／Component GUID，移除升級關聯並把兩個安裝目錄指向 artifacts/qa，實際安裝及解除安裝。已確認 Windows Installer 顯示名稱、版本、可用解除安裝命令及移除後檔案與註冊記錄消失。正式套件與目前 Revit 外掛未被安裝或移除。

尚未執行三版 Revit 實機建模或真實 Claude 金鑰推論驗收。請安裝所用版本並重開 Revit，確認按鈕、AI 登入與查詢；「相機管理」按鈕依 IsShown 切換 Hide／Show，再次點擊即可收合。沿用先前的啟動隱藏邏輯。
