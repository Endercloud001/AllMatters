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
& $GodotPath --headless --path $resourceDir --export-pack Resources (Join-Path $distDir 'AllMatters.pck')
if ($LASTEXITCODE -ne 0) { throw "Resource export failed (exit $LASTEXITCODE)." }
