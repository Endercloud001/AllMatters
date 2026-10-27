$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$shellExecutable = (Get-Process -Id $PID).Path
$fixtureRoot = Join-Path $repoDir ('.local/workflow-tests/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null

function Assert($Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
}
function Write-FixtureFile([string]$Name, [string]$Text) {
    Set-Content -LiteralPath (Join-Path $script:fixture $Name) -Value $Text
}
function New-Fixture([string]$Name) {
    $script:fixture = Join-Path $fixtureRoot $Name
    New-Item -ItemType Directory -Path (Join-Path $fixture 'tools') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $fixture 'tools/Tests') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $fixture 'dist') -Force | Out-Null
    Write-FixtureFile 'calls.txt' ''
    Write-FixtureFile 'failure.txt' ''
    Write-FixtureFile 'tools/Tests/Workflow.Tests.ps1' "'Fixture script tests passed'"
    Write-FixtureFile 'tools/ExportResources.ps1' @'
param($GodotPath)
$root = Split-Path -Parent $PSScriptRoot
Add-Content (Join-Path $root 'calls.txt') 'export'
if ((Get-Content (Join-Path $root 'failure.txt')) -eq 'export') { throw 'injected export failure' }
Set-Content (Join-Path $root 'dist/AllMatters.pck') 'fixture pack'
'Resource export completed'
'@
}
function Run-Fixture {
    & $shellExecutable -NoProfile -File (Join-Path $fixture 'run.ps1') *> (Join-Path $fixture 'output.log')
    $script:code = $LASTEXITCODE
    $script:calls = @(Get-Content (Join-Path $fixture 'calls.txt') | Where-Object { $_ })
}

foreach ($scenario in @('whitespace', 'tests-public-beta', 'export', 'success')) {
    New-Fixture "check-$scenario"
    Copy-Item -LiteralPath (Join-Path $repoDir 'tools/Check.ps1') -Destination (Join-Path $fixture 'tools/Check.ps1')
    Write-FixtureFile 'failure.txt' $scenario
    Write-FixtureFile 'tools/Test.ps1' @'
param($Branch, [switch]$SkipResourceExport)
$root = Split-Path -Parent $PSScriptRoot
if (!$SkipResourceExport) { throw 'Check must reuse its exported resources' }
Add-Content (Join-Path $root 'calls.txt') "tests-$Branch"
if ((Get-Content (Join-Path $root 'failure.txt')) -eq "tests-$Branch") { throw 'injected test failure' }
'@
    Write-FixtureFile 'tools/Build.ps1' @'
param($Branch, [switch]$SkipResourceExport)
if (!$SkipResourceExport) { throw 'Check must reuse its exported resources' }
Add-Content (Join-Path (Split-Path -Parent $PSScriptRoot) 'calls.txt') "build-$Branch"
'@
    Write-FixtureFile 'run.ps1' @'
function global:git {
    $global:LASTEXITCODE = if ((Get-Content (Join-Path $PSScriptRoot 'failure.txt')) -eq 'whitespace') { 1 } else { 0 }
}
& (Join-Path $PSScriptRoot 'tools/Check.ps1')
exit $LASTEXITCODE
'@
    Run-Fixture
    Assert ($code -eq $(if ($scenario -eq 'success') { 0 } else { 1 })) "Check must preserve $scenario exit status; see $fixture/output.log"
    $expected = if ($scenario -eq 'export') { 'export' } else { 'export,tests-public-beta,tests-public,build-public-beta,build-public' }
    Assert (($calls -join ',') -eq $expected) "Check must export once and continue independent branches after $scenario. Calls: $calls"
    if ($scenario -eq 'export') {
        Assert ((Get-Content (Join-Path $fixture 'output.log') -Raw).Contains('SKIP')) 'Resource failure must explicitly mark dependent steps skipped.'
    }
    "PASS: Check $scenario"
}

