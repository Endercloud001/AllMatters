param([switch]$SkipNative)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$shellExecutable = (Get-Process -Id $PID).Path
$fixtureRoot = Join-Path $repoDir ('.local/prototype-workflow-tests/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + $PID)
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$passed = 0
function Assert($Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Done([string]$Name) { $script:passed++; Write-Host "PASS: $Name" }
function Fixture([string]$Name) {
    $directory = Join-Path $fixtureRoot $Name
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    return $directory
}
function WriteJson([string]$Path, $Value) { $Value | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Path -Encoding utf8 }
function RunCheck([string]$Directory) {
    $log = Join-Path $Directory 'check.log'
    & $shellExecutable -NoProfile -File (Join-Path $repoDir 'tools/CheckPrototype.ps1') -Path $Directory *> $log
    return [pscustomobject]@{ Code = $LASTEXITCODE; Log = $log }
}
if (!$SkipNative) {
$valid = Fixture 'valid paths 中文'
Set-Content -LiteralPath (Join-Path $valid 'case.py') -Encoding utf8 -Value "import sys`nfrom pathlib import Path`nassert sys.argv[1] == 'a b 中文'`nassert Path.cwd() == Path(__file__).parent`n"
Set-Content -LiteralPath (Join-Path $valid 'case.cjs') -Encoding utf8 -Value 'if(process.argv[2]!=="a b 中文")process.exit(8);'
WriteJson (Join-Path $valid 'prototype.json') @{ smoke = @(@{ runtime = 'python'; script = 'case.py'; args = @('a b 中文') }, @{ runtime = 'node'; script = 'case.cjs'; args = @('a b 中文') }) }
$result = RunCheck $valid
Assert ($result.Code -eq 0) "Valid syntax/smoke failed; inspect $($result.Log)."
Done 'Python/Node syntax, Unicode paths, literal arguments and smoke working directory'

foreach ($type in @('python', 'node', 'powershell')) {
    $bad = Fixture "bad-$type"
    switch ($type) {
        'python' { Set-Content -LiteralPath (Join-Path $bad 'bad.py') -Value 'def broken(' }
        'node' { Set-Content -LiteralPath (Join-Path $bad 'bad.cjs') -Value 'function {' }
        'powershell' { Set-Content -LiteralPath (Join-Path $bad 'bad.ps1') -Value 'function {' }
    }
    $result = RunCheck $bad
    Assert ($result.Code -ne 0) "$type syntax failure was lost."
    Done "$type syntax errors propagate"
}
$failure = Fixture 'semantic failure'
Set-Content -LiteralPath (Join-Path $failure 'case.py') -Value 'raise SystemExit(7)'
WriteJson (Join-Path $failure 'prototype.json') @{ smoke = @(@{ runtime = 'python'; script = 'case.py' }) }
$result = RunCheck $failure
Assert ($result.Code -ne 0 -and (Get-Content -LiteralPath $result.Log -Raw) -match 'exit 7') 'Smoke exit status was lost.'
Done 'semantic smoke failures preserve native exit code'
WriteJson (Join-Path $valid 'prototype.json') @{ smoke = @(@{ runtime = 'python'; script = 'case.cjs' }) }
$result = RunCheck $valid
Assert ($result.Code -ne 0 -and (Get-Content -LiteralPath $result.Log -Raw) -match 'requires a .py') 'Wrong interpreter was accepted.'
Done 'runtime and script extension mismatch is explicit'
New-Item -ItemType Directory -Path (Join-Path $valid 'node_modules') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $valid 'node_modules/ignored.cjs') -Value 'function {'
WriteJson (Join-Path $valid 'prototype.json') @{ smoke = @() }
$result = RunCheck $valid
Assert ($result.Code -eq 0 -and (Get-Content -LiteralPath $result.Log -Raw) -match 'only syntax was checked') 'Dependency directories were scanned or missing smoke was misreported.'
Done 'dependency directories are excluded and absent smoke is reported honestly'
}
else { Write-Host 'Native runtime tests not run; this invocation covers PowerShell tooling only.' }

$psSmoke = Fixture 'PowerShell smoke'
Set-Content -LiteralPath (Join-Path $psSmoke 'case.ps1') -Value 'param([string]$Value,[switch]$Flag) if ($Value -ne "a b 中文" -or !$Flag) { throw "arguments were lost" }' -Encoding utf8
WriteJson (Join-Path $psSmoke 'prototype.json') @{ smoke = @(@{ runtime = 'pwsh'; script = 'case.ps1'; args = @('-Value', 'a b 中文', '-Flag') }) }
$result = RunCheck $psSmoke
Assert ($result.Code -eq 0) "PowerShell smoke failed; inspect $($result.Log)."
Done 'PowerShell smoke preserves named arguments and switches'

$stateDir = Fixture 'status'
$source = Join-Path $stateDir 'experiment.json'; $report = Join-Path $stateDir 'STATUS.md'
$record = @{ question = 'Current ticket'; requirements = @(@{ id = 'A1'; description = 'automatic evidence'; owner = 'agent'; status = 'todo'; evidence = @() }, @{ id = 'U1'; description = 'user experience'; owner = 'user'; status = 'passed'; evidence = @('actual user reply') }) }
WriteJson $source $record
$state = & (Join-Path $repoDir 'tools/WriteWayfinderStatus.ps1') -Path $source -Output $report
Assert ($state.AgentRemaining -eq 1 -and $state.Stage -eq 'Agent工作未完成') 'User approval hid unfinished Agent work.'
Done 'pending Agent work is not reported as waiting for the user'
$record.requirements[0].status = 'passed'; $record.requirements[0].evidence = @('run results'); $record.requirements[1].status = 'todo'
WriteJson $source $record
$state = & (Join-Path $repoDir 'tools/WriteWayfinderStatus.ps1') -Path $source -Output $report
Assert ($state.AgentRemaining -eq 0 -and $state.UserRemaining -eq 1) 'User experience phase was not derived.'
Done 'user experience stays separate from automatic success'
$record.requirements[1].status = 'passed'; WriteJson $source $record
$caught = $false
try { & (Join-Path $repoDir 'tools/WriteWayfinderStatus.ps1') -Path $source -Output $report -Check | Out-Null } catch { $caught = $_ -match 'differs' }
Assert $caught 'Stale generated report was not detected.'
$state = & (Join-Path $repoDir 'tools/WriteWayfinderStatus.ps1') -Path $source -Output $report
Assert ($state.Stage -like '可发布resolution*') 'Accepted record still reports pending user feedback.'
& (Join-Path $repoDir 'tools/WriteWayfinderStatus.ps1') -Path $source -Output $report -Check | Out-Null
Done 'one state change regenerates acceptance wording and stale reports fail comparison'
$record.requirements[1].evidence = @(); WriteJson $source $record
$caught = $false
try { & (Join-Path $repoDir 'tools/WriteWayfinderStatus.ps1') -Path $source -Output $report | Out-Null } catch { $caught = $_ -match 'needs evidence' }
Assert $caught 'User acceptance without a source was accepted.'
Done 'passed user entries need an actual feedback source'
$caught = $false
try { & (Join-Path $repoDir 'tools/WriteWayfinderStatus.ps1') -Path $source -Output $source | Out-Null } catch { $caught = $_ -match 'must differ' }
Assert $caught 'Report generation could overwrite its source record.'
Assert ((Get-Content -LiteralPath $source -Raw | ConvertFrom-Json).question -eq 'Current ticket') 'Source evidence was modified.'
Done 'generated report cannot overwrite its evidence source'

$long = Join-Path $stateDir 'embedded.html'
Set-Content -LiteralPath $long -Value (('x' * 20000) + 'NEEDLE' + ('y' * 20000)) -NoNewline
$slice = & (Join-Path $repoDir 'tools/ReadTextSlice.ps1') -Path $long -Pattern 'NEEDLE' -MaxChars 80 -Count 1
Assert ($slice.Text.Length -le 80 -and $slice.Text.Contains('NEEDLE') -and $slice.Truncated -and $slice.Line -eq 1) 'Large-line output was not bounded around its match.'
Done 'embedded media lines remain bounded while preserving source location'
$localProps = Join-Path $stateDir 'Paths.local.props'
Set-Content -LiteralPath $localProps -Value '<Project><PropertyGroup><GodotPath>relative/mono.exe</GodotPath></PropertyGroup></Project>'
$paths = & (Join-Path $repoDir 'tools/ReadLocalPaths.ps1') -ConfigPath $localProps
Assert ($paths['GodotPath'] -eq 'relative/mono.exe') 'The existing path reader ignored the selected config.'
Done 'existing path reader can inspect a selected worktree config without mutation'
Write-Output "Prototype workflow tools: $passed checks passed. Logs: $fixtureRoot"
