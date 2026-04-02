param(
  [string[]]$Resolutions = @(
    "1600x900",
    "1280x720",
    "960x540",
    "800x450",
    "640x360"
  ),
  [switch]$ReloadUi,
  [ValidateSet("desktopdup", "screen", "printwindow")]
  [string]$Backend = "printwindow",
  [int]$Left = 40,
  [int]$Top = 40,
  [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$resizeScript = Join-Path $PSScriptRoot "Resize-RiftClient-640x360.ps1"
$reloadScript = Join-Path $PSScriptRoot "Reload-RiftUi.ps1"
$projectPath = Join-Path $repoRoot "DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj"
$captureRoot = Join-Path $env:LOCALAPPDATA "ChromaLink\DesktopDotNet\out"

if (-not (Test-Path $resizeScript)) {
  throw "Resize script not found at $resizeScript"
}

if (-not (Test-Path $reloadScript)) {
  throw "Reload script not found at $reloadScript"
}

if (-not (Test-Path $projectPath)) {
  throw "CLI project not found at $projectPath"
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
  $suffix = if ($ReloadUi) { "resolution-sweep-reload" } else { "resolution-sweep" }
  $OutputRoot = Join-Path $captureRoot $suffix
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$summaryPath = Join-Path $OutputRoot "summary.jsonl"
if (Test-Path $summaryPath) {
  Remove-Item -LiteralPath $summaryPath -Force
}

foreach ($resolution in $Resolutions) {
  if ($resolution -notmatch '^(\d+)x(\d+)$') {
    throw "Resolution '$resolution' must use WIDTHxHEIGHT format."
  }

  $width = [int]$matches[1]
  $height = [int]$matches[2]
  $label = "{0}x{1}" -f $width, $height

  Write-Host ("=== {0}{1} ===" -f $label, $(if ($ReloadUi) { " with reload" } else { "" })) -ForegroundColor Cyan

  & powershell -NoProfile -ExecutionPolicy Bypass -File $resizeScript -ClientWidth $width -ClientHeight $height -Left $Left -Top $Top
  Start-Sleep -Milliseconds 600

  if ($ReloadUi) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $reloadScript
    Start-Sleep -Milliseconds 1000
  }

  & dotnet run --project $projectPath -- capture-dump --backend $Backend
  Start-Sleep -Milliseconds 200

  $destination = Join-Path $OutputRoot $label
  New-Item -ItemType Directory -Force -Path $destination | Out-Null

  Copy-Item (Join-Path $captureRoot "chromalink-color-capture-dump.bmp") (Join-Path $destination "capture.bmp") -Force
  Copy-Item (Join-Path $captureRoot "chromalink-color-capture-dump-annotated.bmp") (Join-Path $destination "capture-annotated.bmp") -Force
  Copy-Item (Join-Path $captureRoot "chromalink-color-capture-dump.json") (Join-Path $destination "capture.json") -Force

  $capture = Get-Content (Join-Path $destination "capture.json") -Raw | ConvertFrom-Json
  $detection = $capture.detection
  [pscustomobject]@{
    resolution = $label
    clientWidth = $capture.clientRect.width
    clientHeight = $capture.clientRect.height
    accepted = $capture.accepted
    reason = $capture.reason
    leftExpected = $capture.controlPatterns.leftExpected
    leftObserved = $capture.controlPatterns.leftObserved
    rightExpected = $capture.controlPatterns.rightExpected
    rightObserved = $capture.controlPatterns.rightObserved
    originX = if ($detection) { $detection.originX } else { $null }
    originY = if ($detection) { $detection.originY } else { $null }
    pitch = if ($detection) { [double]$detection.pitch } else { $null }
    scale = if ($detection) { [double]$detection.scale } else { $null }
    leftControlScore = if ($detection) { [double]$detection.leftControlScore } else { $null }
    rightControlScore = if ($detection) { [double]$detection.rightControlScore } else { $null }
  } | ConvertTo-Json -Compress | Add-Content -LiteralPath $summaryPath
}

Write-Host "" 
Write-Host ("Sweep results saved under {0}" -f $OutputRoot) -ForegroundColor Green
Get-Content -LiteralPath $summaryPath