foreach ($scenario in @('compile-failure', 'fresh', 'reuse', 'missing-pack')) {
    New-Fixture "test-$scenario"
    Copy-Item -LiteralPath (Join-Path $repoDir 'tools/Test.ps1') -Destination (Join-Path $fixture 'tools/Test.ps1')
    Write-FixtureFile 'failure.txt' $scenario
    if ($scenario -eq 'reuse') { Write-FixtureFile 'dist/AllMatters.pck' 'existing fixture pack' }
    Write-FixtureFile 'godot.ps1' @'
Add-Content (Join-Path $PSScriptRoot 'calls.txt') 'godot'
$global:LASTEXITCODE = 0
'@
    Write-FixtureFile 'tools/ReadLocalPaths.ps1' @'
@{ GodotPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'godot.ps1' }
'@
    Write-FixtureFile 'run.ps1' @'
function global:dotnet {
    Add-Content (Join-Path $PSScriptRoot 'calls.txt') 'compile'
    $global:LASTEXITCODE = if ((Get-Content (Join-Path $PSScriptRoot 'failure.txt')) -eq 'compile-failure') { 1 } else { 0 }
}
$reuse = (Get-Content (Join-Path $PSScriptRoot 'failure.txt')) -in @('reuse', 'missing-pack')
& (Join-Path $PSScriptRoot 'tools/Test.ps1') -Branch All -SkipResourceExport:$reuse
exit $LASTEXITCODE
'@
    Run-Fixture
    $expected = switch ($scenario) {
        'compile-failure' { 'compile' }
        'fresh' { 'compile,export,godot,compile,godot' }
        'reuse' { 'compile,godot,compile,godot' }
        'missing-pack' { '' }
    }
    Assert ($code -eq $(if ($scenario -in @('compile-failure', 'missing-pack')) { 1 } else { 0 })) "Test must preserve $scenario exit status; see $fixture/output.log"
    Assert (($calls -join ',') -eq $expected) "Test $scenario must compile before export and reuse only an existing pack. Calls: $calls"
    "PASS: Test $scenario"
}
foreach ($scenario in @('fresh', 'reuse', 'missing-pack')) {
    New-Fixture "build-$scenario"
    Copy-Item -LiteralPath (Join-Path $repoDir 'tools/Build.ps1') -Destination (Join-Path $fixture 'tools/Build.ps1')
    Write-FixtureFile 'failure.txt' $scenario
    Write-FixtureFile 'LICENSE' 'fixture license'
    Write-FixtureFile 'THIRD_PARTY_NOTICES.md' 'fixture notices'
    if ($scenario -eq 'reuse') { Write-FixtureFile 'dist/AllMatters.pck' 'existing fixture pack' }
    Write-FixtureFile 'run.ps1' @'
function global:dotnet {
    Add-Content (Join-Path $PSScriptRoot 'calls.txt') 'compile'
    $output = Join-Path $PSScriptRoot 'bin/public-beta/Debug/net9.0'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Set-Content (Join-Path $output 'AllMatters.dll') 'fixture assembly'
    Set-Content (Join-Path $output 'AllMatters.json') 'fixture manifest'
    $global:LASTEXITCODE = 0
}
$reuse = (Get-Content (Join-Path $PSScriptRoot 'failure.txt')) -in @('reuse', 'missing-pack')
& (Join-Path $PSScriptRoot 'tools/Build.ps1') -Branch public-beta -SkipResourceExport:$reuse
exit $LASTEXITCODE
'@
    Run-Fixture
    Assert ($code -eq $(if ($scenario -eq 'missing-pack') { 1 } else { 0 })) "Build must preserve $scenario exit status; see $fixture/output.log"
    $expected = if ($scenario -eq 'fresh') { 'compile,export' } else { 'compile' }
    Assert (($calls -join ',') -eq $expected) "Build $scenario must obey explicit resource reuse. Calls: $calls"
    $package = Join-Path $fixture 'dist/public-beta/AllMatters'
    Assert ((Test-Path (Join-Path $package 'AllMatters.pck')) -eq ($scenario -ne 'missing-pack')) 'Build must package only a successful or explicitly reused export.'
    "PASS: Build $scenario"
}
"Workflow tests passed. Logs: $fixtureRoot"
exit 0
