$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $PSScriptRoot 'PopupSmokeTest.exe'
& $compiler /nologo /target:winexe /main:PopupSmokeTest /out:$exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\BatteryProbe.cs" "$PSScriptRoot\Receiver.cs" "$PSScriptRoot\TrayApp.cs" "$PSScriptRoot\Test-Popup.cs"
if ($LASTEXITCODE -ne 0) { throw 'Popup test build failed.' }
$test = Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru
if (-not $test.WaitForExit(15000)) { $test.Kill(); throw 'Popup test timed out.' }
Get-Content (Join-Path $PSScriptRoot 'popup-test-result.txt')
if ($test.ExitCode -ne 0) { throw 'Popup regression check failed.' }
