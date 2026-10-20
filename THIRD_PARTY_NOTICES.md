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

## RitsuLib

RitsuLib 0.6.7, by OLC: [BAKAOLC/STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib), MIT licensed. It is a separately installed runtime dependency; AllMatters does not redistribute its assemblies in this package. Preserve RitsuLib's own license and complete variant-pack layout when installing or redistributing it.

## Game assets and assemblies

Slay the Spire 2 and its game resources remain the property of Mega Crit and their respective owners. Game DLLs, native libraries, PCK files and decompiled game code are development inputs only and are not distributed with this mod.
