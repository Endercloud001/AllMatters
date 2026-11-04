param([ValidateSet('public','public-beta','All')][string]$Branch='All')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$branches=if($Branch -eq 'All'){@('public-beta','public')}else{@($Branch)}
foreach($target in $branches){
    & dotnet build (Join-Path $PSScriptRoot 'Rest.csproj') "-p:GameBranch=$target" -c Release --nologo
    if($LASTEXITCODE -ne 0){throw "Rest probe build failed for $target ($LASTEXITCODE)."}
    $output=Join-Path $repo "dist/$target/AllMattersHuntingRestProbe"
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "bin/$target/Release/AllMattersHuntingRestProbe.dll") -Destination $output
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AllMattersHuntingRestProbe.json') -Destination $output
    Write-Output "Rest-only prototype: $output"
}
