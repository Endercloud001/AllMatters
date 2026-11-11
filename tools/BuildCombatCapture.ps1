#Requires -Version 7
param(
    [ValidateSet('public', 'public-beta')][string]$Branch = 'public-beta',
    [switch]$SkipResourceExport
)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$captureDir = Join-Path $repoDir '.local/combat-captures'
$packageDir = Join-Path $repoDir "dist/combat-capture/AllMatters-$Branch"
New-Item -ItemType Directory -Path $captureDir, $packageDir -Force | Out-Null
if ($SkipResourceExport) {
    if (!(Test-Path -LiteralPath (Join-Path $repoDir 'dist/AllMatters.pck') -PathType Leaf)) {
        throw 'No resource pack to reuse. Run without -SkipResourceExport first.'
    }
} else {
    & (Join-Path $PSScriptRoot 'ExportResources.ps1')
}
& dotnet build (Join-Path $repoDir 'AllMatters.csproj') -c PerfCapture "-p:GameBranch=$Branch" '-p:CombatPerfCapture=true' "-p:CombatPerfCaptureOutputDir=$captureDir"
if ($LASTEXITCODE -ne 0) { throw "Capture build failed for $Branch (exit $LASTEXITCODE)." }
$outputDir = Join-Path $repoDir "bin/$Branch/PerfCapture/net9.0"
foreach ($file in @('AllMatters.dll', 'AllMatters.json')) {
    Copy-Item -LiteralPath (Join-Path $outputDir $file) -Destination $packageDir -Force
}
foreach ($file in @('LICENSE', 'THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $repoDir $file) -Destination $packageDir -Force
}
Copy-Item -LiteralPath (Join-Path $repoDir 'dist/AllMatters.pck') -Destination $packageDir -Force
Write-Output "Temporary capture package: $packageDir"
Write-Output "Capture output: $captureDir"
