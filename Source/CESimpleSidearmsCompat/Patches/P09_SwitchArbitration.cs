using System;
using CombatExtended;
using HarmonyLib;
using PeteTimesSix.SimpleSidearms;
using PeteTimesSix.SimpleSidearms.Utilities;
using static PeteTimesSix.SimpleSidearms.Utilities.Enums;
using Verse;

namespace CESimpleSidearmsCompat.Patches
{
    /// <summary>
    /// Axis 9: CE's CompInventory.SwitchToNextViableWeapon (fired on out-of-ammo, primary
    /// destroyed, one-use consumed, ...) picks a replacement by CE's own heuristic, ignoring
    /// SS preferences and remembered sidearms. For SS-managed pawns, try SS's
    /// preference-ordered switch first; fall back to CE's logic (incl. fists) if SS finds
    /// nothing. Specialized CE calls (AOE requests, predicated searches) pass through.
    /// </summary>
    [HarmonyPatch(typeof(CompInventory), nameof(CompInventory.SwitchToNextViableWeapon))]
    public static class CompInventory_SwitchToNextViableWeapon_Patch
    {
        private static bool inSSEquip;

        [HarmonyPrefix]
        public static bool Prefix(CompInventory __instance, bool useAOE, Func<ThingWithComps, CompAmmoUser, bool> predicate, ref bool __result)
        {
            if (inSSEquip || useAOE || predicate != null)
            {
                return true;
            }
            Pawn pawn = __instance.parentPawn;
            if (pawn == null || !pawn.IsValidSidearmsCarrierRightNow())
            {
                return true;
            }
            if (pawn.equipment?.Primary?.def.weaponTags?.Contains("NoSwitch") ?? false)
            {
                return true;
            }

            ThingWithComps before = pawn.equipment?.Primary;
            inSSEquip = true;
            try
            {
                // Blocked during CE reload jobs by the axis-5 guard, which then lets CE's
                // own picker run — intended interplay.
                WeaponAssingment.equipBestWeaponFromInventoryByPreference(pawn, DroppingModeEnum.Combat);
            }
            finally
            {
                inSSEquip = false;
            }
            ThingWithComps after = pawn.equipment?.Primary;
            if (after != null && after != before)
            {
                __result = true;
                return false; // SS handled the switch
            }
            return true; // let CE try (other weapons by its heuristic, or fists)
        }
    }
}
