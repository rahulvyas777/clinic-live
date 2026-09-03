<#
.SYNOPSIS
  Photograph the Pocket app on the Android emulator (or a USB-connected phone).

.EXAMPLE
  .\Shot-Android.ps1 -Name home -OutDir ..\..\shots\pocket
  .\Shot-Android.ps1 -Name visit -Deeplink "cliniclive://visit/DEMO00"

  The screenshot loop for a phone app is two adb calls. Every screenshot in the
  From Prompt to Pocket posts came out of this script — no mock-ups.
#>
param(
    [Parameter(Mandatory)] [string] $Name,
    [string] $OutDir = (Join-Path $PSScriptRoot "..\..\shots\pocket"),
    [string] $Package = "com.cliniclive.pocket",
    [switch] $Launch,
    [int] $SettleMs = 1500
)

$adb = Join-Path ${env:ProgramFiles(x86)} "Android\android-sdk\platform-tools\adb.exe"
if (-not (Test-Path $adb)) { $adb = "adb" }

New-Item -ItemType Directory -Force $OutDir | Out-Null

if ($Launch) {
    # monkey finds the launcher activity for us — MAUI mangles the activity name.
    & $adb shell monkey -p $Package -c android.intent.category.LAUNCHER 1 | Out-Null
    Start-Sleep -Milliseconds 3500
}

Start-Sleep -Milliseconds $SettleMs
& $adb shell screencap -p /sdcard/pocket-shot.png
$target = Join-Path $OutDir "$Name.png"
& $adb pull /sdcard/pocket-shot.png $target | Out-Null
& $adb shell rm /sdcard/pocket-shot.png
Write-Host "  $Name.png  ($([math]::Round((Get-Item $target).Length / 1KB)) KB)"
