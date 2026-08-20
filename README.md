# CombatExtended-SimpleSidearms Compatibility Patch

[![Combat Extended Compatible](Media/Badge_CE_compatible.png)](https://steamcommunity.com/sharedfiles/filedetails/?id=2890901044)
![CE + Simple Sidearms Compatibility Suite](Media/Badge_Suite.png)
![CE + Simple Sidearms Compatibility Patch](Media/Badge_Patch.png)

RimWorld 1.6 compatibility mod making [Combat Extended](https://github.com/CombatExtended-Continued/CombatExtended)
and [Simple Sidearms](https://github.com/PeteTimesSix/SimpleSidearms) work together.

Both mods ship zero knowledge of each other: CE replaces the inventory model
(weight + bulk), ammo, verbs, and adds its own loadout/weapon-switch AI; Simple
Sidearms scores and swaps weapons using vanilla stats and vanilla verb hooks.
This mod bridges the eleven incompatibility axes found by cross-reading both
codebases (and the behavior of Ghosty's deprecated `SidearmsCECompatibility`,
used as a reference only — no code reused).

## The suite

This is the core, repair-only patch of a small family — it adds no behavior, only makes
both mods work as their authors intended. Feature modules build on it:

[![Compatibility Module - Loadouts](Media/Badge_Loadouts.png)](https://github.com/eebette/CombatExtended-SimpleSidearms-Compatibility-Loadouts)

CE loadouts and Simple Sidearms memory as one mental model: loadout weapons are
auto-remembered as sidearms, and ammo sustainment rides CE's own "Ad hoc" switch.

[![Compatibility Module - Tactics](Media/Badge_Tactics.png)](https://github.com/eebette/CombatExtended-SimpleSidearms-Compatibility-Tactics)

Combat-time weapon choice: reload-abort when threatened, target-aware ammo and
armor-aware melee scoring, ammo-depth tiebreak.

## What it fixes

- Sidearm carry limits now respect CE's inventory system (weight *and* bulk). *(#1)*
- Weapon ranking uses real CE damage numbers, so pawns actually pick the better gun. *(#2)*
- Pawns never auto-switch to a gun that has no ammo. *(#3)*
- Enemies spawn with their sidearms loaded and carrying spare ammo. *(#4)*
- Switching weapons no longer interrupts a reload partway through. *(#5)*
- Drawing a melee sidearm when attacked in melee works again. *(#6)*
- Mid-fight auto-switching to a better-suited gun works again. *(#7)*
- Firing a single-use launcher leaves the pawn holding their preferred backup, not fists. *(#8)*
- When a gun runs dry or is destroyed, the replacement follows your sidearm
  preferences instead of CE's guess. *(#9)*
- CE loadout enforcement no longer strips remembered sidearms out of inventories. *(#10)*
- EMP and incendiary weapon detection matches the ammo actually loaded. *(#11)*

## What it fixes (for nerds)

| # | Problem under CE | Fix |
|---|------------------|-----|
| 1 | SS pickup checks ignore CE **bulk** (weight is already CE-aware via CE's `MassUtility.Capacity` patch) | Bulk check appended to `StatCalculator.CanPickupSidearmType` (also gates NPC sidearm generation) |
| 2 | SS ranks ranged weapons with vanilla stats — meaningless for CE guns | CE-model DPS (ammo projectile damage, burst, reload amortization) with SS's speed-bias semantics preserved |
| 3 | SS can auto-switch a pawn to a gun with **no ammo** | Best-ranged-weapon selection re-run over loaded weapons only, preserving next-best fallback |
| 4 | SS-generated NPC sidearms spawn with empty mags, no spare ammo | Post-generation: magazines filled, spare mags added within CE inventory capacity (count from CE's `LoadoutPropertiesExtension` when present) |
| 5 | SS auto-swaps interrupt CE reload jobs | Preference swaps suppressed during `ReloadWeapon`; explicit swaps end the reload cleanly first |
| 6 | SS CQC ("draw melee when melee-attacked") dead — `Verb_MeleeAttackCE` overrides the hooked method | SS's CQC postfix mirrored onto `Verb_MeleeAttackCE.TryCastShot` |
| 7 | SS mid-combat ranged auto-switch dead — requires `Verb_Shoot`, CE uses `Verb_ShootCE` | SS's `Stance_Warmup` logic replicated for CE shoot verbs (reuses SS settings/helpers) |
| 8 | One-use weapons (single-shot launchers): SS re-equip hook never fires (`Verb_ShootCEOneUse` is a separate class) | Post-`SelfConsume` fallback equips by SS preference when the pawn ends up empty-handed |
| 9 | CE's `SwitchToNextViableWeapon` (out-of-ammo, weapon destroyed, …) ignores SS preferences | For SS-managed pawns, SS preference switching runs first; CE logic (incl. fists) is the fallback |
| 10 | CE loadout enforcement drops SS-remembered sidearms (drop/retrieve churn) | SS sidearm memory synced into CE HoldRecords (CE's own drop exemption); self-healing guards on `GetExcessThing`/`GetExcessEquipment` for pre-existing saves |
| 11 | SS EMP/dangerous-weapon detection reads the verb's default projectile, not the loaded CE ammo | Classification re-evaluated from the current CE projectile |

Load order: Harmony → Combat Extended → Simple Sidearms → this mod (declared in About.xml).
Installs via CE's `IPatch` compatibility scanner, with a `StaticConstructorOnStartup`
fallback; dev log line: `[CE+SimpleSidearms] Compatibility patches installed.`

## Building

Requires the .NET SDK and local copies of both mods' assemblies (Steam Workshop
subscription is enough):

```bash
dotnet build Source/CESimpleSidearmsCompat/CESimpleSidearmsCompat.csproj -c Release
```

The build references the workshop DLLs at
`~/.local/share/Steam/steamapps/workshop/content/294100/` (override with
`-p:RimWorldWorkshopDir=...`), compiles against
[Krafs.Rimworld.Ref](https://www.nuget.org/packages/Krafs.Rimworld.Ref) 1.6, and
uses [Krafs.Publicizer](https://github.com/krafs/Publicizer) for access to
internal members of both mods. Output lands in `Assemblies/`.

**No CI**: the compile references live in local Steam Workshop folders and can't
be vendored (CE is CC BY-NC-SA, Simple Sidearms has no license), so releases are
manual local builds with the built DLL committed in `Assemblies/` — cloning the
repo yields a working mod without a toolchain. Full process: [RELEASING.md](RELEASING.md).

## Installing locally

Symlink (or copy) this folder into RimWorld's `Mods` directory:

```bash
ln -s "$(pwd)" ~/.local/share/Steam/steamapps/common/RimWorld/Mods/CESimpleSidearmsCompat
```

## Notes / limitations

- DPS scoring omits hit-chance: CE's accuracy model (spread/sway/sight) has no
  vanilla-stat equivalent, so ranking compares damage-per-cycle. Speed-bias
  behavior from SS settings is preserved.
- Axis-9 arbitration defers to CE for AOE/predicated switch requests (grenade AI,
  urgent pickup) — those are CE-tactical decisions, not preference calls.
- SS-remembered weapons are exempt from CE loadout drops by design: SS memory is
  explicit user intent. Remove the sidearm from SS memory to let CE drop it.
- This mod's own code is [MIT-licensed](LICENSE) — compatible with contributing
  portions into CE (CC BY-NC-SA) since we hold the copyright.
- CE is licensed CC BY-NC-SA 4.0; Simple Sidearms has no published license. This
  mod links against both at build time (never redistributes either) and
  reimplements small SS behaviors (axes 6/7) against CE types; Ghosty's
  deprecated patch was a behavioral reference only, no code reused. Credit to
  PeteTimesSix and the CE team.
- The "Combat Extended Compatible" badge is the CE team's own asset
  (`Media/` in their repo), used as they recommend for compatible mods.
  `Badge_Suite.png` is this suite's mark, shared by all family repos.
