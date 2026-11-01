using System.Reflection;

namespace DungeonSettlersDelvers.Core;

// The Extended Hotbar's two extra item slots, found softly: Core looks in the hotbar's assembly for
// a public static `string[] ExtraItemKeys(string unitGuid)` (or with a System.Guid) and calls it; without the mod or the
// method only Q counts. No hard reference and no reach into the hotbar's internals.
internal static class GroupAiBandageSlotReader
{
    internal const string MethodName = "ExtraItemKeys";

    // The hotbar ships as DungeonSettlersHotbar.* (BepInEx) or DungeonSettlers10Slots (its source project).
    internal static bool IsHotbarAssembly(string name)
        => name != null && (name.StartsWith("DungeonSettlersHotbar", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("DungeonSettlers10Slots", StringComparison.OrdinalIgnoreCase));

    internal static MethodInfo Find(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies ?? Enumerable.Empty<Assembly>())
        {
            string name;
            try { name = assembly.GetName().Name; }
            catch { continue; }
            if (!IsHotbarAssembly(name)) continue;
            var found = FindIn(assembly);
            if (found != null) return found;
        }
        return null;
    }

    // A public static ExtraItemKeys that returns string[]: the string overload first, the System.Guid one only without it
    // (the hotbar's ExtendedHotbarApi has both; GetMethods has no fixed order, so each is asked for by its parameter).
    internal static MethodInfo FindIn(Assembly assembly)
    {
        Type[] types;
        try { types = assembly.GetExportedTypes(); }
        catch { return null; }
        foreach (var parameter in new[] { typeof(string), typeof(Guid) })
            foreach (var type in types)
            {
                MethodInfo method;
                try { method = type.GetMethod(MethodName, BindingFlags.Public | BindingFlags.Static, null, new[] { parameter }, null); }
                catch { continue; }
                if (Fits(method)) return method;
            }
        return null;
    }

    internal static bool Fits(MethodInfo method)
    {
        if (method == null || method.Name != MethodName || method.ReturnType != typeof(string[])) return false;
        var parameters = method.GetParameters();
        return parameters.Length == 1 && (parameters[0].ParameterType == typeof(string) || parameters[0].ParameterType == typeof(Guid));
    }

    // The two extra keys for the unit, never null; a failing call reads as empty slots.
    internal static string[] Read(MethodInfo method, string unitGuid)
    {
        var none = new string[2];
        if (method == null || string.IsNullOrEmpty(unitGuid)) return none;
        try
        {
            object argument = unitGuid;
            if (method.GetParameters()[0].ParameterType == typeof(Guid))
            {
                if (!Guid.TryParse(unitGuid, out var guid)) return none;
                argument = guid;
            }
            var keys = method.Invoke(null, new[] { argument }) as string[];
            for (var i = 0; keys != null && i < none.Length && i < keys.Length; i++)
                none[i] = string.IsNullOrEmpty(keys[i]) ? null : keys[i];
            return none;
        }
        catch { return none; }
    }
}
