# Control Center not showing: diagnosis, cause and repair
# Control Center 無法顯示：查找、故障原因與修復

## 1. Identify the symptom / 先分辨症狀

- Process runs but no window, or the window appears only after switching/minimizing another app: inspect the startup flag and process owner.
- Window visible but no taskbar button/thumbnail: inspect extended window styles.
- Taskbar button visible but generic/wrong icon: inspect the window's icon source and icon lifetime.
- Process exits, no process starts, unsupported hash, broken WMI or installation missing: this workaround is not a general repair for those failures.

程序在跑卻沒視窗，或要切換／縮小其他程式才出現，先查旗標與程序身分；視窗有了但沒工作列按鈕／縮圖，查視窗樣式；按鈕有了但圖示錯，查圖示來源與資源生命週期。若程序閃退、根本沒啟動、雜湊不符、WMI 壞掉或尚未安裝，不能直接推定為相同故障。

## 2. Read-only checks / 唯讀檢查

Run these in PowerShell. Some owner/WMI reads need administrator access; do not launch the GUI as SYSTEM or change permissions just to make a query succeed. Commands below only inspect state.

以下 PowerShell 指令只查詢狀態；部分程序身分與 WMI 讀取需要管理員權限，不要為了讀取而把 GUI 改成 SYSTEM 或調整安全設定。

```powershell
$target = 'C:\Program Files\ControlCenter\ControlCenter.exe'
(Get-Item -LiteralPath $target).VersionInfo | Select-Object FileVersion, ProductVersion
Get-FileHash -LiteralPath $target -Algorithm SHA256
Get-AuthenticodeSignature -LiteralPath $target | Select-Object Status, SignerCertificate
Get-Process -Name ControlCenter -ErrorAction SilentlyContinue | Select-Object Id, SessionId, MainWindowHandle
Get-CimInstance Win32_Process -Filter "Name='ControlCenter.exe'" | ForEach-Object {
    $owner = Invoke-CimMethod -InputObject $_ -MethodName GetOwner
    [pscustomobject]@{Pid=$_.ProcessId;Session=$_.SessionId;Path=$_.ExecutablePath;Owner="$($owner.Domain)\$($owner.User)"}
}
$key = Get-Item 'HKLM:\SOFTWARE\GIGABYTE\SmartManager'
$key.GetValueNames() -contains 'FirstTime'
$key.GetValue('FirstTime')
Get-Service SMV4_Service
powercfg.exe /getactivescheme
$d = Get-CimInstance -Namespace root/wmi -ClassName GB_WMIACPI_Get -OperationTimeoutSec 5 | Select-Object -First 1
Invoke-CimMethod -InputObject $d -MethodName GetChargePolicy -OperationTimeoutSec 5
Invoke-CimMethod -InputObject $d -MethodName GetChargeStop -OperationTimeoutSec 5
```

`MainWindowHandle = 0` alone is not proof of a particular bug. A startup delay or hidden WPF window can also produce it. The helper waits up to 45 seconds for a visible, reasonably sized WPF window from the verified process/session.

`MainWindowHandle = 0` 不能單獨證明某一故障；WPF 尚在初始化或隱藏也可能回傳 0。工作列工具最多等候 45 秒，只選同工作階段、正確程序中已可見且尺寸合理的 WPF 視窗。

## 3. How the causes were located / 如何查到原因

The inspected ControlCenter.exe startup path (MainWindow_SourceInitialized) checks FirstTime value existence and can hide the window and disable ShowInTaskbar even when its data is zero. This was identified from inspection of the installed build; it is not an official vendor-wide defect announcement. Separately, process-owner inspection found SYSTEM ownership via an observed ShowAP/SMV4_Service launch path. Native window-style writes had returned access denied in that context.

本次檢查主程式的 MainWindow_SourceInitialized 啟動分支，確認 FirstTime 是判斷「存在」而非數值是否為 0，該分支可隱藏視窗並停用 ShowInTaskbar。另以程序擁有者查詢確認特定 ShowAP／SMV4_Service 啟動路徑產生 SYSTEM GUI，並與先前原生視窗操作的存取被拒結果對照。這是本次版本調查，不是原廠對所有機型的通病公告。

