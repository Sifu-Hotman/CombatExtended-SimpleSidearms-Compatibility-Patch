using System;
using HarmonyLib;
using Verse;

namespace CESimpleSidearmsCompat
{
    /// <summary>
    /// Failure doctrine for every patch class here - so a broken assumption turns a feature off,
    /// never crashes:
    ///
    /// 1. Attribute pins - an exact target signature; a moved/renamed target won't bind.
    /// 2. Prepare guards (Require/RequireType) - confirm the target + depended-on types exist,
    ///    else log the gameplay consequence and skip the class.
    /// 3. Thin-outer/NoInlining-inner split - the outer try/catch keeps a throw out of the game
    ///    (Log.ErrorOnce) and falls back to upstream behavior.
    /// </summary>
    internal static class PatchGuard
    {
        internal const string LogPrefix = "[CE+SimpleSidearms] ";

        /// <summary>
        /// Shared by every Prepare: check if target method exists.
        /// </summary>
        internal static bool Require(Type type, string method, Type[] args, string consequence)
        {
            if (AccessTools.Method(type, method, args) != null)
            {
                return true;
            }
            Log.Error($"{LogPrefix}{type.Name}.{method} not found - {consequence} "
                      + "The mod that declares it probably moved it.");
            return false;
        }

        /// <summary>Prepare guard for a TYPE a patch body depends on beyond its target.</summary>
        internal static bool RequireType(string fullName, string consequence)
        {
            if (AccessTools.TypeByName(fullName) != null)
            {
                return true;
            }
            Log.Error($"{LogPrefix}{fullName} not found - {consequence} "
                      + "The mod that declares it probably moved it.");
            return false;
        }
    }

    /// <summary>
    /// Hashes an upstream method's IL so a shape change we depend on beyond what our anchors see
    /// (a reshaped body a transpiler would mis-edit) becomes a loud re-verify error at load, not
    /// silent wrong behavior. To bake a hash: set it to "" - Verify then logs the computed value
    /// ("bake me") - run once, paste it in.
    /// </summary>
    internal static class UpstreamFingerprint
    {
        // Baked against SS 1.4.6.0 - re-harvest on upstream updates.
        internal const string StanceTickHash = "d23100c5cf874336";

        // Baked against SS 1.4.6.0 - re-harvest on upstream updates.
        internal const string MeleeDpsBiasedHash = "d2151af4dd9188ca";

        internal static void Verify(Type type, string method, string expected, string protects)
        {
            try
            {
                var mb = AccessTools.Method(type, method);
                if (mb == null)
                {
                    Log.Error($"{PatchGuard.LogPrefix}{type.Name}.{method} not found - {protects} "
                              + "cannot be verified against upstream.");
                    return;
                }
                ulong hash = 14695981039346656037UL; // FNV-1a
                foreach (var instruction in HarmonyLib.PatchProcessor.GetOriginalInstructions(mb))
                {
                    string token = instruction.opcode.Name + OperandText.Render(instruction.operand);
                    foreach (char c in token)
                    {
                        hash = (hash ^ c) * 1099511628211UL;
                    }
                }
                string computed = hash.ToString("x16");
                if (string.IsNullOrEmpty(expected))
                {
                    Log.Message($"{PatchGuard.LogPrefix}FINGERPRINT {type.Name}.{method} = {computed} (bake me)");
                    return;
                }
                if (computed != expected)
                {
                    Log.Error($"{PatchGuard.LogPrefix}{type.Name}.{method} changed shape upstream "
                              + $"(fingerprint {computed}, expected {expected}) - re-verify {protects}.");
                }
            }
            catch (Exception e)
            {
                Log.Warning($"{PatchGuard.LogPrefix}Fingerprint check for {type.Name}.{method} failed to run: " + e.Message);
            }
        }
    }

    /// <summary>Runtime-neutral operand text for the fingerprint hash.</summary>
    internal static class OperandText
    {
        internal static string Render(object operand)
        {
            // Mono and CoreCLR print members, locals and byrefs differently.
            switch (operand)
            {
                case null:
                    return "";
                case System.Reflection.MethodBase m:
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append(Type(m.DeclaringType)).Append("::").Append(m.Name);
                    if (m.IsGenericMethod)
                    {
                        sb.Append('<');
                        foreach (var a in m.GetGenericArguments()) sb.Append(Type(a)).Append(',');
                        sb.Append('>');
                    }
                    sb.Append('(');
                    foreach (var prm in m.GetParameters()) sb.Append(Type(prm.ParameterType)).Append(',');
                    sb.Append(')');
                    if (m is System.Reflection.MethodInfo mi) sb.Append(':').Append(Type(mi.ReturnType));
                    return sb.ToString();
                }
                case System.Reflection.FieldInfo f:
                    return Type(f.DeclaringType) + "::" + f.Name + ":" + Type(f.FieldType);
                case Type t:
                    return Type(t);
                case System.Reflection.Emit.LocalBuilder l:
                    return "loc" + l.LocalIndex + ":" + Type(l.LocalType);
                case System.Reflection.Emit.Label lab:
                    return "L" + lab.GetHashCode();
                case System.Reflection.Emit.Label[] labs:
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var lab in labs) sb.Append('L').Append(lab.GetHashCode()).Append(',');
                    return sb.ToString();
                }
                case string str:
                    return "\"" + str + "\"";
                case IFormattable formattable:
                    return formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
                default:
                    return operand.GetType().Name;
            }
        }

        internal static string Type(Type t)
        {
            if (t == null) return "";
            if (t.IsByRef) return Type(t.GetElementType()) + "&";
            if (t.IsPointer) return Type(t.GetElementType()) + "*";
            if (t.IsArray) return Type(t.GetElementType()) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
            if (t.IsGenericParameter) return "!" + t.GenericParameterPosition;
            if (t.IsGenericType && !t.IsGenericTypeDefinition)
            {
                var sb = new System.Text.StringBuilder(Type(t.GetGenericTypeDefinition())).Append('[');
                foreach (var a in t.GetGenericArguments()) sb.Append(Type(a)).Append(',');
                return sb.Append(']').ToString();
            }
            return t.FullName ?? t.Name;
        }
    }

    /// <summary>
    /// SS enum values.
    /// </summary>
    internal static class SSEnums
    {
        internal static readonly bool Resolved;
        internal static readonly PeteTimesSix.SimpleSidearms.Utilities.Enums.DroppingModeEnum Combat;
        internal static readonly PeteTimesSix.SimpleSidearms.Utilities.Enums.DroppingModeEnum UsedUp;
        internal static readonly PeteTimesSix.SimpleSidearms.Utilities.Enums.PrimaryWeaponMode Melee;

        static SSEnums()
        {
            Resolved = TryResolve("Combat", out Combat)
                       & TryResolve("UsedUp", out UsedUp)
                       & TryResolve("Melee", out Melee);
        }

        internal static bool Require(string consequence)
        {
            if (Resolved)
            {
                return true;
            }
            Log.Error(PatchGuard.LogPrefix + "A Simple Sidearms enum member could not be resolved "
                      + $"by name - {consequence}");
            return false;
        }

        private static bool TryResolve<T>(string name, out T value) where T : struct
        {
            if (Enum.TryParse(name, out value))
            {
                return true;
            }
            Log.Error($"{PatchGuard.LogPrefix}Simple Sidearms enum {typeof(T).Name}.{name} no longer exists.");
            return false;
        }
    }
}
