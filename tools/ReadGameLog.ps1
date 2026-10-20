param(
    [string]$LogDir,
    [string]$ModId = 'AllMatters',
    [ValidateRange(1, 1000)][int]$Tail = 40,
    [switch]$IncludeErrors
)

$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$paths = & (Join-Path $PSScriptRoot 'ReadLocalPaths.ps1')
if (-not $LogDir) { $LogDir = $paths['GameLogDir'] }
if (-not $LogDir) { $LogDir = Join-Path $env:APPDATA 'SlayTheSpire2/logs' }
if (-not [IO.Path]::IsPathRooted($LogDir)) { $LogDir = Join-Path $repoDir $LogDir }
if (-not (Test-Path -LiteralPath $LogDir -PathType Container)) {
    throw "Game log directory not found: $LogDir. Set GameLogDir in Paths.local.props or pass -LogDir."
}
if ([string]::IsNullOrWhiteSpace($ModId)) { throw 'ModId must not be empty.' }

$latest = Get-ChildItem -LiteralPath $LogDir -File -Filter '*.log' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $latest) { throw "No .log files found in: $LogDir" }
Write-Output "Source: $($latest.FullName) (modified $($latest.LastWriteTime.ToString('s')))"
$pattern = [regex]::Escape($ModId)
if ($IncludeErrors) { $pattern = "(?:$pattern)|\b(?:ERROR|FATAL|Exception)\b" }
$matches = @(Get-Content -LiteralPath $latest.FullName |
    Select-String -Pattern $pattern | Select-Object -Last $Tail)
if ($matches.Count -eq 0) {
    Write-Output "No matching lines for '$ModId' in this log. This alone does not establish whether the mod loaded."
} else {
    $matches | ForEach-Object { "{0}: {1}" -f $_.LineNumber, $_.Line }
}
