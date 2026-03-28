# ----------------
# script name: Run-ChromaLink.ps1
# version: 0.2.0
# purpose: Wraps the ChromaLink .NET 9 CLI so common smoke, bench, replay, live, and window-prep commands are easy to run from PowerShell.
# total characters: 1762
# ----------------

param(
  [Parameter(Position = 0)]
  [ValidateSet("smoke", "bench", "capture-dump", "live", "watch", "replay", "prepare-window")]
  [string]$Mode = "smoke",

  [Parameter(Position = 1)]
  [string]$Argument1 = "",

  [Parameter(Position = 2)]
  [string]$Argument2 = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj"

if (-not (Test-Path $projectPath)) {
  throw "ChromaLink CLI project not found at $projectPath"
}

$cliArgs = @("run", "--project", $projectPath, "--", $Mode)

switch ($Mode) {
  "replay" {
    if ([string]::IsNullOrWhiteSpace($Argument1)) {
      throw "replay requires a BMP path as Argument1."
    }
    $cliArgs += $Argument1
  }
  "live" {
    if (-not [string]::IsNullOrWhiteSpace($Argument1)) {
      $cliArgs += $Argument1
    }
    if (-not [string]::IsNullOrWhiteSpace($Argument2)) {
      $cliArgs += $Argument2
    }
  }
  "watch" {
    if (-not [string]::IsNullOrWhiteSpace($Argument1)) {
      $cliArgs += $Argument1
    }
    if (-not [string]::IsNullOrWhiteSpace($Argument2)) {
      $cliArgs += $Argument2
    }
  }
  "prepare-window" {
    if (-not [string]::IsNullOrWhiteSpace($Argument1)) {
      $cliArgs += $Argument1
    }
    if (-not [string]::IsNullOrWhiteSpace($Argument2)) {
      $cliArgs += $Argument2
    }
  }
}

Write-Host "Running ChromaLink CLI: dotnet $($cliArgs -join ' ')" -ForegroundColor Cyan
& dotnet @cliArgs
exit $LASTEXITCODE
