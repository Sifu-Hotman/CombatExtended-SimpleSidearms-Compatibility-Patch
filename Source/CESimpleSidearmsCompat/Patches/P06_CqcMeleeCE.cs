using CombatExtended;
using HarmonyLib;
using PeteTimesSix.SimpleSidearms.Utilities;
using Verse;

namespace CESimpleSidearmsCompat.Patches
{
    /// <summary>
    /// Axis 6: SS's CQC reaction (victim auto-draws a melee weapon when melee-attacked) hooks
    /// vanilla Verb_MeleeAttack.TryCastShot. CE's Verb_MeleeAttackCE overrides that method,
    /// so the SS hook never fires. Mirror SS's postfix on the CE override.
    /// </summary>
    [HarmonyPatch(typeof(Verb_MeleeAttackCE), "TryCastShot")]
    public static class Verb_MeleeAttackCE_TryCastShot_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Verb_MeleeAttackCE __instance)
        {
            Thing targetThing = __instance.CurrentTarget.Thing;
            Pawn caster = __instance.CasterPawn;
            if (caster == null || !(targetThing is Pawn target))
            {
                return;
            }
            if (target.Dead || !target.RaceProps.Humanlike || target.equipment == null)
            {
                return;
            }
            WeaponAssingment.doCQC(target, caster);
        }
    }
}
