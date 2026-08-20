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
            CompInventory inventory = pawn.TryGetComp<CompInventory>();
            if (inventory == null)
            {
                return;
            }

            bool changed = false;
            foreach (ThingWithComps weapon in pawn.inventory.innerContainer.OfType<ThingWithComps>().Where(t => t.def.IsWeapon).ToList())
            {
                CompAmmoUser ammoUser = weapon.TryGetComp<CompAmmoUser>();
                if (ammoUser == null)
                {
                    continue;
                }
                // An empty magazine is not conditional on the ammo system: CompAmmoUser.Initialize
                // skips loading when !UseAmmo and leaves the gun unfireable either way. CE's own
                // generator fills it regardless (LoadWeaponWithRandAmmo's !UseAmmo branch), so a
                // squad would otherwise spawn with loaded primaries and empty sidearms.
                if (ammoUser.HasMagazine && ammoUser.CurMagCount <= 0)
                {
                    ammoUser.ResetAmmoCount();
                    changed = true;
                }
                // Spare ammo is what the ammo system gates.
                if (!Controller.settings.EnableAmmoSystem || !ammoUser.UseAmmo)
                {
                    continue;
                }
                AmmoDef ammoDef = ammoUser.CurrentAmmo ?? ammoUser.SelectedAmmo;
                if (ammoDef == null || inventory.AmmoCountOfDef(ammoDef) > 0)
                {
                    continue;
                }
                int magazines = MagazineCountFor(pawn);
                // MagSizeOverride is CE's "rounds per magazine for generation" knob — one-shot
                // launchers set it because their MagSize is 1.
                int perMagazine = Math.Max(1, ammoUser.MagSizeOverride > 0 ? ammoUser.MagSizeOverride
                                            : ammoUser.HasMagazine ? ammoUser.MagSize : 10);
                Thing ammo = ThingMaker.MakeThing(ammoDef);
                ammo.stackCount = magazines * perMagazine;
                if (inventory.CanFitInInventory(ammo, out int fitCount) && fitCount > 0)
                {
                    if (fitCount < ammo.stackCount)
                    {
                        // Whole magazines only, as CE's own TryGenerateAmmoFor does.
                        ammo.stackCount = fitCount - (fitCount % perMagazine);
                    }
                    if (ammo.stackCount > 0)
                    {
                        inventory.container.TryAdd(ammo, true);
                        changed = true;
                    }
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
