using System.Linq;
using CombatExtended;
using PeteTimesSix.SimpleSidearms;
using SimpleSidearms.rimworld;
using Verse;

namespace CESimpleSidearmsCompat
{
    public static class CompatUtil
    {
        /// <summary>A weapon whose stats follow CE's model (patched verb and/or ammo comp).</summary>
        public static bool IsCEGun(ThingWithComps weapon, out CompAmmoUser ammoUser)
        {
            ammoUser = weapon?.TryGetComp<CompAmmoUser>();
            if (ammoUser != null)
            {
                return true;
            }
            return weapon?.def.Verbs?.FirstOrDefault() is VerbPropertiesCE;
        }

        /// <summary>
        /// True when the weapon can actually fire: no CE ammo comp, ammo system disabled,
        /// rounds in the magazine, or compatible ammo in the carrier's inventory.
        /// </summary>
        public static bool WeaponHasAmmoFor(Pawn carrier, ThingWithComps weapon)
        {
            CompAmmoUser ammoUser = weapon?.TryGetComp<CompAmmoUser>();
            if (ammoUser == null || !ammoUser.UseAmmo)
            {
                return true;
            }
            if (ammoUser.CurMagCount > 0)
            {
                return true;
            }
            CompInventory inventory = carrier?.TryGetComp<CompInventory>();
            if (inventory == null)
            {
                // No carrier context; fall back to CE's own holder-based check.
                return ammoUser.HasAmmoOrMagazine;
            }
            var ammoTypes = ammoUser.Props?.ammoSet?.ammoTypes;
            if (ammoTypes == null)
            {
                return ammoUser.HasAmmoOrMagazine;
            }
            return ammoTypes.Any(link => link?.ammo != null && inventory.AmmoCountOfDef(link.ammo) > 0);
        }

        /// <summary>Projectile the weapon would currently fire (loaded/selected CE ammo, else verb default).</summary>
        public static ThingDef CurrentProjectile(ThingWithComps weapon)
        {
            CompAmmoUser ammoUser = weapon?.TryGetComp<CompAmmoUser>();
            if (ammoUser != null)
            {
                return ammoUser.CurAmmoProjectile;
            }
            return weapon?.def.Verbs?.FirstOrDefault()?.defaultProjectile;
        }

        /// <summary>Does Simple Sidearms remember this weapon (def + stuff) for this pawn?</summary>
        public static bool SSRemembers(Pawn pawn, Thing weapon)
        {
            if (pawn == null || weapon == null)
            {
                return false;
            }
            CompSidearmMemory memory = CompSidearmMemory.GetMemoryCompForPawn(pawn, false);
            if (memory?.RememberedWeapons == null)
            {
                return false;
            }
            ThingDefStuffDefPair pair = new ThingDefStuffDefPair(weapon.def, weapon.Stuff);
            return memory.RememberedWeapons.Contains(pair);
        }
    }
}
