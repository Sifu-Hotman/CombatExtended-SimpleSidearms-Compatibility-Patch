using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using HarmonyLib;
using SimpleSidearms.rimworld;
using Verse;

namespace CESimpleSidearmsCompat.Patches
{
    /// <summary>
    /// Axis 10: CE loadout enforcement (JobGiver_UpdateLoadout → GetExcessThing /
    /// GetExcessEquipment) drops inventory items that aren't in the pawn's CE loadout or
    /// hold records — which includes SS-remembered sidearms, causing drop/retrieve churn.
    ///
    /// Primary fix: mirror SS sidearm memory into CE's HoldRecords (CE's own drop
    /// exemption). Guards on the two excess-getters self-heal saves made before this mod
    /// was added.
    /// </summary>
    public static class HoldSync
    {
        public static void EnsureHeld(Pawn pawn, Thing weapon)
        {
            if (pawn == null || weapon == null || !pawn.IsColonist)
            {
                return;
            }
            Loadout loadout = pawn.GetLoadout();
            if (loadout == null || loadout.defaultLoadout)
            {
                return; // default loadout never drops anything
            }
            pawn.Notify_HoldTrackerItem(weapon, 1);
            HoldRecord record = LoadoutManager.GetHoldRecords(pawn)?.FirstOrDefault(r => r.thingDef == weapon.def);
            if (record != null)
            {
                // Weapon is already carried; without this, the record would be purged as a
                // stale "never picked up" entry after a day.
                record.pickedUp = true;
            }
        }

        public static void SyncForget(Pawn pawn, ThingDefStuffDefPair weaponMemory, CompSidearmMemory memory)
        {
            if (pawn == null || weaponMemory.thing == null)
            {
                return;
            }
            List<HoldRecord> records = LoadoutManager.GetHoldRecords(pawn);
            HoldRecord record = records?.FirstOrDefault(r => r.thingDef == weaponMemory.thing);
            if (record == null)
            {
                return;
            }
            int stillRemembered = memory?.RememberedWeapons?.Count(p => p.thing == weaponMemory.thing) ?? 0;
            if (stillRemembered <= 0)
            {
                records.Remove(record);
            }
            else
            {
                record.count = stillRemembered;
            }
        }
    }

    [HarmonyPatch(typeof(CompSidearmMemory), nameof(CompSidearmMemory.InformOfAddedSidearm))]
    public static class CompSidearmMemory_InformOfAddedSidearm_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CompSidearmMemory __instance, Thing weapon)
        {
            HoldSync.EnsureHeld(__instance.Owner, weapon);
        }
    }

    [HarmonyPatch(typeof(CompSidearmMemory), nameof(CompSidearmMemory.ForgetSidearmMemory))]
    public static class CompSidearmMemory_ForgetSidearmMemory_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CompSidearmMemory __instance, ThingDefStuffDefPair weaponMemory)
        {
            HoldSync.SyncForget(__instance.Owner, weaponMemory, __instance);
        }
    }

    // Guard: pre-existing saves whose remembered sidearms have no hold record yet.
    // Blocks the drop once and registers the record so the next cycle proceeds normally.
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
                HoldSync.EnsureHeld(pawn, dropThing);
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
                HoldSync.EnsureHeld(pawn, dropEquipment);
                __result = false;
                dropEquipment = null;
            }
        }
    }
}
