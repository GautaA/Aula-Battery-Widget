param([string]$OutputPath = (Join-Path $PSScriptRoot 'AulaF75Battery.exe'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /optimize+ /out:$OutputPath /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\BatteryProbe.cs" "$PSScriptRoot\HidTransport.cs" "$PSScriptRoot\KeyboardProfiles.cs" "$PSScriptRoot\Receiver.cs" "$PSScriptRoot\TrayApp.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
