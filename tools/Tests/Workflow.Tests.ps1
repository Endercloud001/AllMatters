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

foreach ($scenario in @('compile-failure', 'host-failure', 'fresh', 'reuse', 'missing-pack')) {
    New-Fixture "test-$scenario"
    Copy-Item -LiteralPath (Join-Path $repoDir 'tools/Test.ps1') -Destination (Join-Path $fixture 'tools/Test.ps1')
    foreach ($script in @('ReadTestLog.ps1', 'Check.ps1')) {
        if (Test-Path (Join-Path $repoDir "tools/$script")) {
            Copy-Item -LiteralPath (Join-Path $repoDir "tools/$script") -Destination (Join-Path $fixture "tools/$script")
        }
    }
    Write-FixtureFile 'failure.txt' $scenario
    if ($scenario -eq 'reuse') { Write-FixtureFile 'dist/AllMatters.pck' 'existing fixture pack' }
    Write-FixtureFile 'godot.ps1' @'
Add-Content (Join-Path $PSScriptRoot 'calls.txt') 'godot'
if ((Get-Content (Join-Path $PSScriptRoot 'failure.txt')) -eq 'host-failure') {
    'System.Exception: injected behavior failure'
    $global:LASTEXITCODE = 7
} else {
    'Passed 2 tests against fixture.'
    $global:LASTEXITCODE = 0
}
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
        'host-failure' { 'compile,export,godot' }
        'fresh' { 'compile,export,godot,compile,godot' }
        'reuse' { 'compile,godot,compile,godot' }
        'missing-pack' { '' }
    }
    Assert ($code -eq $(if ($scenario -in @('compile-failure', 'host-failure', 'missing-pack')) { 1 } else { 0 })) "Test must preserve $scenario exit status; see $fixture/output.log"
    Assert (($calls -join ',') -eq $expected) "Test $scenario must compile before export and reuse only an existing pack. Calls: $calls"
    if ($scenario -ne 'missing-pack') {
        $summary = & (Join-Path $repoDir 'tools/ReadTestLog.ps1') -Path (Join-Path $fixture '.local/tests/public-beta-last.log')
        if ($scenario -eq 'compile-failure') {
            Assert ($summary.BuildExit -eq 1 -and $null -eq $summary.HostExit) 'Runner must log failed compilation without a host exit.'
        } elseif ($scenario -eq 'host-failure') {
            Assert ($summary.HostExit -eq 7 -and $summary.FirstException.Text -match 'injected behavior failure') 'Runner must preserve the actual host exit and assertion.'
        } else {
            Assert ($summary.HostExit -eq 0 -and $summary.Passed -eq 2) 'Runner must capture actual completed host output.'
        }
    }
    "PASS: Test $scenario"
}

New-Fixture 'test-log-summary'
$testLog = Join-Path $fixture 'host.log'
Write-FixtureFile 'host.log' @'
Testing public : 0.107.1 [isolated/sts2.dll]
Test build exit: 0
[ERROR] Attempting to register type Example, but it is not associated with any mod!
PASS: An earlier case
ERROR: The InputMap action "example" doesn't exist.
System.Exception: Input was not routed.
Host exit: 1
'@
$summary = & (Join-Path $repoDir 'tools/ReadTestLog.ps1') -Path $testLog
Assert ($summary.Target -eq 'public' -and $summary.Version -eq '0.107.1') 'Summary must identify the actual target version.'
Assert ($summary.BuildExit -eq 0 -and $summary.HostExit -eq 1 -and $null -eq $summary.Passed) 'Partial passes cannot turn an incomplete run into success.'
Assert ($summary.FirstException.Line -eq 6 -and $summary.FirstException.Text -match 'Input was not routed') 'Summary must preserve assertion source location.'
Assert ($summary.EngineErrors -eq 1 -and $summary.ModMetadataErrors -eq 1) 'Summary must report engine errors and known host metadata noise separately.'
Assert ($summary.Source -eq $testLog) 'Summary must point to the original full log.'
Write-FixtureFile 'host.log' @'
Testing public-beta : 0.111.0 [isolated/sts2.dll]
Test build exit: 1
Preparation failed: compile failure
'@
$summary = & (Join-Path $repoDir 'tools/ReadTestLog.ps1') -Path $testLog
Assert ($summary.BuildExit -eq 1 -and $null -eq $summary.HostExit -and $null -eq $summary.Passed) 'Compile failure must remain preparation failure, with no host result.'
Write-FixtureFile 'host.log' @'
Testing public-beta : 0.111.0 [isolated/sts2.dll]
Test build exit: 0
Passed 31 tests against 0.1.0.0.
Host exit: 0
'@
$summary = & (Join-Path $repoDir 'tools/ReadTestLog.ps1') -Path $testLog
Assert ($summary.Passed -eq 31 -and $summary.HostExit -eq 0) 'Completed host run must retain its pass count and exit code.'
Assert ($LASTEXITCODE -eq 0) 'A completed run with at most one unassociated-mod hint exits clean.'

