# Sources and licenses

AllMatters is distributed under GNU GPL version 3 (see LICENSE). Copyright 2026 Endercloud001 for original AllMatters changes.

## Cleric event

- Source: [MT-SUPER-POWER/STS2-Balance-MOD](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD), retrieved 2026-10-20, repository LICENSE is GNU GPLv3.
- The Cleric event behavior, English/Chinese event text and event image are adapted from that repository. Preserve the upstream author's attribution when redistributing this work.
- The upstream events guide identifies the shipped Cleric image as its remade event artwork. The image is copied without alteration. Other upstream assets, ActsFromThePast code/assets and game resources are not included in this ticket.
- Changes: AllMatters registration/resource identity, option eligibility and localization explanation, event behavior tests, resource export and dual-target packaging. Existing game commands and lifecycle behavior remain the runtime dependency.

## Old Beggar and The Divine Fountain events

- Source: [MT-SUPER-POWER/STS2-Balance-MOD](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD), retrieved 2026-10-20; repository LICENSE is GNU GPLv3. Adapted files: `Sts2BalanceModCode/Events/OldBeggar.cs`, `TheDivineFountain.cs`, and corresponding entries in `Sts2BalanceMod/localization/{eng,zhs}/events.json`.
- Images `Sts2BalanceMod/images/events/OldBeggar.png` and `TheDivineFountain.png` are copied without alteration to AllMatters paths. The upstream events guide identifies these as part of its ten remade event illustrations; original pre-redraw assets are not copied. Old Beggar also reuses the already attributed Cleric portrait.
- Changes: AllMatters model/resource identities, full-payment and removable-target eligibility, mandatory single-card selection, explicit removable-curse wording, corrected Chinese leave text, portrait preloading and behavior tests. Balance's personal choices and beggar-to-Cleric reveal are retained. No ActsFromThePast code/assets or game assets are copied.

## Augmenter, J.A.X. and Mutagenic Strength

- Source: [MT-SUPER-POWER/STS2-Balance-MOD](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD), local revision `d9eff9e067b07689e722c5283e7c3e7c451961ca`, checked 2026-10-20. Repository LICENSE and README declare GNU GPLv3; no separate license or restrictive notice was found for these shipped assets.
- Adapted code: `Sts2BalanceModCode/Events/Augmenter.cs`, `Cards/Jax.cs`, `Relics/MutagenicStrength.cs`, `Powers/MutagenicDecayPower.cs`, plus their English/Chinese localization entries. Preserve upstream attribution when redistributing.
- Images copied unchanged from `Sts2BalanceMod/images/`: `events/augmenter.png`, `card_portraits/big/jax.png`, `relics/MutagenicStrength.png` and its `outlines/` and `big/` variants, `powers/MutagenicDecayPower.png` and its `big/` variant. The event guide identifies Augmenter as the user-selected final illustration; the other images have corresponding master images under upstream `image_gen/source/`. The distinct `Assets/ActsFromPast/` tree and pre-redraw originals were not used. These files are redistributed under the upstream repository's GPLv3 declaration; this records the declaration, not an independent chain-of-title audit.
- Changes: AllMatters resource/model identities; default Innate J.A.X.; upgrade transformation results; exchange the actual original starter relic; delay the entire 3 Strength loss until the third owner turn while retaining native Debuff/Artifact interaction; localized explanations and behavior tests. No unrelated Balance modes or asset processing/build scripts are included or executed.

## Cursed Tome and the three books

