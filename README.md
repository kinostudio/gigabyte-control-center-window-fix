# GIGABYTE Control Center won't open — window and taskbar fix

[繁體中文說明](README.zh-TW.md) · [Download / 下載](https://github.com/kinostudio/gigabyte-control-center-window-fix/releases) · [Diagnosis / 診斷](docs/diagnosis.md)

Community workaround for **GIGABYTE Control Center not showing, running in the background with no window, missing taskbar icon or thumbnail, and an incorrect taskbar icon** on legacy AERO / AORUS laptops. 中文：技嘉 Control Center 打不開、無法顯示、背景執行但沒視窗、工作列圖示或縮圖消失。

**Tested on AERO 15 KB with installer version 24.12.25.01 and internal ControlCenter.exe version 24.12.24.01.** This is an independent repair utility, not an official GIGABYTE patch or a replacement for modern GCC / GiMATE. One-device success does not establish that every community report has the same cause.

## Download and use

| Release asset | Contents / purpose |
| --- | --- |
| `*-with-official-installer.zip` | Repair tools plus the original signed `ControlCenter_Ver_24.12.25.01.exe`; use only when your model's official support page lists that version |
| `*-repair-only.zip` | Small repair bundle for an existing verified installation |
| `SHA256SUMS.txt` | SHA-256 checksums of the release ZIPs |

1. Download from [Releases](https://github.com/kinostudio/gigabyte-control-center-window-fix/releases), verify the checksum, and extract the **entire ZIP** to a writable folder. Do not run inside the archive or copy only one EXE.
2. If Control Center is missing, verify your model in the [official download table](docs/supported-models.md), then manually run the original installer included in the full bundle. Review its own terms and any restart request. Installation and repair are separate steps; the repair tool never installs or reinstalls Control Center automatically.
3. Finish any pending changes in Control Center. Run `ControlCenterRepair.exe` as your signed-in local administrator account and click **Repair and open Control Center**. Approve UAC manually using the same account. Cross-account administrator credentials and other installation paths are not tested.
4. The tool closes only the verified OEM GUI in your current session, backs up and resets the `FirstTime` flag, restarts the GUI as the current user, and repairs its taskbar style/icon.
5. Verify the window is visible, its taskbar button exists, the original G icon appears, and hovering shows a thumbnail. Check `repair-result.json` for `invariantsUnchanged: true`.

The original installer is mirrored unchanged inside the full release ZIP. [Source, signature and version verification](docs/installer-verification.md). The OEM software remains subject to its own terms and is excluded from this project's MIT license.

## One-click automatic repair shortcut

Open `ControlCenterRepair.exe` and click **Create desktop shortcut** to create **Control Center - Auto Repair** on your desktop. Keep the extracted folder in a stable location. Every time you use this shortcut, `ControlCenterRepair.exe --launch` automatically starts the verified repair, asks for normal UAC approval, then closes its launcher on success. On failure the repair window stays open with diagnostics. It restarts the OEM GUI, so finish pending Control Center changes first. Launching again can restart an already-running GUI.

This automates the workaround on every launch **through this shortcut**. It does not intercept the vendor's original shortcuts, services or Windows startup, and does not remove the underlying OEM bug. The startup repair runs as a compiled EXE and does not require PowerShell script execution; it does not suppress UAC or change script execution policy. Reboot persistence is not yet verified.

## Why the window can disappear

Investigation of the inspected build found three relevant behaviors:

- **FirstTime flag:** the initialization branch checks whether the registry value exists, rather than whether its numeric value is nonzero. An existing value of `0` can still select the hide / `ShowInTaskbar = false` path. Setting it to zero is therefore not equivalent to removing it.
- **GUI ownership:** an observed ShowAP / SMV4_Service launch path produced a GUI owned by SYSTEM. Earlier window manipulation then failed with access denied. This is an observed contributing condition, not proof that all foreground problems are permission problems.
- **Taskbar and icon:** restarting the GUI under the current account restored visibility but did not restore the taskbar button. The workaround clears `WS_EX_TOOLWINDOW`, sets `WS_EX_APPWINDOW`, performs a hide/style/show cycle, and assigns icons from the installed `icon.ico`. A helper keeps the icon handles alive until the GUI exits.

See [how to locate the fault, repair it and verify the result](docs/diagnosis.md). It documents evidence, read-only checks, expected results and failure paths without claiming the workaround changes the vendor's original startup logic.

## Compatibility

The OEM installer is listed on multiple official product pages, but **official installer availability is not repair-tool validation**. The [verified official download list](docs/supported-models.md) includes AERO 15 / 15S Intel 10th Gen and several Intel 9th Gen AERO / AORUS families. Only **AERO 15 KB** has completed the earlier interactive repair test.

The repair currently requires the exact verified main EXE SHA-256, installation at `C:\Program Files\ControlCenter`, the legacy `SMV4_Service`, and readable `GB_WMIACPI_Get` charge getters. Unsupported versions fail before the GUI is stopped or FirstTime is removed. Do not bypass the hash check to force compatibility. Newer downloads on the official site are not automatically supported.

## What changes and what is checked

- Changes: current GUI lifetime, backed-up `FirstTime` UI flag, current-process window style and icon handles.
- No patching of OEM EXE/DLL, no injection, no driver installation, no new startup item or persistent service configuration.
- No charge-policy, charge-limit, CPU/GPU-mode or power-plan write commands. The OEM GUI itself is restarted, so charge policy/limit, active power plan and SMV4_Service state are queried before and after. A difference is reported as failure; the tool does not silently write settings back.
- Window changes are temporary. Reboot, OEM updates or using the original launcher can restore the behavior. Run the repair entry again if needed; permanent startup repair is not implemented.

## Build and verification

Windows x64 and .NET Framework 4.x are required for the repair. PowerShell is needed only for building or the optional shortcut creation script; the runtime repair uses a compiled helper and works without enabling .ps1 execution. No Codex installation is needed.

```powershell
.\build.ps1
# Optional: also build the full bundle from your local verified OEM installer
.\build.ps1 -InstallerPath 'D:\Downloads\ControlCenter_Ver_24.12.25.01.exe'
```

The script compiles the three C# programs and runs pure `--self-test` branches. It does not execute the OEM installer, start/stop Control Center or change hardware settings. Outputs are in `dist/`. The full-bundle build rejects a different installer hash or an invalid signature.

Earlier local repair: user confirmed window, taskbar G icon and thumbnail; logs recorded foreground/icon readback and unchanged charge limit (60%), power plan and service status. The beta.2 build adds the --launch shortcut and compiled startup repair (avoiding Restricted PowerShell script policy), with bilingual UI and a wait timeout; it has build/self-test verification. A beta.2 automatic-launch run completed on the same AERO 15 KB with icon readback and unchanged 60% charge limit, power plan and service state. Windows denied that run's foreground activation request; forced foreground is not promised. **Clean-install, reboot and multi-device acceptance remain unverified**. Binaries are unsigned.

## Reporting a matching issue

Use [Issues](https://github.com/kinostudio/gigabyte-control-center-window-fix/issues) and include model, Windows version, installer/internal EXE versions, hash match result, and whether the GUI process runs with no window, no taskbar button, or only a wrong icon. Redact usernames, paths and other personal data from logs before posting. Avoid repeating repair while a previous repair is still running.

MIT applies to the repair code and original documentation only. OEM installer, icons and all vendor components retain their owners' rights. See [third-party notice](THIRD-PARTY-NOTICES.md). No affiliation with or endorsement by GIGABYTE is claimed.
