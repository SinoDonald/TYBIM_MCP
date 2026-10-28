# 個人功能、自動建柱／樑／板／牆與授權選擇

更新日期：2026-10-28；對應 0.7.1。

0.7.1 確認四種建模可獨立執行。建牆預設只建牆；只要求門用 `placeDoors=true`，只要求窗用 `placeWindows=true`，另一種保持 false。`placeOpenings` 為舊版「兩者一起」相容開關，未明確要求門窗兩者時保持 false。CAD 有開口圖層只代表可接續牆線，不能視為授權放門窗。參數確認後才執行，辨識不到或找不到族型時如實回報。

## 0.7.0 四種個人建模功能

依 Donald 自行撰寫的 Sinotech_MCP AutoBuild 更新，沒有搬入 Sinotech 的 Socket 服務、SDK 外部事件或埠號。四種寫入沿用 TYBIM 的文件識別、完整參數確認、交易群組、Undo 與 operationId 狀態查詢。內部工具共 263 項：原有 258 項，加上以下 5 項個人工具。

| 工具 | 用途 |
|---|---|
| `revit_private_get_cad_layers` | 讀取 CAD 圖層、雙線比例、建議牆圖層、柱形狀數、樑圖層名稱特徵、樓板類型、預設上下樓層 |
| `revit_private_create_columns_from_cad` | 只建柱：依矩形／圓形輪廓建立結構柱，指定基準／頂部樓層 |
| `revit_private_create_beams_from_cad` | 只建樑：依平行雙線求中心線，讀取 DWG 尺寸標註；可選一併建下層柱 |
| `revit_private_create_floors_from_cad` | 只建板：依現有牆柱圍出的房間面層邊界建板，CAD 線形輔助辨識井道／樓梯 |
| `revit_private_create_walls_from_cad` | 自動建牆：依封閉雙線求中心線，可選一併建柱、板及門窗 |

功能在「拓源數位 → MCP → AI助手」以對話呼叫，不新增四個 Ribbon 按鈕。例如：

1. 「先讀取目前專案、CAD 圖層、牆與樓板類型及預設樓層，不要修改模型。」
2. 「用剛才的 CAD 柱圖層 S-COL，只在 FL1 到 FL2 建柱。」
3. 「用樑圖層 S-BEAM 在 FL2 自動建樑，這次不要再建柱；沒有尺寸標註時梁深用寬度乘 1.4。」
4. 「用牆圖層 A-WALL 在 FL1 到 FL2 建牆，先不要建板或門窗。」
5. 「牆柱建好後，依 FL1 房間邊界建立 XXX 樓板類型。」

每個寫入都先核對參數再按執行。組合建模的 `placeColumns`、`placeFloors`、`placeOpenings` 預設為 **false**，避免單一指令增加未要求的元件；明確要求時才啟用。單次成功操作可用 Revit Undo 復原；相同 operationId 不會重建，但使用新 operationId 仍可能建立重複元件，尤其單獨建板不會先刪掉既有板。

注意：

- 柱辨識尺寸為 20～150 cm 的矩形或圓；自動判定還需要足夠重複輪廓與合適圖層名稱，少量柱請明確指定圖層。
- 樑雙線間距支援 15～120 cm。RC 標註如 B23(50X70) 按 cm，鋼樑 H500X200 按 mm；沒有文字可用時會回報估算梁深。鋼樑缺相符斷面族型會略過。
- 結構柱／混凝土樑可以從 Revit 設定的族庫載入族群並複製所需尺寸；不存在合適族群時回報失敗，不會憑空建立可用族群。
- 板不是任意 CAD 封閉線直接轉板：必須先有牆柱等房間邊界。原有房間保留，為求邊界暫建的房間會回復；使用現代 Floor.Create 保留內孔。
- 讀取 CAD 採目前視圖裁剪範圍的外接矩形；板只處理完整位於該範圍內的房間。一個 DWG 含多層平面時，請先裁剪至目標平面。超過 10000 筆 CAD 線／弧／圓時需分批。
- 樑標註直接讀取本機連結 DWG，需要該路徑可讀；單檔上限 256 MiB。讀不到／對不準時回報，不代表已取得正確尺寸。

