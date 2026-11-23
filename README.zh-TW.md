# GIGABYTE Control Center 無法顯示／打不開：視窗、工作列圖示與縮圖修復

[English](README.md) · [下載](https://github.com/kinostudio/gigabyte-control-center-window-fix/releases/tag/v0.1.0-beta.5) · [故障原因與查修](docs/diagnosis.md)

針對 **技嘉 Control Center 背景執行卻沒視窗、開啟不顯示、工作列圖示／縮圖消失或圖示錯誤**整理的獨立修復工具。搜尋名稱亦可能為 GIGABYTE ControlCenter、AERO／AORUS Control Center won't open / not showing。

**2026-11-23 原址更新：** 保留原倉庫、`v0.1.0-beta.5` 發布網址與 ZIP 檔名，內容已換成目前的 **ShowAP 優先啟動版**，SHA-256 也已更新。已下載舊檔者請重新下載；ZIP 內 `RELEASE-REVISION.txt` 記錄來源提交與建置時間。開發期間稱為 beta.6 的實作，現在沿用既有 beta.5 下載路徑發送。

實測機型為 **AERO 15 KB**，原廠安裝包 **24.12.25.01**，內部主程式檔案版本 **24.12.24.01**。本工具不是 GIGABYTE 官方修補程式，也不是所有新版 GCC／GiMATE 的通用修復。

## 下載與使用

| 發布檔案 | 用途 |
| --- | --- |
| `*-with-official-installer.zip` | 完整包：修復工具＋未修改且有原廠簽章的 `ControlCenter_Ver_24.12.25.01.exe` |
| `*-repair-only.zip` | 已安裝相符原廠版本者使用，包含完整修復元件 |
| `SHA256SUMS.txt` | 本次 ZIP 的新雜湊，取代之前的核對值 |

1. 下載、核對雜湊，**完整解壓縮**到固定且可寫入的資料夾。不要在 ZIP 中執行，不要只複製單一 EXE。
2. 尚未安裝者先確認[官方機型下載表](docs/supported-models.md)，再手動執行該機型適用的原廠安裝器，閱讀原廠條款及重新開機要求；工具不會自動安裝。
3. 執行 `ControlCenterRepair.exe` 或其桌面捷徑。第一個啟動步驟直接呼叫已核對雜湊的原廠 **ShowAP.exe**，不等待它結束，也不把 WMI、圖示掃描或韌體查詢放在前面。
4. 後面才做不改動視窗的唯讀背景檢查。正常就安靜結束，**不要求工具修復 UAC、不強制重啟、不重設 FirstTime**。
5. 非正常結果至少背景觀察八秒，且須重複出現才請求需要授權的診斷／修復。必要時手動同意正常 UAC。能保留目前使用者 GUI 時，只修工作列／圖示；啟動異常才重啟指定原廠程序。

建立捷徑可使用 Windows「傳送到 → 桌面」，或執行 `ControlCenterRepair.exe --settings` 後按「建立桌面捷徑」。`Create-Shortcut.ps1` 僅供腳本政策允許時選用。請保留完整資料夾；原廠舊捷徑、服務及開機啟動不會被攔截。前一輪仍在觀察或修復時，重複點擊會略過。

完整包中的原廠安裝器未修改，保留其數位簽章，[來源驗證](docs/installer-verification.md)。原廠軟體不套用本修復工具 MIT 授權，權利與使用條款仍屬原廠及各供應商。

## 本次版本查到的故障原因

- **FirstTime 存在性判斷：** 檢查的啟動分支只判斷登錄值存在，即使資料是 0 仍可能隱藏視窗及停用 ShowInTaskbar；設成 0 不是刪除旗標的等價作法。
- **GUI 啟動身分：** 曾觀察到 ShowAP／SMV4_Service 的特定路徑產生 SYSTEM GUI，先前視窗修改因此遭遇存取被拒。這不代表所有前景問題都是權限造成。若已有正常的目前帳戶 GUI，不因另一個不同擁有者 GUI 存在就直接判為失敗。
- **工作列與圖示：** 只改用目前使用者啟動仍未恢復工作列，因此修復 APPWINDOW／TOOLWINDOW 樣式、重建按鈕，並使用安裝資料夾的 icon.ico；輔助程序保留圖示資源直到原廠 GUI 結束。

[中英文診斷文件](docs/diagnosis.md) 說明如何唯讀查找、對照證據、修復、查看結果與回復。倉庫不附個人日誌或原廠完整反編譯內容，也不宣稱原廠公告所有機型都有相同通病。

## 相容性與變更範圍

[官方下載列有此版的機型](docs/supported-models.md) 包含 AERO 15／15S 第 10 代與部分第 9 代 AERO／AORUS。**官方提供安裝器，不等於修復工具已逐機驗證；目前僅 AERO 15 KB 有本次實測紀錄。** 本工具不宣稱此版是原廠最新版本。

固定安裝路徑為 `C:\Program Files\ControlCenter`；啟動前核對 ShowAP 雜湊，修復前核對 GUI 主程式雜湊。受保護修復需要舊版 SMV4_Service 與可讀取的 GB_WMIACPI_Get 充電查詢。未知版本不允許關閉 GUI 或重設旗標，請勿移除保護強迫相容。

工具不改原廠 EXE／DLL、不注入、不安裝驅動、不新增開機項目、不改腳本政策，也沒有 CPU／GPU／充電／電源計畫寫入指令。只有實際修復時才可能備份並重設 FirstTime、重啟指定 GUI，或調整當次程序的視窗／圖示。

受保護修復前後會核對充電政策、上限、電源計畫與服務；差異會回報，不默默寫回設定。**正常唯讀背景路徑不查韌體**，明確記錄 `invariantsChecked: false`，不虛報已核對硬體。原廠入口仍會執行它本身的正常行為。

這是每次啟動時的條件式修復，不是永久修改原廠本體。Windows 仍可能拒絕前景切換；沒有永久置頂、模擬輸入、執行緒輸入綁定或前景鎖定繞過。重開機、乾淨重裝及多機型驗收尚未完成，修復 EXE 本身未簽章。

## 驗證與耗時

三個 C# 程式通過建置與純邏輯自我檢查，涵蓋入口分流、啟動觀察、圖示比對與可見視窗優先；不能取代實機驗收。

2026-11-23 本機紀錄顯示：ShowAP 啟動要求約 **1338 ms** 返回，背景狀態依序為 5 → 2 → 0，約 **5415 ms** 後正常結束，沒有進入受保護修復。前者**不是點擊到畫面出現的時間**，後者也不代表畫面被阻擋等待。直接與 ShowAP 比較的體感速度及穩定前景效果尚不能宣稱已驗收。先前健康分支的 1.9 秒，不能代表另一輪走到約十秒重啟修復的使用情況。

先前修復實測已確認視窗、G 圖示／縮圖，以及 60% 上限、電源計畫與服務前後一致；版本別證據與限制詳見診斷文件。

`launch-result.json` 分開記錄原廠啟動要求與背景檢查時間；`repair-result.json` 記錄實際動作及有執行時的受保護前後核對。前景結果與工作列／圖示成功分開判讀。向 [Issues](https://github.com/kinostudio/gigabyte-control-center-window-fix/issues) 貼日誌前，先遮蔽帳戶及個人路徑。

## 建置

需要 Windows x64、.NET Framework 4.x。PowerShell 僅用於建置，編譯後的修復不執行 PowerShell 腳本，也不需要 Codex。

```powershell
.\build.ps1
.\build.ps1 -InstallerPath 'D:\Downloads\ControlCenter_Ver_24.12.25.01.exe'
```

輸出在 `dist/`；建置只編譯與自我檢查，不會執行原廠安裝器或修復桌面。完整包要求原廠安裝器雜湊相符且簽章有效。打包排除個人日誌、備份及設定，原址更新以修訂檔識別實際來源。

MIT 僅適用修復程式碼與原創文件；原廠安裝器、圖示及其他元件維持各權利人所有，未另行取得原廠再散布授權書或宣稱官方背書。詳見[第三方說明](THIRD-PARTY-NOTICES.md)。
