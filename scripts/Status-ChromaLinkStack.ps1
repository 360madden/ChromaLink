param(
  [string]$BaseUrl = $(if ($env:CHROMALINK_HTTP_BRIDGE_URL) { $env:CHROMALINK_HTTP_BRIDGE_URL } else { 'http://127.0.0.1:7337/' }),
  [switch]$IncludeProcesses
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

foreach ($endpoint in $endpoints) {
  $result = Test-Endpoint -Url $endpoint
  Write-Host ("{0} -> {1} {2} bytes {3}" -f $result.Url, $result.StatusCode, $result.Length, $result.ContentType)
}

if ($IncludeProcesses) {
  Write-Host ""
  Write-Host "Processes"
  Write-Host ("HttpBridge: {0}" -f (Get-MatchCount "*ChromaLink.HttpBridge*"))
  Write-Host ("Monitor: {0}" -f (Get-MatchCount "*ChromaLink.Monitor*"))
  Write-Host ("CLI Watch: {0}" -f (Get-MatchCount "*ChromaLink.Cli*watch*"))
}

exit 0
