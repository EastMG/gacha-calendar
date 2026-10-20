# Capture README screenshots for GachaCalendar using PrintWindow.
# Usage: powershell -NoProfile -File tools/capture-shot.ps1 [-Exe <path>]
# Prints "DOCK=..." / "FLYOUT=..." paths on success.

param(
	[string]$Exe = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class Shot {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$repo = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) {
	$Exe = Join-Path $repo "src\GachaCalendar\bin\Release\net10.0-windows\GachaCalendar.exe"
}
if (-not (Test-Path $Exe)) { throw "exe not found: $Exe" }

$imgDir = Join-Path $repo "docs\images"
New-Item -ItemType Directory -Path $imgDir -Force | Out-Null

Get-Process GachaCalendar -ErrorAction SilentlyContinue | Stop-Process -Force
$env:GACHA_CALENDAR_DATA = Join-Path $repo "data\sample-snapshot.json"
$outLog = Join-Path $imgDir "_o.txt"
$errLog = Join-Path $imgDir "_e.txt"
$p = Start-Process -FilePath $Exe -ArgumentList "--show-flyout" -PassThru -RedirectStandardOutput $outLog -RedirectStandardError $errLog
Start-Sleep -Seconds 4

$targetPid = $p.Id
$wins = New-Object System.Collections.ArrayList
[Shot]::EnumWindows({
	param($h, $l)
	$winPid = 0
	[void][Shot]::GetWindowThreadProcessId($h, [ref]$winPid)
	if ($winPid -eq $targetPid -and [Shot]::IsWindowVisible($h)) {
		$r = New-Object Shot+RECT
		[void][Shot]::GetWindowRect($h, [ref]$r)
		$t = New-Object System.Text.StringBuilder 256
		[void][Shot]::GetWindowTextW($h, $t, 256)
		if ($t.ToString() -eq "二游日历") {
			[void]$wins.Add([pscustomobject]@{ Hwnd = $h; W = $r.Right - $r.Left; H = $r.Bottom - $r.Top; Title = $t.ToString() })
		}
	}
	return $true
}, [IntPtr]::Zero) | Out-Null

# Dock strip is the short one, flyout the tall one.
$dock = $wins | Sort-Object H | Select-Object -First 1
$flyout = $wins | Sort-Object H -Descending | Select-Object -First 1
if ($null -eq $dock) { throw "no visible app window found" }

function Save-Window([IntPtr]$hwnd, [int]$w, [int]$h, [string]$path) {
	$scale = [Shot]::GetDpiForWindow($hwnd) / 96.0
	if ($scale -le 0) { $scale = 1.0 }
	$w = [int][Math]::Round($w * $scale)
	$h = [int][Math]::Round($h * $scale)
	$bmp = New-Object System.Drawing.Bitmap($w, $h)
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$hdc = $g.GetHdc()
	$ok = [Shot]::PrintWindow($hwnd, $hdc, 2)  # PW_RENDERFULLCONTENT
	$g.ReleaseHdc($hdc)
	$g.Dispose()
	$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
	$bmp.Dispose()
	return $ok
}

[string]$dockPath = Join-Path $imgDir "dock.png"
[string]$flyoutPath = Join-Path $imgDir "flyout.png"
$ok1 = Save-Window $dock.Hwnd $dock.W $dock.H $dockPath
"DOCK=$dockPath OK=$ok1 SIZE=$($dock.W)x$($dock.H)"
if ($null -ne $flyout -and $flyout.Hwnd -ne $dock.Hwnd) {
	$ok2 = Save-Window $flyout.Hwnd $flyout.W $flyout.H $flyoutPath
	"FLYOUT=$flyoutPath OK=$ok2 SIZE=$($flyout.W)x$($flyout.H)"
}

Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
