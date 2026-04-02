param(
  [string]$OutputRoot = "",
  [string]$Configuration = "Release",
  [string]$Runtime = "win-x64",
  [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
  $OutputRoot = Join-Path $repoRoot "artifacts\package"
}

$desktopRoot = Join-Path $OutputRoot "desktop"

$sourceCommit = "unknown"
try {
  $commitResult = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
  if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($commitResult)) {
    $sourceCommit = ($commitResult | Select-Object -First 1).Trim()
  }
} catch {
}

$packageVersion = if ($sourceCommit -eq "unknown") { "0.1.0-dev" } else { "0.1.0-dev+$sourceCommit" }

$projects = @(
  [pscustomobject]@{
    Name = "ChromaLink.Cli"
    Project = Join-Path $repoRoot "DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj"
  },
  [pscustomobject]@{
    Name = "ChromaLink.HttpBridge"
    Project = Join-Path $repoRoot "DesktopDotNet\ChromaLink.HttpBridge\ChromaLink.HttpBridge.csproj"
  },
  [pscustomobject]@{
    Name = "ChromaLink.Inspector"
    Project = Join-Path $repoRoot "DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj"
  },
  [pscustomobject]@{
    Name = "ChromaLink.Monitor"
    Project = Join-Path $repoRoot "DesktopDotNet\ChromaLink.Monitor\ChromaLink.Monitor.csproj"
  }
)

foreach ($project in $projects) {
  if (-not (Test-Path $project.Project)) {
    throw "Project not found: $($project.Project)"
  }
}

if (Test-Path $OutputRoot) {
  Remove-Item -LiteralPath $OutputRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $desktopRoot -Force | Out-Null

$published = @()
$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

foreach ($project in $projects) {
  $projectOutput = Join-Path $desktopRoot $project.Name
  New-Item -ItemType Directory -Path $projectOutput -Force | Out-Null

  $restoreArgs = @(
    "restore",
    $project.Project,
    "--runtime", $Runtime,
    "-p:SelfContained=$selfContainedValue",
    "--force-evaluate"
  )

  Write-Host "Restoring $($project.Name) for $Runtime" -ForegroundColor DarkCyan
  & dotnet @restoreArgs
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed for $($project.Name) with exit code $LASTEXITCODE"
  }

  $args = @(
    "publish",
    $project.Project,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained", $selfContainedValue,
    "--no-restore",
    "--output", $projectOutput,
    "-p:PublishSingleFile=false",
    "-p:PublishTrimmed=false",
    "-p:PublishReadyToRun=false",
    "-p:DebugType=None",
    "-p:UseAppHost=true"
  )

  Write-Host "Publishing $($project.Name) -> $projectOutput" -ForegroundColor Cyan
  & dotnet @args
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for $($project.Name) with exit code $LASTEXITCODE"
  }

  $entryPoint = Join-Path $projectOutput "$($project.Name).exe"
  if (-not (Test-Path $entryPoint)) {
    throw "Expected entry point not found: $entryPoint"
  }

  $published += [pscustomobject]@{
    Name = $project.Name
    Project = $project.Project
    Output = $projectOutput
    EntryPoint = $entryPoint
  }
}

$manifest = [pscustomobject]@{
  PackageName = "ChromaLink Desktop Package"
  PackageVersion = $packageVersion
  GeneratedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
  SourceCommit = $sourceCommit
  RepoRoot = $repoRoot
  OutputRoot = $OutputRoot
  DesktopRoot = $desktopRoot
  Configuration = $Configuration
  Runtime = $Runtime
  SelfContained = [bool]$SelfContained
  PackageLayout = "desktop/<ProjectName>"
  Launchers = @(
    "Open-ChromaLink-Product.cmd",
    "Bridge-ChromaLink.cmd",
    "Start-ChromaLinkStack.cmd",
    "Open-ChromaLink-Monitor.cmd",
    "Status-ChromaLinkStack.cmd",
    "Stop-ChromaLinkStack.cmd",
    "Open-ChromaLinkDashboard.cmd"
  )
  Projects = $published
}

$manifestPath = Join-Path $OutputRoot "package-manifest.json"
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8

$readmePath = Join-Path $OutputRoot "README.md"
@'
# ChromaLink Desktop Package

This folder is a predictable publish output assembled from the source repo.

Layout:

- `desktop/ChromaLink.Cli/`
- `desktop/ChromaLink.HttpBridge/`
- `desktop/ChromaLink.Inspector/`
- `desktop/ChromaLink.Monitor/`

Common launchers:

