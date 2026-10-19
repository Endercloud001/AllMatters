param(
    [Parameter(Mandatory)][string]$AssemblyPath,
    [Parameter(Mandatory)][string]$OutputDir,
    [Parameter(Mandatory)][string]$IlspyPath,
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
$assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
$ilspy = (Resolve-Path -LiteralPath $IlspyPath).Path
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$destination = (Resolve-Path -LiteralPath $OutputDir).Path
foreach ($type in $Types) {
    & $ilspy --disable-updatecheck -t $type $assembly -o $destination
    if ($LASTEXITCODE -ne 0) { throw "API export failed for $type (exit $LASTEXITCODE)." }
}
Write-Output "Exported $($Types.Count) types from $assembly to $destination"
