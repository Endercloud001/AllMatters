# THROWAWAY native checkpoint probe; no game, Steam, mods or real save access.
param([string]$OutputDirectory = '.local/issue68-continuation/final-native')
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $repository
try {
    New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
    $records = @()
    $statuses = @()
    foreach ($target in @('public-beta', 'public')) {
        $log = Join-Path $OutputDirectory "$target.log"
        $code = 0
        try {
            & ./tools/Test.ps1 -Branch $target -Filter PrototypeCheckpoint -SkipResourceExport *> $log
            $code = $LASTEXITCODE
        }
        catch {
            $_ | Out-String | Add-Content -LiteralPath $log
            $code = 1
        }
        $statuses += [pscustomobject]@{ target = $target; exitCode = $code; command = "tools/Test.ps1 -Branch $target -Filter PrototypeCheckpoint -SkipResourceExport"; log = "$target.log" }
        foreach ($line in Get-Content -LiteralPath $log) {
            if ($line.StartsWith('CHECKPOINT_RESULT ')) {
                $record = $line.Substring('CHECKPOINT_RESULT '.Length) | ConvertFrom-Json
                $record | Add-Member -NotePropertyName target -NotePropertyValue $target
                $records += $record
            }
        }
    }
    $statuses | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'commands.json')
    $records | ConvertTo-Json -Depth 12 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'observations.json')
    if (@($statuses | Where-Object exitCode -ne 0).Count -or $records.Count -ne 80) {
        throw "Native matrix incomplete: $($records.Count)/80; see commands.json and original logs"
    }
    $counterexamples = @($records | Where-Object { !$_.adapted -and !$_.modMatches }).Count
    $adaptedMatches = @($records | Where-Object { $_.adapted -and $_.modMatches }).Count
    if ($counterexamples -ne 24 -or $adaptedMatches -ne 40) { throw 'Unexpected result counts; inspect observations.json' }
    Write-Output "Native scenarios=80; original counterexamples=$counterexamples; adapted matching=$adaptedMatches/40; both command exits=0; output=$OutputDirectory"
}
finally { Pop-Location }