- `Open-ChromaLink-Product.cmd`
- `Bridge-ChromaLink.cmd`
- `Start-ChromaLinkStack.cmd`
- `Open-ChromaLink-Monitor.cmd`
- `Status-ChromaLinkStack.cmd`
- `Stop-ChromaLinkStack.cmd`
- `Open-ChromaLinkDashboard.cmd`

Notes:

- Package version: `__PACKAGE_VERSION__`
- Source commit: `__SOURCE_COMMIT__`
- The package is framework-dependent unless `-SelfContained` is used.
- launcher windows start minimized by default to reduce the chance of covering the RIFT client during live capture
- `Open-ChromaLink-Product.cmd` is the fastest first-run path. It starts the background stack, waits for readiness, and then opens the monitor.
- `Bridge-ChromaLink.cmd` starts the packaged CLI in `watch` mode so it can keep the rolling snapshot fresh.
- `Start-ChromaLinkStack.cmd` starts the packaged CLI watch loop plus HTTP bridge without opening UI.
- `Open-ChromaLink-Monitor.cmd` opens the packaged monitor explicitly.
- `Status-ChromaLinkStack.cmd` reports bridge endpoint health, snapshot freshness, and package-local process counts.
- `Stop-ChromaLinkStack.cmd` stops only the packaged CLI, HTTP bridge, and monitor processes from this package folder.
- Each project folder keeps the published executable and its supporting DLLs together.

First-run workflow:

1. Double-click `Open-ChromaLink-Product.cmd` for the normal product path.
2. If you want background-only startup, use `Start-ChromaLinkStack.cmd`.
3. If something looks wrong, run `Status-ChromaLinkStack.cmd`.
4. Use `Open-ChromaLinkDashboard.cmd` if you prefer the browser view.
5. Use `Stop-ChromaLinkStack.cmd` when you are done.
'@ | Set-Content -LiteralPath $readmePath -Encoding utf8

(Get-Content -LiteralPath $readmePath -Raw).
  Replace('__PACKAGE_VERSION__', $packageVersion).
  Replace('__SOURCE_COMMIT__', $sourceCommit) |
  Set-Content -LiteralPath $readmePath -Encoding utf8

$openProductPs1 = Join-Path $OutputRoot "Open-ChromaLink-Product.ps1"
@'
param(
  [int]$TimeoutSeconds = 20,
  [string]$BaseUrl = "http://127.0.0.1:7337/"
)

$ErrorActionPreference = "Stop"
$packageRoot = $PSScriptRoot
$startScript = Join-Path $packageRoot "Start-ChromaLinkStack.cmd"
$monitorScript = Join-Path $packageRoot "Open-ChromaLink-Monitor.cmd"
$statusScript = Join-Path $packageRoot "Status-ChromaLinkStack.cmd"

function Normalize-BaseUrl {
  param([string]$Value)

  if ([string]::IsNullOrWhiteSpace($Value)) {
    return "http://127.0.0.1:7337/"
  }

  if ($Value.EndsWith('/')) {
    return $Value
  }

  return "$Value/"
}

function Test-Ready {
  param([string]$Url)

  try {
    $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 2
    return [int]$response.StatusCode -eq 200
  } catch {
    $httpResponse = $_.Exception.Response
    if ($null -ne $httpResponse) {
      return [int]$httpResponse.StatusCode -eq 200
    }

    return $false
  }
}

$BaseUrl = Normalize-BaseUrl $BaseUrl
$readyUrl = "{0}ready" -f $BaseUrl

Start-Process -WindowStyle Minimized -FilePath $startScript | Out-Null

$deadline = [DateTimeOffset]::UtcNow.AddSeconds([math]::Max(1, $TimeoutSeconds))
$ready = $false
while ([DateTimeOffset]::UtcNow -lt $deadline) {
  if (Test-Ready -Url $readyUrl) {
    $ready = $true
    break
  }

  Start-Sleep -Milliseconds 500
}

if ($ready) {
  Start-Process -WindowStyle Minimized -FilePath $monitorScript | Out-Null
  exit 0
}

Start-Process -WindowStyle Minimized -FilePath $statusScript | Out-Null
exit 1
'@ | Set-Content -LiteralPath $openProductPs1 -Encoding utf8

$openProductCmd = Join-Path $OutputRoot "Open-ChromaLink-Product.cmd"
@'
@echo off
setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Open-ChromaLink-Product.ps1" %*
exit /b %ERRORLEVEL%
'@ | Set-Content -LiteralPath $openProductCmd -Encoding ascii

