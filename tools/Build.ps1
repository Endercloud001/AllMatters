param(
    [ValidateSet('public', 'public-beta')][string]$Branch = 'public-beta',
    [string]$GameDataDir,
    [string]$GodotPath,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$arguments = @('build', (Join-Path $repoDir 'AllMatters.csproj'), '-c', $Configuration, "-p:GameBranch=$Branch")
if ($GameDataDir) { $arguments += "-p:GameDataDir=$GameDataDir" }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "Build failed for $Branch (exit $LASTEXITCODE)." }
& (Join-Path $PSScriptRoot 'ExportResources.ps1') -GodotPath $GodotPath
$outputDir = Join-Path $repoDir "bin/$Branch/$Configuration/net9.0"
$packageDir = Join-Path $repoDir "dist/$Branch/AllMatters"
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $outputDir 'AllMatters.dll'), (Join-Path $outputDir 'AllMatters.json') -Destination $packageDir
Copy-Item -LiteralPath (Join-Path $repoDir 'dist/AllMatters.pck'), (Join-Path $repoDir 'LICENSE'), (Join-Path $repoDir 'THIRD_PARTY_NOTICES.md') -Destination $packageDir
Write-Output "Development build packaged at $packageDir"
