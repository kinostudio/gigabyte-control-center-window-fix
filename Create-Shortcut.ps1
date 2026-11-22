$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'ControlCenterRepair.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Extract the complete repair bundle first.' }
$desktop = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut((Join-Path $desktop 'Control Center - Auto Repair.lnk'))
$link.TargetPath = $exe
$link.Arguments = '--launch'
$link.WorkingDirectory = $PSScriptRoot
$link.Description = 'Open legacy GIGABYTE Control Center and apply the verified window/taskbar repair.'
$icon = 'C:\Program Files\ControlCenter\icon.ico'
if (Test-Path -LiteralPath $icon) { $link.IconLocation = "$icon,0" }
$link.Save()
Write-Output 'Created desktop shortcut: Control Center - Auto Repair'
