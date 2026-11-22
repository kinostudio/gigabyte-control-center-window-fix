$ErrorActionPreference = 'Stop'
$out = $PSScriptRoot
$target = 'C:\Program Files\ControlCenter\ControlCenter.exe'
$key = 'HKLM:\SOFTWARE\GIGABYTE\SmartManager'
$report = [ordered]@{started=(Get-Date).ToString('o');changes=@();status='starting'}
$changedFlag = $false
$flag = $null
function Read-Charge {
    $device = Get-CimInstance -Namespace root/wmi -ClassName GB_WMIACPI_Get -OperationTimeoutSec 5 | Select-Object -First 1
    if (!$device) { throw 'Original charge getter unavailable; nothing changed.' }
    $policy = Invoke-CimMethod -InputObject $device -MethodName GetChargePolicy -OperationTimeoutSec 5
    $limit = Invoke-CimMethod -InputObject $device -MethodName GetChargeStop -OperationTimeoutSec 5
    return @{policy=[int]$policy.Data;limit=[int]$limit.Data}
}
function Read-Power {
    $value = (& powercfg.exe /getactivescheme | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Unable to read active power scheme.' }
    return $value
}
try {
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator permission is required.' }
    $hashFile = Join-Path $out 'original-binary-hashes.json'
    $expected = Get-Content -LiteralPath $hashFile -Raw | ConvertFrom-Json | Where-Object Path -EQ $target | Select-Object -ExpandProperty Hash
    $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if (!$expected -or $actual -ne $expected) { throw 'OEM binary changed since inspection; repair not applied to an unverified version.' }
    $report.originalBinaryHash = $actual
    $service = Get-Service -Name SMV4_Service
    $report.serviceBefore = $service.Status.ToString()
    $report.chargeBefore = Read-Charge
    $report.powerBefore = Read-Power
    $item = Get-Item -LiteralPath $key
    $exists = @($item.GetValueNames()) -contains 'FirstTime'
    $flag = @{exists=$exists;value=$null;kind=$null}
    if ($exists) { $flag.value=$item.GetValue('FirstTime');$flag.kind=$item.GetValueKind('FirstTime').ToString() }
    $flag | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'FirstTime-backup.json') -Encoding utf8
    $session = (Get-Process -Id $PID).SessionId
    $processes = @(Get-CimInstance Win32_Process -Filter "Name='ControlCenter.exe'" | Where-Object SessionId -EQ $session)
    foreach ($process in $processes) {
        if ($process.ExecutablePath -ne $target) { throw 'Unexpected ControlCenter executable; nothing restarted.' }
    }
    foreach ($process in $processes) {
        Stop-Process -Id $process.ProcessId -Force
        Wait-Process -Id $process.ProcessId -Timeout 10 -ErrorAction SilentlyContinue
        if (Get-Process -Id $process.ProcessId -ErrorAction SilentlyContinue) { throw 'Original GUI did not stop.' }
    }
    $report.changes += 'Stopped only the verified ControlCenter GUI process; background service retained.'
    if ($exists) { Remove-ItemProperty -LiteralPath $key -Name FirstTime; $changedFlag=$true }
    $report.changes += 'Reset only the OEM FirstTime UI flag to avoid the startup hide/taskbar-disable branch.'
    $app = Start-Process -FilePath $target -WorkingDirectory (Split-Path $target) -WindowStyle Normal -PassThru
    $report.newProcessId = $app.Id
    try { [void]$app.WaitForInputIdle(15000) } catch { }
    Start-Sleep -Seconds 3
    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($app.Id)"
    if (!$process) { throw 'New OEM GUI exited; UI acceptance could not start.' }
    $owner = Invoke-CimMethod -InputObject $process -MethodName GetOwner
    $report.newOwnerIsSystem = ($owner.User -eq 'SYSTEM' -and $owner.Domain -eq 'NT AUTHORITY')
    $report.newOwnerIsCurrentUser = ($owner.User -eq $env:USERNAME)
    if ($report.newOwnerIsSystem -or !$report.newOwnerIsCurrentUser) { throw 'New OEM GUI did not start as the signed-in user.' }
    $windowFix = Join-Path $out 'ControlCenterTaskbarFix.exe'
    if (Test-Path -LiteralPath $windowFix) {
        $windowResult = Join-Path $out 'taskbar-window-result.txt'
        if (Test-Path -LiteralPath $windowResult) { Remove-Item -LiteralPath $windowResult }
        $windowJob = Start-Process -FilePath $windowFix -WindowStyle Hidden -PassThru
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        while (!(Test-Path -LiteralPath $windowResult)) {
            if ($windowJob.HasExited) { throw 'Taskbar/icon stage exited before readiness; inspect taskbar-diagnostic.txt.' }
            if ([DateTime]::UtcNow -gt $deadline) { throw 'Taskbar/icon stage timed out; inspect taskbar-diagnostic.txt.' }
            Start-Sleep -Milliseconds 250
        }
        $report.windowResult = Get-Content -LiteralPath $windowResult -Raw
        if ($report.windowResult -notmatch 'IconReadback=True') { throw 'OEM icon readback was not confirmed.' }
        $report.iconKeeperProcessId = $windowJob.Id
        $report.changes += 'Rebuilt the taskbar button and assigned the existing OEM icon.ico; icon handles retained until the OEM GUI exits.'
    }
    $report.status = 'restarted-as-user; awaiting visual/taskbar acceptance'
} catch {
    $report.status = 'failed'
    $report.error = $_.Exception.Message
    $guiStillRunning = $report.Contains('newProcessId') -and (Get-Process -Id $report.newProcessId -ErrorAction SilentlyContinue)
    if ($changedFlag -and $flag.exists -and !$guiStillRunning) {
        try { New-ItemProperty -LiteralPath $key -Name FirstTime -Value $flag.value -PropertyType $flag.kind -Force | Out-Null; $report.changes += 'Restored original FirstTime flag after failure.' }
        catch { $report.rollbackError=$_.Exception.Message }
    } elseif ($changedFlag -and $guiStillRunning) {
        $report.flagRecovery = 'GUI still running: let OEM initialization finish and recreate FirstTime instead of restoring the hide flag during startup.'
    }
} finally {
    if ($report.Contains('chargeBefore')) {
        try {
            $report.chargeAfter = Read-Charge
            $report.powerAfter = Read-Power
            $report.serviceAfter = (Get-Service SMV4_Service).Status.ToString()
            $report.invariantsUnchanged = ($report.chargeBefore.policy -eq $report.chargeAfter.policy -and
                $report.chargeBefore.limit -eq $report.chargeAfter.limit -and
                $report.powerBefore -eq $report.powerAfter -and $report.serviceBefore -eq $report.serviceAfter)
            if (!$report.invariantsUnchanged) { $report.status='failed'; $report.verificationError='OEM startup changed charge, power or service state; no write-back attempted.' }
        } catch { $report.status='failed'; $report.verificationError=$_.Exception.Message }
    }
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $out 'repair-result.json') -Encoding utf8
}
if ($report.status -eq 'failed') { exit 1 }
