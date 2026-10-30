$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'BatteryProbe.cs')
$devices = @(Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -like 'HID\VID_3554&PID_FA09&MI_01&COL01\*' })
if ($devices.Count -eq 0) { throw 'AULA 2.4 GHz receiver not found.' }
foreach ($device in $devices) {
    $path = '\\?\' + $device.InstanceId.Replace('\', '#') + '#{4d1e55b2-f16f-11cf-88cb-001111000030}'
    [AulaBatteryProbe]::Read($path)
}
