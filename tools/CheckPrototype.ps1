param(
    [string]$Path = (Join-Path (Split-Path -Parent $PSScriptRoot) 'prototypes'),
    [string]$Python = 'python',
    [string]$Node = 'node',
    [switch]$SyntaxOnly
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $Path -PathType Container)) { throw 'Prototype directory is missing; pass -Path explicitly.' }
$root = (Resolve-Path -LiteralPath $Path).Path
$repoDir = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $repoDir ('.local/prototype-checks/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + $PID)
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$excluded = @('.git', '.local', 'node_modules', 'bin', 'obj', '.godot', '.venv', 'venv', '__pycache__', 'dist')
$files = [Collections.Generic.List[IO.FileInfo]]::new()
$stack = [Collections.Generic.Stack[string]]::new(); $stack.Push($root)
while ($stack.Count) {
    foreach ($entry in Get-ChildItem -LiteralPath $stack.Pop() -Force) {
        if ($entry.PSIsContainer) {
            if ($entry.Name -notin $excluded -and !($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) { $stack.Push($entry.FullName) }
        }
        else { $files.Add($entry) }
    }
}
$checks = 0
function Native([string]$Executable, [string[]]$Arguments, [string]$Label) {
    $script:checks++
    $log = Join-Path $logDir ('{0:D3}.log' -f $script:checks)
    & $Executable @Arguments *> $log
    $code = $LASTEXITCODE
    if ($code -ne 0) { Get-Content -LiteralPath $log -Tail 15 | Write-Host; throw "$Label failed (exit $code). Log: $log" }
}
$pyFiles = @($files | Where-Object Extension -eq '.py')
$jsFiles = @($files | Where-Object Extension -in @('.js', '.cjs', '.mjs'))
$psFiles = @($files | Where-Object Extension -eq '.ps1')
if ($pyFiles.Count) {
    $pythonExecutable = (Get-Command $Python -ErrorAction Stop).Source
    foreach ($file in $pyFiles) { Native $pythonExecutable @('-X', 'utf8', '-c', 'import ast,pathlib,sys; p=pathlib.Path(sys.argv[1]); ast.parse(p.read_text(encoding="utf-8-sig"), filename=str(p))', $file.FullName) "Python syntax: $($file.FullName)" }
}
if ($jsFiles.Count) {
    $nodeExecutable = (Get-Command $Node -ErrorAction Stop).Source
    foreach ($file in $jsFiles) { Native $nodeExecutable @('--check', $file.FullName) "Node syntax: $($file.FullName)" }
}
foreach ($file in $psFiles) {
    $parseErrors = $null; $tokens = $null
    $null = [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors) { throw "PowerShell syntax: $($file.FullName): $($parseErrors.Message -join '; ')" }
}
$smokeCount = 0
if (!$SyntaxOnly) {
    foreach ($manifest in @($files | Where-Object Name -eq 'prototype.json')) {
        $config = Get-Content -LiteralPath $manifest.FullName -Raw | ConvertFrom-Json
        foreach ($step in @($config.smoke | Where-Object { $_ })) {
            $entry = [IO.Path]::GetFullPath((Join-Path $manifest.DirectoryName $step.script))
            if (!(Test-Path -LiteralPath $entry -PathType Leaf)) { throw "Smoke script missing: $entry" }
            switch ($step.runtime) {
                'python' { if ([IO.Path]::GetExtension($entry) -ne '.py') { throw 'Python smoke requires a .py script.' }; $exe = (Get-Command $Python -ErrorAction Stop).Source; $nativeArgs = @('-X', 'utf8', $entry) }
                'node' { if ([IO.Path]::GetExtension($entry) -notin @('.js', '.cjs', '.mjs')) { throw 'Node smoke requires a .js/.cjs/.mjs script.' }; $exe = (Get-Command $Node -ErrorAction Stop).Source; $nativeArgs = @($entry) }
                'pwsh' { if ([IO.Path]::GetExtension($entry) -ne '.ps1') { throw 'PowerShell smoke requires a .ps1 script.' }; $exe = (Get-Process -Id $PID).Path; $nativeArgs = @('-NoProfile', '-File', $entry) }
                default { throw "Unknown smoke runtime: $($step.runtime)" }
            }
            $nativeArgs += @($step.args | Where-Object { $null -ne $_ })
            Push-Location $manifest.DirectoryName
            try { Native $exe $nativeArgs "Smoke: $entry" } finally { Pop-Location }
            $smokeCount++
        }
    }
}
[pscustomobject]@{ Directory = $root; PythonSyntax = $pyFiles.Count; NodeSyntax = $jsFiles.Count; PowerShellSyntax = $psFiles.Count; SmokeExecuted = $smokeCount; SyntaxOnly = [bool]$SyntaxOnly; Logs = $logDir }
if (!$smokeCount -and !$SyntaxOnly) { Write-Host 'No smoke entries configured; only syntax was checked.' }
