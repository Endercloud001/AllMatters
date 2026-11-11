#Requires -Version 7
param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
$files = if (Test-Path -LiteralPath $Path -PathType Container) {
    @(Get-ChildItem -LiteralPath $Path -Filter '*.json' -File)
} else { @(Get-Item -LiteralPath $Path) }
foreach ($file in $files) {
    $capture = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    if ($capture.schemaVersion -ne 1 -or !$capture.diagnosticBuild) {
        throw "Not a supported combat capture: $($file.FullName)"
    }
    $frames = @($capture.frames)
    $gaps = @($frames | ForEach-Object { [double]$_.FrameGapMs } | Sort-Object)
    $longest = @($frames | Sort-Object FrameGapMs -Descending | Select-Object -First 5)
    [pscustomobject]@{
        Source = $file.FullName
        GameBranch = $capture.gameBranch
        ModVersion = $capture.modVersion
        Room = $capture.room
        Outcome = $capture.outcome
        Frames = $frames.Count
        MaxFrameGapMs = if ($gaps.Count) { $gaps[-1] } else { $null }
        MedianFrameGapMs = if ($gaps.Count) {
            ($gaps[[int][Math]::Floor(($gaps.Count - 1) / 2)] + $gaps[[int][Math]::Floor($gaps.Count / 2)]) / 2
        } else { $null }
        FramesOver33ms = @($frames | Where-Object FrameGapMs -gt 33.333).Count
        FramesOver100ms = @($frames | Where-Object FrameGapMs -gt 100).Count
        LongestFrames = $longest
        Stages = @($capture.stages)
        Note = $capture.note
    }
}