- Source: [MT-SUPER-POWER/STS2-Balance-MOD](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD), local revision `d9eff9e067b07689e722c5283e7c3e7c451961ca`, checked 2026-10-21. Repository LICENSE declares GNU GPLv3. Preserve upstream attribution when redistributing.
- Adapted code: `Sts2BalanceModCode/Events/CursedTome.cs`, `Relics/{Necronomicon,NilrysCodex,Enchiridion}.cs`, `Cards/Necronomicurse.cs` and the corresponding English/Chinese card and relic entries. The upstream comments credit ActsFromThePast as a reference; no files were copied directly from that repository.
- Images copied unchanged from `Sts2BalanceMod/images/`: `events/CursedTome.png`, `card_portraits/big/necronomicurse.png`, and all three books' base, outline and big relic icons. The upstream event guide identifies the event image as a remade illustration; the curse and book master images are under `image_gen/source/{cards,relics}/`. No original pre-redraw images, `Assets/ActsFromPast/` files, or game assets were copied. Redistribution follows the repository's GPLv3 declaration; this records that declaration rather than an independent chain-of-title audit.
- Changes: AllMatters model and resource identities; whole-team admission; native damage warnings and post-resolution survival checks; deliberate three-book selection; single-claim protection; new bilingual event text and explicit upgraded Nilry candidates in relic text; behavior, resource and native inventory serialization tests. Existing native commands, RNG, filtering, free-cost cleanup and hooks remain responsible for runtime behavior.

## Wheel of Change

- Source: [MT-SUPER-POWER/STS2-Balance-MOD](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD), local revision `d9eff9e067b07689e722c5283e7c3e7c451961ca`, checked 2026-10-21. Repository LICENSE declares GNU GPLv3. Preserve upstream attribution when redistributing.
- Adapted behavior: `Sts2BalanceModCode/Events/WheelOfChange.cs`, retaining independent player choices, event RNG, per-act RNG advancement and result-before-claim flow. Changes: specified gold, native Bad Luck, seven HP loss, mandatory legal removal, restricted Common/Uncommon random selection and explicit empty outcomes; map-visit RNG advancement prevents identical repeated visits within one act while preserving reload determinism. English and Chinese text is written for AllMatters and revised to the user's wording.
- Image copied unchanged from `Sts2BalanceMod/images/events/WheelOfChange.png` to `AllMatters/images/events/wheel_of_change.png`; the repository also includes its master image at `image_gen/source/events/WheelOfChange.png`. Redistribution follows the upstream GPLv3 declaration; this records that declaration, not an independent chain-of-title audit.
- Presentation artwork added 2026-10-21: `Sts2BalanceMod/images/event_extras/wheel.png` and `wheelArrow.png`, copied unchanged to `AllMatters/images/event_extras/wheel.png` and `wheel_arrow.png`. These files also appear in Balance's `Assets/ActsFromPast/images/event_extras/` mirror; its minigame credits [Cany0udance/ActsFromThePast](https://github.com/Cany0udance/ActsFromThePast). Preserve that source attribution along with Balance's. The redistribution basis is Balance's repository GPLv3 declaration; no independent chain-of-title audit is claimed.
- `WheelSpinPresentation.cs` is original AllMatters code displaying the attributed artwork with Godot built-in nodes. No upstream minigame code, spin-button texture or audio is included. No files are taken directly from the separately downloaded ActsFromThePast repository, whose root license remains unspecified.

## Tomb of Lord Red Mask

- Source: [MT-SUPER-POWER/STS2-Balance-MOD](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD), local revision `d9eff9e067b07689e722c5283e7c3e7c451961ca`, checked 2026-10-21. Repository LICENSE declares GNU GPLv3; preserve upstream attribution when redistributing.
- Adapted code: `Sts2BalanceModCode/Events/TombOfLordRedMask.cs` and corresponding English/Chinese event entries. Changes: AllMatters registration/resource identity; Act 3 admission including masked players; fixed 150 Gold offering and insufficient-funds lock; native Red Mask and independent 222 Gold reward retained. Balance credits ActsFromThePast as a reference; no files were copied directly from that separately downloaded repository.
- Image copied unchanged from `Sts2BalanceMod/images/events/TombOfLordRedMask.png` to `AllMatters/images/events/tomb_of_lord_red_mask.png`. Its corresponding master image is present at `image_gen/source/events/TombOfLordRedMask.png`; pre-redraw originals and game assets are not included. Redistribution follows the repository's GPLv3 declaration; this records that declaration, not an independent chain-of-title audit. The optional Huiji Wiki reference returned HTTP 403, so no Wiki text was copied.

## The Library and bottled relics