After relaunch under the current user, the user saw the GUI but no taskbar button. Style repair changed the recorded extended style from `0x80000` to `0xC0000`, adding APPWINDOW while preserving unrelated bits. The correct icon came from the installed `icon.ico`, not the generic icon extracted from ControlCenter.exe. Window icon readback plus the user's visual confirmation established the final taskbar/icon result on this AERO 15 KB.

改用目前使用者啟動後，使用者看見 GUI，但沒有工作列按鈕。樣式修復紀錄由 `0x80000` 變為 `0xC0000`，加入 APPWINDOW 並保留無關旗標；圖示改用既有安裝資料夾的 `icon.ico`，而非 EXE 擷取出的通用圖示。視窗讀回核對加上使用者確認，才構成這台 AERO 15 KB 的工作列／圖示驗收。

No extracted OEM source, full decompilation dump or personal diagnostic logs are published here. The described findings are a technical account of the inspected version, not proof every matching symptom has that cause.

本倉庫不公開原廠完整反編譯內容或個人診斷日誌；同症狀仍可能有不同原因。

## 4. Repair sequence / 修復順序

1. Validate administrator permission and exact main-EXE hash. Read charge policy/limit, active power plan and service state before any GUI stop or flag reset.
2. Record the original FirstTime existence/value/type in `FirstTime-backup.json`; stop only the verified current-session GUI.
3. Remove only the FirstTime flag if it existed, rather than setting it to 0. Restart the signed OEM GUI as the current account and confirm ownership.
4. Select the verified visible WPF window, clear TOOLWINDOW, add APPWINDOW and rebuild its Shell button through hide/style/show. Request normal foreground activation; never set permanent topmost.
5. Load the existing OEM icon.ico; set and read back both icon handles. Keep those handles alive until the OEM GUI exits.
6. Re-read charge policy/limit, power plan and service state; record success only when the invariants match. Inspect the taskbar visually too.

依序核對權限／主程式雜湊及充電等狀態 → 備份 FirstTime → 關閉指定 GUI → 移除旗標而非設為 0 → 以目前帳戶啟動 → 等待正確視窗 → 重建工作列並指定原廠圖示 → 前後核對與人工目視驗證。工具不改寫原廠程式檔，不碰 CPU／GPU 模式或充電上限。

## 5. Logs, failure handling and recovery / 日誌、失敗與回復

- `repair-result.json`: status, error and before/after invariants. `restarted-as-user; awaiting visual/taskbar acceptance` means automated checks passed, not that a human has already accepted the taskbar.
- `taskbar-diagnostic.txt`: candidate window readiness, styles, owner errors and foreground requests.
- `taskbar-window-result.txt`: `IconReadback=True` confirms native icon handle readback, not visual taskbar correctness. Foreground activation may legitimately be denied by Windows.
- `FirstTime-backup.json`: pre-repair flag existence, value and type. Keep a copy before another repair if you need the original baseline.

以上檔案分別記錄前後核對、視窗階段、圖示讀回與旗標備份。自動成功不等於人工驗收；Windows 可能拒絕一般前景切換要求。下一輪會更新旗標備份，需要保留首次基準時先另存備份。

On hash mismatch or missing getters, stop and collect the version/read-only results; do not weaken the guard or repeatedly reinstall unrelated GCC versions. If beta.1 is blocked by Restricted PowerShell script policy, use beta.2: its launcher invokes the compiled ControlCenterStartupRepair.exe instead of the legacy reference script. No execution policy change or bypass is used. The public UI times out after three minutes, but timeout does not prove the child process has exited: inspect logs before another attempt.

雜湊不符或 getter 不可用時停止，先提供版本與唯讀結果，不刪除保護或亂裝其他 GCC 版本。若 beta.1 被 Restricted 腳本政策阻擋，改用 beta.2；入口會呼叫編譯的 ControlCenterStartupRepair.exe，舊 PowerShell 腳本只保留作參考，不會在修復時執行，不變更或略過政策。公開介面三分鐘後逾時不代表背景程序已結束，重試前先查日誌。

On a failure after a flag change, the repair helper attempts flag rollback only when the new GUI is not running; it avoids inserting a hide flag during initialization. To undo window styles/icons, close the OEM GUI normally; those changes are process-local. Manual registry recovery must use the saved existence/value/type while the GUI is closed; restoring FirstTime may also restore the original hidden-window behavior. Do not use an arbitrary zero value as a guessed recovery.

