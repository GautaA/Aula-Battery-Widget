$ErrorActionPreference = 'Stop'
Add-Type -Path @((Join-Path $PSScriptRoot 'BatteryProbe.cs'), (Join-Path $PSScriptRoot 'RazerBatteryProbe.cs'))

function New-Reply([byte]$command, [byte]$value) {
    $frame = [byte[]]::new(91)
    $frame[1] = 2
    $frame[2] = 0x1f
    $frame[6] = 2
    $frame[7] = 7
    $frame[8] = $command
    $frame[10] = $value
    for ($i = 3; $i -lt 89; $i++) { $frame[89] = $frame[89] -bxor $frame[$i] }
    return ,$frame
}
function Assert-Rejected([byte[]]$frame, [byte]$command, [string]$reason) {
    $rejected = $false
    try { [void][RazerBatteryProbe]::Decode($frame, $command) } catch { $rejected = $true }
    if (-not $rejected) { throw "Accepted invalid reply: $reason" }
}

foreach ($raw in @(0, 115, 255)) {
    $frame = New-Reply 0x80 $raw
    if ([RazerBatteryProbe]::Decode($frame, 0x80) -ne $raw) { throw 'Battery value was lost' }
}
$frame = New-Reply 0x80 115
$frame[10] = 250
Assert-Rejected $frame 0x80 'corrupt percentage'
$frame = New-Reply 0x84 1
Assert-Rejected $frame 0x80 'charging reply consumed as a percentage'
$frame = New-Reply 0x80 115
$frame[2] = 0x3f
Assert-Rejected $frame 0x80 'another transaction'
$frame = New-Reply 0x80 0
$frame[1] = 4
Assert-Rejected $frame 0x80 'sleeping mouse mistaken for zero battery'
Assert-Rejected (New-Reply 0x84 2) 0x84 'unknown charging status'
Assert-Rejected ([byte[]]::new(90)) 0x80 'truncated reply'
$request = [RazerBatteryProbe]::Request(0x80)
if ($request.Length -ne 91 -or $request[89] -ne 0x85) { throw 'Incorrect battery getter frame' }
'Razer protocol checks passed (valid levels, corruption, reply matching, sleep, charging, frame size).'
