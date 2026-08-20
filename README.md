# CombatExtended-SimpleSidearms Compatibility Patch

[![Combat Extended Compatible](Media/Badge_CE_compatible.png)](https://steamcommunity.com/sharedfiles/filedetails/?id=2890901044)
![CE + Simple Sidearms Compatibility Suite](Media/Badge_Suite.png)
![CE + Simple Sidearms Compatibility Patch](Media/Badge_Patch.png)

RimWorld compatibility mod making [Combat Extended](https://github.com/CombatExtended-Continued/CombatExtended) and [Simple Sidearms](https://github.com/PeteTimesSix/SimpleSidearms) work together.

This mod patches core compatibility issues but doesn't bridge AI behaviors or UI elements (see [The suite](#the-suite)).

Inspired by the [discontinued mod by Ghosty](https://steamcommunity.com/sharedfiles/filedetails/?id=3694067502), I decompiled that mod and searched *even harder* for incompatibilities between the mods.

## The suite

This is the core, repair-only patch — it adds no behavior, only makes
both mods work as originally intended.

These individual feature modules were made to improve the experience between the 2 mods: sidearm-aware CE AI tweaks, and sidearm-aware CE loadout handling.

- [Compatibility Module - Loadouts](https://github.com/eebette/CombatExtended-SimpleSidearms-Compatibility-Loadouts) — loadout-weapons-as-sidearms and ammo sustainment bridging CE loadouts with SS memory
- [Compatibility Module - Tactics](https://github.com/eebette/CombatExtended-SimpleSidearms-Compatibility-Tactics) — smarter weapon-choice triggers

## Fixes

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

## Load order

> Harmony → Combat Extended → Simple Sidearms → this mod.

## FAQ

**CE compatible?**
I'm not answering that.

**Can I add or remove it mid-save?**
Both are safe. The only thing it writes to a save is CE hold-records, which CE
itself purges and tolerates.

**Does it change balance?**
It makes the game easier in the sense that 2 core combat mods are no longer broken in your save.

But in the traditional sense, no.

**Why is my pawn keeping a sidearm that isn't in its CE loadout?**
Pawns won't automatically drop any weapon Simple Sidearms remembers — which includes
weapons they've equipped, since SS remembers those on its own. Forget it in the SS gizmo
to let CE drop it.

**Does it work with Melee Animation?**
Yes, with one known gap: animated execution kills bypass the melee-attack hook,
so the CQC melee auto-draw doesn't trigger during those. Everything else works.

**AI?**
This mod was engineered with the help of an AI Coding Assistant (Claude Code, Fable 5, Max effort). The amount of researching and deep-diving the compatibility interfaces of both mods would have been insurmountable without it.

I ask that if you have unconstructive feedback regarding the usage of AI while developing this mod, that it remains outside of this community space. Thank you.

## Fixes (for nerds)

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
- SS-remembered weapons are exempt from CE loadout drops by design. This covers every
  entry in the sidearm list, including weapons SS remembered automatically when a pawn
  equipped them. Remove the sidearm from SS memory to let CE drop it.

## Credit

- Thanks of course to PeteTimesSix and the CE team.
- Thanks to Ghosty for the initial research put in to find incompatibilities between the 2 mods.

## License

[MIT-licensed](LICENSE)
