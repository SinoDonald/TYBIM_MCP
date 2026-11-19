# 專案容量與可重建輸出

保留 `src`、`private/revit`、`server/src`、測試原始碼、第三方來源與授權、Revit API 參考、目前安裝檔及用戶端設定工具。測試原始碼是安裝前必要的驗證，不因其名稱含 Test 就刪除。

可移除的容量主要來自部署副本、MSI 解壓與驗證輸出、安裝 staging、bin／obj、Node 依賴與產生的 JavaScript。這些都可從原始碼與鎖定檔重建。

`scripts/Clean-Workspace.ps1` 僅刪除白名單的可重建輸出，先確認絕對路徑在目前專案內、不是連結，且未受 Git 追蹤。任何已追蹤的原始碼都會拒絕刪除。不碰使用者 AppData 的 Revit 外掛、AI 設定或登入資料；不刪除 Release MSI、私人原始碼、node_modules、離線 Revit 參考或 VS 的斷點／個人設定。

```powershell
# 先查看將清理哪些位置
.\scripts\Clean-Workspace.ps1 -IncludeBuildOutputs -WhatIf
# 清除 QA、安裝 staging、產生的 setup 專案與 bin/obj
.\scripts\Clean-Workspace.ps1 -IncludeBuildOutputs
# 另有明確需要時，才加 -IncludePackages 清除可重建的部署套件
```

實際清理結果寫入 `artifacts/workspace-cleanup.json`。清掉 bin／obj 後，下次建置需正常還原 NuGet 依賴；Prepare-Payload 會重新產生 staging 與 setup 專案。既有 MSI 檔案可以繼續使用，與 staging 無關。

## 2026-11-19 深度精簡

本次另清除 artifacts/packages、早期 artifacts/2024／2025 部署、舊獨立設定 ZIP、測試截圖與結果、catalog 產生暫存、一次性審查輸出、server/build、server/node_modules，以及 VS 可重建的索引。保留 .suo 與視窗配置，維持中斷點及個人設定。Release 保留三版最新 MSI 與說明，移除多餘的 setup.exe。

artifacts/reference-packages 內重複 DLL 逐一以 SHA256 確認已有 artifacts/references 副本後，移除下載與解壓快取；授權、套件描述及 Readme 轉存 references/package-notices。保留 2026／2027 編譯參考，避免刪除未安裝 Revit 年份所需的 API。

剩餘 16 個 PowerShell 腳本合計約 84 KB，屬於正式建置／安裝／設定及必要測試，保留用途見 script-audit.md。私人功能、測試原始碼、第三方來源與授權、Git 歷史不移除。

深度清理後，Build-Msi.ps1 會在 Node 依賴不存在時先執行 npm ci；Build.ps1 本來就會還原依賴。第一次重新建置可能需要網路下載依賴。原始碼直接開發時，先在 server 執行 npm ci，再執行 npm run build。部署副本已清除，重新製作 MSI 請使用一般 Build-Msi 流程，不加 -SkipBuild。已安裝的 Revit 外掛、現有 AI 連線與 MSI 使用不受原始碼快取清理影響。

往後 `Verify-Msi.ps1` 與 `Test-PayloadAssembly.ps1` 驗證成功即清除本次暫存；失敗保留問題資料。排錯時可明確加 `-KeepDiagnostics` 保留本次成功驗證的輸出。清理不會刪除測試程式，因此後續仍可完整重跑檢查。