Write-FixtureFile 'host.log' @'
Testing public-beta : 0.111.0 [isolated/sts2.dll]
Test build exit: 0
Host exit: 0
'@
$summary = & (Join-Path $repoDir 'tools/ReadTestLog.ps1') -Path $testLog
Assert ($null -eq $summary.Passed) 'A run with no pass line has an empty pass count.'
Assert ($LASTEXITCODE -ne 0) 'An empty pass count fails the smoke gate.'

Write-FixtureFile 'host.log' @'
Testing public-beta : 0.111.0 [isolated/sts2.dll]
Test build exit: 0
[ERROR] Attempting to register type A, but it is not associated with any mod!
[ERROR] Attempting to register type B, but it is not associated with any mod!
Passed 31 tests against 0.1.0.0.
Host exit: 0
'@
$summary = & (Join-Path $repoDir 'tools/ReadTestLog.ps1') -Path $testLog
Assert ($summary.Passed -eq 31 -and $summary.ModMetadataErrors -eq 2) 'More than one unassociated-mod hint is counted.'
Assert ($LASTEXITCODE -ne 0) 'More than one unassociated-mod hint fails the smoke gate.'
"PASS: Test log summary keeps preparation, behavior, diagnostics and version separate"

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

New-Fixture 'check-placeholder'
Copy-Item -LiteralPath (Join-Path $repoDir 'tools/Check.ps1') -Destination (Join-Path $fixture 'tools/Check.ps1')
New-Item -ItemType Directory -Path (Join-Path $fixture 'src/Features') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $fixture 'src/Tests') -Force | Out-Null
Write-FixtureFile 'src/Features/Real.cs' 'namespace X; public class Real { }'
Write-FixtureFile 'src/Tests/TestPlaceholder.cs' '// Placeholder is allowed inside the test project'
Write-FixtureFile 'tools/Test.ps1' 'param($Branch, [switch]$SkipResourceExport)'
Write-FixtureFile 'tools/Build.ps1' 'param($Branch, [switch]$SkipResourceExport)'
Write-FixtureFile 'run.ps1' @'
function global:git { $global:LASTEXITCODE = 0 }
& (Join-Path $PSScriptRoot 'tools/Check.ps1')
exit $LASTEXITCODE
'@
Run-Fixture
Assert ($code -eq 0) "Production sources without placeholder markers must pass the placeholder check; see $fixture/output.log"
Assert ((Get-Content (Join-Path $fixture 'output.log') -Raw) -match 'Placeholder scan\s+PASS') 'A clean tree reports the placeholder scan as PASS.'
"PASS: Check placeholder clean"

foreach ($case in @(@('Placeholder', 'src/Features/Stub.cs'), @('In a real implementation', 'src/Features/Stub.cs'), @('TODO: Implement', 'src/Features/Stub.cs'), @('Placeholder', 'src/TestsFoo/Stub.cs'))) {
    $marker, $stubPath = $case
    New-Fixture "check-placeholder-$($marker.Substring(0, 4))-$(Split-Path (Split-Path $stubPath) -Leaf)"
    Copy-Item -LiteralPath (Join-Path $repoDir 'tools/Check.ps1') -Destination (Join-Path $fixture 'tools/Check.ps1')
    New-Item -ItemType Directory -Path (Join-Path $fixture (Split-Path $stubPath)) -Force | Out-Null
    Write-FixtureFile $stubPath "// $marker"
    Write-FixtureFile 'tools/Test.ps1' 'param($Branch, [switch]$SkipResourceExport)'
    Write-FixtureFile 'tools/Build.ps1' 'param($Branch, [switch]$SkipResourceExport)'
    Write-FixtureFile 'run.ps1' @'
function global:git { $global:LASTEXITCODE = 0 }
& (Join-Path $PSScriptRoot 'tools/Check.ps1')
exit $LASTEXITCODE
'@
    Run-Fixture
    Assert ($code -ne 0) "A production source $stubPath containing '$marker' must fail the placeholder check; see $fixture/output.log"
    Assert ((Get-Content (Join-Path $fixture 'output.log') -Raw) -match 'Placeholder scan\s+FAIL') "The placeholder scan reports FAIL for '$marker'."
    "PASS: Check placeholder rejects '$marker' in $stubPath"
}
$requiresRoot = if ($env:ALLMATTERS_REQUIRES_ROOT) { $env:ALLMATTERS_REQUIRES_ROOT } else { Join-Path $repoDir 'tools' }
foreach ($name in @('Test.ps1', 'Check.ps1')) {
    $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $requiresRoot $name), [ref]$null, [ref]$null)
    # Verifies the #Requires declaration only, not the 5.1 runtime refusal.
    $required = $ast.ScriptRequirements.RequiredPSVersion
    Assert ($null -ne $required -and $required.Major -ge 7) "$name must declare #Requires -Version 7 so Windows PowerShell 5.1 refuses to run it."
    "PASS: $name requires PowerShell 7"
}
"Workflow tests passed. Logs: $fixtureRoot"
exit 0
