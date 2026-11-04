param([ValidateSet('public','public-beta','All')][string]$Branch='All')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$branches=if($Branch -eq 'All'){@('public-beta','public')}else{@($Branch)}
foreach($target in $branches){
    & dotnet build (Join-Path $PSScriptRoot 'Presentation.csproj') "-p:GameBranch=$target" -c Release --nologo
    if($LASTEXITCODE -ne 0){throw "Presentation probe build failed for $target ($LASTEXITCODE)."}
    $output=Join-Path $repo "dist/$target/AllMattersHuntingPresentationProbe"
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "bin/$target/Release/AllMattersHuntingPresentationProbe.dll") -Destination $output
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AllMattersHuntingPresentationProbe.json') -Destination $output
    Write-Output "Presentation-only prototype: $output"
}
