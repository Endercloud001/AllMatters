param([ValidateSet('public', 'public-beta', 'All')][string]$Branch = 'All')
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $repoDir '.local/checks'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
Start-Transcript -Path (Join-Path $logDir 'last-run.log') -Force | Out-Null
try {
    Push-Location $repoDir
    try {
        foreach ($script in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' -File) {
            $parseErrors = $null
            $tokens = $null
            $null = [Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$parseErrors)
            if ($parseErrors) { throw "PowerShell syntax error in $($script.Name): $($parseErrors.Message -join '; ')" }
        }
        & git diff --check HEAD
        if ($LASTEXITCODE -ne 0) { throw 'Git whitespace check failed.' }
        & (Join-Path $PSScriptRoot 'Test.ps1') -Branch $Branch
        $branches = if ($Branch -eq 'All') { @('public-beta', 'public') } else { @($Branch) }
        foreach ($target in $branches) {
            & (Join-Path $PSScriptRoot 'Build.ps1') -Branch $target
        }
        Write-Output 'AllMatters checks PASSED.'
    }
    finally { Pop-Location }
}
catch {
    Write-Error "AllMatters checks FAILED: $_" -ErrorAction Continue
    exit 1
}
finally { Stop-Transcript | Out-Null }
