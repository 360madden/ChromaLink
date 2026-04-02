param()

Add-Type -AssemblyName System.Windows.Forms

$signature = @"
using System;
using System.Runtime.InteropServices;

public static class ChromaLinkReloadTools
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

$process = Get-Process |
    Where-Object {
        $_.MainWindowHandle -ne 0 -and (
            $_.ProcessName -ieq 'rift' -or
            $_.ProcessName -ieq 'rift_x64' -or
            ($_.MainWindowTitle -like 'RIFT*' -and $_.MainWindowTitle -notlike '*Minion*')
        )
    } |
    Select-Object -First 1

if (-not $process) {
    throw "No likely RIFT window was found."
}

$handle = $process.MainWindowHandle
if ([ChromaLinkReloadTools]::IsIconic($handle)) {
    [void][ChromaLinkReloadTools]::ShowWindowAsync($handle, [ChromaLinkReloadTools]::SwRestore)
    Start-Sleep -Milliseconds 250
}

$shell = New-Object -ComObject WScript.Shell

$activated = $false
for ($attempt = 0; $attempt -lt 5; $attempt++) {
    [void][ChromaLinkReloadTools]::SetForegroundWindow($handle)
    Start-Sleep -Milliseconds 100
    $null = $shell.AppActivate($process.Id)
    Start-Sleep -Milliseconds 100

    if ([ChromaLinkReloadTools]::GetForegroundWindow() -eq $handle) {
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
[System.Windows.Forms.SendKeys]::SendWait("/reloadui")
Start-Sleep -Milliseconds 150
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")

Write-Host "Sent /reloadui to the RIFT window." -ForegroundColor Green
