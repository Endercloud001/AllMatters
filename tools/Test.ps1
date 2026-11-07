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
        $logDir = Join-Path $repoDir '.local/tests'
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
        $logPath = Join-Path $logDir "$targetBranch-last.log"
        Set-Content -LiteralPath $logPath -Value "Testing $targetBranch : unknown"
        $hostStarted = $false
        try {
            # Build with the system SDK; only the Godot process uses the selected runtime root.
            $env:DOTNET_ROOT = $previousDotNetRoot
            $dataDir = if ($targetBranch -eq 'public') { $paths['PublicGameDataDir'] } else { $paths['BetaGameDataDir'] }
            if ($dataDir) {
                if (![IO.Path]::IsPathRooted($dataDir)) { $dataDir = Join-Path $repoDir $dataDir }
                $assembly = Get-Item -LiteralPath (Join-Path $dataDir 'sts2.dll')
                "Testing $targetBranch : $($assembly.VersionInfo.ProductVersion) [$($assembly.FullName)]" | Tee-Object -FilePath $logPath -Append
            }
            & dotnet build (Join-Path $testDir 'AllMatters.Tests.csproj') "-p:GameBranch=$targetBranch" --nologo 2>&1 | Tee-Object -FilePath $logPath -Append
            $buildExit = $LASTEXITCODE
            Add-Content -LiteralPath $logPath -Value "Test build exit: $buildExit"
            if ($buildExit -ne 0) { throw "Test build failed for $targetBranch (exit $buildExit)." }
            if (!$resourcesReady) {
                & (Join-Path $PSScriptRoot 'ExportResources.ps1') -GodotPath $godotExecutable 2>&1 | Tee-Object -FilePath $logPath -Append
                $resourcesReady = $true
            }
            if ($DotNetRoot) { $env:DOTNET_ROOT = $DotNetRoot }
            $arguments = @('--headless', '--path', $testDir)
            if ($Filter) { $arguments += '--', $Filter }
            $hostStarted = $true
            & $godotExecutable @arguments 2>&1 | Tee-Object -FilePath $logPath -Append
            $hostExit = $LASTEXITCODE
            Add-Content -LiteralPath $logPath -Value "Host exit: $hostExit"
            if ($hostExit -ne 0) { throw "Tests failed for $targetBranch (exit $hostExit)." }
        } catch {
            $stage = if ($hostStarted) { 'Host invocation failed' } else { 'Preparation failed' }
            Add-Content -LiteralPath $logPath -Value "${stage}: $_"
            throw
        } finally {
            Write-Output "Test log: $logPath (summarize with tools/ReadTestLog.ps1 -Path <log>)"
        }
    }
}
finally {
    $env:DOTNET_ROOT = $previousDotNetRoot
}
