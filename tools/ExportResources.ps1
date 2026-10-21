param([string]$GodotPath)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$paths = & (Join-Path $PSScriptRoot 'ReadLocalPaths.ps1')
if (!$GodotPath) { $GodotPath = $paths['GodotPath'] }
if (!$GodotPath) { throw 'Set GodotPath in Paths.local.props or pass -GodotPath.' }
if (![IO.Path]::IsPathRooted($GodotPath)) { $GodotPath = Join-Path $repoDir $GodotPath }
$resourceDir = Join-Path $repoDir 'src/Resources'
$distDir = Join-Path $repoDir 'dist'
New-Item -ItemType Directory -Path $distDir -Force | Out-Null
& $GodotPath --headless --editor --path $resourceDir --import --quit
if ($LASTEXITCODE -ne 0) { throw "Resource import failed (exit $LASTEXITCODE)." }
# Preserve Spine scenes as source resources: the stock editor cannot convert their
# game-provided resource types. PCKPacker includes them without parsing dependencies.
& $GodotPath --headless --path $resourceDir --script (Join-Path $repoDir 'src/Tooling/PackResources.gd') -- (Join-Path $distDir 'AllMatters.pck')
if ($LASTEXITCODE -ne 0) { throw "Resource export failed (exit $LASTEXITCODE)." }
