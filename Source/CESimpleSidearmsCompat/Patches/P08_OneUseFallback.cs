using CombatExtended;
using HarmonyLib;
using PeteTimesSix.SimpleSidearms;
using PeteTimesSix.SimpleSidearms.Utilities;
using static PeteTimesSix.SimpleSidearms.Utilities.Enums;
using Verse;

namespace CESimpleSidearmsCompat.Patches
{
    /// <summary>
    /// Axis 8: SS re-equips after a one-use weapon is consumed by hooking vanilla
    /// Verb_ShootOneUse.SelfConsume; CE's Verb_ShootCEOneUse is a separate class, so that
    /// hook never fires. CE natively re-equips a same-def weapon (and otherwise calls
    /// SwitchToNextViableWeapon, which axis 9 routes through SS preferences); this fallback
    /// covers the remaining case where the pawn ends up empty-handed.
    /// </summary>
    [HarmonyPatch(typeof(Verb_ShootCEOneUse), "SelfConsume")]
    public static class Verb_ShootCEOneUse_SelfConsume_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Verb_ShootCEOneUse __instance)
        {
            Pawn pawn = __instance.ShooterPawn;
            if (pawn == null || pawn.Dead || pawn.equipment == null)
            {
                return;
            }
            if (pawn.equipment.Primary != null || !pawn.IsValidSidearmsCarrierRightNow())
            {
                return;
            }
            WeaponAssingment.equipBestWeaponFromInventoryByPreference(pawn, DroppingModeEnum.UsedUp);
        }
    }
}
