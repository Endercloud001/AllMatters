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

## RitsuLib

RitsuLib 0.6.7, by OLC: [BAKAOLC/STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib), MIT licensed. It is a separately installed runtime dependency; AllMatters does not redistribute its assemblies in this package. Preserve RitsuLib's own license and complete variant-pack layout when installing or redistributing it.

## Game assets and assemblies

Slay the Spire 2 and its game resources remain the property of Mega Crit and their respective owners. Game DLLs, native libraries, PCK files and decompiled game code are development inputs only and are not distributed with this mod.
