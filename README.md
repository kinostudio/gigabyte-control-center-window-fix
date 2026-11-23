# GIGABYTE Control Center won't open — window and taskbar fix

[繁體中文](README.zh-TW.md) · [Downloads](https://github.com/kinostudio/gigabyte-control-center-window-fix/releases/tag/v0.1.0-beta.1) · [Diagnosis and causes](docs/diagnosis.md)

Community workaround for **GIGABYTE Control Center not showing, running in the background with no window, missing taskbar icon/thumbnail, or the wrong taskbar icon** on legacy AERO / AORUS laptops. 中文：技嘉 Control Center 打不開、無法顯示、背景執行卻沒視窗、工作列圖示／縮圖消失。

**Updated in place on 2026-11-23.** The repository, `v0.1.0-beta.1` release URL and ZIP filenames stay the same; their contents and SHA-256 have changed to the current **ShowAP-first** implementation. Re-download if you have an older copy. `RELEASE-REVISION.txt` inside the ZIP identifies its source commit and build. Only this original beta.1 release is retained; the current implementation is updated in place.

Independent utility, not an official GIGABYTE patch. Documented device: **AERO 15 KB**, OEM installer **24.12.25.01**, internal main EXE **24.12.24.01**. It is not a universal modern GCC / GiMATE repair.

## Downloads and setup

| Asset | Contents |
| --- | --- |
| `*-with-official-installer.zip` | Complete bundle: repair tools and unmodified signed `ControlCenter_Ver_24.12.25.01.exe` |
| `*-repair-only.zip` | All repair components for an existing verified OEM installation |
| `SHA256SUMS.txt` | Current ZIP checksums; these replace the earlier checksums |

1. Download and verify the checksum. Extract the **entire ZIP** to a stable writable folder; do not run from the ZIP or copy only one EXE.
2. If the OEM application is missing, confirm your exact model in the [official download table](docs/supported-models.md) and manually run its appropriate installer. Review vendor terms and any restart request. Installation is not automatic.
3. Run `ControlCenterRepair.exe`, or a desktop shortcut to it. The first launch step invokes the installed, hash-verified **ShowAP.exe**, matching the vendor entry. The wrapper does not wait for ShowAP to exit.
4. Read-only background checks start afterward. A healthy result ends silently with **no tool-requested UAC, no forced restart and no FirstTime reset**. Background observation does not move or activate a healthy window.
5. Nonzero state must persist for at least eight seconds and repeat before protected diagnosis/repair is requested. Approve UAC manually only when needed. A usable current-user GUI receives window-only repair where possible; startup defects may require a verified restart.

Create a desktop shortcut using Windows **Send to → Desktop**, or run `ControlCenterRepair.exe --settings` and click **Create desktop shortcut**. The optional `Create-Shortcut.ps1` is only a convenience when script policy permits. Keep the extracted folder in place. Vendor shortcuts, services and Windows startup are not redirected. Repeated launches are ignored while the first observation/repair is running.

The original installer stays signed and unmodified. [Installer verification](docs/installer-verification.md). It is excluded from the repair tool's MIT license and retains its owners' rights and terms.

## Faults found in the inspected build

- **FirstTime existence check:** a startup branch can hide the window and disable ShowInTaskbar merely because the registry value exists, even if its value is zero. Setting it to zero is not the same as removing it.
- **GUI ownership:** an observed ShowAP / SMV4_Service path created a SYSTEM-owned GUI and earlier window changes failed with access denied. This is an observed condition, not the cause of every matching symptom. A healthy current-user window is not rejected just because another different-owner GUI exists.
- **Taskbar/icon state:** current-user relaunch alone did not restore the taskbar. The repair adjusts APPWINDOW/TOOLWINDOW, rebuilds the Shell button, and loads the installed OEM icon.ico. A helper keeps icon handles alive until the GUI exits.

The [bilingual diagnostic guide](docs/diagnosis.md) explains read-only checks, evidence, repair, logs and recovery. No OEM decompilation dump or personal logs are included. These findings are not a vendor-wide defect announcement.

## Compatibility and changes

The [verified official installer listings](docs/supported-models.md) include AERO 15 / 15S Intel 10th Gen and several Intel 9th Gen AERO / AORUS families. **Installer listing is not repair validation**; only AERO 15 KB has the documented device tests. This version is not claimed to be the latest OEM Control Center.

Fixed installation path: `C:\Program Files\ControlCenter`. The known ShowAP hash is checked before launch; the known main GUI hash is checked before repair. Protected repair requires the legacy SMV4_Service and readable GB_WMIACPI_Get charge getters. Do not bypass guards for another version.

The tool does not patch OEM EXE/DLL files, inject code, install drivers, create startup items, alter script policy, or issue CPU/GPU/charge/power-plan setting commands. Only an actual repair may reset the backed-up FirstTime flag, restart the verified GUI, or change its process-local window/icon state.

Protected repair compares charge policy/limit, active plan and service before/after; differences are reported, not silently written back. **Healthy background-only checks do not query firmware**: `invariantsChecked: false` states this explicitly. The OEM launcher itself still performs its normal vendor behavior.

This is a per-launch workaround, not a permanent OEM patch. Windows can deny foreground activation; no permanent topmost, synthetic input, thread-input attachment or foreground-lock bypass is used. Reboot, clean-install and multi-device acceptance remain unverified. Binaries are unsigned.

## Verification and timing

The three C# programs compile and pass pure self-tests covering routing, startup observation, icon comparison and visible-window preference. These tests do not replace device acceptance.

A 2026-11-23 device record shows ShowAP launch submission returning in **1338 ms**, followed by background state samples 5 → 2 → 0 and a healthy completion at **5415 ms**, without protected repair. Submission time is **not** click-to-visible time, and background completion is not a blocked opening delay. Direct ShowAP speed comparison and reliable foreground behavior are not claimed verified. Earlier healthy-window 1.9-second results did not represent a later run that required about 10 seconds of restart repair.

Earlier repair tests confirmed a visible GUI, G icon/thumbnail and unchanged 60% charge limit, power plan and service. Source/version-specific evidence is in the diagnostic guide.

Local logs: `launch-result.json` records submission time and read-only samples; `repair-result.json` records the selected action and actual protected invariant checks, if performed. Activation and taskbar logs are separate from successful icon readback. Redact usernames and paths before posting logs to [Issues](https://github.com/kinostudio/gigabyte-control-center-window-fix/issues).

## Build

Windows x64 and .NET Framework 4.x are required. PowerShell is used for building, not the compiled runtime repair; no Codex installation is needed.

```powershell
.\build.ps1
.\build.ps1 -InstallerPath 'D:\Downloads\ControlCenter_Ver_24.12.25.01.exe'
```

Outputs go to `dist/`. The build compiles and self-tests without executing the OEM installer or repairing the desktop. Full-bundle builds require the exact expected installer hash and valid signature. Archives exclude personal logs, backups and settings. Build revision metadata distinguishes in-place updates.

## Rights

MIT covers the repair code and original documentation only. The original installer, icons and vendor components retain their respective owners' rights; no separate OEM redistribution grant or GIGABYTE endorsement is claimed. See [third-party notice](THIRD-PARTY-NOTICES.md).
