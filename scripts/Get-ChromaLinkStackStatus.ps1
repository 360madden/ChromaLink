param(
  [string]$BaseUrl = $(if ($env:CHROMALINK_HTTP_BRIDGE_URL) { $env:CHROMALINK_HTTP_BRIDGE_URL } else { 'http://127.0.0.1:7337/' }),
  [switch]$IncludeProcesses
)

$ErrorActionPreference = "Stop"
$snapshotPath = Join-Path $env:LOCALAPPDATA "ChromaLink\DesktopDotNet\out\chromalink-live-telemetry.json"

function Normalize-BaseUrl {
  param([string]$Value)

  if ([string]::IsNullOrWhiteSpace($Value)) {
    return 'http://127.0.0.1:7337/'
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
      ContentType = Normalize-ContentType $response.Headers['Content-Type']
      Length = if ($response.Content) { $response.Content.Length } else { 0 }
    }
  } catch {
    $httpResponse = $_.Exception.Response
    if ($null -ne $httpResponse) {
      $contentType = $httpResponse.Headers['Content-Type']
      $length = 0
      try {
        $stream = $httpResponse.GetResponseStream()
        if ($null -ne $stream) {
          $reader = New-Object System.IO.StreamReader($stream)
          $body = $reader.ReadToEnd()
          $length = $body.Length
          $reader.Dispose()
          $stream.Dispose()
        }
      } catch {
      }

      return [pscustomobject]@{
        Url = $Url
        Responded = $true
        StatusCode = [int]$httpResponse.StatusCode
        ContentType = Normalize-ContentType $contentType
        Length = $length
      }
    }

    return [pscustomobject]@{
      Url = $Url
      Responded = $false
      StatusCode = 0
      ContentType = ''
      Length = 0
    }
  }
}

function Get-MatchCount {
  param([string]$Pattern)

  $processes = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like $Pattern }
  return @($processes).Count
}

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

function Normalize-ContentType {
  param([object]$Value)

  if ($null -eq $Value) {
    return ''
  }

  if ($Value -is [Array]) {
    return ($Value -join ', ')
  }

  return [string]$Value
}

$BaseUrl = Normalize-BaseUrl $BaseUrl
$trimmedBaseUrl = $BaseUrl.TrimEnd('/')
$endpoints = @(
  "$trimmedBaseUrl/latest-snapshot",
  "$trimmedBaseUrl/health",
  "$trimmedBaseUrl/ready",
  "$trimmedBaseUrl/dashboard"
)

Write-Host "ChromaLink Stack Status"
Write-Host ("BaseUrl: {0}" -f $BaseUrl)

$endpointResults = @()
foreach ($endpoint in $endpoints) {
  $result = Test-Endpoint -Url $endpoint
  $endpointResults += $result
  Write-Host ("{0} -> {1} {2} bytes {3}" -f $result.Url, $result.StatusCode, $result.Length, $result.ContentType)
}

Write-Host ""
Write-Host "Snapshot"
$snapshot = Get-Snapshot
if ($null -eq $snapshot) {
  Write-Host ("Path: {0}" -f $snapshotPath)
  Write-Host "Status: missing"
} else {
  $ageSeconds = Get-SnapshotAgeSeconds -Snapshot $snapshot
  $aggregate = $snapshot.aggregate
  $frameCounts = ''
  if ($null -ne $snapshot.metrics -and $null -ne $snapshot.metrics.frameTypeCounts) {
    $frameCounts = @(
      'CoreStatus={0}' -f $snapshot.metrics.frameTypeCounts.CoreStatus
      'PlayerVitals={0}' -f $snapshot.metrics.frameTypeCounts.PlayerVitals
      'PlayerPosition={0}' -f $snapshot.metrics.frameTypeCounts.PlayerPosition
    ) -join ' '
  }

  Write-Host ("Path: {0}" -f $snapshotPath)
  if ($null -ne $aggregate) {
    Write-Host ("Ready: {0}" -f $aggregate.ready.ToString().ToLowerInvariant())
    Write-Host ("Healthy: {0}" -f $aggregate.healthy.ToString().ToLowerInvariant())
    Write-Host ("Fresh: {0}" -f $aggregate.fresh.ToString().ToLowerInvariant())
    Write-Host ("Stale: {0}" -f $aggregate.stale.ToString().ToLowerInvariant())
    Write-Host ("AgeSeconds: {0:F2}" -f $ageSeconds)
    Write-Host ("AcceptedFrames: {0}" -f $aggregate.acceptedFrames)
    if (-not [string]::IsNullOrWhiteSpace($frameCounts)) {
      Write-Host ("FrameCounts: {0}" -f $frameCounts)
    }
  } else {
    Write-Host "Status: no aggregate"
  }

  if ($null -ne $snapshot.contract) {
    Write-Host ("TelemetryContract: {0}/v{1}" -f $snapshot.contract.name, $snapshot.contract.schemaVersion)
  }
  if ($null -ne $snapshot.profile) {
    Write-Host ("Profile: {0}" -f $snapshot.profile.name)
  }
}

Write-Host ""
Write-Host "Processes"
Write-Host ("HttpBridge: {0}" -f (Get-MatchCount "*ChromaLink.HttpBridge*"))
Write-Host ("Monitor: {0}" -f (Get-MatchCount "*ChromaLink.Monitor*"))
Write-Host ("CLI Watch: {0}" -f (Get-MatchCount "*ChromaLink.Cli*watch*"))
Write-Host ("CLI Live: {0}" -f (Get-MatchCount "*ChromaLink.Cli*live*"))
