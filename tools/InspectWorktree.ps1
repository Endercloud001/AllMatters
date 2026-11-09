param([string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$branch = & git -C $root branch --show-current
if ($LASTEXITCODE -ne 0) { throw 'RepositoryRoot must belong to the intended Git worktree.' }
$common = & git -C $root rev-parse --path-format=absolute --git-common-dir
if ($LASTEXITCODE -ne 0) { throw 'Cannot locate the common Git directory.' }
$sourceConfig = Join-Path (Split-Path -Parent $common) 'Paths.local.props'
$config = Join-Path $root 'Paths.local.props'
$settings = @{}
if (Test-Path -LiteralPath $config -PathType Leaf) {
    $values = & (Join-Path $PSScriptRoot 'ReadLocalPaths.ps1') -ConfigPath $config
    foreach ($name in $values.Keys) {
        if ($name -notmatch '(Path|Dir|Root)$') { continue }
        $value = $values[$name]
        if (!$value) { continue }
        $resolved = if ([IO.Path]::IsPathRooted($value)) { $value } else { Join-Path $root $value }
        $settings[$name] = [pscustomobject]@{ Path = $resolved; Exists = Test-Path -LiteralPath $resolved }
    }
}
$candidates = @()
$ancestor = Get-Item -LiteralPath $root
for ($i = 0; $i -lt 4 -and $ancestor; $i++) {
    $candidates += @(Get-ChildItem -LiteralPath $ancestor.FullName -Filter 'Godot*mono*console.exe' -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
    $ancestor = $ancestor.Parent
}
$commands = @{}
foreach ($name in @('python', 'node', 'dotnet', 'pwsh')) {
    $command = Get-Command $name -ErrorAction SilentlyContinue
    $commands[$name] = if ($command) { $command.Source } else { $null }
}
[pscustomobject]@{
    Repository = $root; Branch = $branch; LocalConfig = $config
    LocalConfigExists = Test-Path -LiteralPath $config -PathType Leaf
    MainWorktreeConfig = $sourceConfig
    MainWorktreeConfigExists = Test-Path -LiteralPath $sourceConfig -PathType Leaf
    Paths = $settings; GodotCandidates = @($candidates | Select-Object -Unique); Commands = $commands
}
# Read-only inspection: missing tools are reported, not silently installed or configured.
