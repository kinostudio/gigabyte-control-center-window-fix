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
3. Run `ControlCenterRepair.exe` (or the desktop **Control Center - Auto Repair** shortcut). The default launch checks in the background; it does not show a routine repair window. Approve normal UAC manually using your signed-in administrator account.
4. A healthy existing GUI is restored/activated without restarting or resetting FirstTime. A visible current-user GUI with only style/icon defects receives window repair without restart. If no GUI is running and FirstTime is absent, the OEM GUI is started directly and inspected. Hidden/wrong-owner/startup defects use the backed-up flag reset and restart path only when needed. Unknown inspection errors stop with an error instead of speculative repair.
5. Verify the window/taskbar visually. `repair-result.json` records the selected `action`, `preflightWindowState` and before/after invariant checks. Normal completion is silent; failure shows a concise message.

The original installer is mirrored unchanged inside the full release ZIP. [Source, signature and version verification](docs/installer-verification.md). The OEM software remains subject to its own terms and is excluded from this project's MIT license.

## Background check and desktop shortcut

The default EXE launch and `--launch` both run in the background. To create a shortcut, use Windows **Send to → Desktop (create shortcut)** on the extracted EXE, or run `ControlCenterRepair.exe --settings` and click **Create desktop shortcut**. Keep the extracted folder in a stable location. The optional `Create-Shortcut.ps1` is only a convenience when your script policy permits it.

Each launch checks first and repairs only an abnormal state. The existing healthy GUI is not killed, no flag backup is overwritten on the skip path, and the normal repair form does not flash. UAC is still required for protected state checks/repair; this version does not bypass or change security policy. Concurrent shortcut launches are ignored while the first check is running. An error prompt may appear on failure.

Only launches through this EXE/shortcut use this workflow. Vendor shortcuts, services and Windows startup remain unchanged. This is a conditional workaround, not a permanent patch of the OEM binary; foreground activation remains subject to Windows rules.

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

- Changes only when needed: GUI restart for startup faults, backed-up `FirstTime` flag reset, or current-process style/icon repair. Healthy state is left intact apart from normal restore/activation.
- No patching of OEM EXE/DLL, no injection, no driver installation, no new startup item or persistent service configuration.
- No charge-policy, charge-limit, CPU/GPU-mode or power-plan write commands. Charge policy/limit, active power plan and SMV4_Service state are queried before and after every completed check, including repair-skipped runs. A difference is reported as failure; the tool does not silently write settings back.
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

Beta.3 background skip validation on 2026-11-22: preflight state 0, action `reused-without-repair`, GUI PID 89704 unchanged, and charge limit 60% / power plan / service unchanged. New conditional abnormal-state paths and reboot persistence have not yet received separate real-device acceptance.

## Foreground activation / beta.4

The launcher's foreground permission is delegated only to its specific startup helper, then to the specific window helper that performs activation, using Windows AllowSetForegroundWindow. This does not use ASFW_ANY, permanent topmost, synthetic input, thread-input attachment or a foreground-lock setting change. A normal user click on the shortcut is important; unattended background launches may have no foreground right to delegate. Windows can still reject activation, especially after the user switches to another app. The launcher logs the grant and the window helper records the actual foreground result separately from taskbar repair success.

[Microsoft: AllowSetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-allowsetforegroundwindow) · [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)
