param([ValidateSet('public','public-beta','All')][string]$Branch='All',[string]$Filter='PrototypeHunting',[switch]$SkipResourceExport)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location $repo
try {
    foreach($probe in @('presentation','five-worm','queen','rest','shop','treasure')) {
        & (Join-Path $PSScriptRoot "$probe/Build.ps1") -Branch $Branch
    }
    if(!(Test-Path -LiteralPath (Join-Path $repo '.local/resource-search/ActsFromThePast.pck'))){throw 'Exact author resource input is missing. Follow RESOURCE-SEARCH.md; no substitute art is used.'}
    & (Join-Path $repo 'tools/Test.ps1') -Branch $Branch -Filter $Filter -SkipResourceExport:$SkipResourceExport
}
finally {Pop-Location}
