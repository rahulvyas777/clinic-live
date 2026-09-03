<#
.SYNOPSIS
  Turn the Android emulator's virtual-scene camera (the "virtualscene" back camera).

.DESCRIPTION
  The emulator lets you look around its virtual room by holding Alt and moving the
  mouse inside the emulator window. This script does exactly that, programmatically,
  so a screenshot loop can point the phone's camera at a poster — e.g. a ticket QR
  loaded with `adb emu virtualscene-image wall <png>` (Part 8).

  It only ever touches the emulator window this harness started.

.EXAMPLE
  .\Look-Around.ps1 -Dx -200        # turn left
  .\Look-Around.ps1 -Dx 150 -Dy 40  # turn right and tilt down a little
#>
param(
    [int] $Dx = 0,
    [int] $Dy = 0,
    [int] $StepPx = 12
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class EmuLook {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
}
"@
[EmuLook]::SetProcessDPIAware() | Out-Null

$emu = Get-Process -Name "qemu-system-x86_64" -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $emu) { throw "No emulator window found." }

[EmuLook]::ShowWindow($emu.MainWindowHandle, 9) | Out-Null      # restore
[EmuLook]::SetForegroundWindow($emu.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 600

$r = New-Object EmuLook+RECT
[EmuLook]::GetWindowRect($emu.MainWindowHandle, [ref]$r) | Out-Null
[System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int](($r.L + $r.R) / 2), [int](($r.T + $r.B) / 2))
Start-Sleep -Milliseconds 300

# hold Alt, glide the mouse, release Alt
[EmuLook]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 150
$steps = [Math]::Max([Math]::Abs($Dx), [Math]::Abs($Dy)) / $StepPx
if ($steps -lt 1) { $steps = 1 }
$sx = [int]($Dx / $steps); $sy = [int]($Dy / $steps)
for ($i = 0; $i -lt $steps; $i++) { [EmuLook]::mouse_event(1, $sx, $sy, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 30 }
Start-Sleep -Milliseconds 150
[EmuLook]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)

[EmuLook]::ShowWindow($emu.MainWindowHandle, 6) | Out-Null      # minimise again
Write-Host "  turned dx=$Dx dy=$Dy"
