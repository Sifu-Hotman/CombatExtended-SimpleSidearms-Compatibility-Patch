using System;
using System.Linq;
using CombatExtended;
using HarmonyLib;
using PeteTimesSix.SimpleSidearms.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace CESimpleSidearmsCompat.Patches
{
    /// <summary>
    /// Axis 2: SS scores ranged weapons with vanilla verb stats, which are meaningless on CE
    /// weapons (zeroed accuracy, ammo-driven damage, reload downtime). These patches make
    /// SS's DPS ranking use CE's stat model while preserving SS's speed-bias semantics.
    /// </summary>
    [HarmonyPatch(typeof(StatCalculator), nameof(StatCalculator.RangedSpeed))]
    public static class StatCalculator_RangedSpeed_Patch
    {
        // Fold reload downtime into the cycle time so slow-reloading weapons rank lower.
        // Also feeds SS's AverageSpeedRanged, keeping the bias baseline consistent.
        [HarmonyPostfix]
        public static void Postfix(ThingWithComps weapon, ref float __result)
        {
            CompAmmoUser ammoUser = weapon?.TryGetComp<CompAmmoUser>();
            if (ammoUser == null)
            {
                return;
            }
            int magSize = ammoUser.MagSize;
            if (magSize <= 0)
            {
                return;
            }
            float reloadTime = weapon.GetStatValue(CE_StatDefOf.ReloadTime);
            if (reloadTime <= 0f)
            {
                return;
            }
            float burst = Math.Max(1, weapon.def.Verbs?.FirstOrDefault()?.burstShotCount ?? 1);
            float burstsPerMag = Math.Max(1f, magSize / burst);
            __result += reloadTime / burstsPerMag;
        }
    }

    [HarmonyPatch(typeof(StatCalculator), nameof(StatCalculator.RangedDPS))]
    public static class StatCalculator_RangedDPS_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ThingWithComps weapon, float speedBias, float averageSpeed, float distance, ref float __result)
        {
            if (!CompatUtil.IsCEGun(weapon, out CompAmmoUser ammoUser))
            {
                return true;
            }
            VerbProperties atkProps = weapon.GetComp<CompEquippable>()?.PrimaryVerb?.verbProps;
            if (atkProps == null)
            {
                __result = 0f;
                return false;
            }
            // Mirrors SS's own (quirky, squared-vs-unsquared) range gate so relative ordering
            // stays consistent with what SS's callers expect.
            if (atkProps.range * atkProps.range < distance || atkProps.minRange * atkProps.minRange > distance)
            {
                __result = -1f;
                return false;
            }
            __result = CEDps(weapon, ammoUser, atkProps, speedBias, averageSpeed) * CEHitFactor(weapon, distance);
            return false;
        }

        /// <summary>
        /// Distance-dependent hit proxy from CE's accuracy stats (spread + sway = angular
        /// error, converted to lateral miss distance at range). Not CE's real ballistics —
        /// just enough distance falloff that SS ranks a shotgun above a sniper up close and
        /// the reverse at range, mirroring the role vanilla hit-chance plays in SS's formula.
        /// </summary>
        internal static float CEHitFactor(ThingWithComps weapon, float distance)
        {
            float spread = weapon.GetStatValue(CE_StatDefOf.ShotSpread);
            float sway = weapon.GetStatValue(CE_StatDefOf.SwayFactor);
            float lateralMissCells = distance * (spread + sway) * 0.01745f;
            return Mathf.Clamp01(0.4f / Mathf.Max(0.04f, lateralMissCells));
        }

        internal static float CEDps(ThingWithComps weapon, CompAmmoUser ammoUser, VerbProperties atkProps, float speedBias, float averageSpeed)
        {
            ThingDef projectile = CompatUtil.CurrentProjectile(weapon) ?? atkProps.defaultProjectile;
            float damage = projectile?.projectile?.GetDamageAmount(weapon) ?? 0f;
            int pellets = (projectile?.projectile as ProjectilePropertiesCE)?.pelletCount ?? 1;
            damage *= Math.Max(1, pellets);
            float burst = Math.Max(1, atkProps.burstShotCount);
            float speed = StatCalculator.RangedSpeed(weapon); // includes our reload amortization

            // Same speed-bias adjustment SS applies in its vanilla formulas.
            float diffFromAverage = (speed - averageSpeed) * (speedBias - 1f);
            speed += diffFromAverage;
            if (speed <= 0f)
            {
                return 0f;
            }
            // Flat damage-per-cycle; the distance-aware RangedDPS variant multiplies in
            // CEHitFactor, the distance-free RangedDPSAverage uses this as-is.
            return damage * burst / speed;
        }
    }

    [HarmonyPatch(typeof(StatCalculator), nameof(StatCalculator.RangedDPSAverage))]
    public static class StatCalculator_RangedDPSAverage_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ThingWithComps weapon, float speedBias, float averageSpeed, ref float __result)
        {
            if (!CompatUtil.IsCEGun(weapon, out CompAmmoUser ammoUser))
            {
                return true;
            }
            VerbProperties atkProps = weapon.GetComp<CompEquippable>()?.PrimaryVerb?.verbProps;
            if (atkProps == null)
            {
                __result = 0f;
                return false;
            }
            __result = StatCalculator_RangedDPS_Patch.CEDps(weapon, ammoUser, atkProps, speedBias, averageSpeed);
            return false;
        }
    }
}
