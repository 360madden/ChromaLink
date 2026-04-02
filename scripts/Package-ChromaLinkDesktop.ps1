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
    "--runtime", $Runtime
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
  GeneratedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
  RepoRoot = $repoRoot
  OutputRoot = $OutputRoot
  DesktopRoot = $desktopRoot
  Configuration = $Configuration
  Runtime = $Runtime
  SelfContained = [bool]$SelfContained
  PackageLayout = "desktop/<ProjectName>"
  Launchers = @(
    "Bridge-ChromaLink.cmd",
    "Start-ChromaLinkStack.cmd",
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

- `Bridge-ChromaLink.cmd`
- `Start-ChromaLinkStack.cmd`
- `Open-ChromaLinkDashboard.cmd`

Notes:

- The package is framework-dependent unless `-SelfContained` is used.
- `Bridge-ChromaLink.cmd` starts the packaged CLI in `watch` mode so it can keep the rolling snapshot fresh.
- `Start-ChromaLinkStack.cmd` starts the packaged CLI watch loop, HTTP bridge, and monitor together.
- Each project folder keeps the published executable and its supporting DLLs together.
'@ | Set-Content -LiteralPath $readmePath -Encoding utf8

$bridgeScript = Join-Path $OutputRoot "Bridge-ChromaLink.cmd"
@'
@echo off
setlocal

start "" "%~dp0desktop\ChromaLink.Cli\ChromaLink.Cli.exe" watch

exit /b 0
'@ | Set-Content -LiteralPath $bridgeScript -Encoding ascii

$startScript = Join-Path $OutputRoot "Start-ChromaLinkStack.cmd"
@'
@echo off
setlocal

start "" "%~dp0desktop\ChromaLink.Cli\ChromaLink.Cli.exe" watch
start "" "%~dp0desktop\ChromaLink.HttpBridge\ChromaLink.HttpBridge.exe"
start "" "%~dp0desktop\ChromaLink.Monitor\ChromaLink.Monitor.exe"

exit /b 0
'@ | Set-Content -LiteralPath $startScript -Encoding ascii

$dashboardScript = Join-Path $OutputRoot "Open-ChromaLinkDashboard.cmd"
@'
@echo off
setlocal

start "" "http://127.0.0.1:7337/"

exit /b 0
'@ | Set-Content -LiteralPath $dashboardScript -Encoding ascii

Write-Host ""
Write-Host "ChromaLink desktop package written to $OutputRoot" -ForegroundColor Green
Write-Host "Manifest: $manifestPath" -ForegroundColor Green
Write-Host "Launchers: Bridge-ChromaLink.cmd, Start-ChromaLinkStack.cmd, Open-ChromaLinkDashboard.cmd" -ForegroundColor Green
