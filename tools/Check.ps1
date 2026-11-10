#Requires -Version 7
param([ValidateSet('public', 'public-beta', 'All')][string]$Branch = 'All')
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $repoDir '.local/checks'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$results = [Collections.Generic.List[object]]::new()

function Invoke-Check([string]$Name, [scriptblock]$Action, [string]$SkipReason) {
    if ($SkipReason) {
        $results.Add([pscustomobject]@{ Check = $Name; Status = 'SKIP'; Detail = $SkipReason })
        return
    }
    try {
        & $Action
        $results.Add([pscustomobject]@{ Check = $Name; Status = 'PASS'; Detail = '' })
    }
    catch {
        $results.Add([pscustomobject]@{ Check = $Name; Status = 'FAIL'; Detail = "$_" })
        Write-Error $_ -ErrorAction Continue
    }
}

Start-Transcript -Path (Join-Path $logDir 'last-run.log') -Force | Out-Null
try {
    Push-Location $repoDir
    try {
        Invoke-Check 'PowerShell syntax' {
            $failures = @()
            foreach ($script in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' -File -Recurse) {
                $parseErrors = $null
                $tokens = $null
                $null = [Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$parseErrors)
                if ($parseErrors) { $failures += "$($script.Name): $($parseErrors.Message -join '; ')" }
            }
            if ($failures.Count) { throw ($failures -join [Environment]::NewLine) }
        }
        Invoke-Check 'Workflow scripts' { & (Join-Path $PSScriptRoot 'Tests/Workflow.Tests.ps1') }
        Invoke-Check 'Git whitespace' {
            & git diff --check HEAD
            if ($LASTEXITCODE -ne 0) { throw "Git whitespace check failed (exit $LASTEXITCODE)." }
        }
        Invoke-Check 'Placeholder scan' {
            $hits = @()
            $srcRoot = Join-Path $repoDir 'src'
            # Scans src/**/*.cs outside src/Tests/; a tree without src/ has nothing to scan.
            if (Test-Path -LiteralPath $srcRoot) {
                foreach ($file in Get-ChildItem -LiteralPath $srcRoot -Filter '*.cs' -File -Recurse) {
                    $firstSegment = [IO.Path]::GetRelativePath($srcRoot, $file.FullName).Split([IO.Path]::DirectorySeparatorChar)[0]
                    if ([string]::Equals($firstSegment, 'Tests', [StringComparison]::OrdinalIgnoreCase)) { continue }
                    $lineNumber = 0
                    foreach ($line in [IO.File]::ReadLines($file.FullName)) {
                        $lineNumber++
                        if ($line -match 'Placeholder|In a real implementation|TODO: Implement') {
                            $hits += "$($file.FullName.Substring($repoDir.Length + 1)):${lineNumber}: $($line.Trim())"
                        }
                    }
                }
            }
            if ($hits.Count) { throw ($hits -join [Environment]::NewLine) }
        }
        Invoke-Check 'Resources' { & (Join-Path $PSScriptRoot 'ExportResources.ps1') }
        $skipReason = if ($results[-1].Status -ne 'PASS') { 'Fresh resource export failed.' } else { '' }
        $branches = if ($Branch -eq 'All') { @('public-beta', 'public') } else { @($Branch) }
        foreach ($target in $branches) {
            Invoke-Check "Tests $target" { & (Join-Path $PSScriptRoot 'Test.ps1') -Branch $target -SkipResourceExport } -SkipReason $skipReason
        }
        foreach ($target in $branches) {
            Invoke-Check "Build $target" { & (Join-Path $PSScriptRoot 'Build.ps1') -Branch $target -SkipResourceExport } -SkipReason $skipReason
        }
        $results | Format-Table -AutoSize -Wrap
        if (@($results | Where-Object Status -ne 'PASS').Count) {
            Write-Output 'AllMatters checks FAILED. See .local/checks/last-run.log.'
            exit 1
        }
        Write-Output 'AllMatters checks PASSED.'
    }
    finally { Pop-Location }
}
finally { Stop-Transcript | Out-Null }
