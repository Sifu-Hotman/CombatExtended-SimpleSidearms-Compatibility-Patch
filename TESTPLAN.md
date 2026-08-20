# Test plan

One staged save per cluster of axes. Stage once with dev tools, save, then every
code iteration is: rebuild → relaunch → load save (assemblies don't hot-reload).

## Automated acceptance runs

Most of this plan runs unattended via `test/run-assert.sh <scenario> <save>`
(in-game assertion runner in `test/StagingMod/Source/CETestRunner.cs`, results as
`test/SaveData/test-results-<scenario>.json`):

```
./test/run-test.sh stage                       # regenerate CETEST saves (kill after letter)
./test/run-assert.sh cetest1 CETEST-1-pickup
./test/run-assert.sh cetest2 CETEST-2-selection
./test/run-assert.sh cetest3 CETEST-3-combat
./test/run-assert.sh cetest4 CETEST-4-generation
```

Full green pass recorded 2026-08-20 (all four scenarios, zero exceptions in logs).
Coverage highlights beyond the manual checklist: axis-5 direct unit hit (SS switch
entry point invoked DURING a live CE reload job — reload survived), axis-8 full
chain (rocket actually fired at a ground cell, consumption → SS-preference
re-equip), axis-10 hold-record lifecycle + dedup, axis-4 per-raider capacity audit
+ orphan-ammo scan + generator idempotence. The Loadouts module's derivations are
disabled in-memory for these runs, so they exercise the compat patch alone.

Findings worth knowing (none are compat-patch defects):
- **SS upstream quirk:** `CompSidearmMemory.InformOfAddedSidearm` has no duplicate
  guard (the dedup is commented out upstream) — repeated calls grow
  RememberedWeapons. The patch's own hold records dedup correctly regardless.
  Candidate for the SS upstream report batch (issue #5).
- **SS drafted-weapon-selection skips manual-use weapons:** drafting a pawn whose
  primary is a one-use launcher holsters it in favor of a sidearm. SS-native
  (vanilla-visible too), amplified by CE's launcher availability.
- Test-harness scenario design must keep hostiles away from behavior-under-test
  pawns (return fire / melee charges corrupt phases) and target ground cells for
  AOE weapons.

## Dev-tool crib sheet

- Map: main menu → **Dev quicktest** (instant 75×75 map with debug colonists), or
  `./test/run-test.sh -quicktest`.
- Debug actions (top icon bar → wrench): **Spawning → Spawn pawn** (pick kind +
  faction), **Try place near thing...** (weapons, CE ammo — search e.g.
  "FMJ", "ammo"), **Pawns → Damage until down**, **General → Explosion...**.
- God mode (hotkey `Ctrl+Shift+G` / icon) to insta-build.
- SS gizmo on a selected pawn: sidearm list; right-click weapons on ground →
  "equip as sidearm".
- CE loadouts: Assign tab → Manage loadouts.
- Confirm patch installed: dev console shows
  `[CE+SimpleSidearms] Compatibility patches installed.`

## Save 1 — "pickup" (axes 1, 10)

Stage: 1 colonist. Fill inventory near CE bulk cap (spawn + force-carry armor
plates / ammo crates). Spawn heavy sidearm (LMG) + light sidearm (pistol) nearby.

- A1: try pick up LMG via SS right-click → expect deny ("no free space") even
  though raw *mass* would fit. Pistol → allowed.
- A10: give colonist non-default CE loadout that does NOT contain the pistol.
  Remember pistol as sidearm (SS gizmo). Wait/skip time → pawn must NOT drop it.
  Forget the sidearm in SS gizmo → CE should then drop it (exemption removed).

## Save 2 — "selection" (axes 2, 3, 9, 11)

Stage: 1 drafted colonist with: rifle (loaded), pistol sidearm (loaded), revolver
sidearm (empty, NO spare .44 ammo in inventory). Caliber matters: the dry gun
must not share ammo with anything carried — CE treats "reloadable from
inventory" as having ammo (which is correct; a shared-caliber gun IS usable).
Spawn hostile pirates at distance (Spawn pawn → faction pirate).

- A2: SS never shows DPS in its UI (it's internal selection state) — probe it via
  Debug actions → **CE+SS Compat → Log carried weapon DPS**, then click the pawn.
  Console prints one row per carried gun: `dpsAvg` sane and different per weapon
  (not 0/NaN for loaded guns), `cycle` includes reload time, the dry revolver
  shows `mag 0/6, hasAmmoOrMag=False`. (A dry gun whose caliber IS carried —
  e.g. a MAC-10 next to M1911 spares, both .45 ACP — correctly shows True:
  CE can reload it from inventory, so it counts as usable.)
- A3/A9: dev-drain rifle mag (fire until empty or unload+drop ammo mid-fight) →
  pawn must auto-switch to the LOADED pistol, never the empty SMG, and never
  fists while a loaded gun remains.
- A11: what the patch changes is *classification* (read from loaded CE ammo, not
  the verb's default projectile), so probe it directly: Debug actions →
  **CE+SS Compat → Log weapon classification**, click the pawn. Expect EMP
  grenades `EMP=True`, incendiary-loaded guns `dangerous=True`, plain FMJ guns
  both False. Notes on behavior: grenades are `manualUse=True` and SS NEVER
  auto-equips manual-use weapons for player colonists, and SS's out-of-ammo
  re-equip path hardcodes skip-EMP (no target context) — so don't expect a pawn
  to auto-draw EMP grenades even against mechs; that eligibility only exists in
  the axis-7 mid-warmup swap, and only for non-manual EMP weapons (none in
  Core+CE).

## Save 3 — "combat flow" (axes 5, 6, 7)

Stage: colonist A: ranged primary + melee sidearm (knife/gladius). Colonist B:
sniper rifle + shotgun sidearm. Melee raider nearby.

- A6 (CQC): let melee raider reach colonist A → A must auto-draw the melee
  sidearm when attacked (SS CQC setting on).
- A7: order B to attack a target at close range while holding the sniper →
  during warmup B should swap to the shotgun (SS "ranged combat auto-switch"
  on; tune threshold in SS settings). Caveats, both SS-by-design (the swap
  trigger only runs during aim warmup with the current weapon): a target outside
  the CURRENT weapon's range can't even be ordered ("Out of range" — no job, no
  warmup, no swap; swap manually via gizmo), and no switch happens when only the
  current weapon can reach the target from where the pawn stands.
- A5: start a CE reload (empty mag, spare ammo in inventory, take cover) →
  while reload job runs, SS must not cancel it (watch job readout; reload
  completes).

## Save 4 — "generation + one-use" (axes 4, 8)

- A4: staged raiders are force-fed the SS generator until each carries a ranged
  ammo-using sidearm (natural generation is chance-rolled; melee shivs are
  common and irrelevant — no ammo to provision). Inspect raider Gear tabs:
  ranged sidearms have a full magazine + a spare ammo stack in inventory; melee
  sidearms correctly get nothing. For extra samples use Debug actions →
  **CE+SS Compat → Force-generate SS sidearm** on any pawn (logs the resulting
  inventory with mag counts). Watch dev log for CE "over capacity" warnings
  (should be none).
- A8: give colonist a one-use launcher (CE disposable, e.g. RPG-7/AT launcher
  variants) + a remembered pistol sidearm. Fire the launcher → launcher consumed,
  pawn re-equips per SS preference (pistol), not bare fists.

## Regression sweep

Load any save, play 10 min with dev log open: no red errors, no yellow spam from
Harmony/CE/SS, caravan dialog opens, pawn Gear tab renders, save+reload works.
