using System.Collections.Generic;
using HarmonyLib;
using PeteTimesSix.SimpleSidearms;
using PeteTimesSix.SimpleSidearms.Utilities;
using Verse;

namespace CESimpleSidearmsCompat.Patches
{
    /// <summary>
    /// Axis 3: SS's best-ranged-weapon selection is ammo-blind and can hand a pawn an empty
    /// gun. If the original pick has no usable ammo, ask SS the same question again with the
    /// dry weapons hidden — so SS's own filter chain picks the replacement, including the
    /// third-party rules it applies for other mods (VFE off-hand shields, Tacticowl
    /// dual-wield) and whatever it adds next. Re-deriving that chain here meant re-deriving
    /// it wrong: it silently missed both of those.
    /// </summary>
    [HarmonyPatch(typeof(GettersFilters), nameof(GettersFilters.findBestRangedWeapon))]
    public static class GettersFilters_findBestRangedWeapon_Patch
    {
        /// <summary>Non-null only for the duration of one re-run, for one pawn.</summary>
        internal static Pawn HidingDryWeaponsFor;

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, LocalTargetInfo? target, bool skipManualUse, bool skipDangerous, bool skipEMP, bool includeEquipped,
                                   ref (ThingWithComps weapon, float dps, float averageSpeed) __result)
        {
            if (HidingDryWeaponsFor != null)
            {
                return; // this call IS the re-run
            }
            if (__result.weapon == null || CompatUtil.WeaponHasAmmoFor(pawn, __result.weapon))
            {
                return;
            }

            HidingDryWeaponsFor = pawn;
            try
            {
                __result = GettersFilters.findBestRangedWeapon(pawn, target, skipManualUse, skipDangerous, skipEMP, includeEquipped);
            }
            finally
            {
                HidingDryWeaponsFor = null;
            }
        }
    }

    /// <summary>
    /// The one seam the re-run needs: while it is in flight, the pawn's carried-weapon list
    /// does not include guns with no usable ammo.
    /// </summary>
    [HarmonyPatch(typeof(Extensions), nameof(Extensions.GetCarriedWeapons))]
    public static class Extensions_GetCarriedWeapons_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, List<ThingWithComps> __result)
        {
            if (__result == null || GettersFilters_findBestRangedWeapon_Patch.HidingDryWeaponsFor != pawn)
            {
                return;
            }
            __result.RemoveAll(w => w != null && w.def.IsRangedWeapon && !CompatUtil.WeaponHasAmmoFor(pawn, w));
        }
    }
}
