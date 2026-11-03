param([string]$Repository = (Join-Path $PSScriptRoot '../../../../../AllMatters'))
$ErrorActionPreference = 'Stop'
foreach ($version in @('public-v0.107.1','public-beta-v0.111.0')) {
 $root = Join-Path $Repository ".local/api/queries/$version"
 $run = Get-Content (Join-Path $root 'MegaCrit.Sts2.Core.Runs.RunManager.decompiled.cs') -Raw
 $start = $run.IndexOf('public async Task EnterMapPointInternal(')
 $end = $run.IndexOf('private AbstractRoom CreateRoom', $start)
 $enter = $run.Substring($start, $end-$start)
 if (!($enter.IndexOf('SaveManager.Instance.SaveRun(null)') -lt $enter.IndexOf('roomType = RollRoomTypeFor') -and $enter.IndexOf('roomType = RollRoomTypeFor') -lt $enter.IndexOf('await EnterRoom(abstractRoom)'))) { throw "Entry order changed: $version" }
 $combat = Get-Content (Join-Path $root 'MegaCrit.Sts2.Core.Combat.CombatManager.decompiled.cs') -Raw
 if (!($combat.IndexOf('await Hook.AfterCombatVictory(') -lt $combat.IndexOf('room.MarkPreFinished()') -and $combat.IndexOf('room.MarkPreFinished()') -lt $combat.IndexOf('this.CombatWon?.Invoke(room)'))) { throw "Victory order changed: $version" }
 $relic = Get-Content (Join-Path $root 'MegaCrit.Sts2.Core.Commands.RelicCmd.decompiled.cs') -Raw
 if (!$relic.Contains('player.AddRelicInternal(relic, index)')) { throw "Relic command changed: $version" }
 Write-Output "$version PASS source-order: save < resolve < enter; victory-hook < prefinished < CombatWon; Obtain delegates AddRelicInternal"
}
Write-Output 'Static source checks only: no compile, gameplay, multiplayer or restore claim.'
