using System;
using System.Linq;
using CombatExtended;
using HarmonyLib;
using SimpleSidearms.rimworld;
using UnityEngine;
using Verse;

namespace CESimpleSidearmsCompat.Patches
{
    /// <summary>
    /// Axis 4: SS-generated NPC sidearms spawn with empty magazines and no spare ammo under
    /// CE. After SS generates, load every ammo-using inventory weapon and stock spare
    /// magazines, respecting CE inventory capacity.
    /// </summary>
    [HarmonyPatch(typeof(PawnSidearmsGenerator), nameof(PawnSidearmsGenerator.TryGenerateSidearmFor))]
    public static class PawnSidearmsGenerator_TryGenerateSidearmFor_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, bool __result)
        {
            if (!__result || pawn?.inventory?.innerContainer == null)
            {
                return;
            }
            if (!Controller.settings.EnableAmmoSystem)
            {
                return;
            }
            CompInventory inventory = pawn.TryGetComp<CompInventory>();
            if (inventory == null)
            {
                return;
            }

            bool changed = false;
            foreach (ThingWithComps weapon in pawn.inventory.innerContainer.OfType<ThingWithComps>().Where(t => t.def.IsWeapon).ToList())
            {
                CompAmmoUser ammoUser = weapon.TryGetComp<CompAmmoUser>();
                if (ammoUser == null || !ammoUser.UseAmmo)
                {
                    continue;
                }
                if (ammoUser.HasMagazine && ammoUser.CurMagCount <= 0)
                {
                    ammoUser.ResetAmmoCount(); // fill the magazine with default ammo
                    changed = true;
                }
                AmmoDef ammoDef = ammoUser.CurrentAmmo ?? ammoUser.SelectedAmmo;
                if (ammoDef == null || inventory.AmmoCountOfDef(ammoDef) > 0)
                {
                    continue;
                }
                int magazines = MagazineCountFor(pawn);
                int perMagazine = Math.Max(1, ammoUser.HasMagazine ? ammoUser.MagSize : 10);
                Thing ammo = ThingMaker.MakeThing(ammoDef);
                ammo.stackCount = magazines * perMagazine;
                if (inventory.CanFitInInventory(ammo, out int fitCount) && fitCount > 0)
                {
                    ammo.stackCount = Math.Min(ammo.stackCount, fitCount);
                    inventory.container.TryAdd(ammo, true);
                    changed = true;
                }
            }
            if (changed)
            {
                inventory.UpdateInventory();
            }
        }

        private static int MagazineCountFor(Pawn pawn)
        {
            // Prefer CE's own per-kind sidearm loadout config when present.
            SidearmOption option = pawn.kindDef?.GetModExtension<LoadoutPropertiesExtension>()?
                                   .sidearms?.FirstOrDefault(s => s.magazineCount.TrueMax > 0f);
            if (option != null)
            {
                int count = Mathf.RoundToInt(option.magazineCount.RandomInRange);
                if (count > 0)
                {
                    return count;
                }
            }
            return Rand.RangeInclusive(1, 3);
        }
    }
}
