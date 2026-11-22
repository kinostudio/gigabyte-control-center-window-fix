# OEM installer verification / 原廠安裝包驗證

Checked / 日期：2026-11-22。

| Item / 項目 | Result / 結果 |
| --- | --- |
| Filename / 檔名 | `ControlCenter_Ver_24.12.25.01.exe` |
| Bytes / 位元組 | `268873264` |
| SHA-256 | `E0F2C552C81B81CF9680564E7E843BF450D231B8E603A23A6690E3675391989E` |
| Authenticode | `Valid`; signer / 簽署者：`Giga-byte Technology Co., Ltd.` |
| Installer version / 安裝包版本 | `24.12.25.01` |
| Internal main EXE / 內部主程式 | `ControlCenter.exe`, file/product version `24.12.24.01` |
| Main EXE SHA-256 | `DE1D8F61FB204BD9435F1982801EA5CDCA3C2912E7F7651F81A3C83CE042A231` |
| Match with repaired installation / 與先前修復比對 | Main EXE and ShowAP.exe match; not a claim that every installed file is identical / 主程式與 ShowAP 相符，不代表全部檔案皆相同 |

Official download ZIP / 官方下載 ZIP：
[24.12.25.01 on download.gigabyte.com](https://download.gigabyte.com/FileList/Driver/nb-driver-64bit-win10-win11-dchu-controlcenter-24.12.25.01.zip)

Official model listings / 官方機型頁：[supported-models.md](supported-models.md)。

The release mirrors the user-provided EXE whose vendor signature was validated locally. The hash above was computed locally; it is not presented as a checksum published by GIGABYTE. We did not download and byte-compare a second copy from the official ZIP. The original EXE is compressed without modification inside the full bundle and is never automatically executed.

公開包鏡像的是使用者提供、已在本機驗證原廠簽章的 EXE。上列 SHA-256 是本機計算值，不是假稱原廠公告的 checksum；未另行下載官方 ZIP 的第二份檔案逐位元比對。完整包內的原廠 EXE 未修改，也不會被修復工具自動執行。

The installed SMV4_Service.exe hash differed from the copy extracted from this installer. The repair's strict binary guard covers the main GUI only; the service is queried, not replaced or reconfigured. This is another reason not to equate the earlier working installation with a clean installation of the entire package.

目前已安裝的 SMV4_Service.exe 與此安裝包解出的版本雜湊不同。修復工具的嚴格雜湊限制針對 GUI 主程式；服務僅查詢，不替換或重新設定。因此先前修復成功不等於已驗證整包乾淨安裝的行為。

No separate OEM binary redistribution grant was found in the inspected materials. The installer is not covered by the repair tool's MIT license, and this repository does not claim independent relicensing or GIGABYTE endorsement. See [third-party notice](../THIRD-PARTY-NOTICES.md).

檢查資料未找到另行提供的原廠再散布授權書；安裝包不屬於修復工具 MIT 授權，本倉庫不宣稱重新授權或獲原廠背書，詳見[第三方說明](../THIRD-PARTY-NOTICES.md)。