程式皆在 `private/revit/AutoBuild`，獨立工具入口為 `AutoWallCommands.cs`、`PrivateRegistration.cs`，MCP schema 在 `server/src/private/register.ts`。來源檔案雜湊保存在 Git 忽略的 `private/autobuild-provenance.json`。新增 DWG 解析函式庫 ACadSharp 的來源及授權另在 `third_party/acadsharp`，不將第三方函式庫冒認為私人原創。

仍採下述選項 3（私人著作權／書面授權），沒有序號或離線授權驗證。本次程式、幾何與套件檢查不代表已在實際 DWG／Revit 完成四種建模驗收。

## 自動建牆的分類與操作

這是 Donald 個人開發功能，由 Sinotech_MCP 的私人 AutoBuild 程式適配至 TYBIM_MCP，不屬於 Study 或 mcp-servers-for-revit 的外來共同開發程式。

- Revit 程式：`private/revit/AutoBuild`，支援程式在 `private/revit/Common`。
- 註冊入口：`private/revit/PrivateRegistration.cs`。
- MCP 工具：`server/src/private/register.ts`。
- 私人幾何／MCP 測試：`private/tests`。
- 私人聲明：`private/LICENSE.md`，安裝套件會附 `personal-features/LICENSE.md`；原始碼標頭與 Commands DLL 的 AssemblyMetadata 亦標明 Donald 的權利聲明。

上述私人來源目錄已排除 Git，請另做私人備份。內部 MSI 會包含編譯後的個人功能；「原始碼不進 Git」不代表編譯後的功能沒有進入安裝包。`OWNERSHIP.json` 記錄分類，既有兩份上游原始快照與授權保持不變。

使用步驟：

1. 在 Revit 開啟專案與含連結 DWG 的視圖，再開啟「拓源數位 → MCP → AI助手」。
2. 選擇 AI 引擎，完成登入／API Key 連線，從模型清單選取模型。
3. 輸入：「讀取目前專案與連結 CAD 的圖層、基本牆類型及樓層，先不要建牆。」AI 應呼叫 `revit_project_info` 與 `revit_private_get_cad_layers`。
4. 明確指定 CAD 連結、圖層、基本牆型、基準與頂部樓層。例如：「用剛才查到的 A_WALL 圖層、基本牆型 XXX，從 FL1 到 FL2 自動建牆，最大辨識牆厚 1000 mm。」若要逐層分段，另指定 `byLevel=true`。
5. 核對面板的完整操作參數，按「執行此計畫」後才呼叫 `revit_private_create_walls_from_cad`。這是參數確認，並非已試建的幾何預覽。
6. 完成後核對新增牆、失敗數及警告統計，可使用 Revit Undo；自行儲存模型。逾時或結果不明時，用原 `documentId` 與 `operationId` 查詢 `revit_upstream_operation_status`，不得換新 ID 盲目重做。

限制：目前沿用直線封閉雙線區域求中心線的做法；圓弧／雲形線不參與建牆，非水平線段會略過。開口圖層依 OPEN／DOOR／WINDOW／開口／門／窗名稱辨識。牆型須為基本牆，頂部樓層須高於基準樓層。程式會限制過大的 CAD 線量及單次建牆數，超過時應分批選圖層或連結。不要將成功的獨立幾何測試當成所有 DWG 均已完成 Revit 實機驗收。

## 這次採用的授權方式：選項 3

依 Donald 在本次對話的指示，先加上私人著作權及「使用前須取得書面授權」聲明，沒有加入執行時啟用程序。

- 使用者先向 Donald 取得約定範圍的書面授權，再安裝並使用個人功能。
- 程式不會要求序號、授權登入或匯入授權檔，既有使用流程不增加啟用步驟。
- 聲明不會阻止他人複製 DLL 執行；不是防拷或不可破解的保證。
- Codex 登入與 Claude／Gemini API Key 是 AI 供應商的帳號／用量權限，不等於 Donald 對 TYBIM 個人功能的授權。

## 選項 1：沿用既有產品授權系統

