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
    $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec $TimeoutSeconds
    return [pscustomobject]@{
      Responded = $true
      StatusCode = [int]$response.StatusCode
      ContentType = $response.Headers['Content-Type']
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
        Responded = $true
        StatusCode = [int]$httpResponse.StatusCode
        ContentType = $contentType
        Length = $length
      }
    }

    return [pscustomobject]@{
      Responded = $false
      StatusCode = 0
      ContentType = ''
      Length = 0
    }
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
  $contentType = $response.ContentType
  if ($contentType -is [Array]) {
    $contentType = $contentType -join ', '
  }
  $results += [pscustomobject]@{
    Url = $url
    StatusCode = $response.StatusCode
    ContentType = $contentType
    Length = $response.Length
    Responded = $response.Responded
  }
}

foreach ($result in $results) {
  Write-Host ("{0} -> {1} {2} bytes {3}" -f $result.Url, $result.StatusCode, $result.Length, $result.ContentType)
}

$anyResponse = $results | Where-Object { $_.Responded }
if (-not $anyResponse) {
  Write-Error "No HTTP bridge endpoints responded."
  exit 1
}

exit 0
