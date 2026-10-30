param(
    [Parameter(Mandatory=$true)][ValidateSet('dump','tap','type','swipe','back','home','screenshot')][string]$Action,
    [string]$Text,
    [switch]$Contains,
    [string]$Device = 'emulator-5554',
    [string]$Name = 'android'
)
$ErrorActionPreference = 'Stop'
$adbPath = Join-Path $env:LOCALAPPDATA 'Android/Sdk/platform-tools/adb.exe'
function Invoke-Adb { & $adbPath -s $Device @args }
function Read-Ui {
    for ($readAttempt = 0; $readAttempt -lt 4; $readAttempt++) {
        $dump = (Invoke-Adb shell uiautomator dump /sdcard/acompaar-test-ui.xml 2>&1) -join "`n"
        if ($dump -match 'dumped to') {
            $xmlText = (Invoke-Adb exec-out cat /sdcard/acompaar-test-ui.xml) -join "`n"
            return [xml]$xmlText
        }
        Start-Sleep -Milliseconds 400
    }
    throw 'Android did not return a current UI hierarchy'
}
switch ($Action) {
    'dump' {
        $ui = Read-Ui
        $ui.SelectNodes('//node') | Where-Object { $_.text -or $_.'content-desc' } |
            ForEach-Object { [pscustomobject]@{text=$_.text; description=$_.'content-desc'; class=$_.class; bounds=$_.bounds; id=$_.'resource-id'} } |
            ConvertTo-Json -Depth 3 -Compress
    }
    'tap' {
        for ($attempt = 0; $attempt -lt 5; $attempt++) {
            $ui = Read-Ui
            $node = $ui.SelectNodes('//node') | Where-Object {
                if ($Contains) { $_.text.Contains($Text) -or $_.'content-desc'.Contains($Text) }
                else { $_.text -eq $Text -or $_.'content-desc' -eq $Text }
            } | Select-Object -First 1
            if ($node) {
                $numbers = [regex]::Matches($node.bounds, '\d+') | ForEach-Object { [int]$_.Value }
                $tapX = [int](($numbers[0] + $numbers[2]) / 2)
                $tapY = [int](($numbers[1] + $numbers[3]) / 2)
                Invoke-Adb shell input tap $tapX $tapY
                Write-Output "Tapped: $Text"
                exit 0
            }
            Invoke-Adb shell input swipe 540 1900 540 700 500
        }
        throw "Visible control not found: $Text"
    }
    'type' { Invoke-Adb shell input text ($Text.Replace(' ', '%s')); Write-Output 'Typed into focused field' }
    'swipe' { Invoke-Adb shell input swipe 540 1900 540 700 500 }
    'back' { Invoke-Adb shell input keyevent 4 }
    'home' { Invoke-Adb shell input keyevent 3 }
    'screenshot' {
        $androidRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
        $artifactRoot = Join-Path $androidRoot 'app/build/visible-e2e'
        New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
        Invoke-Adb shell screencap -p /sdcard/acompaar-test-screen.png
        Invoke-Adb pull /sdcard/acompaar-test-screen.png (Join-Path $artifactRoot "$Name.png")
    }
}
