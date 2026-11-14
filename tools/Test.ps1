#Requires -Version 7
param(
    [ValidateSet('public', 'public-beta', 'All')][string]$Branch = 'public-beta',
    [string]$GodotPath,
    [string]$DotNetRoot,
    [string]$Filter,
    [switch]$SkipResourceExport,
    [string]$ArchiveName = 'run',
    [string]$ArchivePath,
    [switch]$StrictDiagnostics
)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path $repoDir 'src/Tests'
$logDir = Join-Path $repoDir '.local/tests'
if ($ArchiveName -notmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]{0,79}$' -or $ArchiveName.EndsWith('.')) {
    throw 'ArchiveName must be 1-80 letters, digits, dots, underscores or hyphens, with no trailing dot.'
}
if (!$ArchivePath) { $ArchivePath = Join-Path $logDir 'archives' }
if (![IO.Path]::IsPathRooted($ArchivePath)) { $ArchivePath = Join-Path $repoDir $ArchivePath }
New-Item -ItemType Directory -Path $ArchivePath -Force | Out-Null
# CreateDirectory would silently reuse an existing directory; New-Item without Force refuses it.
$runDir = Join-Path $ArchivePath ("$ArchiveName-" + (Get-Date -Format 'yyyyMMdd-HHmmssfff') + '-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDir | Out-Null
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$command = 'Test.ps1 ' + (($PSBoundParameters.GetEnumerator() | Sort-Object Key | ForEach-Object {
    if ($_.Value -is [Management.Automation.SwitchParameter]) {
        '-{0}:${1}' -f $_.Key, $_.Value.IsPresent.ToString().ToLowerInvariant()
    } else {
        "-{0} '{1}'" -f $_.Key, ([string]$_.Value).Replace("'", "''")
    }
}) -join ' ')
$previousDotNetRoot = $env:DOTNET_ROOT
$results = [Collections.Generic.List[object]]::new()
$failure = $null
$branches = if ($Branch -eq 'All') { @('public-beta', 'public') } else { @($Branch) }
$resourcesReady = $SkipResourceExport.IsPresent
try {
    foreach ($targetBranch in $branches) {
        $logPath = Join-Path $runDir "$targetBranch.log"
        Set-Content -LiteralPath $logPath -Value @("Testing $targetBranch : unknown", "Test command: $command", 'Host started: False')
        $hostStarted = $false
        try {
            # Resolve preparation inside the target log so even a missing executable leaves evidence.
            $paths = & (Join-Path $PSScriptRoot 'ReadLocalPaths.ps1')
            if (!$GodotPath) { $GodotPath = $paths['GodotPath'] }
            if (!$DotNetRoot) { $DotNetRoot = $paths['TestDotNetRoot'] }
            if (!$GodotPath) { throw 'Set GodotPath in Paths.local.props or pass -GodotPath (Godot mono executable).' }
            if (![IO.Path]::IsPathRooted($GodotPath)) { $GodotPath = Join-Path $repoDir $GodotPath }
            $godotExecutable = (Resolve-Path -LiteralPath $GodotPath).Path
            if ($DotNetRoot) {
                if (![IO.Path]::IsPathRooted($DotNetRoot)) { $DotNetRoot = Join-Path $repoDir $DotNetRoot }
                $DotNetRoot = (Resolve-Path -LiteralPath $DotNetRoot).Path
            }
            if ($resourcesReady -and !(Test-Path -LiteralPath (Join-Path $repoDir 'dist/AllMatters.pck') -PathType Leaf)) {
                throw 'No resource pack to reuse. Run without -SkipResourceExport first.'
            }
            $env:DOTNET_ROOT = $previousDotNetRoot
            $dataDir = if ($targetBranch -eq 'public') { $paths['PublicGameDataDir'] } else { $paths['BetaGameDataDir'] }
            if ($dataDir) {
                if (![IO.Path]::IsPathRooted($dataDir)) { $dataDir = Join-Path $repoDir $dataDir }
                $assembly = Get-Item -LiteralPath (Join-Path $dataDir 'sts2.dll')
                "Testing $targetBranch : $($assembly.VersionInfo.ProductVersion) [$($assembly.FullName)]" | Tee-Object -FilePath $logPath -Append
            }
            Add-Content -LiteralPath $logPath -Value "Build command: dotnet build $testDir/AllMatters.Tests.csproj -p:GameBranch=$targetBranch --nologo"
            & dotnet build (Join-Path $testDir 'AllMatters.Tests.csproj') "-p:GameBranch=$targetBranch" --nologo 2>&1 | Tee-Object -FilePath $logPath -Append
            $buildExit = $LASTEXITCODE
            Add-Content -LiteralPath $logPath -Value "Test build exit: $buildExit"
            if ($buildExit -ne 0) { throw "Test build failed for $targetBranch (exit $buildExit)." }
            if (!$resourcesReady) {
                Add-Content -LiteralPath $logPath -Value "Export command: ExportResources.ps1 -GodotPath $godotExecutable"
                & (Join-Path $PSScriptRoot 'ExportResources.ps1') -GodotPath $godotExecutable 2>&1 | Tee-Object -FilePath $logPath -Append
                $resourcesReady = $true
            }
            if ($DotNetRoot) { $env:DOTNET_ROOT = $DotNetRoot }
            $arguments = @('--headless', '--path', $testDir)
            if ($Filter) { $arguments += '--', $Filter }
            Add-Content -LiteralPath $logPath -Value @("Host command: $godotExecutable $($arguments | ConvertTo-Json -Compress)", 'Host started: True')
            $hostStarted = $true
            & $godotExecutable @arguments 2>&1 | Tee-Object -FilePath $logPath -Append
            $hostExit = $LASTEXITCODE
            Add-Content -LiteralPath $logPath -Value "Host exit: $hostExit"
            if ($hostExit -ne 0) { throw "Tests failed for $targetBranch (exit $hostExit)." }
        } catch {
            $failure = $_
            $stage = if ($hostStarted) { 'Host invocation failed' } else { 'Preparation failed' }
            Add-Content -LiteralPath $logPath -Value "${stage}: $_"
        } finally {
            # Only this iteration's log is copied; never collect a previous target's last.log.
            try {
                Copy-Item -LiteralPath $logPath -Destination (Join-Path $logDir "$targetBranch-last.log") -Force
            } catch {
                if (!$failure) { $failure = $_ }
                Write-Warning "last.log update failed; raw evidence: $logPath. $_"
            }
            try {
                $summary = & (Join-Path $PSScriptRoot 'ReadTestLog.ps1') -Path $logPath -StrictDiagnostics:$StrictDiagnostics -NoExit
                $results.Add($summary)
                if (!$summary.Success -and !$failure) { $failure = "Test summary failed for $targetBranch." }
            } catch {
                if (!$failure) { $failure = $_ }
                Write-Warning "Summary failed; raw evidence: $logPath. $_"
            }
            Write-Output "Test log: $logPath"
        }
        if ($failure) { break }
    }
} finally {
    $env:DOTNET_ROOT = $previousDotNetRoot
    try {
        [pscustomobject]@{ Command = $command; RequestedTarget = $Branch; StrictDiagnostics = $StrictDiagnostics.IsPresent
            Success = !$failure; Failure = $(if ($failure) { "$failure" } else { $null }); Targets = @($results.ToArray())
            ArchivePath = $runDir } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runDir 'summary.json')
        Write-Output "Test summary: $(Join-Path $runDir 'summary.json')"
    } catch {
        if (!$failure) { $failure = $_ }
        Write-Warning "Cannot write summary; raw evidence: $runDir. $_"
    }
}
if ($failure) { throw $failure }
$global:LASTEXITCODE = 0
