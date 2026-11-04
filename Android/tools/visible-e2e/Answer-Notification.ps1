param(
    [Parameter(Mandatory=$true)][int]$CaseIndex,
    [string]$Manifest = 'app/build/visible-e2e/reprueba-run.json'
)
$ErrorActionPreference = 'Stop'
$run = Get-Content -LiteralPath $Manifest -Raw -Encoding UTF8 | ConvertFrom-Json
if ($CaseIndex -lt 0 -or $CaseIndex -ge $run.cases.Count) { throw 'Invalid case index' }
$case = $run.cases[$CaseIndex]
$remaining = [long]$case.at - [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
if ($remaining -gt 45000) { throw 'Run this step within 45 seconds of the scheduled time' }
if ($remaining -gt 0) { Start-Sleep -Milliseconds ([int]$remaining + 1200) }
$adbPath = Join-Path $env:LOCALAPPDATA 'Android/Sdk/platform-tools/adb.exe'
& $adbPath -s emulator-5554 shell input keyevent 3
& $adbPath -s emulator-5554 shell cmd statusbar expand-notifications
Start-Sleep -Milliseconds 1500
$dump = & "$PSScriptRoot/Android-Ui.ps1" -Action dump
if (($dump -join '') -notmatch [regex]::Escape($case.name)) { throw "Expected notification not visible: $($case.name)" }
$label = if ($case.type -eq 'med') { if ($case.done) { 'Lo tom' + [char]0xE9 } else { 'No lo tom' + [char]0xE9 } }
         else { if ($case.done) { 'Asist' + [char]0xED } else { 'No asist' + [char]0xED } }
$controls = ($dump -join '') | ConvertFrom-Json
if (@($controls | Where-Object { $_.text -eq $label -or $_.description -eq $label }).Count -ne 1) {
    throw 'Notification action is missing or ambiguous; inspect the current screenshot before continuing'
}
& "$PSScriptRoot/Android-Ui.ps1" -Action screenshot -Name "nueva-prueba-notificacion-$CaseIndex"
& "$PSScriptRoot/Android-Ui.ps1" -Action tap -Text $label
Start-Sleep -Milliseconds 2000
& "$PSScriptRoot/Android-Ui.ps1" -Action screenshot -Name "nueva-prueba-respuesta-$CaseIndex"
$result = [pscustomobject]@{ case=$case.name; action=$label; tappedAt=[DateTimeOffset]::UtcNow.ToString('o'); notificationObserved=$true }
$result | ConvertTo-Json -Compress | Add-Content -LiteralPath 'app/build/visible-e2e/reprueba-actions.jsonl' -Encoding UTF8
$result | ConvertTo-Json -Compress
