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

function Invoke-BridgeEndpoint {
  param(
    [string]$Url,
    [int]$TimeoutSeconds
  )

  try {
    return Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec $TimeoutSeconds
  } catch {
    return $null
  }
}

$BaseUrl = Normalize-BaseUrl $BaseUrl
$trimmedBaseUrl = $BaseUrl.TrimEnd('/')
$healthUrls = @(
  ("$trimmedBaseUrl/latest-snapshot"),
  ("$trimmedBaseUrl/health"),
  ("$trimmedBaseUrl/ready"),
  ("$trimmedBaseUrl/snapshot")
)

Write-Host "ChromaLink HTTP Bridge Probe"
Write-Host ("BaseUrl: {0}" -f $BaseUrl)

$results = @()
foreach ($url in $healthUrls) {
  $response = Invoke-BridgeEndpoint -Url $url -TimeoutSeconds $TimeoutSeconds
  if ($null -ne $response) {
    $contentType = $response.Headers['Content-Type']
    if ($contentType -is [Array]) {
      $contentType = $contentType -join ', '
    }
    $results += [pscustomobject]@{
      Url = $url
      StatusCode = [int]$response.StatusCode
      ContentType = $contentType
      Length = if ($response.Content) { $response.Content.Length } else { 0 }
    }
  } else {
    $results += [pscustomobject]@{
      Url = $url
      StatusCode = 0
      ContentType = ''
      Length = 0
    }
  }
}

foreach ($result in $results) {
  Write-Host ("{0} -> {1} {2} bytes {3}" -f $result.Url, $result.StatusCode, $result.Length, $result.ContentType)
}

$anyOk = $results | Where-Object { $_.StatusCode -ge 200 -and $_.StatusCode -lt 300 }
if (-not $anyOk) {
  Write-Error "No HTTP bridge endpoints responded successfully."
  exit 1
}

exit 0