$bridgeScript = Join-Path $OutputRoot "Bridge-ChromaLink.cmd"
@'
@echo off
setlocal

start "" /min "%~dp0desktop\ChromaLink.Cli\ChromaLink.Cli.exe" watch

exit /b 0
'@ | Set-Content -LiteralPath $bridgeScript -Encoding ascii

$startScript = Join-Path $OutputRoot "Start-ChromaLinkStack.cmd"
@'
@echo off
setlocal

start "" /min "%~dp0desktop\ChromaLink.Cli\ChromaLink.Cli.exe" watch
start "" /min "%~dp0desktop\ChromaLink.HttpBridge\ChromaLink.HttpBridge.exe"

exit /b 0
'@ | Set-Content -LiteralPath $startScript -Encoding ascii

$openMonitorScript = Join-Path $OutputRoot "Open-ChromaLink-Monitor.cmd"
@'
@echo off
setlocal

start "" /min "%~dp0desktop\ChromaLink.Monitor\ChromaLink.Monitor.exe" --start-minimized

exit /b 0
'@ | Set-Content -LiteralPath $openMonitorScript -Encoding ascii

$statusPs1 = Join-Path $OutputRoot "Status-ChromaLinkStack.ps1"
@'
param(
  [string]$BaseUrl = "http://127.0.0.1:7337/"
)

$ErrorActionPreference = "Stop"
$packageRoot = $PSScriptRoot
$desktopRoot = Join-Path $packageRoot "desktop"
$snapshotPath = Join-Path $env:LOCALAPPDATA "ChromaLink\DesktopDotNet\out\chromalink-live-telemetry.json"

function Normalize-BaseUrl {
  param([string]$Value)

  if ([string]::IsNullOrWhiteSpace($Value)) {
    return "http://127.0.0.1:7337/"
  }

  if ($Value.EndsWith('/')) {
    return $Value
  }

  return "$Value/"
}

function Test-Endpoint {
  param([string]$Url)

  try {
    $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 3
    return [pscustomobject]@{
      Url = $Url
      Responded = $true
      StatusCode = [int]$response.StatusCode
      Length = if ($response.Content) { $response.Content.Length } else { 0 }
      ContentType = [string]$response.Headers['Content-Type']
    }
  } catch {
    $httpResponse = $_.Exception.Response
    if ($null -ne $httpResponse) {
      return [pscustomobject]@{
        Url = $Url
        Responded = $true
        StatusCode = [int]$httpResponse.StatusCode
        Length = 0
        ContentType = [string]$httpResponse.Headers['Content-Type']
      }
    }

    return [pscustomobject]@{
      Url = $Url
      Responded = $false
      StatusCode = 0
      Length = 0
      ContentType = ""
    }
  }
}

function Get-PackagedProcesses {
  param([string]$ExecutableName)

  Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq $ExecutableName -and
    -not [string]::IsNullOrWhiteSpace($_.ExecutablePath) -and
    $_.ExecutablePath.StartsWith($desktopRoot, [System.StringComparison]::OrdinalIgnoreCase)
  }
}

function Get-Snapshot {
  if (-not (Test-Path -LiteralPath $snapshotPath)) {
    return $null
  }

  Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json
}

function Get-SnapshotAgeSeconds {
  param($Snapshot)

  if ($null -eq $Snapshot -or [string]::IsNullOrWhiteSpace([string]$Snapshot.generatedAtUtc)) {
    return [double]::PositiveInfinity
  }

  try {
    $generatedAt = [DateTimeOffset]::Parse([string]$Snapshot.generatedAtUtc)
    return [math]::Max(0, ([DateTimeOffset]::UtcNow - $generatedAt).TotalSeconds)
  } catch {
    return [double]::PositiveInfinity
  }
}

$BaseUrl = Normalize-BaseUrl $BaseUrl
$trimmedBaseUrl = $BaseUrl.TrimEnd('/')
$endpoints = @(
  "$trimmedBaseUrl/latest-snapshot",
  "$trimmedBaseUrl/health",
  "$trimmedBaseUrl/ready"
)

Write-Host "Packaged ChromaLink Stack Status"
Write-Host ("PackageRoot: {0}" -f $packageRoot)
Write-Host ("BaseUrl: {0}" -f $BaseUrl)

Write-Host ""
Write-Host "Endpoints"
foreach ($endpoint in $endpoints) {
  $result = Test-Endpoint -Url $endpoint
  Write-Host ("{0} -> {1} {2} bytes {3}" -f $result.Url, $result.StatusCode, $result.Length, $result.ContentType)
}

