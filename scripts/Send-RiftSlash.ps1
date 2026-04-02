param(
  [Parameter(Mandatory = $true)]
  [string]$CommandText,

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

public static class ChromaLinkSlashTools
{
    public const int SwRestore = 9;

    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
}
"@

Add-Type -TypeDefinition $signature -Language CSharp

$process = Get-ChromaLinkRiftProcess

if (-not $process) {
    throw "No likely RIFT window was found. Expected a rift or rift_x64 game process."
}

$handle = $process.MainWindowHandle
$windowSnapshot = Get-ChromaLinkRiftWindowSnapshot -Handle ([System.IntPtr]$handle)
$isMinimized = [bool]$windowSnapshot.IsMinimized

if ($DryRun) {
    Write-ChromaLinkRiftDryRunSummary -Title "ChromaLink Slash Dry Run" -Process $process -Mode "focus-sendkeys-chat" -CommandText $CommandText
    return
}

if ($isMinimized) {
    [void][ChromaLinkSlashTools]::ShowWindowAsync($handle, [ChromaLinkSlashTools]::SwRestore)
    Start-Sleep -Milliseconds 250
}

$shell = New-Object -ComObject WScript.Shell

$activated = $false
for ($attempt = 0; $attempt -lt 5; $attempt++) {
    [void][ChromaLinkSlashTools]::SetForegroundWindow($handle)
    Start-Sleep -Milliseconds 100
    $null = $shell.AppActivate($process.Id)
    Start-Sleep -Milliseconds 100

    if ([ChromaLinkSlashTools]::GetForegroundWindow() -eq $handle) {
        $activated = $true
        break
    }

    [System.Windows.Forms.SendKeys]::SendWait("%")
    Start-Sleep -Milliseconds 100
}

if (-not $activated) {
    throw "RIFT did not become the foreground window. Aborting to avoid typing into another app."
}

[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 150
[System.Windows.Forms.SendKeys]::SendWait($CommandText)
Start-Sleep -Milliseconds 150
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")

Write-Host ("Sent {0} to the RIFT window." -f $CommandText) -ForegroundColor Green
