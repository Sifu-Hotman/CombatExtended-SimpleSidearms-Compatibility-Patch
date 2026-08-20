# Combat Extended — conventions for third-party mods

Reference distilled 2026-08-20 from three independent sweeps of
`CombatExtended-Continued/CombatExtended` (branch `Development`, HEAD `04fcf6d7`,
CE 16.7.3.0): its documentation and wiki, its **entire** 2024+ line-level PR
review corpus (572 review comments / 493 conversation comments / all 119
closed-unmerged PRs), and its in-tree compatibility architecture.

Scope: rules that bind **mods which patch or interoperate with CE**. Rules that
apply only to CE contributors are marked `[CE-only]` and kept because they are
the bar any upstream contribution would be held to.

Evidence discipline from the sweeps is preserved: every rule carries a citation
(file path, PR number, or quote). `INFERRED` marks inference rather than a
stated rule. Contradictions are kept with dates rather than resolved.

---

## 1. Entry points CE actually offers

| Hook | Contract | Notes |
|---|---|---|
| `CombatExtended.Compatibility.IPatch` | `CanInstall()` / `Install()` | Auto-discovered by scanning **every loaded assembly**, so external mods can implement it. Runs inside `LongEventHandler.QueueLongEvent(patches.Install, "CE_LongEvent_CompatibilityPatches", …)` (`ModSettings/Controller.cs:138`). |
| `BlockerRegistry` | 6 `Register*Callback` methods | The only formally documented API (`API-Reference.md`). Projectile interception. |
| `TurretRegistry` | `RegisterReloadableTurret(...)` | Makes CE's reload/ammo machinery work on a foreign turret type. |
| `Patches.UsedAmmoCallbacks`, `RegisterCollisionBodyFactorCallback` | static callback lists | Declare extra ammo defs; override pawn hitboxes. |
| `CompCIWSTarget` | abstract `ThingComp` | Explicitly documented "for third-party mods compatibility". |
| Empty stub methods | Harmony-patch them yourself | `Harmony_AlienRace.cs:15` — "we've implemented several empty functions that return a default value". Undocumented elsewhere; no index of which methods are stubs. |

**Registry callbacks are preferred over patching CE** where a registry exists:
8 distinct compat modules register rather than patch, and registries
short-circuit to a single `bool` when nobody registered (`BlockerRegistry.cs:26-55`,
`TurretRegistry.cs:29-44`). There is no registry for weapon selection, loadouts,
or inventory — patching is the only door there.

**`E2` caveat:** the `IPatch` scan swallows `ReflectionTypeLoadException` and
logs only under `Prefs.DevMode` (`Compatibility/Patches.cs:34-45`) — if your
assembly fails to load, you get silence in a normal game.

**No ABI policy.** *"we generally don't have a defined ABI compatibility
policy—we generally try to avoid breaking things for known extension points,
like `Verb*` classes"* — mszabo-wikia, issue #4413, 2026-01-03; anything else
*"would likely be liable to change."* Precedent: CE 1.5.6.2.0 moved
`CompAmmoUser` between classes and told dependent mods to recompile.

---

## 2. Failure handling — the house rule

**Missing target → silent skip. Present-but-changed → `Log.Error` and degrade.
Never throw.** 9 of 10 of CE's own reflective compat patches gate on
`Prepare()`; transpilers that lose their anchor log and yield the *unmodified*
instruction stream rather than aborting.

- *"Let's us know which transpiler patch is failing if/when they fail."* —
  ViralReaction, PR #4450, 2026-02-03, establishing the idiom
  `Log.Error($"Combat Extended :: Failed to find injection point when applying Patch: {HarmonyBase.GetClassName(...)}")`
  (≥20 sites use it verbatim).
- Auto-patchers wrap **per item** so one bad def can't abort the batch
  (`GunAutoPatcher.cs:122-130`).
- CE will disable a broken third-party patch and tell the user to complain
  upstream rather than crash (`Harmony_GraphicApparelDetour.cs:74-79`).

