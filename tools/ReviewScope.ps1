param([string]$Base = 'origin/main', [string]$ReviewedFrom)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
Push-Location $repoDir
try {
    $baseCommit = & git rev-parse --verify --end-of-options "$Base^{commit}"
    if ($LASTEXITCODE -ne 0) { throw 'Base must resolve to a commit.' }
    $mergeBase = & git merge-base $baseCommit HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Base and HEAD have no common ancestor.' }
    Write-Output "PR comparison: $Base...HEAD"
    & git log --oneline "$baseCommit..HEAD"
    & git diff --stat "$mergeBase..HEAD"
    if ($ReviewedFrom) {
        $reviewCommit = & git rev-parse --verify --end-of-options "$ReviewedFrom^{commit}"
        if ($LASTEXITCODE -ne 0) { throw 'ReviewedFrom must resolve to a commit.' }
        if ($reviewCommit -ne $mergeBase) {
            Write-Warning 'The reviewed start differs from the PR merge-base. Inspect the extra commits before merging.'
            & git log --oneline "$mergeBase..$reviewCommit"
        }
    }
    Write-Output 'Uncommitted changes (outside the committed PR comparison):'
    & git status --short
}
finally { Pop-Location }
