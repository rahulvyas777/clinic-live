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
    [int] $SettleMs = 1500,
    [switch] $FullScreen    # toasts live outside the window (Part 5)
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
# Physical pixels everywhere: on a 125% monitor a DPI-unaware script captures a
# quarter of the screen and calls it "full".
[Win32]::SetProcessDPIAware() | Out-Null

New-Item -ItemType Directory -Force $OutDir | Out-Null

$proc = Get-Process -Name $ProcessName -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw "No window found for $ProcessName - is the app running?" }
$hwnd = $proc.MainWindowHandle

[Win32]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds $SettleMs

# GUARD 1: Windows may refuse to bring a window forward for a script (foreground
# lock). The old version of this script trusted the rectangle anyway and, once,
# photographed the unrelated window that was covering it. Never again.
if ([Win32]::GetForegroundWindow() -ne $hwnd) {
    throw "The app window is not in front (Windows refused the focus change). Click the app and rerun - this script will not photograph whatever is covering it."
}

# DWMWA_EXTENDED_FRAME_BOUNDS (9) excludes the invisible resize borders.
$rect = New-Object Win32+RECT
[Win32]::DwmGetWindowAttribute($hwnd, 9, [ref]$rect, 16) | Out-Null

if ($FullScreen) {
    # Toasts render outside the window, so this captures the whole primary
    # screen MINUS the taskbar — and it refuses to run unless the app window is
    # maximised, so nothing but the app can be in the picture. A desktop
    # screenshot is a privacy leak waiting to be committed.
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen
    $work = $screen.WorkingArea
    $covers = ($rect.Left -le $work.Left + 16) -and ($rect.Top -le $work.Top + 16) -and
              ($rect.Right -ge $work.Right - 16) -and ($rect.Bottom -ge $work.Bottom - 16)
    if (-not $covers) { throw "Maximise the app window before a -FullScreen shot (nothing else may be visible)." }
    $rect.Left = $work.Left; $rect.Top = $work.Top; $rect.Right = $work.Right; $rect.Bottom = $work.Bottom
}

if (-not $FullScreen) {
    # The extended frame bounds include a few pixels of shadow ABOVE the title bar,
    # through which a sliver of whatever window is behind can show. Not ours; crop it.
    $rect.Top += 12
}

# GUARD 2: every corner and the centre of the rectangle must belong to OUR
# top-level window. An always-on-top window, a stale rectangle from a resize in
# progress, or a window that is partly off-screen all fail here — and the shot
# is refused rather than taken with a stranger in the frame.
$probe = @(
    @{ X = $rect.Left + 4;  Y = $rect.Top + 4 },
    @{ X = $rect.Right - 4; Y = $rect.Top + 4 },
    @{ X = $rect.Left + 4;  Y = $rect.Bottom - 4 },
    @{ X = $rect.Right - 4; Y = $rect.Bottom - 4 },
    @{ X = [int](($rect.Left + $rect.Right) / 2); Y = [int](($rect.Top + $rect.Bottom) / 2) }
)
foreach ($p in $probe) {
    $pt = New-Object Win32+POINT; $pt.X = $p.X; $pt.Y = $p.Y
    $owner = [Win32]::GetAncestor([Win32]::WindowFromPoint($pt), 2)   # GA_ROOT
    if ($owner -ne $hwnd) {
        throw "Refusing the shot: the point ($($p.X),$($p.Y)) is not on the app window. Something else is in the frame, or the window is still resizing - fix that and rerun."
    }
}

$w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
$target = Join-Path $OutDir "$Name.png"
$bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "  $Name.png  ($w x $h)"