**Load-bearing consequence for anyone implementing `IPatch`:** `Patches.Install()`
(`Compatibility/Patches.cs:62-71`) has **no** try/catch around each patch's
`Install()`. An exception thrown there takes down CE's entire compatibility long
event — i.e. every other mod's compat patches, not just yours. Guard your own
`Install()`.

---

## 3. Detection and soft dependency

- **XML, folder granularity:** `LoadFolders.xml` + `IfModActive="<packageId>"`;
  comma-separated ids supported. Gates `Defs/`, `Patches/`, **and**
  `Assemblies/`. Adding a `LoadFolders.xml` means you must re-declare the root
  `<li>/</li>` or your own content stops loading (`LoadFolders.xml:4-5`).
- **XML, element granularity:** vanilla `MayRequire="CETeam.CombatExtended"`.
  Do **not** put `MayRequire` on an `<Operation>` — *"doesn't really work
  properly… might break silently"* (N7Huntsman, PR #3557, 2024-11-19).
- **C#, in the wild:** `ModsConfig.IsActive("CETeam.CombatExtended")`.
  (CE internally prefers `ModLister.HasActiveModWithName("<display name>")`,
  12 of 13 sites — a legacy habit; packageId is the safer identifier and CE
  itself falls back to it for mods with unstable names.)
- **C#, no reference at all:** resolve by string in Harmony `Prepare()` /
  `TargetMethod()` — `AccessTools.TypeByName(...) != null`
  (`Harmony_VOID.cs:11-31`, 6 of 10 live files).
- **Strongest guard is no guard:** gate the whole assembly at LoadFolders, then
  the code inside needs zero defensive checks. All 9 of CE's compat DLLs do
  this and contain no runtime mod-presence checks at all.

**Reference assemblies:** CE never references a game install or Steam path. It
commits *stripped stub DLLs* (`Source/packages/*-reference.dll`, 10–28 KB,
generated by `Reference.py`, which rewrites every method body to `throw null;`)
and references them with `<Private>False</Private>`. `Condition="Exists(...)"`
appears zero times repo-wide. Publicizing is the sanctioned way to reach
internals: *"I'm not entirely opposed to using reflection, but it does tend to
be more fragile than publicized assemblies, and it is nearly always slower."* —
perkinslr, PR #3122, 2024-05-27. Asking upstream to widen access is preferred
over both.

---

## 4. Performance rules

- **Never scan the map per invocation on a projectile-frequency path.** Register
  on spawn/despawn, or scan once per tick and cache (`API-Reference.md`
  §Performance).