- Source: [MT-SUPER-POWER/STS2-Balance-MOD](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD), local revision `d9eff9e067b07689e722c5283e7c3e7c451961ca`, checked 2026-10-21. The repository LICENSE declares GPLv3. Preserve upstream attribution when redistributing.
- Adapted code: `Sts2BalanceModCode/Events/TheLibrary.cs`. Changes: AllMatters identity, Act 2 admission, explicit other-original-character unlocked reward pools, uniform sampling without replacement, empty-pool eligibility, owner-only single choice, and the cleaning option. Keep Balance's floor-rounded 33% healing and noncombat `NoUpgradeRoll` behavior. Chinese event copy was supplied by the user; English text is an AllMatters translation.
- Image copied unchanged from `Sts2BalanceMod/images/events/TheLibrary.png` to `AllMatters/images/events/the_library.png`. The corresponding source image exists under `image_gen/source/events/TheLibrary.png`. Redistribution follows the repository's GPLv3 declaration; this records that declaration, not an independent chain-of-title audit. No pre-redraw originals, ActsFromThePast files, game art or game assemblies were copied.
- Bottled Flame, Bottled Lightning and Bottled Tornado implementations, binding patches and descriptions are original AllMatters work (copyright 2026 Endercloud001), distributed under GPL-3.0-only. They use native models/commands and RitsuLib's existing SavedAttachedState bridge; no separate persistence framework is included.
- Bottle icons `images/relics/bottled_{flame,lightning,tornado}.png` were supplied by the user on 2026-10-21 as artwork from the original Slay the Spire. They are copied unchanged, including their transparency, for both inventory and enlarged views. Original artwork rights remain with Mega Crit and the respective rights holders; the mod's GPL code license does not relicense these images. The previous original SVG icons have been replaced; the SVG hover outlines are AllMatters vector silhouettes following the supplied bottle shape.

## Masked Bandits and the Red Mask Bandits

