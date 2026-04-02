param(
  [switch]$Watch,
  [int]$IntervalMs = 500
)

$ErrorActionPreference = "Stop"

$snapshotPath = Join-Path $env:LOCALAPPDATA "ChromaLink\DesktopDotNet\out\chromalink-live-telemetry.json"

function Format-SectionLine {
  param(
    [string]$Label,
    [object]$Value
  )

  return "{0}: {1}" -f $Label, $Value
}

function Show-Snapshot {
  if (-not (Test-Path -LiteralPath $snapshotPath)) {
    Write-Host "Telemetry snapshot not found at $snapshotPath" -ForegroundColor Yellow
    return
  }

  $json = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json
  $aggregate = $json.aggregate
  $metrics = $json.metrics

  try {
    if ($Host -ne $null -and $Host.UI -ne $null -and $Host.UI.RawUI -ne $null) {
      Clear-Host
    }
  } catch {
  }
  Write-Host "ChromaLink Telemetry" -ForegroundColor Cyan
  Write-Host (Format-SectionLine "Contract" ("{0}/v{1}" -f $json.contract.name, $json.contract.schemaVersion))
  Write-Host (Format-SectionLine "GeneratedUtc" $json.generatedAtUtc)
  Write-Host (Format-SectionLine "Ready" $aggregate.ready)
  Write-Host (Format-SectionLine "AcceptedFrames" $aggregate.acceptedFrames)
  Write-Host (Format-SectionLine "AcceptedSamples" $metrics.acceptedSamples)
  Write-Host (Format-SectionLine "RejectedSamples" $metrics.rejectedSamples)
  Write-Host (Format-SectionLine "LastBackend" $json.lastBackend)

  if ($json.lastDetection -ne $null) {
    Write-Host (Format-SectionLine "Detection" ("origin {0},{1} pitch {2} scale {3}" -f $json.lastDetection.originX, $json.lastDetection.originY, $json.lastDetection.pitch, $json.lastDetection.scale))
  }

  Write-Host ""
  Write-Host "CoreStatus" -ForegroundColor Green
  if ($aggregate.coreStatus -ne $null) {
    Write-Host (Format-SectionLine "Sequence" $aggregate.coreStatus.sequence)
    Write-Host (Format-SectionLine "PlayerHealthPctQ8" $aggregate.coreStatus.playerHealthPctQ8)
    Write-Host (Format-SectionLine "TargetHealthPctQ8" $aggregate.coreStatus.targetHealthPctQ8)
    Write-Host (Format-SectionLine "PlayerFlags" $aggregate.coreStatus.playerFlags)
  } else {
    Write-Host "missing"
  }

  Write-Host ""
  Write-Host "PlayerVitals" -ForegroundColor Green
  if ($aggregate.playerVitals -ne $null) {
    Write-Host (Format-SectionLine "Sequence" $aggregate.playerVitals.sequence)
    Write-Host (Format-SectionLine "Health" ("{0}/{1}" -f $aggregate.playerVitals.healthCurrent, $aggregate.playerVitals.healthMax))
    Write-Host (Format-SectionLine "Resource" ("{0}/{1}" -f $aggregate.playerVitals.resourceCurrent, $aggregate.playerVitals.resourceMax))
  } else {
    Write-Host "missing"
  }

  Write-Host ""
  Write-Host "PlayerPosition" -ForegroundColor Green
  if ($aggregate.playerPosition -ne $null) {
    Write-Host (Format-SectionLine "Sequence" $aggregate.playerPosition.sequence)
    Write-Host (Format-SectionLine "Position" ("{0}, {1}, {2}" -f $aggregate.playerPosition.x, $aggregate.playerPosition.y, $aggregate.playerPosition.z))
  } else {
    Write-Host "missing"
  }

  Write-Host ""
  Write-Host "FrameCounts" -ForegroundColor Green
  foreach ($property in $metrics.frameTypeCounts.PSObject.Properties) {
    Write-Host (Format-SectionLine $property.Name $property.Value)
  }
}

if ($Watch) {
  while ($true) {
    Show-Snapshot
    Start-Sleep -Milliseconds ([Math]::Max(100, $IntervalMs))
  }
}

Show-Snapshot
