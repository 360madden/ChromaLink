param(
  [Parameter(Mandatory = $true, Position = 0)]
  [ValidateRange(1, 12)]
  [int]$Slot,

  [switch]$Background,

  [switch]$Focus,

  [switch]$DryRun
)

$targetScript = Join-Path $PSScriptRoot "Trigger-RiftMainBar.ps1"

if (-not (Test-Path -LiteralPath $targetScript)) {
    throw "Main action bar helper not found at $targetScript"
}

$invokeSplat = @{
    Slot = $Slot
}

if ($Background) {
    $invokeSplat.Background = $true
}

if ($Focus) {
    $invokeSplat.Focus = $true
}

if ($DryRun) {
    $invokeSplat.DryRun = $true
}

& $targetScript @invokeSplat
