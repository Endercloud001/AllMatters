param(
    [ValidateSet('public', 'public-beta', 'All')][string]$Branch = 'public-beta',
    [string]$GodotPath,
    [string]$DotNetRoot,
    [string]$Filter,
    [switch]$SkipResourceExport
)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path $repoDir 'src/Tests'
$paths = & (Join-Path $PSScriptRoot 'ReadLocalPaths.ps1')
if (!$GodotPath) { $GodotPath = $paths['GodotPath'] }
if (!$DotNetRoot) { $DotNetRoot = $paths['TestDotNetRoot'] }
if (!$GodotPath) { throw 'Set GodotPath in Paths.local.props or pass -GodotPath (Godot mono executable).' }
if (![IO.Path]::IsPathRooted($GodotPath)) { $GodotPath = Join-Path $repoDir $GodotPath }
if ($DotNetRoot -and ![IO.Path]::IsPathRooted($DotNetRoot)) { $DotNetRoot = Join-Path $repoDir $DotNetRoot }
$godotExecutable = (Resolve-Path -LiteralPath $GodotPath).Path
$resourcesReady = $SkipResourceExport.IsPresent
if ($resourcesReady -and !(Test-Path -LiteralPath (Join-Path $repoDir 'dist/AllMatters.pck') -PathType Leaf)) {
    throw 'No resource pack to reuse. Run without -SkipResourceExport first.'
}
$previousDotNetRoot = $env:DOTNET_ROOT
try {
    if ($DotNetRoot) { $DotNetRoot = (Resolve-Path -LiteralPath $DotNetRoot).Path }
    $branches = if ($Branch -eq 'All') { @('public-beta', 'public') } else { @($Branch) }
    foreach ($targetBranch in $branches) {
        # Build with the system SDK; only the Godot process uses the selected runtime root.
        $env:DOTNET_ROOT = $previousDotNetRoot
        $dataDir = if ($targetBranch -eq 'public') { $paths['PublicGameDataDir'] } else { $paths['BetaGameDataDir'] }
        if ($dataDir) {
            if (![IO.Path]::IsPathRooted($dataDir)) { $dataDir = Join-Path $repoDir $dataDir }
            $assembly = Get-Item -LiteralPath (Join-Path $dataDir 'sts2.dll')
            Write-Output "Testing $targetBranch : $($assembly.VersionInfo.ProductVersion) [$($assembly.FullName)]"
        }
        & dotnet build (Join-Path $testDir 'AllMatters.Tests.csproj') "-p:GameBranch=$targetBranch" --nologo
        if ($LASTEXITCODE -ne 0) { throw "Test build failed for $targetBranch (exit $LASTEXITCODE)." }
        if (!$resourcesReady) {
            & (Join-Path $PSScriptRoot 'ExportResources.ps1') -GodotPath $godotExecutable
            $resourcesReady = $true
        }
        if ($DotNetRoot) { $env:DOTNET_ROOT = $DotNetRoot }
        $arguments = @('--headless', '--path', $testDir)
        if ($Filter) { $arguments += '--', $Filter }
        & $godotExecutable @arguments
        if ($LASTEXITCODE -ne 0) { throw "Tests failed for $targetBranch (exit $LASTEXITCODE)." }
    }
}
finally {
    $env:DOTNET_ROOT = $previousDotNetRoot
}