本輪檢查 TYBIM_MCP、Sinotech_MCP 及指定 TYBIM_2025 原始碼，沒有找到可直接沿用的產品啟用、序號驗證或離線簽章驗證入口。TYBIM_2025 的 `LicenseManager.UsageMode == Designtime` 只是 WinForms 設計階段辨識，不是使用者產品授權。若另有未包含於這些原始碼中的授權 DLL 或服務，可以另行接入。

若未來建立或提供既有系統，典型做法是：

1. Donald 在授權管理端建立使用者／序號，設定允許的功能、期限及裝置數。
2. 使用者安裝後，在 TYBIM 的啟用介面登入或輸入序號，首次向授權伺服器驗證。
3. Revit 原生個人功能在執行前檢查授權；不只隱藏按鈕，避免直接呼叫 MCP 繞過。
4. 期限、撤銷、換機與離線寬限依產品政策處理。需要先定義無網路時允許多久、如何換機與續期。

對使用者的影響：多一道產品啟用程序，通常首次或定期需要網路；可由你集中管理名額、續期與停用。這套產品授權與 AI 帳號各自獨立。

## 選項 2：離線授權檔

這次沒有建立此系統；未來可以採用由開發者簽發的離線檔案：

1. 使用者開啟 TYBIM 的授權介面，取得裝置識別碼並提供給 Donald。
2. Donald 用保管於開發者端的私鑰，簽發包含產品、功能、使用者／單位、期限與選用裝置綁定資料的授權檔。
3. 使用者匯入授權檔；Revit 原生層以內建公鑰驗證簽章、期限及裝置限制，再允許個人功能執行。
4. 私鑰不可放進 DLL、MSI 或 Git；使用者拿到的是授權內容與簽章，不能用修改文字的方式產生有效新授權。
5. 若設定到期日或裝置綁定，使用者續期、換機或裝置識別變更時，需要 Donald 重發授權檔。

對使用者的影響：首次要提供裝置碼並匯入檔案，之後可離線使用；沒有線上管理端時，撤銷已發出的離線授權不會即時生效。簽章驗證提升控制力，仍不能保證 DLL 無法被反編譯或修改。

## Settings／Switch 的對應行為

TYBIM_MCP 於啟動建立命令註冊，預設載入已編譯的原有、upstream、Study 與個人工具；不另要求使用者逐個啟用。原有 258 項加上 5 項個人工具，內部套件預設共 263 項。

AI助手顯示時啟用本機 MCP listener；按 Ribbon 按鈕收合或按面板 X 關閉時停止接受新連線、拒絕及清除尚未執行的佇列工作，正在進行的 AI 回合會提出取消。已接受的要求會保留連線至結果送出，再關閉；未執行的要求回傳明確停用原因，避免誤認交易結果不明。重新開啟沿用命令註冊與操作紀錄，不重建整套命令。已進入 Revit 的同步交易不會強制中斷，仍須等候結果。

外部 Codex／其他 MCP 用戶端也需要 AI助手面板開啟，才能連到這個橋接服務。模型與 codex.exe 均為不可編輯的下拉選單；執行檔選項由有效的已存設定、設定檔、PATH 及本機 Codex 安裝目錄偵測。

## 解除安裝清理範圍

MSI 只為該年度 `Addins/TYBIM_MCP/<年份>` 已安裝的目錄樹加入解除安裝清理，移除包含啟動後產生的 `bridge.config.json` 等檔案。年度 `Addins/<年份>` 中只由 MSI 移除自己的 `TYBIM.Mcp.addin`，不對該共用目錄做萬用字元刪除；其他年份與其他外掛保留。

清理使用 Windows Installer [RemoveFile](https://learn.microsoft.com/en-us/windows/win32/msi/removefile-table) 與純解除安裝條件；由舊版升級移除時不執行額外清理，避免晚執行的舊版移除刪到新檔案。自動清理針對已安裝的目錄；額外手動新增且不在 MSI 目錄清單內的子資料夾不會遞迴掃除，應自行搬離或刪除。

`%LOCALAPPDATA%/TYBIM` 的個人登入／API Key、相機資料、專案原始碼與模型不在這次 addin 清理範圍。需要清除金鑰時，先於 AI助手操作登出／清除金鑰。
