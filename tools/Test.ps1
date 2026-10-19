param(
    [ValidateSet('public', 'public-beta')][string]$Branch = 'public-beta',
    [Parameter(Mandatory)][string]$GodotPath,
    [string]$DotNetRoot,
    [string]$Filter
)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path $repoDir 'src/Tests'
$godotExecutable = (Resolve-Path -LiteralPath $GodotPath).Path
& dotnet build (Join-Path $testDir 'AllMatters.Tests.csproj') "-p:GameBranch=$Branch" --nologo
if ($LASTEXITCODE -ne 0) { throw "Test build failed for $Branch (exit $LASTEXITCODE)." }
$previousDotNetRoot = $env:DOTNET_ROOT
try {
    if ($DotNetRoot) { $env:DOTNET_ROOT = (Resolve-Path -LiteralPath $DotNetRoot).Path }
    $arguments = @('--headless', '--path', $testDir)
    if ($Filter) { $arguments += '--', $Filter }
    & $godotExecutable @arguments
    if ($LASTEXITCODE -ne 0) { throw "Tests failed for $Branch (exit $LASTEXITCODE)." }
}
finally {
    $env:DOTNET_ROOT = $previousDotNetRoot
}
