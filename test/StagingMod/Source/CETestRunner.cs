using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CombatExtended;
using PeteTimesSix.SimpleSidearms.Utilities;
using RimWorld;
using SimpleSidearms.rimworld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static PeteTimesSix.SimpleSidearms.Utilities.Enums;
using SSCore = PeteTimesSix.SimpleSidearms.SimpleSidearms;

namespace CESSCompatTestStaging
{
    /// <summary>
    /// Acceptance harness for the CETEST saves (compat patch axes). Launch with:
    ///   -celoadsave=CETEST-1-pickup     -ceassert=cetest1
    ///   -celoadsave=CETEST-2-selection  -ceassert=cetest2
    ///   -celoadsave=CETEST-3-combat     -ceassert=cetest3
    ///   -celoadsave=CETEST-4-generation -ceassert=cetest4
    /// Same phase/check machinery as the Loadouts module's SupplyTestRunner; this
    /// runner owns scenarios prefixed "cetest" and ignores everything else (the
    /// Loadouts staging mod owns "supply" and does the same, so both mods can sit
    /// in one profile). Results: test-results-&lt;scenario&gt;.json in the save-data
    /// folder, then self-shutdown.
    /// Beyond TESTPLAN criteria, phases hunt under-the-surface bugs: hold-record
    /// duplication, CE capacity overruns, orphan ammo, generator idempotence.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CETestBoot
    {
        static CETestBoot()
        {
            if (!GenCommandLine.TryGetCommandLineArg("ceassert", out string scenario)
                || scenario.NullOrEmpty() || !scenario.StartsWith("cetest"))
            {
                return;
            }
            if (GenCommandLine.TryGetCommandLineArg("celoadsave", out string save) && !save.NullOrEmpty())
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    Log.Message($"[CETest] Auto-loading save '{save}'.");
                    GameDataSaveLoader.LoadGame(save);
                });
            }
        }
    }

    public class CETestRunnerComponent : GameComponent
    {
        private class Check
        {
            public string name;
            public Func<(bool pass, string detail)> eval;
            public bool informational;
            public bool passed;
            public string lastDetail = "not evaluated";
        }

        private class Phase
        {
            public string label;
            public Action mutate;
            public List<Check> checks = new List<Check>();
            public int deadlineTicks;
            public int minTicks;
            public bool failed;
        }

        private List<Phase> phases;
        private int phaseIndex = -1;
        private int phaseStartTick;
        private string scenario;
        private bool active;
        private bool done;

        public CETestRunnerComponent(Game game)
        {
        }

        public override void LoadedGame()
        {
            if (!GenCommandLine.TryGetCommandLineArg("ceassert", out scenario)
                || scenario.NullOrEmpty() || !scenario.StartsWith("cetest"))
            {
                return;
            }
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    DisableLoadoutsModule();
                    phases = BuildScenario(scenario);
                }
                catch (Exception e)
                {
                    Log.Error("[CETest] Scenario build failed: " + e);
                    WriteResults(crashed: e.ToString());
                    Root.Shutdown();
                    return;
                }
                active = true;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                Log.Message($"[CETest] Scenario '{scenario}' started, {phases.Count} phases.");
                AdvancePhase();
            });
        }

        /// <summary>
        /// The Loadouts module shares this test profile; switch its derivations off
        /// (in-memory only — no WriteSettings) so CETEST scenarios exercise the compat
        /// patch alone.
        /// </summary>
        private static void DisableLoadoutsModule()
        {
            try
            {
                Type mod = GenTypes.GetTypeInAnyAssembly("CESidearmsSupply.SupplyMod");
                object settings = mod?.GetProperty("Settings")?.GetValue(null);
                if (settings == null)
                {
                    return;
                }
                foreach (string field in new[] { "loadoutWeaponsAsSidearms", "ammoForAllRemembered", "refetchAllRemembered" })
                {
                    settings.GetType().GetField(field)?.SetValue(settings, false);
                }
                Log.Message("[CETest] Loadouts module derivations disabled for this run.");
            }
            catch (Exception e)
            {
                Log.Warning("[CETest] Could not disable Loadouts module: " + e.Message);
            }
        }

        public override void GameComponentTick()
        {
            if (!active || done)
            {
                return;
            }
            if (Find.TickManager.Paused || Find.TickManager.CurTimeSpeed != TimeSpeed.Superfast)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
            }
            int tick = Find.TickManager.TicksGame;
            if (tick % 30 != 0)
            {
                return;
            }

            Phase phase = phases[phaseIndex];
            bool allPass = true;
            foreach (Check check in phase.checks)
            {
                if (check.passed && !check.informational)
                {
                    continue;
                }
                try
                {
                    (bool pass, string detail) = check.eval();
                    check.lastDetail = detail;
                    check.passed = pass || check.informational;
                    if (!pass && !check.informational)
                    {
                        allPass = false;
                    }
                }
                catch (Exception e)
                {
                    check.lastDetail = "EXCEPTION: " + e.Message;
                    if (!check.informational)
                    {
                        allPass = false;
                    }
                }
            }

            if (tick - phaseStartTick < phase.minTicks)
            {
                return;
            }
            if (allPass)
            {
                Log.Message($"[CETest] Phase '{phase.label}' PASSED at tick {tick}.");
                AdvancePhase();
            }
            else if (tick - phaseStartTick > phase.deadlineTicks)
            {
                phase.failed = true;
                Log.Warning($"[CETest] Phase '{phase.label}' FAILED (deadline {phase.deadlineTicks} ticks).");
                AdvancePhase();
            }
        }

        private void AdvancePhase()
        {
            phaseIndex++;
            if (phaseIndex >= phases.Count)
            {
                Finish();
                return;
            }
            Phase phase = phases[phaseIndex];
            phaseStartTick = Find.TickManager.TicksGame;
            try
            {
                phase.mutate?.Invoke();
            }
            catch (Exception e)
            {
                Log.Error($"[CETest] Mutation for phase '{phase.label}' threw: " + e);
                phase.failed = true;
                foreach (Check c in phase.checks)
                {
                    c.lastDetail = "mutation threw: " + e.Message;
                }
                AdvancePhase();
            }
        }

        private void Finish()
        {
            done = true;
            WriteResults();
            Log.Message("[CETest] Scenario complete; shutting down.");
            Root.Shutdown();
        }

        private void WriteResults(string crashed = null)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"scenario\": \"{scenario}\",\n");
            bool overall = crashed == null && phases != null && phases.All(p => !p.failed);
            sb.Append($"  \"passed\": {(overall ? "true" : "false")},\n");
            if (crashed != null)
            {
                sb.Append($"  \"crashed\": \"{Escape(crashed)}\",\n");
            }
            sb.Append($"  \"ticks\": {(Find.TickManager?.TicksGame ?? 0)},\n");
            sb.Append("  \"phases\": [\n");
            if (phases != null)
            {
                for (int i = 0; i < phases.Count; i++)
                {
                    Phase p = phases[i];
                    sb.Append("    {\n");
                    sb.Append($"      \"label\": \"{Escape(p.label)}\",\n");
                    sb.Append($"      \"passed\": {((!p.failed) ? "true" : "false")},\n");
                    sb.Append($"      \"reached\": {(i <= phaseIndex ? "true" : "false")},\n");
                    sb.Append("      \"checks\": [\n");
                    for (int j = 0; j < p.checks.Count; j++)
                    {
                        Check c = p.checks[j];
                        sb.Append("        {");
                        sb.Append($"\"name\": \"{Escape(c.name)}\", ");
                        sb.Append($"\"passed\": {(c.passed ? "true" : "false")}, ");
                        sb.Append($"\"informational\": {(c.informational ? "true" : "false")}, ");
                        sb.Append($"\"detail\": \"{Escape(c.lastDetail)}\"");
                        sb.Append("}");
                        sb.Append(j < p.checks.Count - 1 ? ",\n" : "\n");
                    }
                    sb.Append("      ]\n");
                    sb.Append(i < phases.Count - 1 ? "    },\n" : "    }\n");
                }
            }
            sb.Append("  ]\n}\n");
            string path = Path.Combine(GenFilePaths.SaveDataFolderPath, $"test-results-{scenario}.json");
            File.WriteAllText(path, sb.ToString());
            Log.Message($"[CETest] Results written to {path}");
        }

        private static string Escape(string s)
        {
            return s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }

        // ---- shared helpers -----------------------------------------------

        private static Pawn Colonist(string nick)
        {
            Pawn pawn = Find.CurrentMap.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(p => p.Name is NameTriple nt && nt.Nick == nick);
            if (pawn == null)
            {
                throw new InvalidOperationException("Colonist not found: " + nick);
            }
            return pawn;
        }

        private static ThingDef D(string defName) => DefDatabase<ThingDef>.GetNamed(defName);

        private static ThingWithComps Carried(Pawn pawn, ThingDef def)
        {
            if (pawn.equipment?.Primary?.def == def)
            {
                return pawn.equipment.Primary;
            }
            return pawn.inventory.innerContainer.OfType<ThingWithComps>().FirstOrDefault(t => t.def == def);
        }

        private static Check C(string name, Func<(bool, string)> eval, bool informational = false)
        {
            return new Check { name = name, eval = eval, informational = informational };
        }

        private static List<Pawn> Hostiles()
        {
            return Find.CurrentMap.mapPawns.AllPawnsSpawned
                .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed).ToList();
        }

        private List<Phase> BuildScenario(string name)
        {
            switch (name)
            {
                case "cetest1": return BuildCetest1();
                case "cetest2": return BuildCetest2();
                case "cetest3": return BuildCetest3();
                case "cetest4": return BuildCetest4();
                default: throw new InvalidOperationException("Unknown scenario: " + name);
            }
        }

        // -- CETEST-1: axes 1 (bulk pickup) + 10 (hold sync) ----------------

        private List<Phase> BuildCetest1()
        {
            Pawn bulky = Colonist("Bulky");
            ThingDef lmg = D("Gun_LMG");
            ThingDef revolver = D("Gun_Revolver");
            ThingDef pistol = D("Gun_Autopistol");

            int PistolHoldRecords() => bulky.GetHoldRecords()?.Count(r => r._def == pistol) ?? 0;

            return new List<Phase>
            {
                new Phase
                {
                    label = "pickup-legality-and-hold",
                    deadlineTicks = 3000,
                    checks =
                    {
                        C("lmg-denied-by-bulk", () =>
                        {
                            bool ok = StatCalculator.CanPickupSidearmType(new ThingDefStuffDefPair(lmg, null), bulky, out string err);
                            return (!ok, $"canPickup={ok} err='{err}'");
                        }),
                        C("revolver-denial-not-bulk", () =>
                        {
                            // Pawn already holds rifle+pistol, so SS's own ranged-slot cap
                            // legitimately denies a third ranged weapon. Axis 1 only owns the
                            // BULK gate: the light revolver must never be rejected as too
                            // heavy — its denial reason must be SS's slot cap, not P01.
                            bool ok = StatCalculator.CanPickupSidearmType(new ThingDefStuffDefPair(revolver, null), bulky, out string err);
                            bool bulkDenial = err != null && err.Contains("heavy");
                            return (!bulkDenial, $"canPickup={ok} err='{err}' (must not be a bulk denial)");
                        }),
                        C("bulk-within-capacity", () =>
                        {
                            CompInventory inv = bulky.TryGetComp<CompInventory>();
                            return (inv.currentBulk <= inv.capacityBulk + 0.01f,
                                $"bulk {inv.currentBulk:F1}/{inv.capacityBulk:F1} weight {inv.currentWeight:F1}/{inv.capacityWeight:F1}");
                        }),
                        C("remembered-pistol-not-excess", () =>
                        {
                            bool excess = Utility_HoldTracker.GetExcessThing(bulky, out Thing dropThing, out int _);
                            bool pistolTargeted = excess && dropThing?.def == pistol;
                            return (!pistolTargeted, $"excess={excess} dropThing={dropThing?.def?.defName ?? "none"}");
                        }),
                        C("no-hold-records-written", () =>
                        {
                            // The exemption is answered in the GetExcess* postfixes and nothing
                            // is written back: CE's hold-tracker is shared with the player's own
                            // "hold this" command, and editing it clobbered their records.
                            int n = PistolHoldRecords();
                            return (n == 0, $"pistol hold records={n} (want 0 — exemption is read-only)");
                        }),
                    }
                },
                new Phase
                {
                    label = "forget-releases-hold",
                    deadlineTicks = 4000,
                    mutate = () =>
                    {
                        CompSidearmMemory.GetMemoryCompForPawn(bulky)
                            .ForgetSidearmMemory(new ThingDefStuffDefPair(pistol, null));
                    },
                    checks =
                    {
                        C("still-no-hold-records", () =>
                        {
                            int n = PistolHoldRecords();
                            return (n == 0, $"pistol hold records={n}");
                        }),
                        C("pistol-now-excess", () =>
                        {
                            bool excess = Utility_HoldTracker.GetExcessThing(bulky, out Thing dropThing, out int _);
                            return (excess && dropThing?.def == pistol,
                                $"excess={excess} dropThing={dropThing?.def?.defName ?? "none"}");
                        }),
                    }
                },
                new Phase
                {
                    label = "re-remember-idempotent",
                    deadlineTicks = 4000,
                    mutate = () =>
                    {
                        ThingWithComps pistolThing = Carried(bulky, pistol);
                        CompSidearmMemory mem = CompSidearmMemory.GetMemoryCompForPawn(bulky);
                        for (int i = 0; i < 3; i++)
                        {
                            mem.InformOfAddedSidearm(pistolThing);
                        }
                    },
                    checks =
                    {
                        C("exemption-survives-repeat-remembers", () =>
                        {
                            // SS's memory list grows on repeated remembers (see below); the
                            // exemption must stay a clean yes/no regardless, and must still not
                            // write anything into CE's tracker.
                            bool excess = Utility_HoldTracker.GetExcessThing(bulky, out Thing dropThing, out int _);
                            bool pistolTargeted = excess && dropThing?.def == pistol;
                            int n = PistolHoldRecords();
                            return (!pistolTargeted && n == 0,
                                $"pistolTargeted={pistolTargeted} hold records={n} (want false/0)");
                        }),
                        C("ss-memory-dup-upstream-quirk", () =>
                        {
                            // SS's InformOfAddedSidearm has NO duplicate guard upstream (the
                            // dedup code is commented out in SS source) — repeated calls grow
                            // RememberedWeapons. Recorded here as an upstream quirk; the compat
                            // patch's own state (hold records, previous check) must still dedup.
                            int n = CompSidearmMemory.GetMemoryCompForPawn(bulky)
                                .RememberedWeapons.Count(p => p.thing == pistol);
                            return (n == 1, $"pistol memory entries after 3x remember={n} (SS-native, no upstream guard)");
                        }, informational: true),
                    }
                },
            };
        }

        // -- CETEST-2: axes 2 (CE DPS), 3/9 (ammo-aware selection), 11 (classification) --

        private List<Phase> BuildCetest2()
        {
            Pawn picky = Colonist("Picky");
            ThingDef rifleDef = D("Gun_AssaultRifle");
            ThingDef pistolDef = D("Gun_Autopistol");
            ThingDef revolverDef = D("Gun_Revolver");
            ThingDef grenadeDef = D("Weapon_GrenadeEMP");

            return new List<Phase>
            {
                new Phase
                {
                    label = "scoring-and-classification",
                    deadlineTicks = 4000,
                    checks =
                    {
                        C("dry-revolver-has-no-ammo", () =>
                        {
                            var user = Carried(picky, revolverDef)?.TryGetComp<CompAmmoUser>();
                            return (user != null && !user.HasAmmoOrMagazine,
                                $"mag {user?.CurMagCount}/{user?.MagSize} hasAmmoOrMag={user?.HasAmmoOrMagazine}");
                        }),
                        C("loaded-guns-have-ammo", () =>
                        {
                            var rifle = Carried(picky, rifleDef)?.TryGetComp<CompAmmoUser>();
                            var pistolUser = Carried(picky, pistolDef)?.TryGetComp<CompAmmoUser>();
                            bool ok = rifle?.HasAmmoOrMagazine == true && pistolUser?.HasAmmoOrMagazine == true;
                            return (ok, $"rifle={rifle?.HasAmmoOrMagazine} pistol={pistolUser?.HasAmmoOrMagazine}");
                        }),
                        C("ce-dps-sane", () =>
                        {
                            float bias = SSCore.Settings.SpeedSelectionBiasRanged;
                            float rifleDps = StatCalculator.RangedDPS(Carried(picky, rifleDef), bias, 0f, 20f);
                            float pistolDps = StatCalculator.RangedDPS(Carried(picky, pistolDef), bias, 0f, 20f);
                            bool sane = rifleDps > 0f && pistolDps > 0f
                                && !float.IsNaN(rifleDps) && !float.IsNaN(pistolDps)
                                && !float.IsInfinity(rifleDps) && !float.IsInfinity(pistolDps)
                                && Math.Abs(rifleDps - pistolDps) > 0.01f;
                            return (sane, $"rifle@20={rifleDps:F2} pistol@20={pistolDps:F2}");
                        }),
                        C("rifle-beats-pistol-at-range", () =>
                        {
                            float bias = SSCore.Settings.SpeedSelectionBiasRanged;
                            float rifleDps = StatCalculator.RangedDPS(Carried(picky, rifleDef), bias, 0f, 30f);
                            float pistolDps = StatCalculator.RangedDPS(Carried(picky, pistolDef), bias, 0f, 30f);
                            return (rifleDps > pistolDps, $"rifle@30={rifleDps:F2} pistol@30={pistolDps:F2}");
                        }),
                        C("best-weapon-never-dry", () =>
                        {
                            Pawn target = Hostiles().FirstOrDefault(h => !(h.RaceProps?.IsMechanoid ?? false));
                            var (weapon, dps, _) = GettersFilters.findBestRangedWeapon(picky,
                                target != null ? new LocalTargetInfo(target) : (LocalTargetInfo?)null);
                            bool ok = weapon != null && weapon.def != revolverDef;
                            return (ok, $"best={weapon?.def?.defName ?? "null"} dps={dps:F2} target={(target != null ? "yes" : "no")}");
                        }),
                        C("emp-grenade-classified-emp", () =>
                        {
                            ThingWithComps grenade = Carried(picky, grenadeDef);
                            return (grenade != null && GettersFilters.isEMPWeapon(grenade),
                                $"grenade={(grenade != null)} isEMP={(grenade != null ? GettersFilters.isEMPWeapon(grenade).ToString() : "n/a")}");
                        }),
                        C("fmj-rifle-not-emp-not-dangerous", () =>
                        {
                            ThingWithComps rifle = Carried(picky, rifleDef);
                            bool emp = GettersFilters.isEMPWeapon(rifle);
                            bool danger = GettersFilters.isDangerousWeapon(rifle);
                            return (!emp && !danger, $"isEMP={emp} isDangerous={danger}");
                        }),
                    }
                },
                new Phase
                {
                    label = "dry-primary-switches-to-loaded",
                    deadlineTicks = 6000,
                    mutate = () =>
                    {
                        // Drain the rifle completely: empty mag AND remove its caliber from
                        // inventory so CE cannot count it reloadable, then run SS's re-equip.
                        ThingWithComps rifle = Carried(picky, rifleDef);
                        CompAmmoUser user = rifle.TryGetComp<CompAmmoUser>();
                        user.CurMagCount = 0;
                        List<ThingDef> rifleAmmo = user.Props.ammoSet.ammoTypes.Select(l => (ThingDef)l.ammo).ToList();
                        foreach (Thing stack in picky.inventory.innerContainer.Where(t => rifleAmmo.Contains(t.def)).ToList())
                        {
                            stack.Destroy(DestroyMode.Vanish);
                        }
                        WeaponAssingment.equipBestWeaponFromInventoryByPreference(picky, DroppingModeEnum.Combat);
                    },
                    checks =
                    {
                        C("primary-is-loaded-pistol", () =>
                        {
                            ThingDef primary = picky.equipment?.Primary?.def;
                            return (primary == pistolDef, $"primary={primary?.defName ?? "none"}");
                        }),
                        C("never-dry-revolver-or-fists", () =>
                        {
                            ThingDef primary = picky.equipment?.Primary?.def;
                            return (primary != null && primary != revolverDef, $"primary={primary?.defName ?? "FISTS"}");
                        }),
                    }
                },
            };
        }

        // -- CETEST-3: axes 6 (CQC), 7 (warmup swap), 5 (reload guard) ------

        // Captured synchronously inside the axis-9 queued-equip phase; a poll-based check
        // would race the job it is meant to observe.
        private ThingDef queuedFrom;
        private JobDef queuedJob;
        private ThingDef queuedTarget;
        private ThingDef queuedPrimaryImmediately;
        private bool queuedResult;

        private List<Phase> BuildCetest3()
        {
            Pawn fency = Colonist("Fency");
            Pawn scopey = Colonist("Scopey");
            ThingDef gladius = D("MeleeWeapon_Gladius");
            ThingDef sniper = D("Gun_SniperRifle");
            ThingDef shotgun = D("Gun_PumpShotgun");

            return new List<Phase>
            {
                new Phase
                {
                    label = "cqc-melee-draw",
                    deadlineTicks = 30000,
                    checks =
                    {
                        C("fency-draws-gladius", () =>
                        {
                            ThingDef primary = fency.equipment?.Primary?.def;
                            return (primary == gladius, $"primary={primary?.defName ?? "none"} raiders={Hostiles().Count}");
                        }),
                    }
                },
                new Phase
                {
                    label = "warmup-swap-to-shotgun",
                    deadlineTicks = 10000,
                    mutate = () =>
                    {
                        SSCore.Settings.RangedCombatAutoSwitch = true;
                        SSCore.Settings.RangedCombatAutoSwitchMaxWarmup = 5f;
                        Pawn target = Hostiles().FirstOrDefault();
                        if (target == null)
                        {
                            throw new InvalidOperationException("No hostile left for warmup-swap phase");
                        }
                        // Move the target close to Scopey so short range favors the shotgun.
                        IntVec3 near = scopey.Position + new IntVec3(6, 0, 0);
                        near = near.ClampInsideMap(scopey.Map);
                        if (!near.Standable(scopey.Map))
                        {
                            CellFinder.TryFindRandomCellNear(scopey.Position, scopey.Map, 8,
                                c => c.Standable(scopey.Map), out near);
                        }
                        target.Position = near;
                        target.Notify_Teleported();
                        scopey.drafter.Drafted = true;
                        Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                        scopey.jobs.TryTakeOrderedJob(job);
                    },
                    checks =
                    {
                        C("scopey-swaps-to-shotgun", () =>
                        {
                            ThingDef primary = scopey.equipment?.Primary?.def;
                            return (primary == shotgun, $"primary={primary?.defName ?? "none"} job={scopey.CurJobDef?.defName}");
                        }),
                    }
                },
                new Phase
                {
                    label = "reload-guard",
                    deadlineTicks = 15000,
                    mutate = () =>
                    {
                        foreach (Pawn hostile in Hostiles())
                        {
                            hostile.Destroy(DestroyMode.Vanish);
                        }
                        scopey.drafter.Drafted = false;
                        scopey.jobs.StopAll();
                        // Drain whatever Scopey now holds (shotgun after the swap phase, or
                        // sniper if the swap failed); spares from staging are in inventory.
                        ThingWithComps primary = scopey.equipment.Primary;
                        CompAmmoUser user = primary.TryGetComp<CompAmmoUser>();
                        user.CurMagCount = 0;
                        Job reload = user.TryMakeReloadJob();
                        if (reload == null)
                        {
                            throw new InvalidOperationException("TryMakeReloadJob returned null (no spare ammo?)");
                        }
                        scopey.jobs.StartJob(reload, JobCondition.InterruptForced);
                        // Axis 5 direct hit: while the reload job runs, fire SS's switch
                        // entry point — the patch must refuse to cancel the reload.
                        WeaponAssingment.equipBestWeaponFromInventoryByPreference(scopey, DroppingModeEnum.Combat);
                    },
                    checks =
                    {
                        C("reload-survives-ss-switch-call", () =>
                        {
                            // Passes once the reload finished with the same weapon still equipped.
                            ThingWithComps primary = scopey.equipment?.Primary;
                            CompAmmoUser user = primary?.TryGetComp<CompAmmoUser>();
                            bool full = user != null && user.CurMagCount == user.MagSize;
                            return (full, $"primary={primary?.def?.defName} mag={user?.CurMagCount}/{user?.MagSize} job={scopey.CurJobDef?.defName}");
                        }),
                        C("reload-job-observed", () =>
                        {
                            bool reloading = scopey.CurJobDef == CE_JobDefOf.ReloadWeapon;
                            return (reloading, $"job={scopey.CurJobDef?.defName}");
                        }, informational: true),
                    }
                },
                new Phase
                {
                    // Axis 9, stopJob:false path (CE's CompReload calls it that way when a
                    // pawn's gun is empty mid-cast). CE wants an interruptible
                    // EquipFromInventory job, not an instant swap — but it should be equipping
                    // SS's preferred weapon, not the first viable one in CE's own list order.
                    label = "queued-equip-uses-ss-preference",
                    deadlineTicks = 10000,
                    mutate = () =>
                    {
                        scopey.jobs.StopAll();
                        ThingWithComps sniperThing = scopey.inventory.innerContainer
                            .OfType<ThingWithComps>().FirstOrDefault(t => t.def == sniper)
                            ?? (scopey.equipment.Primary?.def == sniper ? scopey.equipment.Primary : null);
                        if (sniperThing == null)
                        {
                            throw new InvalidOperationException("Scopey is not carrying the sniper");
                        }
                        // CE only offers weapons it considers firable, so make sure the one SS
                        // is about to prefer actually has rounds.
                        sniperThing.TryGetComp<CompAmmoUser>()?.ResetAmmoCount();
                        CompSidearmMemory memory = CompSidearmMemory.GetMemoryCompForPawn(scopey);
                        memory.DefaultRangedWeapon = new ThingDefStuffDefPair(sniper, null);

                        queuedFrom = scopey.equipment.Primary?.def;
                        bool handled = scopey.TryGetComp<CompInventory>()
                            .SwitchToNextViableWeapon(useFists: true, useAOE: false, stopJob: false);
                        // Captured synchronously: the job runs within a few ticks, well inside
                        // the runner's 30-tick poll interval.
                        queuedResult = handled;
                        queuedJob = scopey.CurJobDef;
                        queuedTarget = (scopey.CurJob?.targetA.Thing)?.def;
                        queuedPrimaryImmediately = scopey.equipment.Primary?.def;
                    },
                    checks =
                    {
                        C("equip-was-queued-not-instant", () =>
                        {
                            bool queued = queuedJob == CE_JobDefOf.EquipFromInventory;
                            bool unchanged = queuedPrimaryImmediately == queuedFrom;
                            return (queued && unchanged,
                                $"handled={queuedResult} job={queuedJob?.defName ?? "none"} "
                                + $"primaryAtCall={queuedPrimaryImmediately?.defName ?? "none"} (was {queuedFrom?.defName ?? "none"})");
                        }),
                        C("queued-weapon-is-ss-preference", () =>
                            (queuedTarget == sniper, $"jobTarget={queuedTarget?.defName ?? "none"}")),
                        C("preference-actually-equipped", () =>
                        {
                            ThingDef primary = scopey.equipment?.Primary?.def;
                            return (primary == sniper, $"primary={primary?.defName ?? "none"} job={scopey.CurJobDef?.defName ?? "none"}");
                        }),
                    }
                },
            };
        }

        // -- CETEST-4: axes 4 (NPC sidearm ammo) + 8 (one-use fallback) -----

        private List<Phase> BuildCetest4()
        {
            Pawn boomy = Colonist("Boomy");
            ThingDef pistol = D("Gun_Autopistol");

            (bool ok, string detail) RaiderProvisioning()
            {
                var problems = new List<string>();
                int checkedRaiders = 0;
                foreach (Pawn raider in Hostiles().Where(h => !(h.RaceProps?.IsMechanoid ?? false)))
                {
                    checkedRaiders++;
                    var carriedWeapons = raider.inventory.innerContainer.OfType<ThingWithComps>()
                        .Where(t => t.def.IsWeapon).ToList();
                    if (raider.equipment?.Primary != null)
                    {
                        carriedWeapons.Add(raider.equipment.Primary);
                    }
                    var validAmmoDefs = new HashSet<ThingDef>();
                    foreach (ThingWithComps weapon in carriedWeapons)
                    {
                        CompAmmoUser user = weapon.TryGetComp<CompAmmoUser>();
                        if (user == null || !user.UseAmmo)
                        {
                            continue;
                        }
                        foreach (var link in user.Props.ammoSet.ammoTypes)
                        {
                            validAmmoDefs.Add(link.ammo);
                        }
                        bool isSidearm = weapon != raider.equipment?.Primary;
                        if (isSidearm && user.CurMagCount <= 0)
                        {
                            problems.Add($"{raider.LabelShort}: sidearm {weapon.def.defName} mag {user.CurMagCount}/{user.MagSize}");
                        }
                        bool hasSpare = raider.inventory.innerContainer.Any(t =>
                            user.Props.ammoSet.ammoTypes.Any(l => (ThingDef)l.ammo == t.def));
                        if (isSidearm && !hasSpare)
                        {
                            problems.Add($"{raider.LabelShort}: no spare ammo for sidearm {weapon.def.defName}");
                        }
                    }
                    foreach (Thing stack in raider.inventory.innerContainer.Where(t => t.def is AmmoDef))
                    {
                        // CE injects loose thrown grenades (AmmoDefs that are themselves
                        // weapons) into raid inventories with no launcher — legitimate.
                        if (stack.def.IsWeapon)
                        {
                            continue;
                        }
                        if (!validAmmoDefs.Contains(stack.def))
                        {
                            problems.Add($"{raider.LabelShort}: ORPHAN ammo {stack.def.defName} x{stack.stackCount}");
                        }
                    }
                    CompInventory inv = raider.TryGetComp<CompInventory>();
                    if (inv != null && inv.currentBulk > inv.capacityBulk + 0.01f)
                    {
                        problems.Add($"{raider.LabelShort}: OVER BULK {inv.currentBulk:F1}/{inv.capacityBulk:F1}");
                    }
                    if (inv != null && inv.currentWeight > inv.capacityWeight + 0.01f)
                    {
                        problems.Add($"{raider.LabelShort}: OVER WEIGHT {inv.currentWeight:F1}/{inv.capacityWeight:F1}");
                    }
                }
                return (problems.Count == 0 && checkedRaiders > 0,
                    problems.Count == 0 ? $"{checkedRaiders} raiders clean" : string.Join(" | ", problems.Take(6)));
            }

            return new List<Phase>
            {
                new Phase
                {
                    label = "raider-ammo-provisioning",
                    deadlineTicks = 4000,
                    checks = { C("raiders-provisioned-no-orphans-no-overcap", () => RaiderProvisioning()) }
                },
                new Phase
                {
                    label = "generator-idempotence",
                    deadlineTicks = 4000,
                    mutate = () =>
                    {
                        Pawn raider = Hostiles().FirstOrDefault(h => !(h.RaceProps?.IsMechanoid ?? false));
                        if (raider != null)
                        {
                            TestStagingComponent.ForceRangedSidearm(raider);
                        }
                    },
                    checks = { C("still-clean-after-regeneration", () => RaiderProvisioning()) }
                },
                new Phase
                {
                    label = "one-use-fallback",
                    deadlineTicks = 25000,
                    mutate = () =>
                    {
                        // No live hostiles at all — a raider (even disarmed) charges into
                        // melee and kills the attack job. The rocket can target locations,
                        // so fire it at open ground.
                        foreach (Pawn hostile in Hostiles())
                        {
                            hostile.Destroy(DestroyMode.Vanish);
                        }
                        // Drafting makes SS swap to the pistol (launchers are manual-use, SS
                        // skips them) — switch back via CE's own inventory API and suppress
                        // the warmup auto-switch so the ROCKET is what actually fires.
                        boomy.drafter.Drafted = true;
                        SSCore.Settings.RangedCombatAutoSwitch = false;
                        ThingWithComps launcher =
                            boomy.equipment.Primary != null && boomy.equipment.Primary.def.defName.Contains("Rocket")
                                ? boomy.equipment.Primary
                                : boomy.inventory.innerContainer.OfType<ThingWithComps>()
                                    .FirstOrDefault(t => t.def.defName.Contains("Rocket"));
                        if (launcher == null)
                        {
                            throw new InvalidOperationException("Launcher vanished before the one-use phase");
                        }
                        if (boomy.equipment.Primary != launcher)
                        {
                            boomy.TryGetComp<CompInventory>().TrySwitchToWeapon(launcher);
                        }
                        // As far as this launcher can actually shoot, so the blast cannot reach
                        // the shooter, but inside its range so a drafted AttackStatic fires
                        // instead of standing there unable to reach the cell. At a fixed 10
                        // cells Boomy was downed or killed by his own rocket; at a fixed 24 he
                        // was out of range and never fired. Both decided the phase on something
                        // other than the one-use fallback.
                        float launcherRange = launcher.GetComp<CompEquippable>()?.PrimaryVerb?.verbProps?.range ?? 12f;
                        int shotDistance = Math.Max(6, Math.Min(18, (int)launcherRange - 2));
                        IntVec3 targetCell = boomy.Position + new IntVec3(shotDistance, 0, 0);
                        targetCell = targetCell.ClampInsideMap(boomy.Map);
                        if (!targetCell.Standable(boomy.Map) || targetCell.DistanceTo(boomy.Position) < shotDistance - 2f)
                        {
                            CellFinder.TryFindRandomCellNear(boomy.Position, boomy.Map, shotDistance + 4,
                                c => c.Standable(boomy.Map) && c.DistanceTo(boomy.Position) > shotDistance - 2f, out targetCell);
                        }
                        Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, new LocalTargetInfo(targetCell));
                        boomy.jobs.TryTakeOrderedJob(job);
                    },
                    checks =
                    {
                        C("launcher-consumed-pistol-equipped", () =>
                        {
                            bool launcherAnywhere =
                                (boomy.equipment?.Primary?.def.defName.Contains("Rocket") ?? false)
                                || boomy.inventory.innerContainer.Any(t => t.def.defName.Contains("Rocket"));
                            ThingDef primary = boomy.equipment?.Primary?.def;
                            return (!launcherAnywhere && primary == pistol,
                                $"launcherPresent={launcherAnywhere} primary={primary?.defName ?? "FISTS"}");
                        }),
                        C("boomy-health-forensics", () =>
                        {
                            bool pistolCarried = boomy.inventory.innerContainer.Any(t => t.def == pistol);
                            return (true, $"downed={boomy.Downed} dead={boomy.Dead} drafted={boomy.Drafted} pistolInInventory={pistolCarried} job={boomy.CurJobDef?.defName}");
                        }, informational: true),
                        C("launcher-not-in-inventory", () =>
                        {
                            bool present = boomy.inventory.innerContainer.Any(t => t.def.defName.Contains("Rocket"));
                            return (!present, $"launcher in inventory={present}");
                        }, informational: true),
                    }
                },
            };

        }
    }
}
