if (-not ("ChromaLinkRiftCommonWin32" -as [type])) {
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class ChromaLinkRiftCommonWin32
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetClientRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ClientToScreen(IntPtr hwnd, ref Point point);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsIconic(IntPtr hwnd);
}
"@
}

function Get-ChromaLinkRiftProcess {
    Get-Process |
        Where-Object {
            $_.MainWindowHandle -ne 0 -and (
                $_.ProcessName -ieq 'rift' -or
                $_.ProcessName -ieq 'rift_x64'
            )
        } |
        Select-Object -First 1
}

function Get-ChromaLinkRiftConfigPath {
    Join-Path $env:APPDATA "RIFT\rift.cfg"
}

function Get-ChromaLinkRiftSavedConfig {
    $path = Get-ChromaLinkRiftConfigPath
    if (-not (Test-Path -LiteralPath $path)) {
        return $null
    }

    $sections = @{}
    $currentSection = ""
    foreach ($rawLine in Get-Content -LiteralPath $path) {
        $line = $rawLine.Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith(";") -or $line.StartsWith("#")) {
            continue
        }

        if ($line -match '^\[(.+)\]$') {
            $currentSection = $matches[1]
            if (-not $sections.ContainsKey($currentSection)) {
                $sections[$currentSection] = @{}
            }
            continue
        }

        if ($line -match '^([^=]+?)\s*=\s*(.*)$') {
            if (-not $sections.ContainsKey($currentSection)) {
                $sections[$currentSection] = @{}
            }

            $sections[$currentSection][$matches[1].Trim()] = $matches[2].Trim()
        }
    }

    $client = if ($sections.ContainsKey("Client")) { $sections["Client"] } else { @{} }
    $video = if ($sections.ContainsKey("Video")) { $sections["Video"] } else { @{} }
    $window = if ($sections.ContainsKey("Window")) { $sections["Window"] } else { @{} }

    $resolutionX = $null
    $resolutionY = $null
    $topX = $null
    $topY = $null
    $windowMode = $null

    [void][int]::TryParse([string]$video["ResolutionX"], [ref]$resolutionX)
    [void][int]::TryParse([string]$video["ResolutionY"], [ref]$resolutionY)
    [void][int]::TryParse([string]$window["TopX"], [ref]$topX)
    [void][int]::TryParse([string]$window["TopY"], [ref]$topY)
    [void][int]::TryParse([string]$video["WindowMode"], [ref]$windowMode)

    [pscustomobject]@{
        Path = $path
        DocumentsDirectory = [string]$client["DocumentsDirectory"]
        PlayInBackground = [string]$client["PlayInBackground"]
        ResolutionX = $resolutionX
        ResolutionY = $resolutionY
        WindowMode = $windowMode
        TopX = $topX
        TopY = $topY
        LockWindowSize = [string]$window["LockWindowSize"]
        RawSections = $sections
    }
}

function Get-ChromaLinkRiftWindowSnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [System.IntPtr]$Handle
    )

    $windowRect = New-Object ChromaLinkRiftCommonWin32+Rect
    $clientRect = New-Object ChromaLinkRiftCommonWin32+Rect
    $point = New-Object ChromaLinkRiftCommonWin32+Point

    $haveWindowRect = [ChromaLinkRiftCommonWin32]::GetWindowRect($Handle, [ref]$windowRect)
    $haveClientRect = [ChromaLinkRiftCommonWin32]::GetClientRect($Handle, [ref]$clientRect)
    $haveClientPoint = $haveClientRect -and [ChromaLinkRiftCommonWin32]::ClientToScreen($Handle, [ref]$point)

    [pscustomobject]@{
        IsMinimized = [ChromaLinkRiftCommonWin32]::IsIconic($Handle)
        WindowLeft = if ($haveWindowRect) { $windowRect.Left } else { $null }
        WindowTop = if ($haveWindowRect) { $windowRect.Top } else { $null }
        WindowWidth = if ($haveWindowRect) { $windowRect.Right - $windowRect.Left } else { $null }
        WindowHeight = if ($haveWindowRect) { $windowRect.Bottom - $windowRect.Top } else { $null }
        ClientLeft = if ($haveClientPoint) { $point.X } else { $null }
        ClientTop = if ($haveClientPoint) { $point.Y } else { $null }
        ClientWidth = if ($haveClientRect) { $clientRect.Right - $clientRect.Left } else { $null }
        ClientHeight = if ($haveClientRect) { $clientRect.Bottom - $clientRect.Top } else { $null }
    }
}

function Write-ChromaLinkRiftDryRunSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Title,

        [Parameter(Mandatory = $true)]
        $Process,

        [Parameter(Mandatory = $true)]
        [string]$Mode,

        [string]$CommandText = "",
        $Slot = $null,
        [string]$KeyLabel = ""
    )

    $snapshot = Get-ChromaLinkRiftWindowSnapshot -Handle ([System.IntPtr]$Process.MainWindowHandle)
    $savedConfig = Get-ChromaLinkRiftSavedConfig
    $processPath = ""
    try {
        $processPath = [string]$Process.Path
    }
    catch {
        $processPath = ""
    }

    Write-Host $Title -ForegroundColor Cyan
    Write-Host ("Pid: {0}" -f $Process.Id)
    Write-Host ("Process: {0}" -f $Process.ProcessName)
    Write-Host ("WindowTitle: {0}" -f $Process.MainWindowTitle)
    if (-not [string]::IsNullOrWhiteSpace($processPath)) {
        Write-Host ("ProcessPath: {0}" -f $processPath)
    }
    Write-Host ("Mode: {0}" -f $Mode)
    Write-Host ("Minimized: {0}" -f $snapshot.IsMinimized.ToString().ToLowerInvariant())
    if ($null -ne $Slot) {
        Write-Host ("Slot: {0}" -f $Slot)
    }
    if (-not [string]::IsNullOrWhiteSpace($KeyLabel)) {
        Write-Host ("Key: {0}" -f $KeyLabel)
    }
    if (-not [string]::IsNullOrWhiteSpace($CommandText)) {
        Write-Host ("CommandText: {0}" -f $CommandText)
    }

    Write-Host ("LiveWindow: {0},{1} {2}x{3}" -f $snapshot.WindowLeft, $snapshot.WindowTop, $snapshot.WindowWidth, $snapshot.WindowHeight)
    Write-Host ("LiveClient: {0},{1} {2}x{3}" -f $snapshot.ClientLeft, $snapshot.ClientTop, $snapshot.ClientWidth, $snapshot.ClientHeight)

    if ($null -ne $savedConfig) {
        Write-Host ("SavedConfigPath: {0}" -f $savedConfig.Path)
        if ($savedConfig.ResolutionX -ne $null -and $savedConfig.ResolutionY -ne $null) {
            Write-Host ("SavedResolution: {0}x{1}" -f $savedConfig.ResolutionX, $savedConfig.ResolutionY)
            if ($snapshot.ClientWidth -ne $null -and $snapshot.ClientHeight -ne $null) {
                $matches = ($snapshot.ClientWidth -eq $savedConfig.ResolutionX) -and ($snapshot.ClientHeight -eq $savedConfig.ResolutionY)
                Write-Host ("SavedResolutionMatchesLiveClient: {0}" -f $matches.ToString().ToLowerInvariant())
            }
        }
        if ($savedConfig.WindowMode -ne $null) {
            Write-Host ("SavedWindowMode: {0}" -f $savedConfig.WindowMode)
        }
        if ($savedConfig.TopX -ne $null -and $savedConfig.TopY -ne $null) {
            Write-Host ("SavedWindowTopLeft: {0},{1}" -f $savedConfig.TopX, $savedConfig.TopY)
        }
        if (-not [string]::IsNullOrWhiteSpace($savedConfig.PlayInBackground)) {
            Write-Host ("SavedPlayInBackground: {0}" -f $savedConfig.PlayInBackground)
        }
        if (-not [string]::IsNullOrWhiteSpace($savedConfig.DocumentsDirectory)) {
            Write-Host ("SavedDocumentsDirectory: {0}" -f $savedConfig.DocumentsDirectory)
        }
    }
    else {
        Write-Host "SavedConfigPath: missing"
    }
}
