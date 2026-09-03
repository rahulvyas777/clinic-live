<#
.SYNOPSIS
  Photograph the Pocket app's window on Windows (the desktop build, Part 10).

.EXAMPLE
  .\Shot-Windows.ps1 -Name windows-home
#>
param(
    [Parameter(Mandatory)] [string] $Name,
    [string] $OutDir = (Join-Path $PSScriptRoot "..\..\shots\pocket"),
    [string] $ProcessName = "ClinicLive.Pocket",
    [int] $SettleMs = 1500
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);
}
"@

New-Item -ItemType Directory -Force $OutDir | Out-Null

$proc = Get-Process -Name $ProcessName -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw "No window found for $ProcessName - is the app running?" }

[Win32]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds $SettleMs

# DWMWA_EXTENDED_FRAME_BOUNDS (9) excludes the invisible resize borders.
$rect = New-Object Win32+RECT
[Win32]::DwmGetWindowAttribute($proc.MainWindowHandle, 9, [ref]$rect, 16) | Out-Null

$w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
$target = Join-Path $OutDir "$Name.png"
$bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "  $Name.png  ($w x $h)"
