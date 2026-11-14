param([Parameter(Mandatory)][string]$Path, [switch]$StrictDiagnostics, [switch]$NoExit)
$ErrorActionPreference = 'Stop'
$file = Get-Item -LiteralPath $Path
$summary = [ordered]@{
    Source = $file.FullName; Target = $null; Version = $null
    BuildExit = $null; HostExit = $null; Passed = $null
    FirstException = $null; FirstDiagnostic = $null; PreparationFailure = $null
    EngineErrors = 0; ModMetadataErrors = 0; Command = $null; HostStarted = $false
    RunnerFailure = $null; Success = $false; BuildCommand = $null; HostCommand = $null
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
        if ($line -match '^Test command: (.*)$') { $summary.Command = $Matches[1] }
        if ($line -match '^Build command: (.*)$') { $summary.BuildCommand = $Matches[1] }
        if ($line -match '^Host command: (.*)$') { $summary.HostCommand = $Matches[1] }
        if ($line -eq 'Host started: True') { $summary.HostStarted = $true }
        if ($line -match '^(Preparation failed|Host invocation failed): (.*)$') { $summary.RunnerFailure = $Matches[2] }
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
$summary.Success = $null -ne $summary.Passed -and $summary.ModMetadataErrors -le 1 -and !$summary.RunnerFailure -and
    ($null -eq $summary.BuildExit -or $summary.BuildExit -eq 0) -and ($null -eq $summary.HostExit -or $summary.HostExit -eq 0)
if ($StrictDiagnostics) {
    $summary.Success = $summary.Success -and $summary.Passed -gt 0 -and $summary.BuildExit -eq 0 -and $summary.HostExit -eq 0 -and
        $summary.EngineErrors -eq 0 -and $summary.ModMetadataErrors -eq 0
}
[pscustomobject]$summary
$global:LASTEXITCODE = if ($summary.Success) { 0 } else { 1 }
if (!$NoExit) { exit $LASTEXITCODE }
