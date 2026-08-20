using System;
using System.Collections.Generic;
using System.Reflection;
using CombatExtended.Compatibility;
using HarmonyLib;
using Verse;

namespace CESimpleSidearmsCompat
{
    public static class Bootstrap
    {
        public const string HarmonyId = "eebette.CESimpleSidearmsCompat";
        private const string LogPrefix = "[CE+SimpleSidearms] ";
        private static bool patched;

        private static bool DependenciesPresent =>
            ModsConfig.IsActive("CETeam.CombatExtended")
            && ModsConfig.IsActive("PeteTimesSix.SimpleSidearms");

        /// <summary>
        /// Applies the patch classes one at a time so a single broken patch target — the
        /// expected failure mode when CE or Simple Sidearms updates — costs only that one
        /// fix instead of the whole layer. Nothing is allowed to escape: this runs inside
        /// CE's compatibility long event, which does NOT guard the patches it invokes, so
        /// an exception here would take down every other mod's CE compat patches too.
        /// Matches CE's own convention: missing target degrades with a named error, never
        /// throws.
        /// </summary>
        public static void EnsurePatched()
        {
            if (patched)
            {
                return;
            }
            patched = true;

            if (!DependenciesPresent)
            {
                Log.Warning(LogPrefix + "Combat Extended or Simple Sidearms is not active; compatibility patches skipped.");
                return;
            }

            int applied = 0;
            var failures = new List<string>();
            try
            {
                var harmony = new Harmony(HarmonyId);
                foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(Bootstrap).Assembly))
                {
                    try
                    {
                        List<MethodInfo> methods = harmony.CreateClassProcessor(type).Patch();
                        if (methods != null && methods.Count > 0)
                        {
                            applied++;
                        }
                    }
                    catch (Exception e)
                    {
                        failures.Add(type.Name);
                        Log.Error($"{LogPrefix}Patch class {type.Name} could not be applied — that one fix is inactive, the others still work. This usually means CE or Simple Sidearms changed a patched member. {e}");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"{LogPrefix}Patching aborted: {e}");
                return;
            }

            if (failures.Count > 0)
            {
                Log.Warning($"{LogPrefix}Installed {applied} patch class(es); {failures.Count} failed ({string.Join(", ", failures)}).");
            }
            else
            {
                Log.Message($"{LogPrefix}Compatibility patches installed ({applied} patch classes).");
            }
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