- Source: [MT-SUPER-POWER/STS2-Balance-MOD](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD), local revision `d9eff9e067b07689e722c5283e7c3e7c451961ca`, checked 2026-10-21. Repository LICENSE and README declare GPLv3. Upstream event and monster comments credit [Cany0udance/ActsFromThePast](https://github.com/Cany0udance/ActsFromThePast); retain both attributions.
- Adapted code: `Sts2BalanceModCode/Events/MaskedBandits.cs`, `Encounters/RedMaskBandits.cs`, and `Monsters/{Pointy,Romeo,Bear}.cs`. English/Chinese event dialogue, monster names/moves/reactions and encounter entries are copied from Balance's corresponding localization tables and renamed to AllMatters IDs. Changes: public participant reward collection; one explicit 25–35 gold reward while retaining normal cards/potion odds; event bestiary registration with a public-only section; native RitsuLib creature factory and native attack animation in Bear's two moves. Bear Hug remains -2 Dexterity and Romeo retains the actual upstream 3 Weak effect.
- Assets: `Sts2BalanceMod/monsters/{pointy,romeo,bear}/` PNG atlases, Spine `.spskel`/`.spatlas` files, skeleton `.tres` and visual `.tscn` scenes, plus `scenes/actsfromthepast-red_mask_bandits_event.tscn`. Only resource paths change to `res://AllMatters/`; binary skeletons and PNG artwork are unchanged. Balance also contains these assets in its ActsFromPast mirror. Redistribution follows Balance's repository GPLv3 declaration; this is not an independent chain-of-title audit or a claim to relicense underlying artwork. No files were copied directly from the separately downloaded ActsFromThePast repository.
- Game-provided Spine native libraries are read from the local installation only for headless resource tests; no game library, game resource pack or reverse-engineered source is included in AllMatters output. The mod loads Spine through the game's existing runtime.

## RitsuLib

RitsuLib 0.6.7, by OLC: [BAKAOLC/STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib), MIT licensed. It is a separately installed runtime dependency; AllMatters does not redistribute its assemblies in this package. Preserve RitsuLib's own license and complete variant-pack layout when installing or redistributing it.

## Colosseum

- Source: MT-SUPER-POWER/STS2-Balance-MOD, revision `d9eff9e067b07689e722c5283e7c3e7c451961ca`, checked 2026-10-22. Root LICENSE declares GPLv3; upstream credits ActsFromThePast. Adapted `Sts2BalanceModCode/Events/Colosseum.cs` and the continuous-combat lifecycle idea in `Patches/Events/ColosseumCombatPatch.cs`. Changes: AllMatters registration, Act 3 admission, native Living Fog/Damp Cultist first encounter, three complete native elite candidates, exact rewards, guarded choices, branch-specific reset and native history restoration.
- Illustration: `Sts2BalanceMod/images/events/Colosseum.png`, copied unchanged to `AllMatters/images/events/colosseum.png`; master image exists at `image_gen/source/events/Colosseum.png`. Upstream identifies this as remade event artwork. Redistribution follows its repository GPLv3 declaration; no independent chain-of-title audit is claimed. No pre-redraw original is included.
- Chinese narrative is adapted from the user's supplied reference screenshot; English is an AllMatters translation. The first encounter layout is original AllMatters work. All participating monsters and three elite encounter scenes use the installed game's native models/resources; no additional game artwork, assemblies or decompiled source is redistributed.

## Game assets and assemblies

The v0.1.1 Brilliance portrait is copied unchanged from the locally owned Slay the Spire `desktop-1.0.jar`, entry `images/1024Portraits/purple/attack/brilliance.png`. It remains Mega Crit/original artists' artwork; the repository's code license does not relicense it. Native Slay the Spire 2 events, models and visuals are referenced from the player's installation.

N'loth's Hungry Face and N'loth's Gift use the unchanged `images/relics/{nloth,nlothsGift}.png` and corresponding `images/relics/outline/` images from the same owned STS1 archive. The images retain Mega Crit/original artists' ownership and are not relicensed by this repository.

Blue Candle likewise uses unchanged `images/relics/blueCandle.png` and `images/relics/outline/blueCandle.png` from the owned STS1 archive, with the same original artwork ownership.

Slay the Spire 2 and its game resources remain the property of Mega Crit and their respective owners. Game DLLs, native libraries, PCK files and decompiled game code are development inputs only and are not distributed with this mod.

## Mind Bloom, Lagavulin and Mark of the Bloom

- Source: MT-SUPER-POWER/STS2-Balance-MOD, revision `d9eff9e067b07689e722c5283e7c3e7c451961ca`, checked 2026-10-21. Repository LICENSE declares GPLv3. Preserve upstream attribution.
- Adapted code: `Events/MindBloom.cs`, `Monsters/Lagavulin.cs`, `Powers/{AsleepLagavulinPower,MetallicizePower}.cs`, `Relics/MarkOfTheBloom.cs` under `Sts2BalanceModCode/`. Upstream credits ActsFromThePast. Changes: one double-Lagavulin encounter; specified shared effects, curses and exact rewards; native dual-target lifecycle and reward handling; AllMatters model/resource identities. Event prose is written for AllMatters; monster/power/relic localization is adapted from Balance.
- Assets: `Sts2BalanceMod/images/events/MindBloom.png`, `images/relics/{,big/,outlines/}MarkOfTheBloom.png`, `images/powers/AsleepLagavulinPower.png`, the five scene/Spine/atlas/texture files in `monsters/lagavulin/`, and `Assets/ActsFromPast/images/powers/actsfromthepast-metallicize_power.png` (Metallicize icon, used for both sizes). Event/relic master images exist in upstream `image_gen/source/`; upstream identifies the event illustration as remade art. Monster scenes and text resources only change resource paths; PNG and binary skeleton files are unchanged. The sleep icon is used for both icon sizes.
- The Lagavulin Spine assets also occur in Balance's ActsFromPast mirror. As with the existing bandit assets, redistribution follows Balance's repository GPLv3 declaration and preserves Balance/ActsFromThePast/original game attribution; this is not an independent audit of the underlying artwork's rights or a claim to relicense original game artwork. No files were copied directly from the separate ActsFromThePast archive. See `docs/research/2026-10-21-issue-12-source-assets.md` for remaining provenance limitations.
- No game DLL, PCK, native Spine library, decompiled code, or mirrored Mind Bloom music is distributed.

Chrysalis and Cursed Key use the unchanged STS1 archive entries images/1024Portraits/colorless/skill/chrysalis.png, images/relics/cursedKey.png and images/relics/outline/cursedKey.png, retaining Mega Crit/original artists' ownership.

Strange Mushroom and Du-Vu Doll likewise use unchanged images/relics/{mushroom,duvuDoll}.png and corresponding outline images from the owned STS1 archive; original artwork ownership is retained.

Ink Bottle and the global Symbiotic Virus replacement use unchanged `images/relics/{ink_bottle,virus}.png` and corresponding `images/relics/outline/` entries from the same owned STS1 archive. Ambrosia is a static composite of that archive's `images/potion/eye/{body,liquid}.png`, with a green liquid tint; its outline is `images/potion/eye/outline.png`. The potion image also identifies its temporary power. These assets retain Mega Crit/original artists' ownership and are not relicensed by this repository.

## Jungle Maze Sentries

- Source: MT-SUPER-POWER/STS2-Balance-MOD, revision `d9eff9e067b07689e722c5283e7c3e7c451961ca`, checked 2026-10-27; repository LICENSE declares GPLv3, upstream credits ActsFromThePast.
- Adapted `Sts2BalanceModCode/Monsters/Sentry.cs`: fixed requested values, two opposite first moves, one Artifact in multiplayer, AllMatters model/resource identities and native event integration.
- Assets: the five scene/Spine/atlas/texture files in `Sts2BalanceMod/monsters/sentry/` (Spine 4.2.11). Scene and atlas paths are adapted; binary skeleton and PNG are unchanged. Original STS1 artwork remains Mega Crit/original artists' property. Redistribution follows the same Balance/ActsFromThePast provenance limitations as the Lagavulin assets above, without claiming an independent underlying artwork license audit. No game DLL, PCK or native Spine library is distributed.

## Sentry laser and short-beam audio

- Added 2026-10-27 from the locally owned original Slay the Spire `desktop-1.0.jar`, by Mega Crit and the respective rights holders. `src/Resources/AllMatters/vfx/sentry_laser.png` is the nonrotated 981×101 packed `combat/laserThin` region at (2,104) of `vfx/vfx2.png`, located by `vfx/vfx.atlas`. No full atlas is distributed.
- `src/Resources/AllMatters/sfx/sentry_beam_short.ogg` is the unchanged `audio/sound/SOTE_SFX_SlowMagic_BeamShort_v1.ogg` from that jar. These game assets retain their original ownership; the mod's GPL code license does not relicense them.
- `SmallLaserEffect.cs` is original AllMatters Godot implementation based on observation of the original game's behavior. No Java source, jar, game library or separately downloaded ActsFromThePast implementation is redistributed.

## Returning Trash Heap content (#40)

Electrodynamics, Auto Shields, Bullseye and Core Surge portraits are unchanged STS1 archive entries `images/1024Portraits/blue/{power/electrodynamics,skill/auto_shields,attack/lock_on,attack/core_surge}.png`. The Electrodynamics and Lock-On power icons come unchanged from Balance `images/powers/{ElectrodynamicsPower,LockOnPower}.png` at the revision credited above. Original game artwork remains Mega Crit/original artists’ property; the repository code license does not relicense it.

The six red/green returning portraits (#41) are unchanged owned STS1 `images/1024Portraits/{green/attack/endless_agony,green/skill/terror,red/attack/drop_kick,red/attack/reaper,red/skill/disarm,red/skill/ghostly_armor}.png`, retaining Mega Crit/original artists’ ownership.

The six colorless portraits (#42) are unchanged owned STS1 entries `images/1024Portraits/{purple/attack/talk_to_the_hand,purple/power/fasting,purple/skill/alpha,purple/skill/foreign_influence,colorless/skill/beta,colorless/power/omega}.png`. Power icons extract the corresponding `128/{talk_to_hand,fasting,omega}` regions from `powers/powers.atlas` and `powers/powers.png`, restoring original transparent padding. Artwork remains Mega Crit/original artists property.
