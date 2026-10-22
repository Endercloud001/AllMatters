$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
Push-Location $repoDir
try {
    if (!(Test-Path -LiteralPath (Join-Path $repoDir '.githooks/pre-push') -PathType Leaf)) {
        throw 'The repository pre-push script is missing; hook configuration was not changed.'
    }
    $existing = & git config --local --get core.hooksPath
    if ($existing -and $existing -ne '.githooks') {
        throw 'An existing hooksPath is configured; preserve it and integrate checks there manually.'
    }
    $effective = & git config --get core.hooksPath
    if ($effective -and $effective -ne '.githooks') {
        throw 'An inherited hooksPath is configured; preserve its existing hooks.'
    }
    $hookDirectory = & git rev-parse --git-path hooks
    if (!$existing -and (Test-Path -LiteralPath $hookDirectory)) {
        $activeHooks = @(Get-ChildItem -LiteralPath $hookDirectory -File | Where-Object Name -NotLike '*.sample')
        if ($activeHooks.Count -gt 0) {
            throw 'Existing Git hooks are present; preserve them and integrate checks there manually.'
        }
    }
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
        & chmod +x (Join-Path $repoDir '.githooks/pre-push')
        if ($LASTEXITCODE -ne 0) { throw 'Could not make the hook executable.' }
    }
    & git config --local core.hooksPath .githooks
    if ($LASTEXITCODE -ne 0) { throw 'Could not configure repository-local hooks.' }
    Write-Output 'Installed advisory pre-push checks for this repository. Failures are reported, not blocked.'
}
finally { Pop-Location }
