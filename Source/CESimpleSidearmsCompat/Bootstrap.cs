using CombatExtended.Compatibility;
using HarmonyLib;
using Verse;

namespace CESimpleSidearmsCompat
{
    public static class Bootstrap
    {
        public const string HarmonyId = "eebette.CESimpleSidearmsCompat";
        private static bool patched;

        public static void EnsurePatched()
        {
            if (patched)
            {
                return;
            }
            patched = true;
            new Harmony(HarmonyId).PatchAll(typeof(Bootstrap).Assembly);
            Log.Message("[CE+SimpleSidearms] Compatibility patches installed.");
        }
    }

    // Primary entry point: discovered and installed by CE's own compatibility scanner.
    public class CECompatPatch : IPatch
    {
        public bool CanInstall()
        {
            return ModsConfig.IsActive("PeteTimesSix.SimpleSidearms");
        }

        public void Install()
        {
            Bootstrap.EnsurePatched();
        }
    }

    // Fallback in case CE's scanner changes behavior; EnsurePatched is idempotent.
    [StaticConstructorOnStartup]
    public static class BootstrapFallback
    {
        static BootstrapFallback()
        {
            Bootstrap.EnsurePatched();
        }
    }
}
