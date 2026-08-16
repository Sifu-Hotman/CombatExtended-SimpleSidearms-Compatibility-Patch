using CombatExtended;
using HarmonyLib;
using PeteTimesSix.SimpleSidearms;
using PeteTimesSix.SimpleSidearms.Intercepts;
using PeteTimesSix.SimpleSidearms.Utilities;
using static PeteTimesSix.SimpleSidearms.Utilities.Enums;
using RimWorld;
using Verse;
using Verse.AI;
using SSCore = PeteTimesSix.SimpleSidearms.SimpleSidearms;

namespace CESimpleSidearmsCompat.Patches
{
    /// <summary>
    /// Axis 7: SS's mid-combat "swap to a more accurate ranged weapon" only triggers for
    /// vanilla Verb_Shoot, so it is silently dead under CE (Verb_ShootCE). Replicates SS's
    /// Stance_Warmup postfix for CE shoot verbs, reusing SS's own helpers and settings.
    /// </summary>
    [HarmonyPatch(typeof(Stance_Warmup), nameof(Stance_Warmup.StanceTick))]
    public static class Stance_Warmup_StanceTick_CE_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Stance_Warmup __instance)
        {
            if (!SSCore.Settings.RangedCombatAutoSwitch)
            {
                return;
            }
            if (!(__instance.verb is Verb_ShootCE))
            {
                return; // vanilla verbs are handled by SS's own patch
            }
            Pawn pawn = __instance.stanceTracker.pawn;
            if (Stance_Warmup_StanceTick_Postfix.IsHunting(pawn))
            {
                return;
            }
            if (!pawn.IsValidSidearmsCarrierRightNow())
            {
                return;
            }

            float aimingDelayFactor = pawn.GetStatValue(StatDefOf.AimingDelayFactor, true);
            int warmupTicks = (__instance.verb.verbProps.warmupTime * aimingDelayFactor).SecondsToTicks();
            if (warmupTicks <= 0 || __instance.ticksLeft / (float)warmupTicks < 1f - SSCore.Settings.RangedCombatAutoSwitchMaxWarmup)
            {
                return;
            }

            LocalTargetInfo target = __instance.focusTarg;
            bool empGood = target.Pawn?.RaceProps.IsMechanoid ?? false;

            var jobData = Stance_Warmup_StanceTick_Postfix.AttackJobDataStore.FromJob(pawn.CurJob);

            bool skipManualUse = true;
            bool skipDangerous = pawn.IsColonistPlayerControlled && SSCore.Settings.SkipDangerousWeapons;
            bool skipEMP = (pawn.IsColonistPlayerControlled && SSCore.Settings.SkipEMPWeapons) || !empGood;

            bool swapped = WeaponAssingment.trySwapToMoreAccurateRangedWeapon(
                pawn, target, MiscUtils.shouldDrop(pawn, DroppingModeEnum.Combat, false), skipManualUse, skipDangerous, skipEMP);

            if (swapped && jobData.HasValue)
            {
                Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                jobData.Value.ApplyToJob(job);
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, false);
            }
        }
    }
}
