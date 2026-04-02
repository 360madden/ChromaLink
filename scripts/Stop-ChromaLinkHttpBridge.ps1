param(
  [switch]$Force
)

$ErrorActionPreference = "Stop"

function Get-BridgeProcessMatches {
  $patterns = @(
    '*ChromaLink.HttpBridge*',
    '*ChromaLink.HttpBridge.csproj*',
    '*ChromaLink.HttpBridge.exe*'
  )

  $matches = @()
  foreach ($pattern in $patterns) {
    $matches += Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like $pattern }
  }

  @($matches | Sort-Object ProcessId -Unique)
}

$processes = Get-BridgeProcessMatches

if ($processes.Count -eq 0) {
  Write-Host "No ChromaLink HTTP Bridge processes matched."
  exit 0
}

foreach ($process in $processes) {
  try {
    Stop-Process -Id $process.ProcessId -Force:$Force.IsPresent -ErrorAction Stop
    $processName = if ([string]::IsNullOrWhiteSpace($process.Name)) { 'process' } else { $process.Name }
    Write-Host ("Stopped {0} (PID {1})" -f $processName, $process.ProcessId)
  } catch {
    Write-Warning ("Failed to stop PID {0}: {1}" -f $process.ProcessId, $_.Exception.Message)
  }
}

exit 0
