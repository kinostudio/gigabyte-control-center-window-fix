# GIGABYTE Control Center 無法顯示／打不開：視窗、工作列圖示與縮圖修復

[English](README.md) · [下載修復包](https://github.com/kinostudio/gigabyte-control-center-window-fix/releases) · [診斷與故障原因](docs/diagnosis.md)

針對社群常見的 **技嘉 Control Center 背景執行卻沒有視窗、開啟不顯示、工作列沒有圖示／縮圖、圖示錯誤**等症狀，整理本次調查與獨立修復工具。搜尋名稱亦可能寫成 GIGABYTE ControlCenter、AERO Control Center、AORUS Control Center won't open / not showing。

**先前修復實測機型：AERO 15 KB；原廠安裝包版本 24.12.25.01，內部 ControlCenter.exe 檔案版本 24.12.24.01。** 本工具不是 GIGABYTE 官方修補程式，也不適用於所有現代 GCC／GiMATE。一台電腦成功，不代表所有玩家遇到的症狀都有相同原因。

## 下載與使用

| 發布檔案 | 用途 |
| --- | --- |
| `*-with-official-installer.zip` | 完整包：修復工具＋未修改的原廠簽章安裝器 `ControlCenter_Ver_24.12.25.01.exe` |
| `*-repair-only.zip` | 已安裝正確版本者使用的精簡修復包 |
| `SHA256SUMS.txt` | ZIP 的 SHA-256 核對值 |

1. 從 [Releases](https://github.com/kinostudio/gigabyte-control-center-window-fix/releases) 下載，核對 SHA-256，**完整解壓縮**到可寫入的資料夾；不要在 ZIP 內執行，也不要只複製單一 EXE。
2. 尚未安裝者，先確認[官方機型下載表](docs/supported-models.md)，再手動執行完整包內的原廠安裝器，閱讀原廠條款並依需要重新開機。安裝與修復是分開的步驟，修復工具不會自動執行安裝器或重裝。
3. 執行 `ControlCenterRepair.exe` 或桌面 **Control Center - Auto Repair** 捷徑。預設在背景檢查，不顯示例行修復視窗；仍須以目前登入的管理員帳戶手動同意正常 UAC。
4. 正常的既有視窗直接還原／啟用，不重開、不重設 FirstTime。只有工作列或圖示異常則保留程序修復視窗。未執行且沒有 FirstTime 時直接啟動後檢查；隱藏、啟動身分或旗標造成異常時才進入備份、重設與重啟流程。讀取失敗不能當成正常，也不盲目修復。
5. 核對視窗與工作列；`repair-result.json` 會記錄實際 `action`、檢查結果與前後一致性。正常完成不跳修復視窗，失敗才顯示簡短錯誤。

完整包保留原廠 EXE 本身，不修改安裝器或簽章。[來源與驗證紀錄](docs/installer-verification.md)。原廠安裝器權利歸原廠，不套用本修復工具的 MIT 授權。

## 背景檢查與新捷徑

直接執行 EXE 或使用 `--launch` 都走背景模式。可對解壓縮後的 EXE 使用 Windows「傳送到 → 桌面（建立捷徑）」；也可執行 `ControlCenterRepair.exe --settings`，在管理介面按「建立桌面捷徑」。可選的 `Create-Shortcut.ps1` 只在腳本政策允許時使用。

每次先確認狀態，正常就略過修復；不殺掉正常 GUI、不覆蓋 FirstTime 備份，也不閃現修復主視窗。異常才依需要處理，失敗才顯示提示。受保護狀態的讀取與修復仍需手動 UAC，不會變更或略過安全政策；前一輪仍執行時重複點擊會略過。

請保留完整解壓縮資料夾。只有新 EXE／捷徑會走這個流程，原廠舊捷徑、服務與開機啟動不會被攔截。這是條件式套用修復，不是永久改寫原廠本體；Windows 仍可能拒絕前景切換。

## 故障原因摘要

本次檢查的版本發現以下行為，並非宣稱原廠已公告所有機型均有這些問題：

- **FirstTime 判斷存在性：** 初始化分支只看登錄值是否存在，即使數值是 `0`，仍可能進入隱藏視窗及停用 `ShowInTaskbar` 的路徑。因此設為 0 不是刪除旗標的等價作法。
- **啟動身分：** 實際觀察到 ShowAP／SMV4_Service 的特定啟動路徑讓 GUI 由 SYSTEM 擁有，先前修改視窗時因此遭遇「存取被拒」。這只是已觀察到的因素，不能把所有前景問題都歸因於權限。
- **工作列與圖示：** 改用目前使用者啟動後，視窗雖恢復，工作列仍缺少按鈕；因此移除 `WS_EX_TOOLWINDOW`、加入 `WS_EX_APPWINDOW`，經短暫隱藏／樣式變更／顯示重建工作列，再從既有 `icon.ico` 設定圖示。圖示資源由輔助程序保留到原廠 GUI 關閉。

[完整中英文診斷文件](docs/diagnosis.md) 列出如何查找、讀取證據、修復、驗證與失敗處理。

## 支援範圍與限制

[官方下載列有此版本的機型表](docs/supported-models.md) 包含 AERO 15／15S 第 10 代 Intel 與部分第 9 代 AERO／AORUS。**原廠提供安裝器的機型，與本修復工具已實機驗證的機型，是兩件事；目前僅 AERO 15 KB 完成先前的互動驗證。** 官方可能另有較新版本，不能直接沿用本工具。

修復工具目前要求主程式 SHA-256 與核對版本一致，固定安裝於 `C:\Program Files\ControlCenter`，具備舊版 `SMV4_Service` 與可讀取的 `GB_WMIACPI_Get` 充電查詢。未知版本在關閉 GUI、刪除 FirstTime 前就會拒絕執行；不要改掉雜湊來強迫相容。

本工具不改寫原廠 EXE／DLL、不注入、不安裝驅動、不新增開機啟動項目、不調整 CPU／GPU 或充電上限。修復前後會查詢充電政策、上限、電源計畫與服務狀態；若原廠啟動後出現差異，回報失敗，不自動回寫硬體設定。先前本機紀錄的充電上限前後均為 60%。

視窗修復是目前程序的暫時狀態，原廠啟動邏輯未被改寫；重開機、更新或使用原廠入口可能再次出現，必要時重新從修復入口開啟。尚未提供開機永久修復。

## 建置與驗證

執行需求：Windows x64、.NET Framework 4.x；不需要安裝 Codex，也不需要啟用 PowerShell 腳本執行。PowerShell 只用於建置或可選的捷徑建立腳本。

```powershell
.\build.ps1
.\build.ps1 -InstallerPath 'D:\Downloads\ControlCenter_Ver_24.12.25.01.exe'
```

輸出在 `dist/`。建置只編譯工具、執行不碰原廠程式的純邏輯自我檢查，不會執行原廠安裝器或修改硬體。完整包建置會檢查安裝器雜湊與簽章。

先前本機修復已由使用者確認視窗、G 圖示與縮圖正常，紀錄也確認前景及充電等前後核對成功。beta.2 新增 --launch 自動修復捷徑，並改用編譯 EXE 執行修復以免受 Restricted 腳本政策阻擋，保留中英文介面與等候逾時，通過建置與純邏輯檢查；beta.2 已在同一台 AERO 15 KB 完成新捷徑的自動啟動與前後核對，圖示讀回成功、60% 上限與電源／服務不變；該輪 Windows 拒絕前景切換要求，不能保證強制置前。**乾淨安裝、重開機與多機型驗收仍未完成**。工具本身未簽章。

回報問題請至 [Issues](https://github.com/kinostudio/gigabyte-control-center-window-fix/issues)，提供機型、Windows／原廠版本、雜湊核對結果與具體症狀。日誌可能含帳戶、路徑等資料，公開前先遮蔽；前一輪仍在修復時不要重複執行。

MIT 僅適用修復程式碼與原創文件，原廠安裝器與圖示等維持原權利人所有；詳見[第三方說明](THIRD-PARTY-NOTICES.md)。本專案未獲 GIGABYTE 官方背書。

beta.3 背景略過分支實測：2026-11-22 預檢為 0、動作 `reused-without-repair`，GUI PID 前後均為 89704，60% 上限與電源／服務一致。新版條件式異常分支與重新開機持續性尚未另行實機驗收。

## 啟動時顯示在前方／beta.4

入口透過 Windows `AllowSetForegroundWindow`，只把此次使用者啟動的前景權限傳給指定修復程序，再傳給實際執行啟用的視窗工具。不使用對所有程序授權、永久置頂、模擬輸入、執行緒輸入綁定或更改前景鎖定設定。請親自點新捷徑；無人操作的背景啟動可能沒有可傳遞的前景權限。若中途操作其他程式，Windows 仍可能拒絕切到前景。授權與實際前景結果分開記錄，不把工作列修復成功當成已成功置前。

[Microsoft 官方前景權限說明](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-allowsetforegroundwindow)。
