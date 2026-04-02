param(
  [int]$ExpectedClientWidth = 640,
  [int]$ExpectedClientHeight = 360,
  [switch]$RequireExpectedClientSize,
  [switch]$Json
)

$ErrorActionPreference = "Stop"

$commonPath = Join-Path $PSScriptRoot "Rift-HelperCommon.ps1"
if (-not (Test-Path -LiteralPath $commonPath)) {
  throw "Shared RIFT helper script not found at $commonPath"
}

. $commonPath

function Get-ProcessPathSafe {
  param($Process)

  try {
    return [string]$Process.Path
  }
  catch {
    return ""
  }
}

$process = Get-ChromaLinkRiftProcess

if ($null -eq $process) {
  $result = [pscustomobject]@{
    ok = $false
    reason = "No likely RIFT window was found. Expected a rift or rift_x64 game process."
    processFound = $false
    expectedClientWidth = $ExpectedClientWidth
    expectedClientHeight = $ExpectedClientHeight
    requireExpectedClientSize = $RequireExpectedClientSize.ToBool()
    actionReady = $false
    backgroundMainBarReady = $false
    focusChatReady = $false
    captureReady = $false
    process = $null
    liveWindow = $null
    savedConfig = Get-ChromaLinkRiftSavedConfig
  }
}
else {
  $snapshot = Get-ChromaLinkRiftWindowSnapshot -Handle ([System.IntPtr]$process.MainWindowHandle)
  $savedConfig = Get-ChromaLinkRiftSavedConfig

  $hasLiveClient = ($null -ne $snapshot.ClientWidth) -and ($null -ne $snapshot.ClientHeight)
  $liveClientMatchesExpected = $hasLiveClient -and $snapshot.ClientWidth -eq $ExpectedClientWidth -and $snapshot.ClientHeight -eq $ExpectedClientHeight
  $savedResolutionMatchesLiveClient = $false
  if ($null -ne $savedConfig -and $hasLiveClient -and $savedConfig.ResolutionX -ne $null -and $savedConfig.ResolutionY -ne $null) {
    $savedResolutionMatchesLiveClient = ($savedConfig.ResolutionX -eq $snapshot.ClientWidth) -and ($savedConfig.ResolutionY -eq $snapshot.ClientHeight)
  }

  $backgroundMainBarReady = -not [bool]$snapshot.IsMinimized
  $focusChatReady = $true
  $captureReady = $backgroundMainBarReady -and $liveClientMatchesExpected
  $actionReady = $backgroundMainBarReady
  $ok = if ($RequireExpectedClientSize) { $captureReady } else { $actionReady }

  $reason =
    if ([bool]$snapshot.IsMinimized) {
      "RIFT is running but minimized."
    }
    elseif (-not $hasLiveClient) {
      "RIFT is running but the live client rectangle could not be resolved."
    }
    elseif ($RequireExpectedClientSize -and -not $liveClientMatchesExpected) {
      "RIFT is running, but the live client size does not match the expected ${ExpectedClientWidth}x${ExpectedClientHeight} profile."
    }
    else {
      "RIFT is ready for the requested helper path."
    }

  $result = [pscustomobject]@{
    ok = $ok
    reason = $reason
    processFound = $true
    expectedClientWidth = $ExpectedClientWidth
    expectedClientHeight = $ExpectedClientHeight
    requireExpectedClientSize = $RequireExpectedClientSize.ToBool()
    actionReady = $actionReady
    backgroundMainBarReady = $backgroundMainBarReady
    focusChatReady = $focusChatReady
    captureReady = $captureReady
    process = [pscustomobject]@{
      id = $process.Id
      name = $process.ProcessName
      title = $process.MainWindowTitle
      path = Get-ProcessPathSafe -Process $process
    }
    liveWindow = [pscustomobject]@{
      minimized = [bool]$snapshot.IsMinimized
      windowLeft = $snapshot.WindowLeft
      windowTop = $snapshot.WindowTop
      windowWidth = $snapshot.WindowWidth
      windowHeight = $snapshot.WindowHeight
      clientLeft = $snapshot.ClientLeft
      clientTop = $snapshot.ClientTop
      clientWidth = $snapshot.ClientWidth
      clientHeight = $snapshot.ClientHeight
      clientMatchesExpected = $liveClientMatchesExpected
    }
    savedConfig = if ($null -eq $savedConfig) {
      $null
    } else {
      [pscustomobject]@{
        path = $savedConfig.Path
        documentsDirectory = $savedConfig.DocumentsDirectory
        playInBackground = $savedConfig.PlayInBackground
        resolutionX = $savedConfig.ResolutionX
        resolutionY = $savedConfig.ResolutionY
        windowMode = $savedConfig.WindowMode
        topX = $savedConfig.TopX
        topY = $savedConfig.TopY
        lockWindowSize = $savedConfig.LockWindowSize
        savedResolutionMatchesLiveClient = $savedResolutionMatchesLiveClient
      }
    }
  }
}

