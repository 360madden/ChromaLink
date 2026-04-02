param(
  [string]$BaseUrl = $(if ($env:CHROMALINK_HTTP_BRIDGE_URL) { $env:CHROMALINK_HTTP_BRIDGE_URL } else { 'http://127.0.0.1:7337/' }),
  [int]$TimeoutSeconds = 3
)

$ErrorActionPreference = "Stop"

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

function Invoke-JsonEndpoint {
  param(
    [string]$Url,
    [int]$TimeoutSeconds
  )

  try {
    $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec $TimeoutSeconds
    $body = $response.Content
    $json = $null
    if (-not [string]::IsNullOrWhiteSpace($body)) {
      $json = $body | ConvertFrom-Json
    }

    return [pscustomobject]@{
      Responded = $true
      StatusCode = [int]$response.StatusCode
      ContentType = $response.Headers['Content-Type']
      Json = $json
    }
  } catch {
    $httpResponse = $_.Exception.Response
    if ($null -ne $httpResponse) {
      try {
        $stream = $httpResponse.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $body = $reader.ReadToEnd()
        $reader.Dispose()
        $stream.Dispose()

        $json = $null
        if (-not [string]::IsNullOrWhiteSpace($body)) {
          $json = $body | ConvertFrom-Json
        }

        return [pscustomobject]@{
          Responded = $true
          StatusCode = [int]$httpResponse.StatusCode
          ContentType = $httpResponse.Headers['Content-Type']
          Json = $json
        }
      } catch {
      }
    }

    return [pscustomobject]@{
      Responded = $false
      StatusCode = 0
      ContentType = ''
      Json = $null
    }
  }
}

$BaseUrl = Normalize-BaseUrl $BaseUrl
$trimmedBaseUrl = $BaseUrl.TrimEnd('/')
$health = Invoke-JsonEndpoint -Url "$trimmedBaseUrl/health" -TimeoutSeconds $TimeoutSeconds
$ready = Invoke-JsonEndpoint -Url "$trimmedBaseUrl/ready" -TimeoutSeconds $TimeoutSeconds
$latest = Invoke-JsonEndpoint -Url "$trimmedBaseUrl/latest-snapshot" -TimeoutSeconds $TimeoutSeconds

Write-Host "ChromaLink HTTP Bridge Status"
Write-Host ("BaseUrl: {0}" -f $BaseUrl)
Write-Host ("Health: {0}" -f ($(if ($health.Responded) { "$($health.StatusCode)" } else { "unreachable" })))
Write-Host ("Ready: {0}" -f ($(if ($ready.Responded) { "$($ready.StatusCode)" } else { "unreachable" })))
Write-Host ("Snapshot: {0}" -f ($(if ($latest.Responded) { "$($latest.StatusCode)" } else { "unreachable" })))

if ($health.Json -ne $null) {
  Write-Host ("  healthy={0} ready={1} fresh={2} stale={3}" -f $health.Json.healthy, $health.Json.ready, $health.Json.fresh, $health.Json.stale)
  Write-Host ("  snapshotAgeSeconds={0}" -f $health.Json.snapshotAgeSeconds)
}

if ($ready.Json -ne $null) {
  Write-Host ("  ready={0} fresh={1} stale={2}" -f $ready.Json.ready, $ready.Json.fresh, $ready.Json.stale)
}

if ($latest.Responded -and $latest.Json -ne $null -and $latest.Json.aggregate -ne $null) {
  Write-Host ("  frames={0} contract={1}/v{2}" -f $latest.Json.aggregate.acceptedFrames, $latest.Json.contract.name, $latest.Json.contract.schemaVersion)
}

if (-not $health.Responded -and -not $ready.Responded -and -not $latest.Responded) {
  exit 1
}

exit 0
