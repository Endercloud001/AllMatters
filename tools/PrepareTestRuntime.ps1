param([string]$OutputDir)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$paths = & (Join-Path $PSScriptRoot 'ReadLocalPaths.ps1')
if (!$OutputDir) { $OutputDir = $paths['TestDotNetRoot'] }
if (!$OutputDir) { $OutputDir = 'docs/local/tools/dotnet9' }
if (![IO.Path]::IsPathRooted($OutputDir)) { $OutputDir = Join-Path $repoDir $OutputDir }
if (!$IsWindows) { throw 'This runtime-directory helper uses Windows junctions. Supply a .NET 9 DotNetRoot on other systems.' }
$runtimes = & dotnet --list-runtimes
if ($LASTEXITCODE -ne 0) { throw 'Cannot list installed .NET runtimes.' }
$runtime = $runtimes | ForEach-Object {
    if ($_ -match '^Microsoft.NETCore.App (9\.\d+\.\d+) \[(.+)\]$') {
        [pscustomobject]@{ Version = [version]$Matches[1]; BaseDir = $Matches[2] }
    }
} | Sort-Object Version -Descending | Select-Object -First 1
if (!$runtime) { throw 'Install .NET 9 before preparing the test runtime. No runtime is downloaded by this command.' }
$installedRoot = Split-Path -Parent (Split-Path -Parent $runtime.BaseDir)
$runtimeHost = Get-ChildItem -LiteralPath (Join-Path $installedRoot 'host/fxr') -Directory |
    Where-Object { $_.Name -match '^9\.\d+\.\d+$' } |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (!$runtimeHost) { throw "No .NET 9 host/fxr found under $installedRoot." }
$runtimeTarget = Join-Path $runtime.BaseDir $runtime.Version.ToString()
$links = @(
    @{ Path = Join-Path $OutputDir "host/fxr/$($runtimeHost.Name)"; Target = $runtimeHost.FullName },
    @{ Path = Join-Path $OutputDir "shared/Microsoft.NETCore.App/$($runtime.Version)"; Target = $runtimeTarget }
)
foreach ($link in $links) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $link.Path) -Force | Out-Null
    if (!(Test-Path -LiteralPath $link.Path)) {
        New-Item -ItemType Junction -Path $link.Path -Target $link.Target | Out-Null
    }
}
Write-Output "Test .NET 9 runtime root: $([IO.Path]::GetFullPath($OutputDir))"
