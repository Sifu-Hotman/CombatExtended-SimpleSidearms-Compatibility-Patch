using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PeteTimesSix.SimpleSidearms;
using PeteTimesSix.SimpleSidearms.Utilities;
using Verse;
using SSCore = PeteTimesSix.SimpleSidearms.SimpleSidearms;

namespace CESimpleSidearmsCompat.Patches
{
    /// <summary>
    /// Axis 3: SS's best-ranged-weapon selection is ammo-blind and can hand a pawn an empty
    /// gun. If the original pick has no usable ammo, redo the selection over loaded weapons
    /// only (preserving the fall-back to next-best, unlike simply nulling the result).
    /// </summary>
    [HarmonyPatch(typeof(GettersFilters), nameof(GettersFilters.findBestRangedWeapon))]
    public static class GettersFilters_findBestRangedWeapon_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, LocalTargetInfo? target, bool skipManualUse, bool skipDangerous, bool skipEMP, bool includeEquipped,
                                   ref (ThingWithComps weapon, float dps, float averageSpeed) __result)
        {
            if (__result.weapon == null || CompatUtil.WeaponHasAmmoFor(pawn, __result.weapon))
            {
                return;
            }

            // Mirror the original filter chain, plus the ammo requirement.
            List<ThingWithComps> options = pawn.GetCarriedWeapons(includeEquipped)
                .Where(t => t.def.IsRangedWeapon)
                .Where(t => CompatUtil.WeaponHasAmmoFor(pawn, t))
                .Where(t => SSCore.Settings.AllowBlockedWeaponUse || StatCalculator.canUseSidearmInstance(t, pawn, out _))
                .Where(t => !pawn.IsColonistPlayerControlled || !GettersFilters.isManualUse(t))
                .Where(t => !skipManualUse || !GettersFilters.isManualUse(t))
                .Where(t => !skipDangerous || !GettersFilters.isDangerousWeapon(t))
                .Where(t => !skipEMP || !GettersFilters.isEMPWeapon(t))
                .ToList();

            if (options.Count == 0)
            {
                __result = (null, -1f, __result.averageSpeed);
                return;
            }

            float averageSpeed = GettersFilters.AverageSpeedRanged(options);
            (ThingWithComps weapon, float dps, float averageSpeed) best = (null, -1f, averageSpeed);

            if (target.HasValue)
            {
                float targetDistance = target.Value.Cell.DistanceTo(pawn.Position);
                foreach (ThingWithComps candidate in options)
                {
                    Verb primaryVerb = candidate.GetComp<CompEquippable>()?.PrimaryVerb;
                    VerbProperties verbProps = primaryVerb?.verbProps;
                    if (verbProps == null)
                    {
                        continue;
                    }
                    if (targetDistance < verbProps.EffectiveMinRange(target.Value, pawn)
                        || targetDistance > verbProps.AdjustedRange(primaryVerb, pawn))
                    {
                        continue;
                    }
                    float dps = StatCalculator.RangedDPS(candidate, SSCore.Settings.SpeedSelectionBiasRanged, averageSpeed, targetDistance);
                    if (dps > best.dps)
                    {
                        best = (candidate, dps, averageSpeed);
                    }
                }
            }
            else
            {
                foreach (ThingWithComps candidate in options)
                {
                    float dps = StatCalculator.RangedDPSAverage(candidate, SSCore.Settings.SpeedSelectionBiasRanged, averageSpeed);
                    if (dps > best.dps)
                    {
                        best = (candidate, dps, averageSpeed);
                    }
                }
            }

            __result = best;
        }
    }
}
