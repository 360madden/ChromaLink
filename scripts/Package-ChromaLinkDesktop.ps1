param(
  [string]$OutputRoot = "",
  [string]$Configuration = "Release",
  [string]$Runtime = "win-x64",
  [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
  $OutputRoot = Join-Path $repoRoot "artifacts\desktop-stack\latest"
}

$projects = @(
  [pscustomobject]@{
    Name = "Cli"
    Project = Join-Path $repoRoot "DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj"
  },
  [pscustomobject]@{
    Name = "HttpBridge"
    Project = Join-Path $repoRoot "DesktopDotNet\ChromaLink.HttpBridge\ChromaLink.HttpBridge.csproj"
  },
  [pscustomobject]@{
    Name = "Inspector"
    Project = Join-Path $repoRoot "DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj"
  },
  [pscustomobject]@{
    Name = "Monitor"
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

New-Item -ItemType Directory -Path $OutputRoot | Out-Null

$publishRoot = Join-Path $OutputRoot "publish"
New-Item -ItemType Directory -Path $publishRoot | Out-Null

$published = @()

foreach ($project in $projects) {
  $projectOutput = Join-Path $publishRoot $project.Name
  New-Item -ItemType Directory -Path $projectOutput | Out-Null

  $args = @(
    "publish",
    $project.Project,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--output", $projectOutput
  )

  if ($SelfContained) {
    $args += "-p:SelfContained=true"
  }

  Write-Host "Publishing $($project.Name) -> $projectOutput" -ForegroundColor Cyan
  & dotnet @args
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for $($project.Name) with exit code $LASTEXITCODE"
  }

  $published += [pscustomobject]@{
    Name = $project.Name
    Project = $project.Project
    Output = $projectOutput
  }
}

$manifest = [pscustomobject]@{
  GeneratedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
  RepoRoot = $repoRoot
  OutputRoot = $OutputRoot
  Configuration = $Configuration
  Runtime = $Runtime
  SelfContained = [bool]$SelfContained
  PublishRoot = $publishRoot
  Projects = $published
}

$manifestPath = Join-Path $OutputRoot "package-manifest.json"
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8

$readmePath = Join-Path $OutputRoot "README.txt"
@"
ChromaLink desktop package

Output root:
  $OutputRoot

Published tools:
  - Cli
  - HttpBridge
  - Inspector
  - Monitor

Launchers:
  - Start-ChromaLinkStack.cmd
  - Open-ChromaLinkDashboard.cmd

Notes:
  - This package is framework-dependent unless -SelfContained is used.
  - The HttpBridge should be started before opening the dashboard.
  - The Monitor reads the live snapshot produced by the bridge/CLI.
"@ | Set-Content -LiteralPath $readmePath -Encoding utf8

$startScript = Join-Path $OutputRoot "Start-ChromaLinkStack.cmd"
@'
@echo off
setlocal

start "" "%~dp0publish\HttpBridge\ChromaLink.HttpBridge.exe"
start "" "%~dp0publish\Monitor\ChromaLink.Monitor.exe"

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
Write-Host "Launchers: Start-ChromaLinkStack.cmd, Open-ChromaLinkDashboard.cmd" -ForegroundColor Green