若已改旗標後失敗，且新 GUI 已結束，修復工具才嘗試回復；GUI 初始化中不插回可能隱藏視窗的旗標。正常關閉原廠 GUI 即結束該程序的視窗修復。手動恢復登錄值須在 GUI 關閉時，依備份的存在性／數值／型別處理，且可能恢復原本隱藏行為；不能猜一個 0 當作還原。

## 6. Evidence and limits / 驗證與限制

Earlier user acceptance: visible GUI, taskbar G icon and thumbnail; native record on 2026-11-22 showed foreground activation and icon readback true. Charge policy 4 / limit 60%, power plan and SMV4_Service state matched before/after. Reboot, original-shortcut persistence, clean installation and other laptops remain unverified. Public-build pure self-tests do not establish those behaviors.

先前使用者已確認視窗、G 圖示與縮圖；2026-11-22 原生紀錄顯示前景／圖示讀回成功，充電政策 4、上限 60%、電源計畫及服務狀態前後一致。重開機、原廠捷徑持續性、乾淨安裝與其他機型仍未驗證，公開版純邏輯測試不能取代這些驗收。

### Windows behavior references / Windows 官方參考

- [Taskbar](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar)
- [Extended window styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles)
- [SetWindowLongPtr](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptra)
- [Icons](https://learn.microsoft.com/en-us/windows/win32/menurc/about-icons)

### beta.2 automatic-launch follow-up / 自動入口追加核對

On 2026-11-22 at 19:46 (Asia/Taipei), the desktop shortcut invoked the compiled startup helper with manual UAC approval. The record confirmed current-user GUI ownership, APPWINDOW style, IconReadback=True and unchanged charge policy 4 / limit 60%, power plan and service. ForegroundRequest=False / IsForeground=False: Windows did not grant the normal foreground request. This establishes the automatic repair sequence and invariant checks, not guaranteed foreground activation or reboot persistence.

2026-11-22 19:46（台灣時間），桌面捷徑透過手動 UAC 啟動編譯版修復工具，完成目前使用者 GUI、工作列樣式、圖示讀回及 60% 上限／電源／服務前後核對。該輪前景要求為 False，因此不宣稱可強制置前或已驗證重開機。使用原廠舊入口仍不會自動套用；新捷徑每次重新套用。

### beta.3 conditional background launch / 條件式背景啟動

Default launch is silent except for UAC and failures. Preflight checks current-session process path/owner, visible WPF window, taskbar style bits, and pixel comparison of both window icons against the installed OEM icon. Minimized healthy windows are restored rather than restarted. Check state 0 means reuse/activate; state 4 means window-only repair without restarting; state 5 means no usable current-user window, requiring startup handling. Unknown checks stop with an error. No running GUI plus an absent FirstTime flag takes a direct-start-and-inspect path before any reset is considered.

預設不顯示修復主視窗，正常完成也不跳通知；UAC 與失敗提示除外。預檢查核目前工作階段的程序路徑／擁有者、可見 WPF 視窗、工作列樣式，以及大小圖示與既有原廠圖示的像素比對。正常但縮小的視窗只還原，不重啟。0 為正常略過，4 為保留程序修復視窗，5 才進入啟動問題處理；未知錯誤停止。沒有程序且 FirstTime 不存在時，先直接啟動檢查，不先行改登錄值。

`repair-result.json` records `action`: `reused-without-repair`, `started-without-repair`, `window-repair-only; GUI retained`, or `startup-repair-and-restart`. FirstTime backup/reset happens only on the startup-repair path. Charge/power/service snapshots are still compared. `--settings` explicitly opens the management UI; it is not the default desktop workflow.

The beta.3 healthy-state run at 19:58 on 2026-11-22 (Asia/Taipei) recorded `ready; repair skipped`, `preflightWindowState: 0`, action `reused-without-repair`, and unchanged PID 89704. Charge policy 4 / limit 60%, active plan and service matched before/after. No FirstTime backup/reset or GUI restart occurred. New abnormal-state decisions are not represented as separately hardware-tested.
