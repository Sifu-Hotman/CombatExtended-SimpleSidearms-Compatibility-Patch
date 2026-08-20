using CombatExtended;
using HarmonyLib;
using PeteTimesSix.SimpleSidearms.Utilities;
using RimWorld;
using SimpleSidearms.rimworld;
using Verse;

namespace CESimpleSidearmsCompat.Patches
{
    /// <summary>
    /// Axis 1: SS pickup checks only weight (already CE-aware via CE's MassUtility.Capacity patch).
    /// Adds the missing CE bulk check so pawns can't grab sidearms past their CarryBulk.
    /// </summary>
    [HarmonyPatch(typeof(StatCalculator), nameof(StatCalculator.CanPickupSidearmType))]
    public static class StatCalculator_CanPickupSidearmType_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ThingDefStuffDefPair sidearmType, Pawn pawn, ref string errString, ref bool __result)
        {
            if (!__result || pawn == null || sidearmType.thing == null)
            {
                return;
            }
            CompInventory inventory = pawn.TryGetComp<CompInventory>();
            if (inventory == null)
            {
                return;
            }
            float bulk = sidearmType.thing.GetStatValueAbstract(CE_StatDefOf.Bulk, sidearmType.stuff);
            if (bulk <= 0f)
            {
                return;
            }
            // currentBulk is CE's cached figure, kept fresh on every inventory change;
            // capacityBulk is a live CarryBulk stat read. Still far cheaper than the
            // GetAvailableBulk(true) full recount, which matters because SS calls this
            // inside a filter over every valid sidearm pair at pawn generation.
            if (bulk > inventory.capacityBulk - inventory.currentBulk)
            {
                errString = "SidearmPickupFail_NoFreeSpace".Translate();
                __result = false;
            }
        }
    }
}
