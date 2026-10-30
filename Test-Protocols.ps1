$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $PSScriptRoot 'ProtocolTests.exe'
& $compiler /nologo /target:exe /out:$exe "$PSScriptRoot\BatteryProbe.cs" "$PSScriptRoot\HidTransport.cs" "$PSScriptRoot\KeyboardProfiles.cs" "$PSScriptRoot\Test-Protocols.cs"
if ($LASTEXITCODE -ne 0) { throw 'Protocol test build failed.' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Protocol checks failed.' }
