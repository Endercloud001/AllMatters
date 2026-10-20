$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$paths = @{}
$configPath = Join-Path $repoDir 'Paths.local.props'
if (Test-Path -LiteralPath $configPath) {
    [xml]$config = Get-Content -LiteralPath $configPath -Raw
    foreach ($property in $config.SelectNodes('/Project/PropertyGroup/*')) {
        $paths[$property.Name] = $property.InnerText.Trim()
    }
}
return $paths