Write-Host ""
Write-Host "Snapshot"
$snapshot = Get-Snapshot
if ($null -eq $snapshot) {
  Write-Host ("Path: {0}" -f $snapshotPath)
  Write-Host "Status: missing"
} else {
  $aggregate = $snapshot.aggregate
  Write-Host ("Path: {0}" -f $snapshotPath)
  Write-Host ("AgeSeconds: {0:F2}" -f (Get-SnapshotAgeSeconds -Snapshot $snapshot))
  if ($null -ne $aggregate) {
    Write-Host ("Ready: {0}" -f $aggregate.ready.ToString().ToLowerInvariant())
    Write-Host ("Healthy: {0}" -f $aggregate.healthy.ToString().ToLowerInvariant())
    Write-Host ("Stale: {0}" -f $aggregate.stale.ToString().ToLowerInvariant())
    Write-Host ("AcceptedFrames: {0}" -f $aggregate.acceptedFrames)
  }
  if ($null -ne $snapshot.contract) {
    Write-Host ("TelemetryContract: {0}/v{1}" -f $snapshot.contract.name, $snapshot.contract.schemaVersion)
  }
}

Write-Host ""
Write-Host "Package Processes"
Write-Host ("CLI Watch: {0}" -f @((Get-PackagedProcesses -ExecutableName "ChromaLink.Cli.exe")).Count)
Write-Host ("HttpBridge: {0}" -f @((Get-PackagedProcesses -ExecutableName "ChromaLink.HttpBridge.exe")).Count)
Write-Host ("Monitor: {0}" -f @((Get-PackagedProcesses -ExecutableName "ChromaLink.Monitor.exe")).Count)
'@ | Set-Content -LiteralPath $statusPs1 -Encoding utf8

$statusCmd = Join-Path $OutputRoot "Status-ChromaLinkStack.cmd"
@'
@echo off
setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Status-ChromaLinkStack.ps1" %*
exit /b %ERRORLEVEL%
'@ | Set-Content -LiteralPath $statusCmd -Encoding ascii

$stopPs1 = Join-Path $OutputRoot "Stop-ChromaLinkStack.ps1"
@'
param(
  [switch]$Force
)

$ErrorActionPreference = "Stop"
$packageRoot = $PSScriptRoot
$desktopRoot = Join-Path $packageRoot "desktop"
$targets = @(
  "ChromaLink.Cli.exe",
  "ChromaLink.HttpBridge.exe",
  "ChromaLink.Monitor.exe"
)

$processes = Get-CimInstance Win32_Process | Where-Object {
  $_.Name -in $targets -and
  -not [string]::IsNullOrWhiteSpace($_.ExecutablePath) -and
  $_.ExecutablePath.StartsWith($desktopRoot, [System.StringComparison]::OrdinalIgnoreCase)
}

if (@($processes).Count -eq 0) {
  Write-Host "No packaged ChromaLink stack processes matched."
  exit 0
}

foreach ($process in @($processes | Sort-Object ProcessId -Unique)) {
  try {
    Stop-Process -Id $process.ProcessId -Force:$Force.IsPresent -ErrorAction Stop
    Write-Host ("Stopped {0} (PID {1})" -f $process.Name, $process.ProcessId)
  } catch {
    Write-Warning ("Failed to stop PID {0}: {1}" -f $process.ProcessId, $_.Exception.Message)
  }
}
'@ | Set-Content -LiteralPath $stopPs1 -Encoding utf8

$stopCmd = Join-Path $OutputRoot "Stop-ChromaLinkStack.cmd"
@'
@echo off
setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Stop-ChromaLinkStack.ps1" %*
exit /b %ERRORLEVEL%
'@ | Set-Content -LiteralPath $stopCmd -Encoding ascii

$dashboardScript = Join-Path $OutputRoot "Open-ChromaLinkDashboard.cmd"
@'
@echo off
setlocal

start "" /min "http://127.0.0.1:7337/"

exit /b 0
'@ | Set-Content -LiteralPath $dashboardScript -Encoding ascii

Write-Host ""
Write-Host "ChromaLink desktop package written to $OutputRoot" -ForegroundColor Green
Write-Host "Manifest: $manifestPath" -ForegroundColor Green
Write-Host "Launchers: Open-ChromaLink-Product.cmd, Bridge-ChromaLink.cmd, Start-ChromaLinkStack.cmd, Open-ChromaLink-Monitor.cmd, Status-ChromaLinkStack.cmd, Stop-ChromaLinkStack.cmd, Open-ChromaLinkDashboard.cmd" -ForegroundColor Green