if ($Json) {
  $result | ConvertTo-Json -Depth 6
}
else {
  Write-Host "RIFT Input Readiness" -ForegroundColor Cyan
  Write-Host ("Ready: {0}" -f $result.ok.ToString().ToLowerInvariant())
  Write-Host ("Reason: {0}" -f $result.reason)
  Write-Host ("ActionReady: {0}" -f $result.actionReady.ToString().ToLowerInvariant())
  Write-Host ("BackgroundMainBarReady: {0}" -f $result.backgroundMainBarReady.ToString().ToLowerInvariant())
  Write-Host ("FocusChatReady: {0}" -f $result.focusChatReady.ToString().ToLowerInvariant())
  Write-Host ("CaptureReady: {0}" -f $result.captureReady.ToString().ToLowerInvariant())
  Write-Host ("ExpectedClient: {0}x{1}" -f $result.expectedClientWidth, $result.expectedClientHeight)
  Write-Host ("RequireExpectedClientSize: {0}" -f $result.requireExpectedClientSize.ToString().ToLowerInvariant())

  if ($null -ne $result.process) {
    Write-Host ""
    Write-Host "Process"
    Write-Host ("Pid: {0}" -f $result.process.id)
    Write-Host ("Name: {0}" -f $result.process.name)
    Write-Host ("Title: {0}" -f $result.process.title)
    if (-not [string]::IsNullOrWhiteSpace([string]$result.process.path)) {
      Write-Host ("Path: {0}" -f $result.process.path)
    }
  }

  if ($null -ne $result.liveWindow) {
    Write-Host ""
    Write-Host "Live Window"
    Write-Host ("Minimized: {0}" -f $result.liveWindow.minimized.ToString().ToLowerInvariant())
    Write-Host ("WindowRect: {0},{1} {2}x{3}" -f $result.liveWindow.windowLeft, $result.liveWindow.windowTop, $result.liveWindow.windowWidth, $result.liveWindow.windowHeight)
    Write-Host ("ClientRect: {0},{1} {2}x{3}" -f $result.liveWindow.clientLeft, $result.liveWindow.clientTop, $result.liveWindow.clientWidth, $result.liveWindow.clientHeight)
    Write-Host ("ClientMatchesExpected: {0}" -f $result.liveWindow.clientMatchesExpected.ToString().ToLowerInvariant())
  }

  if ($null -ne $result.savedConfig) {
    Write-Host ""
    Write-Host "Saved Config"
    Write-Host ("Path: {0}" -f $result.savedConfig.path)
    if ($result.savedConfig.resolutionX -ne $null -and $result.savedConfig.resolutionY -ne $null) {
      Write-Host ("Resolution: {0}x{1}" -f $result.savedConfig.resolutionX, $result.savedConfig.resolutionY)
    }
    if ($result.savedConfig.windowMode -ne $null) {
      Write-Host ("WindowMode: {0}" -f $result.savedConfig.windowMode)
    }
    if ($result.savedConfig.topX -ne $null -and $result.savedConfig.topY -ne $null) {
      Write-Host ("TopLeft: {0},{1}" -f $result.savedConfig.topX, $result.savedConfig.topY)
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$result.savedConfig.playInBackground)) {
      Write-Host ("PlayInBackground: {0}" -f $result.savedConfig.playInBackground)
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$result.savedConfig.documentsDirectory)) {
      Write-Host ("DocumentsDirectory: {0}" -f $result.savedConfig.documentsDirectory)
    }
    Write-Host ("SavedResolutionMatchesLiveClient: {0}" -f $result.savedConfig.savedResolutionMatchesLiveClient.ToString().ToLowerInvariant())
  }
}

if (-not $result.ok) {
  exit 1
}

exit 0
