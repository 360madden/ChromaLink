[CmdletBinding()]
param(
    [string]$SavedVariablesPath,
    [switch]$Json,
    [switch]$All
)

. "$PSScriptRoot\Rift-HelperCommon.ps1"

function Get-ChromaLinkDefaultAbilityExportPath {
    param(
        [string]$ExplicitPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $savedConfig = Get-ChromaLinkRiftSavedConfig
    $documentsDirectory = [string]$savedConfig.DocumentsDirectory
    if ([string]::IsNullOrWhiteSpace($documentsDirectory)) {
        $documentsDirectory = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'RIFT'
    }

    $documentsDirectory = $documentsDirectory -replace '/', '\'
    $savedRoot = Join-Path $documentsDirectory 'Interface\Saved'
    if (-not (Test-Path -LiteralPath $savedRoot)) {
        throw "RIFT saved-variables root was not found at '$savedRoot'."
    }

    $candidate = Get-ChildItem -Path $savedRoot -Filter 'ChromaLink.lua' -Recurse -ErrorAction Stop |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $candidate) {
        throw "No ChromaLink ability export file was found under '$savedRoot'."
    }

    return $candidate.FullName
}

function Read-LuaStringField {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$FieldName
    )

    $match = [regex]::Match(
        $Text,
        '(?s)\b' + [regex]::Escape($FieldName) + '\s*=\s*"(?<body>.*?)",')

    if (-not $match.Success) {
        return $null
    }

    $value = $match.Groups['body'].Value
    $value = $value -replace '\\\r?\n', "`n"
    $value = $value -replace '\\"', '"'
    $value = $value -replace '\\\\', '\'
    return $value
}

function Read-LuaScalarField {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$FieldName
    )

    $match = [regex]::Match(
        $Text,
        '(?m)\b' + [regex]::Escape($FieldName) + '\s*=\s*(?<value>"[^"]*"|[^,\r\n}]+)')

    if (-not $match.Success) {
        return $null
    }

    $value = $match.Groups['value'].Value.Trim()
    if ($value.StartsWith('"') -and $value.EndsWith('"')) {
        return $value.Substring(1, $value.Length - 2)
    }

    return $value
}

function Convert-ChromaLinkRawAbilityTable {
    param(
        [string]$RawText
    )

    if ([string]::IsNullOrWhiteSpace($RawText)) {
        return @()
    }

    $lines = @(
        $RawText -split "`n" |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and -not $_.StartsWith('--') }
    )

    if ($lines.Count -lt 2) {
        return @()
    }

    $rows = @()
    foreach ($line in $lines | Select-Object -Skip 1) {
        $parts = $line.Split('|')
        if ($parts.Count -lt 9) {
            continue
        }

        $rows += [pscustomobject]@{
            Name = $parts[0]
            CastSeconds = [double]$parts[1]
            CooldownSeconds = [double]$parts[2]
            Range = [int]$parts[3]
            Weapon = $parts[4]
            CostType = $parts[5]
            CostValue = [int]$parts[6]
            IdNew = $parts[7]
            Id = $parts[8]
        }
    }

    return $rows
}

$resolvedPath = Get-ChromaLinkDefaultAbilityExportPath -ExplicitPath $SavedVariablesPath
$item = Get-Item -LiteralPath $resolvedPath
$text = Get-Content -LiteralPath $resolvedPath -Raw

$playerBlock = [regex]::Match(
    $text,
    '(?s)\bplayer\s*=\s*\{\s*calling\s*=\s*(?<calling>"[^"]*"|[^,\r\n}]+),\s*level\s*=\s*(?<level>[\d.]+),\s*name\s*=\s*"(?<name>.*?)",\s*role\s*=\s*(?<role>"[^"]*"|[^,\r\n}]+)')

if (-not $playerBlock.Success) {
    throw "Could not parse the player block from '$resolvedPath'."
}

$normalizeScalar = {
    param($value)
    $textValue = [string]$value
    if ($textValue.StartsWith('"') -and $textValue.EndsWith('"')) {
        return $textValue.Substring(1, $textValue.Length - 2)
    }
    return $textValue
}

$offensiveRawText = Read-LuaStringField -Text $text -FieldName 'offensiveRawText'
$rawText = Read-LuaStringField -Text $text -FieldName 'rawText'

$result = [pscustomobject]@{
    Path = $resolvedPath
    LastWriteTime = $item.LastWriteTime.ToString('o')
    Status = Read-LuaScalarField -Text $text -FieldName 'status'
    ExportCount = [int](Read-LuaScalarField -Text $text -FieldName 'exportCount')
    ExportReason = Read-LuaScalarField -Text $text -FieldName 'exportReason'
    SchemaVersion = [int](Read-LuaScalarField -Text $text -FieldName 'schemaVersion')
    Player = [pscustomobject]@{
        Name = $playerBlock.Groups['name'].Value
        Level = [int]$playerBlock.Groups['level'].Value
        Calling = & $normalizeScalar $playerBlock.Groups['calling'].Value
        Role = & $normalizeScalar $playerBlock.Groups['role'].Value
    }
    Counts = [pscustomobject]@{
        Active = [int](Read-LuaScalarField -Text $text -FieldName 'active')
        Offensive = [int](Read-LuaScalarField -Text $text -FieldName 'offensive')
        Total = [int](Read-LuaScalarField -Text $text -FieldName 'total')
    }
    OffensiveAbilities = @(Convert-ChromaLinkRawAbilityTable -RawText $offensiveRawText)
    AllAbilities = @(Convert-ChromaLinkRawAbilityTable -RawText $rawText)
}

if ($Json) {
    if (-not $All) {
        $result = [pscustomobject]@{
            Path = $result.Path
            LastWriteTime = $result.LastWriteTime
            Status = $result.Status
            ExportCount = $result.ExportCount
            ExportReason = $result.ExportReason
            SchemaVersion = $result.SchemaVersion
            Player = $result.Player
            Counts = $result.Counts
            OffensiveAbilities = $result.OffensiveAbilities
        }
    }

    $result | ConvertTo-Json -Depth 6
    exit 0
}

Write-Host "ChromaLink ability export" -ForegroundColor Cyan
Write-Host ("Path: {0}" -f $result.Path)
Write-Host ("LastWriteTime: {0}" -f $result.LastWriteTime)
Write-Host ("Status: {0}  ExportCount: {1}  Reason: {2}" -f $result.Status, $result.ExportCount, $result.ExportReason)
Write-Host ("Player: {0}  Level: {1}  Calling: {2}  Role: {3}" -f $result.Player.Name, $result.Player.Level, $result.Player.Calling, $result.Player.Role)
Write-Host ("Counts: total={0} active={1} offensive={2}" -f $result.Counts.Total, $result.Counts.Active, $result.Counts.Offensive)
Write-Host ""
Write-Host "Offensive abilities" -ForegroundColor Cyan
$result.OffensiveAbilities |
    Sort-Object Name |
    Format-Table Name, CastSeconds, CooldownSeconds, Range, CostType, CostValue -AutoSize

if ($All) {
    Write-Host ""
    Write-Host "All exported abilities" -ForegroundColor Cyan
    $result.AllAbilities |
        Sort-Object Name |
        Format-Table Name, CastSeconds, CooldownSeconds, Range, CostType, CostValue -AutoSize
}
