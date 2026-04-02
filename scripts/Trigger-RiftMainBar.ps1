param(
  [Parameter(Mandatory = $true, Position = 0)]
  [ValidateRange(1, 12)]
  [int]$Slot,

  [switch]$Background,

  [switch]$Focus,

  [switch]$DryRun
)

Add-Type -AssemblyName System.Windows.Forms

$commonPath = Join-Path $PSScriptRoot "Rift-HelperCommon.ps1"
if (-not (Test-Path -LiteralPath $commonPath)) {
    throw "Shared RIFT helper script not found at $commonPath"
}

. $commonPath

$signature = @"
using System;
using System.Runtime.InteropServices;

public static class ChromaLinkMainBarTools
{
    public const int SwRestore = 9;
    public const int WmKeyDown = 0x0100;
    public const int WmKeyUp = 0x0101;

    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);
}
"@

Add-Type -TypeDefinition $signature -Language CSharp

$slotBindings = @{
  1 = @{ KeyLabel = '1'; SendKeysText = '1'; VirtualKey = [uint32]0x31 }
  2 = @{ KeyLabel = '2'; SendKeysText = '2'; VirtualKey = [uint32]0x32 }
  3 = @{ KeyLabel = '3'; SendKeysText = '3'; VirtualKey = [uint32]0x33 }
  4 = @{ KeyLabel = '4'; SendKeysText = '4'; VirtualKey = [uint32]0x34 }
  5 = @{ KeyLabel = '5'; SendKeysText = '5'; VirtualKey = [uint32]0x35 }
  6 = @{ KeyLabel = '6'; SendKeysText = '6'; VirtualKey = [uint32]0x36 }
  7 = @{ KeyLabel = '7'; SendKeysText = '7'; VirtualKey = [uint32]0x37 }
  8 = @{ KeyLabel = '8'; SendKeysText = '8'; VirtualKey = [uint32]0x38 }
  9 = @{ KeyLabel = '9'; SendKeysText = '9'; VirtualKey = [uint32]0x39 }
  10 = @{ KeyLabel = '0'; SendKeysText = '0'; VirtualKey = [uint32]0x30 }
  11 = @{ KeyLabel = '-'; SendKeysText = '-'; VirtualKey = [uint32]0xBD }
  12 = @{ KeyLabel = '='; SendKeysText = '='; VirtualKey = [uint32]0xBB }
}

$binding = $slotBindings[$Slot]
if (-not $binding) {
  throw "Unsupported main bar slot: $Slot"
}

if ($Background -and $Focus) {
  throw "Choose either -Background or -Focus, not both."
}

$process = Get-ChromaLinkRiftProcess

if (-not $process) {
    throw "No likely RIFT window was found. Expected a rift or rift_x64 game process."
}

$handle = $process.MainWindowHandle
$useBackground = $Background -or (-not $Focus)
$modeLabel = if ($useBackground) { "background-postmessage" } else { "focus-sendkeys" }
$windowSnapshot = Get-ChromaLinkRiftWindowSnapshot -Handle ([System.IntPtr]$handle)
$isMinimized = [bool]$windowSnapshot.IsMinimized

if ($DryRun) {
    Write-ChromaLinkRiftDryRunSummary -Title "ChromaLink Main Bar Dry Run" -Process $process -Mode $modeLabel -Slot $Slot -KeyLabel $binding.KeyLabel
    if ($useBackground -and $isMinimized) {
        Write-Host "Would fail: background mode requires a non-minimized RIFT window." -ForegroundColor Yellow
    }
    return
}

if ($useBackground) {
    if ($isMinimized) {
        throw "RIFT is minimized. Background PostMessage mode requires the game window to stay open even if it is not focused."
    }

    $scanCode = [ChromaLinkMainBarTools]::MapVirtualKey($binding.VirtualKey, 0)
    $keyDownLParam = [IntPtr](1 -bor ($scanCode -shl 16))
    $keyUpLParam = [IntPtr](1 -bor ($scanCode -shl 16) -bor 0xC0000000)

    $downOk = [ChromaLinkMainBarTools]::PostMessage($handle, [uint32][ChromaLinkMainBarTools]::WmKeyDown, [IntPtr]$binding.VirtualKey, $keyDownLParam)
    Start-Sleep -Milliseconds 40
    $upOk = [ChromaLinkMainBarTools]::PostMessage($handle, [uint32][ChromaLinkMainBarTools]::WmKeyUp, [IntPtr]$binding.VirtualKey, $keyUpLParam)

    if (-not ($downOk -and $upOk)) {
        throw ("PostMessage failed for main bar slot {0} ({1})." -f $Slot, $binding.KeyLabel)
    }

    Write-Host ("Posted main bar slot {0} via key {1} to the RIFT window without changing focus." -f $Slot, $binding.KeyLabel) -ForegroundColor Yellow
    return
}

if ($isMinimized) {
    [void][ChromaLinkMainBarTools]::ShowWindowAsync($handle, [ChromaLinkMainBarTools]::SwRestore)
    Start-Sleep -Milliseconds 250
}

$shell = New-Object -ComObject WScript.Shell

$activated = $false
for ($attempt = 0; $attempt -lt 5; $attempt++) {
    [void][ChromaLinkMainBarTools]::SetForegroundWindow($handle)
    Start-Sleep -Milliseconds 100
    $null = $shell.AppActivate($process.Id)
    Start-Sleep -Milliseconds 100

    if ([ChromaLinkMainBarTools]::GetForegroundWindow() -eq $handle) {
        $activated = $true
        break
    }

    [System.Windows.Forms.SendKeys]::SendWait("%")
    Start-Sleep -Milliseconds 100
}

if (-not $activated) {
    throw "RIFT did not become the foreground window. Aborting to avoid sending input into another app."
}

Start-Sleep -Milliseconds 100
[System.Windows.Forms.SendKeys]::SendWait($binding.SendKeysText)

Write-Host ("Triggered main bar slot {0} via key {1} on the RIFT window." -f $Slot, $binding.KeyLabel) -ForegroundColor Green
