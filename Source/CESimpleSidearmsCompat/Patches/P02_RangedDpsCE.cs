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
    /// <summary>
    /// Scoring runs per tick per warming-up pawn per carried weapon, so every stat read here
    /// uses RimWorld's own one-tick cache rather than a full StatWorker evaluation. Nothing
    /// these stats depend on — quality, attachments, damage — can change inside a tick.
    /// </summary>
    internal static class StatCache
    {
        internal const int Ticks = 1;
    }

    [HarmonyPatch(typeof(StatCalculator), nameof(StatCalculator.RangedSpeed))]
    public static class StatCalculator_RangedSpeed_Patch
    {
        private const int StatCacheTicks = StatCache.Ticks;

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
            float reloadTime = weapon.GetStatValue(CE_StatDefOf.ReloadTime, cacheStaleAfterTicks: StatCacheTicks);
            if (reloadTime <= 0f)
            {
                return;
            }
            // Live verb props, matching CEDps and SS's own RangedSpeed — def.Verbs[0] is the
            // static value and disagrees on weapons CE swaps verbs for (under-barrel launchers).
            float burst = Math.Max(1, weapon.GetComp<CompEquippable>()?.PrimaryVerb?.verbProps?.burstShotCount ?? 1);
            float burstsPerMag = Math.Max(1f, magSize / burst);
            __result += reloadTime / burstsPerMag;
        }
    }

    [HarmonyPatch(typeof(StatCalculator), nameof(StatCalculator.RangedDPS))]
    public static class StatCalculator_RangedDPS_Patch
    {
        /// <summary>CE caps the shooting-accuracy term here (Verb_LaunchProjectileCE.ShootingAccuracy).</summary>
        internal const float MaxShootingAccuracy = 4.5f;

        /// <summary>
        /// Stand-in range for the distance-free scoring path, which SS uses when no target is
        /// known. SS averaged the weapon's short/medium/long accuracy stats there; this plays
        /// the same role for CE weapons, which have those stats stripped.
        /// </summary>
        internal const float NoTargetReferenceDistance = 20f;

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
        /// Distance-dependent hit proxy from CE's accuracy stats, converted to lateral miss
        /// distance at range. Not CE's real ballistics — just enough distance falloff that SS
        /// ranks a shotgun above a sniper up close and the reverse at range, mirroring the
        /// role vanilla hit-chance plays in SS's formula.
        ///
        /// Sway is deliberately NOT summed into the spread as if it were degrees: CE's own
        /// SwayAmplitude is (4.5 - shooting accuracy) x SwayFactor, so the raw factor is a
        /// multiplier, not an angle. Adding it directly let sway account for ~90% of the term
        /// on a typical gun and made both the weapon's real spread and the shooter's skill
        /// nearly irrelevant to the ranking.
        /// </summary>
        internal static float CEHitFactor(ThingWithComps weapon, float distance)
        {
            float spreadDegrees = weapon.GetStatValue(CE_StatDefOf.ShotSpread, cacheStaleAfterTicks: StatCache.Ticks);
            float swayFactor = weapon.GetStatValue(CE_StatDefOf.SwayFactor, cacheStaleAfterTicks: StatCache.Ticks);
            Pawn carrier = CompatUtil.CarrierOf(weapon);
            float shootingAccuracy = carrier != null
                ? Mathf.Min(carrier.GetStatValue(StatDefOf.ShootingAccuracyPawn, cacheStaleAfterTicks: StatCache.Ticks), MaxShootingAccuracy)
                : MaxShootingAccuracy; // unknown shooter: score the weapon on its own spread
            float angularErrorDegrees = spreadDegrees + Mathf.Max(0f, MaxShootingAccuracy - shootingAccuracy) * swayFactor;
            float lateralMissCells = distance * angularErrorDegrees * 0.01745f;
            return Mathf.Clamp01(0.4f / Mathf.Max(0.04f, lateralMissCells));
        }

        internal static float CEDps(ThingWithComps weapon, CompAmmoUser ammoUser, VerbProperties atkProps, float speedBias, float averageSpeed)
        {
            ThingDef projectile = CompatUtil.CurrentProjectile(weapon, ammoUser) ?? atkProps.defaultProjectile;
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
            // Flat damage-per-cycle; both variants multiply in CEHitFactor.
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
            // SS's own no-target formula ends by weighting damage with the weapon's accuracy
            // stats, which for a CE gun resolve to the vanilla AccuracyBase fallback — i.e.
            // purely the quality factor. Dropping that term made an awful gun score identical
            // to a masterwork one, leaving carry order to decide. CE keeps quality (and
            // attachments, and damaged parts) in ShotSpread, so scoring the hit proxy at a
            // fixed reference range restores the signal without leaving CE's own model.
            __result = StatCalculator_RangedDPS_Patch.CEDps(weapon, ammoUser, atkProps, speedBias, averageSpeed)
                       * StatCalculator_RangedDPS_Patch.CEHitFactor(weapon, StatCalculator_RangedDPS_Patch.NoTargetReferenceDistance);
            return false;
        }
    }
}
