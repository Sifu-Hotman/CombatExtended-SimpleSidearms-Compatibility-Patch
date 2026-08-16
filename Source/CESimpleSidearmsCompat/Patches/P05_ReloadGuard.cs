using CombatExtended;
using HarmonyLib;
using PeteTimesSix.SimpleSidearms.Utilities;
using Verse;
using Verse.AI;

namespace CESimpleSidearmsCompat.Patches
{
    /// <summary>
    /// Axis 5: SS's automatic weapon swapping can fire mid CE reload, cancelling the reload
    /// job and wasting the attempt. Automatic preference swaps are suppressed during a
    /// reload; explicit/specific swaps end the reload cleanly first.
    /// </summary>
    [HarmonyPatch(typeof(WeaponAssingment), nameof(WeaponAssingment.equipBestWeaponFromInventoryByPreference))]
    public static class WeaponAssingment_equipBestByPreference_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn pawn)
        {
            return pawn?.CurJobDef != CE_JobDefOf.ReloadWeapon;
        }
    }

    [HarmonyPatch(typeof(WeaponAssingment), nameof(WeaponAssingment.equipSpecificWeapon))]
    public static class WeaponAssingment_equipSpecificWeapon_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Pawn pawn)
        {
            if (pawn?.CurJobDef == CE_JobDefOf.ReloadWeapon)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false, canReturnToPool: true);
            }
        }
    }
}
