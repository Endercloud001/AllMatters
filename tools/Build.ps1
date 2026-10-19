param(
    [ValidateSet('public', 'public-beta')][string]$Branch = 'public-beta',
    [string]$GameDataDir,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$arguments = @('build', (Join-Path $repoDir 'AllMatters.csproj'), '-c', $Configuration, "-p:GameBranch=$Branch")
if ($GameDataDir) { $arguments += "-p:GameDataDir=$GameDataDir" }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "Build failed for $Branch (exit $LASTEXITCODE)." }
$outputDir = Join-Path $repoDir "bin/$Branch/$Configuration/net9.0"
$packageDir = Join-Path $repoDir "dist/$Branch/AllMatters"
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $outputDir 'AllMatters.dll'), (Join-Path $outputDir 'AllMatters.json') -Destination $packageDir
Write-Output "Development build packaged at $packageDir"
