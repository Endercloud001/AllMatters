param(
    [string]$AssemblyPath,
    [string]$OutputDir,
    [string]$IlspyPath,
    [ValidateSet('public', 'public-beta')][string]$Branch = 'public-beta',
    [string]$Find,
    [switch]$Signatures,
    [string]$Member,
    [ValidateRange(1, 500)][int]$Limit = 40,
    [string[]]$Types = @(
        'MegaCrit.Sts2.Core.Models.EventModel',
        'MegaCrit.Sts2.Core.Models.ActModel',
        'MegaCrit.Sts2.Core.Models.ModelDb',
        'MegaCrit.Sts2.Core.Modding.ModHelper',
        'MegaCrit.Sts2.Core.Modding.ModManager',
        'MegaCrit.Sts2.Core.Models.CardModel',
        'MegaCrit.Sts2.Core.Models.PowerModel',
        'MegaCrit.Sts2.Core.Models.RelicModel',
        'MegaCrit.Sts2.Core.Models.Cards.HelloWorld',
        'MegaCrit.Sts2.Core.Models.Powers.HelloWorldPower',
        'MegaCrit.Sts2.Core.Commands.CardCmd',
        'MegaCrit.Sts2.Core.Commands.CardPileCmd',
        'MegaCrit.Sts2.Core.Commands.PowerCmd',
        'MegaCrit.Sts2.Core.Commands.RelicCmd',
        'MegaCrit.Sts2.Core.Commands.RewardsCmd',
        'MegaCrit.Sts2.Core.Rooms.CombatRoom',
        'MegaCrit.Sts2.Core.Rooms.EventRoom',
        'MegaCrit.Sts2.Core.Runs.RunManager',
        'MegaCrit.Sts2.Core.Models.AbstractModel',
        'MegaCrit.Sts2.Core.Multiplayer.Game.EventSynchronizer',
        'MegaCrit.Sts2.Core.Multiplayer.Replay.CombatReplayWriter',
        'MegaCrit.Sts2.Core.Rewards.RewardsSet',
        'MegaCrit.Sts2.Core.Combat.CombatManager'
    )
)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$paths = & (Join-Path $PSScriptRoot 'ReadLocalPaths.ps1')
if (!$AssemblyPath) {
    $dataDir = if ($Branch -eq 'public') { $paths['PublicGameDataDir'] } else { $paths['BetaGameDataDir'] }
    if (!$dataDir) { throw "Configure the game data directory for $Branch or pass -AssemblyPath." }
    $AssemblyPath = Join-Path $dataDir 'sts2.dll'
}
if (![IO.Path]::IsPathRooted($AssemblyPath)) { $AssemblyPath = Join-Path $repoDir $AssemblyPath }
$assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
$sourceDir = Split-Path -Parent $assembly
$releaseInfoPath = @((Join-Path $sourceDir 'release_info.json'), (Join-Path $sourceDir '../release_info.json')) |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$gameVersion = if ($releaseInfoPath) { (Get-Content -LiteralPath $releaseInfoPath -Raw | ConvertFrom-Json).version } else { 'unknown' }
$targetLabel = if ($PSBoundParameters.ContainsKey('AssemblyPath')) { 'explicit assembly' } else { $Branch }
Write-Output "Target: $targetLabel; game: $gameVersion; product: $((Get-Item -LiteralPath $assembly).VersionInfo.ProductVersion)"
if ($Find -or $Signatures) {
    if ($Find -and $Signatures) { throw 'Choose -Find or -Signatures, not both.' }
    if ($Signatures -and !$PSBoundParameters.ContainsKey('Types')) { throw 'Pass the fully qualified -Types for signature inspection.' }
    $inspector = Join-Path $repoDir 'src/Tooling/ApiInspector/ApiInspector.csproj'
    $arguments = @('--assembly', $assembly, '--limit', "$Limit")
    if ($paths['BetaGameDataDir']) {
        $dependencyDir = $paths['BetaGameDataDir']
        if (![IO.Path]::IsPathRooted($dependencyDir)) { $dependencyDir = Join-Path $repoDir $dependencyDir }
        $arguments += '--dependency', $dependencyDir
    }
    if ($Find) {
        & dotnet run --project $inspector --verbosity quiet -- @arguments --find $Find
        if ($LASTEXITCODE -ne 0) { throw "API type lookup failed or found no matches (exit $LASTEXITCODE)." }
    }
    else {
        foreach ($type in $Types) {
            $memberArguments = if ($Member) { @('--member', $Member) } else { @() }
            & dotnet run --project $inspector --verbosity quiet -- @arguments --type $type @memberArguments
            if ($LASTEXITCODE -ne 0) { throw "API member lookup failed or found no matches for $type (exit $LASTEXITCODE)." }
        }
    }
    return
}
if (!$IlspyPath) { $IlspyPath = $paths['IlspyPath'] }
if (!$IlspyPath) { throw 'Set IlspyPath in Paths.local.props or pass -IlspyPath.' }
if (![IO.Path]::IsPathRooted($IlspyPath)) { $IlspyPath = Join-Path $repoDir $IlspyPath }
$ilspy = (Resolve-Path -LiteralPath $IlspyPath).Path
if (!$OutputDir) {
    $versionName = "$targetLabel-$gameVersion" -replace '[^a-zA-Z0-9._-]', '_'
    $OutputDir = Join-Path $repoDir "docs/local/api/queries/$versionName"
}
if (![IO.Path]::IsPathRooted($OutputDir)) { $OutputDir = Join-Path $repoDir $OutputDir }
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$destination = (Resolve-Path -LiteralPath $OutputDir).Path
foreach ($type in $Types) {
    & $ilspy --disable-updatecheck -t $type $assembly -o $destination
    if ($LASTEXITCODE -ne 0) { throw "API export failed for $type (exit $LASTEXITCODE)." }
    $files = Get-ChildItem -LiteralPath $destination -File | Where-Object { $_.Name -like "$type*" }
    $memberHits = 0
    foreach ($file in $files) {
        Write-Output "Source: $($file.FullName)"
        if ($Member) {
            $matchesInFile = @(Select-String -LiteralPath $file.FullName -SimpleMatch $Member)
            $memberHits += $matchesInFile.Count
            $matchesInFile | Select-Object -First $Limit |
                ForEach-Object { Write-Output "$($_.LineNumber): $($_.Line.Trim())" }
        }
    }
    if ($Member -and $memberHits -eq 0) { throw "No source matches for '$Member' in $type. Export directory: $destination" }
}
Write-Output "Exported $($Types.Count) types from $assembly to $destination"
