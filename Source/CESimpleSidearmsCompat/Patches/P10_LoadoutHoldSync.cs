using CombatExtended;
using HarmonyLib;
using Verse;

namespace CESimpleSidearmsCompat.Patches
{
    /// <summary>
    /// Axis 10: CE loadout enforcement (JobGiver_UpdateLoadout → GetExcessThing /
    /// GetExcessEquipment) drops inventory items that aren't in the pawn's CE loadout or
    /// hold records — which includes SS-remembered sidearms, causing drop/retrieve churn.
    ///
    /// The exemption is answered where CE asks the question, and nothing is written back.
    /// CE's hold-tracker is shared state: HoldRecord has no owner field and
    /// Notify_HoldTrackerItem merges by ThingDef, so a record we created and one the
    /// player created with CE's own "hold N of these" command are the same object.
    /// Editing it from here corrupted player-set counts, fought CE's clear-forced-hold
    /// button, and — because CE deletes picked-up records whose def has left the
    /// inventory container — churned a create/delete cycle for equipped weapons.
    /// </summary>
    [HarmonyPatch(typeof(Utility_HoldTracker), nameof(Utility_HoldTracker.GetExcessThing))]
    public static class Utility_HoldTracker_GetExcessThing_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref Thing dropThing, ref int dropCount, ref bool __result)
        {
            if (!__result || dropThing == null || !dropThing.def.IsWeapon)
            {
                return;
            }
            if (CompatUtil.SSRemembers(pawn, dropThing))
            {
                __result = false;
                dropThing = null;
                dropCount = 0;
            }
        }
    }

    [HarmonyPatch(typeof(Utility_HoldTracker), nameof(Utility_HoldTracker.GetExcessEquipment))]
    public static class Utility_HoldTracker_GetExcessEquipment_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref ThingWithComps dropEquipment, ref bool __result)
        {
            if (!__result || dropEquipment == null)
            {
                return;
            }
            if (CompatUtil.SSRemembers(pawn, dropEquipment))
            {
                __result = false;
                dropEquipment = null;
            }
        }
    }
}
