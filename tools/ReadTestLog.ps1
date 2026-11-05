param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
$file = Get-Item -LiteralPath $Path
$summary = [ordered]@{
    Source = $file.FullName; Target = $null; Version = $null
    BuildExit = $null; HostExit = $null; Passed = $null
    FirstException = $null; FirstDiagnostic = $null; PreparationFailure = $null
    EngineErrors = 0; ModMetadataErrors = 0
}
$reader = [IO.StreamReader]::new($file.FullName)
try {
    $number = 0
    while ($null -ne ($line = $reader.ReadLine())) {
        $number++
        if ($line -match '^Testing (\S+) : (\S+)') { $summary.Target = $Matches[1]; $summary.Version = $Matches[2] }
        if ($line -match '^Test build exit: (-?\d+)$') { $summary.BuildExit = [int]$Matches[1] }
        if ($line -match '^Host exit: (-?\d+)$') { $summary.HostExit = [int]$Matches[1] }
        if ($line -match '^Passed (\d+) tests against ') { $summary.Passed = [int]$Matches[1] }
        $location = [pscustomobject]@{ Line = $number; Text = $line.Substring(0, [Math]::Min(400, $line.Length)) }
        if (!$summary.FirstException -and $line -match '^System\.\S*Exception:') { $summary.FirstException = $location }
        if (!$summary.PreparationFailure -and $line -match '^Preparation failed:') { $summary.PreparationFailure = $location }
        if ($line -match '^\[ERROR\] Attempting to register type .*not associated with any mod!') {
            $summary.ModMetadataErrors++
        } elseif ($line -match '^(ERROR:|FATAL:|\[(ERROR|FATAL)\])') {
            $summary.EngineErrors++
            if (!$summary.FirstDiagnostic) { $summary.FirstDiagnostic = $location }
        }
    }
} finally { $reader.Dispose() }
[pscustomobject]$summary
