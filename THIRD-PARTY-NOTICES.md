# Third-party notice / 第三方元件說明

The full release bundle contains the user-provided, unmodified, digitally signed
`ControlCenter_Ver_24.12.25.01.exe` from GIGABYTE. GIGABYTE and its suppliers retain
all rights to that installer and its contents. This project's MIT license covers
the repair tool only, not the OEM software. No GIGABYTE endorsement is claimed.

完整發布包包含使用者提供、未修改且具有有效 GIGABYTE 數位簽章的原廠安裝器。
原廠安裝器及其內含元件的權利屬於 GIGABYTE 與各供應商；本專案 MIT 授權僅適用
於修復工具。未取得另行提供的再散布授權書，也未宣稱此說明取代原廠使用條款。
若權利人對鏡像檔案有疑慮，請透過本倉庫 issue 聯絡維護者。

The official download remains the preferred reference. See
[installer verification](docs/installer-verification.md) for source, signature,
SHA-256 and the distinction between installer and internal executable versions.
The installer is never run automatically by the repair tool.

The taskbar helper reads `icon.ico` from the user's existing OEM installation;
no separately extracted OEM icon, DLL or driver is included in the repair-only ZIP.
Windows and .NET runtime components must already be available on the system.

原廠安裝器不會被修復工具自動執行。工作列工具使用既有安裝資料夾的 `icon.ico`；
精簡修復包沒有另行附上原廠圖示、DLL 或驅動。Windows 與 .NET 使用系統既有版本。
