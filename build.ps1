param([string]$InstallerPath)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'ControlCenterWindowFix'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
foreach ($name in 'ControlCenterRepair', 'ControlCenterStartupRepair', 'ControlCenterTaskbarFix') {
    & $csc /nologo /codepage:65001 /target:winexe /platform:x64 /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll /r:System.Web.Extensions.dll /r:Microsoft.CSharp.dll "/out:$stage\$name.exe" (Join-Path $root "src\$name.cs")
    if ($LASTEXITCODE) { throw "Compilation failed: $name" }
    $test = Start-Process -FilePath (Join-Path $stage "$name.exe") -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
    if ($test.ExitCode -ne 0) { throw "Self-test failed: $name" }
}
foreach ($file in 'repair-ui-startup.ps1', 'Create-Shortcut.ps1', 'original-binary-hashes.json', 'README.md', 'README.zh-TW.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md') {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $stage
}
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $stage -Recurse -Force
$sourceCommit = 'unavailable (source archive)'
$treeState = 'unavailable'
if (Get-Command git -ErrorAction SilentlyContinue) {
    $revision = & git -c "safe.directory=$root" -C $root rev-parse HEAD 2>$null
    if ($LASTEXITCODE -eq 0) {
        $sourceCommit = $revision
        $treeState = if (& git -c "safe.directory=$root" -C $root status --porcelain) { 'dirty' } else { 'clean' }
    }
}
@("Release path: v0.1.0-beta.5 (in-place update)", "Build UTC: $([DateTime]::UtcNow.ToString('o'))", "Source commit: $sourceCommit", "Working tree: $treeState", 'Flow: OEM ShowAP first; read-only background inspection afterward') | Set-Content -LiteralPath (Join-Path $stage 'RELEASE-REVISION.txt') -Encoding utf8
# Explicit lists keep personal logs and the OEM installer out of the repair-only archive.
$items = @('ControlCenterRepair.exe','ControlCenterStartupRepair.exe','ControlCenterTaskbarFix.exe','repair-ui-startup.ps1','Create-Shortcut.ps1','original-binary-hashes.json','README.md','README.zh-TW.md','LICENSE','THIRD-PARTY-NOTICES.md','RELEASE-REVISION.txt','docs') | ForEach-Object { Join-Path $stage $_ }
# Keep existing download links valid; RELEASE-REVISION identifies the current contents.
$prefix = 'gigabyte-control-center-window-fix-v0.1.0-beta.5'
Compress-Archive -LiteralPath $items -DestinationPath (Join-Path $dist "$prefix-repair-only.zip") -Force
if ($InstallerPath) {
    $installer = Get-Item -LiteralPath $InstallerPath
    $expected = 'E0F2C552C81B81CF9680564E7E843BF450D231B8E603A23A6690E3675391989E'
    if ((Get-FileHash -LiteralPath $installer.FullName).Hash -ne $expected) { throw 'Unexpected OEM installer SHA-256.' }
    if ((Get-AuthenticodeSignature -LiteralPath $installer.FullName).Status -ne 'Valid') { throw 'OEM installer signature is not valid.' }
    $copy = Join-Path $stage 'ControlCenter_Ver_24.12.25.01.exe'
    Copy-Item -LiteralPath $installer.FullName -Destination $copy
    Compress-Archive -LiteralPath ($items + @($copy)) -DestinationPath (Join-Path $dist "$prefix-with-official-installer.zip") -Force
}
Get-ChildItem -LiteralPath $dist -Filter "$prefix-*.zip" | Get-FileHash -Algorithm SHA256 | ForEach-Object { "$($_.Hash.ToLowerInvariant())  $(Split-Path $_.Path -Leaf)" } | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Write-Output 'Build and pure self-tests passed. No OEM application or installer was executed.'