- **No LINQ or list allocation in hot loops** when one value is wanted — *"LinQ
  is slow, as is list/generator creation. Since we only need a single value, we
  can filter as we go."* (perkinslr, PR #2945, 2024-01-13). A measured de-LINQ
  of suppression code cut ~3.5% of total frame time (PR #3529).
  **Contradiction, and it matters:** outside hot paths LINQ is fine and removing
  it needs a stated reason — *"Why replace this linq with a loop? If it's
  performance, that's fine."* (perkinslr, PR #4171, 2025-08-18).
- **Cache per-tick anything that cannot change within a tick** — PR #4517,
  2026-03-20.
- **Cheap early-outs go outside the loop** — PR #2945, 2024-01-12.
- **Never call inventory/stat recomputation from draw code** — *"This will be
  calling CompInventory.UpdateInventory multiple times per frame"*
  (ViralReaction, PR #4537, 2026-04-18).
- **Comps tick on intervals** (`CompTickInterval` / `IsHashIntervalTick`), not
  every tick (PR #3988, 1.6 delta-ticking) — *except* combat-critical ticks,
  which CE deliberately left per-tick.
- **Benchmark inside RimWorld**, not a desktop .NET harness; maintainers
  counter-benchmark (perkinslr, PR #4029, 2025-07-14).
- **Micro-optimisation that costs readability loses**, even when it wins —
  the "bus test" (perkinslr, PR #4029, 2025-07-13).
- **Blanket defensive null-checking is a regression, not safety** — *"nulls
  checking everything everywhere will lead to slowing the game down"*
  (ViralReaction, PR #4148, 2025-08-11). Operative rule: cast-and-check once at
  entry, then stop; targeted null checks on comp props and public API are still
  requested.

---

## 5. XML patching

- Patch the **parent/abstract def**, not each child (PR #3527).
- **Never overwrite a whole node** (`statBases`, `stages`, `verbs`) to change one
  child — split into targeted operations (PR #4536, repeated 8× in one review).
- **Don't `PatchOperationReplace` over a third-party def** — create a `CE_`
  variant and swap it in (PR #4455, 2026-02-18). Exception: defs inheriting from
  a foreign framework parent may need wholesale replacement (PR #3328).
- **Don't wrap mod-gated patches in `PatchOperationFindMod`** — LoadFolders
  already gates them, and a redundant sequence aborts every subsequent operation
  on first failure (PR #4637, 2026-07-09).
- `PatchOperationMakeGunCECompatible` is additive and has a **fixed accepted node
  set**; anything outside it (e.g. `tools`) needs a separate operation, and it
  needs the target to already have `<verbs>`.
- Files live at `ModPatches/<Mod>/Patches/<Mod>/File.xml` — the doubled mod name
  is deliberate: LoadFolders merges by relative path, so identical paths silently
  overwrite. CI-enforced since PR #4386 (2026-02-27).
- When behaviour needs per-item exceptions, **expose an XML field and blacklist
  in XML** rather than growing conditional C# (PR #3169, 2024-06-07).

---

## 6. Def and save stability

- **Renaming a defName requires a `BackCompatibilityConverter`** (PR #4049).
- **Never delete a def that can exist in a save** — deprecate it (PR #3733).
- **Keep dead XML fields** so old third-party XML keeps loading; announce with
  `Log.ErrorOnce` (PR #4145, PR #3764).
- **Defs are not storage for player-created content** — scribe it separately
  (PR #4626, 2026-06-20).
- Initialise load-sensitive fields in `PostSpawnSetup`; value types silently
  default on load (PR #3989).
- CE itself is **not save-removable** and requires a new save (`README.md`).
- Notable absence: across the whole 2024+ review corpus there is essentially
  **no** `Scribe`/`ExposeData`/mid-save discussion. CE polices def identity, not
  serialisation.

---

## 7. Compatibility etiquette

- **CE projectiles are invisible to vanilla-typed patches.** `ProjectileCE` is
  not a `Projectile`; `Verb_LaunchProjectileCE` does not derive from
  `Verb_LaunchProjectile`. Patches written against vanilla types no-op
  **silently** (issue #4357, 2026-02-23).
- **Use CE's utilities, inherit CE's classes** rather than re-deriving geometry
  or duplicating `Building_TurretGunCE` (PR #3182). `CE_Utility` is kept
  deliberately def-free *"to facilitate mods using CE through function pointers
  and opaque types"* (perkinslr, PR #3764).
- **A bug in the other mod is fixed upstream, not shimmed** in an unrelated
  patch (PR #4255, 2025-11-29). **Contradiction:** CE does patch outward when
  the alternative would distort CE's own model (PR #4608, 2026-06-02). The line
  is model corruption, not bug ownership.
- **Either side may own a compat patch**; CE de-scopes on request and treats
  self-patched mods as not its problem (PR #3701; issue #4472). *"If you wish to
  create a patch for any requested mod, there is no need to ask for permission."*
- **Mark incompatible rather than XML-stripping** another mod's core
  functionality when no C# fix exists (PR #4213).
- **Don't patch a mod that is mid-rewrite** (PR #4013, PR #4215).
- Badge use is invited: *"Include the following badge graphic in your mod
  descriptions"* (`Media/MediaFolder_README.md`).

---

## 8. If you ever submit upstream `[CE-only, but the bar to expect]`

- PRs target `Development`. `dotnet format --verify-no-changes` and the
  LoadFolders test are hard CI gates. `TreatWarningsAsErrors=true`.
- The PR template mandates **Reasoning** and **Alternatives** sections plus a
  testing checklist: compiles without warnings, game runs without errors,
  *"(For compatibility patches) …with and without patched mod loaded"*,
  and playtest duration.
- **Attach a savegame** for anything perf- or behaviour-sensitive; every
  performance PR in the corpus ships one.
- **Speculative or AI-generated defensive fixes are refused without in-game
  reproduction steps** — *"Do you have steps to properly reproduce the supposed
  errors or are these purely theoretical?"* (ViralReaction, PR #4675,
  2026-07-29); *"these changes aren't always best practice or may come with
  other knock-on effects (performance, structure, etc.) the AI doesn't
  appreciate."* (N7Huntsman, 2026-08-02). Lead with reproduction and playtest
  evidence, never with rationale.
- **Ask before a large refactor**; unrequested cross-cutting rewrites die
  (PR #4148). Split orthogonal changes into their own PRs (PR #3927).
- **Contested design calls are settled on Discord**, not in the PR (PR #3536).
  A GitHub-only argument on a design question is unlikely to prevail.
- "Closed" often means "re-landed by a maintainer" (PR #4255 → #4367; nine
  confirmed successor chains).
- Style specifics reviewers demand repeatedly: `break` out of loops rather than
  flag-and-continue; name constants (`GenTicks.TicksPerRealSecond`, not `60`);
  no `== true`/`== false`; explicit visibility; fields at the top of the class;
  English-only comments; booleans named as booleans.

---

## 9. Dated patterns — do not copy from old examples

| Pattern | Superseded | Since |
|---|---|---|
| `PatchOperationFindMod` + `PatchOperationSequence` mod gating | `LoadFolders.xml` `IfModActive` | 2024-04-11 (PR #3073) |
| `PatchOperation_ConditionalGeneric` | `PatchOperationSettingsConditional` (takes the **private backing field** name) | `[Obsolete]`, removal pending |
| `DupeFinder.py` / `duplicates.yml` | xUnit `LoadFoldersXmlTests` | 2026-02-27 (PR #4386) |
| Block-scoped namespaces | file-scoped | 2025 |
| `lerpPositions` | `trajectoryWorker` | 2025-04 (PR #3764) |
| `aiUseBurstMode` | dead; retained only so old XML loads | PR #870 |

Also stale: CE's own `PatchOperationFindMod` class has **zero** call sites and
shadows the vanilla one — a latent name-collision hazard, not a feature.

---

## 10. Coverage and known gaps

Read: CE repo docs, wiki (all 5 pages), `.github/` templates and all 6
workflows, `LoadFolders.xml` in full, all 4 custom `PatchOperation` classes,
both discovery drivers, all 11 `IPatch` modules, all 9 compat projects, all 11
reflective Harmony compat files, all 5 auto-patchers, `Source/Tests/`, releases
`v1.6.7.0.0`–`v1.6.7.3.0` plus a breaking-change sweep back to `v1.5.6.0.0`, and
the complete 2024+ PR review corpus.

Gaps that no GitHub-only reconstruction can close:

1. **The CE Discord.** The patch guide defers all C#-patch conventions to it, and
   multiple PR threads terminate in a Discord permalink. A material share of
   design decisions lives only there.
2. **The stub-method surface** — CE ships empty default-returning methods for
   modders to patch, documented in exactly one comment, with no index.
3. **Whether `IPatch` is public API or incidentally open.** The scan covers all
   AppDomain assemblies; no document promises third parties may implement it.
4. **Prefix/postfix preference, Harmony priority negotiation, and serialisation
   review** produced *no discourse at all* in 2024+. Interpretations there are
   inferred from code counts.
