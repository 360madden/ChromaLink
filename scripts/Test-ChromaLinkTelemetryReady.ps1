param(
  [int]$MaxAgeSeconds = 5,
  [switch]$RequireCompleteState,
  [switch]$RequireAnyFrame
)

$ErrorActionPreference = "Stop"

$snapshotPath = Join-Path $env:LOCALAPPDATA "ChromaLink\DesktopDotNet\out\chromalink-live-telemetry.json"

function Get-Snapshot {
  if (-not (Test-Path -LiteralPath $snapshotPath)) {
    return $null
  }

  return Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json
}

function Get-SnapshotAgeSeconds {
  param(
    $Snapshot
  )

  if ($null -eq $Snapshot -or [string]::IsNullOrWhiteSpace([string]$Snapshot.generatedAtUtc)) {
    return [double]::PositiveInfinity
  }

  try {
    $generatedAt = [DateTimeOffset]::Parse([string]$Snapshot.generatedAtUtc)
    $ageSeconds = ([DateTimeOffset]::UtcNow - $generatedAt).TotalSeconds
    if ($ageSeconds -lt 0) {
      return 0.0
    }
    return $ageSeconds
  } catch {
    return [double]::PositiveInfinity
  }
}

$snapshot = Get-Snapshot
if ($null -eq $snapshot) {
  Write-Host "Telemetry snapshot missing: $snapshotPath" -ForegroundColor Yellow
  exit 1
}

$ageSeconds = Get-SnapshotAgeSeconds -Snapshot $snapshot
$aggregate = $snapshot.aggregate
$metrics = $snapshot.metrics

$hasAnyFrame = $false
if ($null -ne $aggregate) {
  $hasAnyFrame = ($null -ne $aggregate.coreStatus) -or ($null -ne $aggregate.playerVitals) -or ($null -ne $aggregate.playerPosition)
}

$isFresh = $ageSeconds -le $MaxAgeSeconds
$isReady = $false
if ($null -ne $aggregate) {
  $isReady = [bool]$aggregate.ready
}

$frameCount = 0
if ($null -ne $metrics -and $null -ne $metrics.frameTypeCounts) {
  $frameCount = @($metrics.frameTypeCounts.PSObject.Properties).Count
}

$ok = $isFresh -and $isReady
if ($RequireAnyFrame) {
  $ok = $ok -and $hasAnyFrame
}
if ($RequireCompleteState) {
  $ok = $ok -and $hasAnyFrame -and $isReady
}

Write-Host ("TelemetryReady={0}" -f $ok.ToString().ToLowerInvariant())
Write-Host ("TelemetryFresh={0}" -f $isFresh.ToString().ToLowerInvariant())
Write-Host ("TelemetryAgeSeconds={0:F2}" -f $ageSeconds)
Write-Host ("TelemetryReadyState={0}" -f $isReady.ToString().ToLowerInvariant())
Write-Host ("TelemetryHasAnyFrame={0}" -f $hasAnyFrame.ToString().ToLowerInvariant())
Write-Host ("TelemetryFrameTypeCount={0}" -f $frameCount)
Write-Host ("TelemetrySnapshotPath={0}" -f $snapshotPath)

if (-not $ok) {
  exit 1
}

exit 0
