using System.Linq;
using CombatExtended;
using LudeonTK;
using PeteTimesSix.SimpleSidearms;
using PeteTimesSix.SimpleSidearms.Utilities;
using Verse;
using SSCore = PeteTimesSix.SimpleSidearms.SimpleSidearms;

namespace CESSCompatTestStaging
{
    /// <summary>
    /// Dev-mode probes for numbers SS never displays in its UI (DPS is internal
    /// selection state). Debug actions menu → "CE+SS Compat".
    /// </summary>
    public static class DebugActions
    {
        [DebugAction("CE+SS Compat", "Log carried weapon DPS", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogCarriedWeaponDps(Pawn pawn)
        {
            var weapons = pawn.GetCarriedWeapons(includeEquipped: true).Where(w => w.def.IsRangedWeapon).ToList();
            if (weapons.Count == 0)
            {
                Log.Message($"[CE+SS] {pawn.LabelShort}: no ranged weapons carried.");
                return;
            }
            float averageSpeed = GettersFilters.AverageSpeedRanged(weapons);
            Log.Message($"[CE+SS] {pawn.LabelShort}: averageSpeed={averageSpeed:F2}, speedBias={SSCore.Settings.SpeedSelectionBiasRanged:F2}");
            foreach (ThingWithComps weapon in weapons)
            {
                float dpsAvg = StatCalculator.RangedDPSAverage(weapon, SSCore.Settings.SpeedSelectionBiasRanged, averageSpeed);
                float speed = StatCalculator.RangedSpeed(weapon);
                CompAmmoUser ammoUser = weapon.TryGetComp<CompAmmoUser>();
                string ammoState = ammoUser == null ? "no CE ammo comp"
                                   : !ammoUser.UseAmmo ? "ammo system off"
                                   : $"mag {ammoUser.CurMagCount}/{ammoUser.MagSize}, hasAmmoOrMag={ammoUser.HasAmmoOrMagazine}";
                Log.Message($"[CE+SS]   {weapon.LabelCap} | dpsAvg={dpsAvg:F2} | cycle={speed:F2}s | {ammoState}");
            }
        }

        [DebugAction("CE+SS Compat", "Force-generate SS sidearm", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceGenerateSidearm(Pawn pawn)
        {
            bool got = TestStagingComponent.ForceRangedSidearm(pawn);
            Log.Message($"[CE+SS] {pawn.LabelShort}: ranged ammo-using sidearm present={got}. Inventory:");
            foreach (Thing thing in pawn.inventory.innerContainer)
            {
                CompAmmoUser ammoUser = (thing as ThingWithComps)?.TryGetComp<CompAmmoUser>();
                string mag = ammoUser == null ? "" : $" | mag {ammoUser.CurMagCount}/{ammoUser.MagSize}";
                Log.Message($"[CE+SS]   {thing.LabelCap} x{thing.stackCount}{mag}");
            }
        }

        [DebugAction("CE+SS Compat", "Log weapon classification", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogWeaponClassification(Pawn pawn)
        {
            foreach (ThingWithComps weapon in pawn.GetCarriedWeapons(includeEquipped: true))
            {
                CompAmmoUser ammoUser = weapon.TryGetComp<CompAmmoUser>();
                ThingDef projectile = ammoUser?.CurAmmoProjectile;
                string damage = projectile?.projectile?.damageDef?.defName ?? "n/a";
                Log.Message($"[CE+SS]   {weapon.LabelCap} | EMP={GettersFilters.isEMPWeapon(weapon)} | dangerous={GettersFilters.isDangerousWeapon(weapon)} | manualUse={GettersFilters.isManualUse(weapon)} | ceProjectileDamage={damage}");
            }
        }
    }
}
