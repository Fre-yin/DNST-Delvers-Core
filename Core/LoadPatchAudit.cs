using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Reports non-Core Harmony owners on native load boundaries. Foreign patches
// are not rejected because another mod may have a valid compatibility layer,
// but every active owner is visible before destructive deserialization begins.
internal static class LoadPatchAudit
{
    private static readonly object Gate = new();
    private static readonly HashSet<string> Reported = new(StringComparer.Ordinal);

    internal static void Run(string coreOwner)
    {
        try
        {
            var inspected = 0;
            var foreign = 0;
            var compatible = 0;
            var coreFinalizers = 0;
            foreach (var target in Targets())
            {
                if (target.Method == null)
                {
                    ReportOnce("missing:" + target.Name,
                        "CORE_LOAD_PATCH_AUDIT_TARGET_MISSING target=" + target.Name);
                    LoadIntegrityGuard.ReportFailure("load-patch-target-missing:" + target.Name,
                        new MissingMethodException(target.Name));
                    continue;
                }

                inspected++;
                var patches = Harmony.GetPatchInfo(target.Method);
                if (patches?.Finalizers.Any(patch =>
                        string.Equals(patch.owner, coreOwner, StringComparison.Ordinal)) == true)
                    coreFinalizers++;
                else
                {
                    ReportOnce("core-finalizer-missing:" + target.Name,
                        "CORE_LOAD_FINALIZER_MISSING target=" + target.Name);
                    LoadIntegrityGuard.ReportFailure("core-finalizer-missing:" + target.Name,
                        new InvalidOperationException("Core load finalizer is missing for " + target.Name + "."));
                }
                if (patches == null) continue;
                foreign += Report(target, "prefix", patches.Prefixes, coreOwner, ref compatible);
                foreign += Report(target, "postfix", patches.Postfixes, coreOwner, ref compatible);
                foreign += Report(target, "transpiler", patches.Transpilers, coreOwner, ref compatible);
                foreign += Report(target, "finalizer", patches.Finalizers, coreOwner, ref compatible);
                foreign += Report(target, "il-manipulator", patches.ILManipulators, coreOwner, ref compatible);
            }

            DelversHost.Info("CORE_LOAD_PATCH_AUDIT_PASS inspected=" + inspected
                + " coreFinalizers=" + coreFinalizers
                + " compatiblePatches=" + compatible
                + " foreignPatches=" + foreign);
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_LOAD_PATCH_AUDIT_FAILED: " + ex);
            LoadIntegrityGuard.ReportFailure("load-patch-audit", ex);
        }
    }

    internal static void ResetLifecycle()
    {
        lock (Gate) Reported.Clear();
    }

    private static int Report(Target target, string stage,
        IEnumerable<Patch> patches, string coreOwner, ref int compatible)
    {
        var count = 0;
        foreach (var patch in patches)
        {
            if (string.Equals(patch.owner, coreOwner, StringComparison.Ordinal)) continue;
            var patchMethod = patch.PatchMethod;
            var declaringType = patchMethod?.DeclaringType;
            var assembly = declaringType?.Assembly.GetName();
            var patchName = patchMethod == null
                ? "unknown"
                : (declaringType?.FullName ?? "unknown") + "." + patchMethod.Name;
            var identity = target.Name + "|" + stage + "|" + patch.owner + "|" + patchName;

            if (LoadPatchCompatibilityRules.IsKnownExtendedHotbar(target.Name, stage,
                    assembly?.Name, assembly?.Version?.ToString(), declaringType?.FullName,
                    patchMethod?.Name))
            {
                compatible++;
                ReportCompatibleOnce(identity,
                    "CORE_KNOWN_COMPATIBLE_LOAD_PATCH integration=extended-hotbar"
                    + " version=" + Safe(assembly?.Version?.ToString())
                    + " target=" + target.Name
                    + " stage=" + stage
                    + " owner=" + Safe(patch.owner)
                    + " patch=" + Safe(patchName));
                continue;
            }

            count++;
            ReportOnce(identity, "CORE_FOREIGN_LOAD_PATCH_DETECTED target=" + target.Name
                + " stage=" + stage
                + " owner=" + Safe(patch.owner)
                + " patch=" + Safe(patchName));
        }
        return count;
    }

    private static void ReportOnce(string identity, string message)
    {
        if (!MarkReported(identity)) return;
        DelversHost.Warning(message);
    }

    private static void ReportCompatibleOnce(string identity, string message)
    {
        if (!MarkReported(identity)) return;
        DelversHost.Info(message);
    }

    private static bool MarkReported(string identity)
    {
        lock (Gate) return Reported.Add(identity);
    }

    internal static IEnumerable<Target> Targets()
    {
        yield return new("ComponentList.Deserialize",
            AccessTools.Method(typeof(ComponentList), nameof(ComponentList.Deserialize)));
        yield return new("UnitProfileComponent.Deserialize",
            AccessTools.Method(typeof(UnitProfileComponent), nameof(UnitProfileComponent.Deserialize),
                new[] { typeof(ComponentSaveBaseData), typeof(IEntity) }));
        yield return new("CampaignDataContainer.Deserialize",
            AccessTools.Method(typeof(CampaignDataContainer), nameof(CampaignDataContainer.Deserialize),
                new[] { typeof(CampaignSaveData) }));
        yield return new("CampaignLoadingEventHandler.FinalizeCampaignLoad",
            AccessTools.Method(typeof(CampaignLoadingEventHandler), "FinalizeCampaignLoad",
                new[] { typeof(MapType) }));
        yield return new("EntityLoadHelper.LoadUnit",
            AccessTools.Method(typeof(EntityLoadHelper), nameof(EntityLoadHelper.LoadUnit),
                new[] { typeof(UnitEntity), typeof(Vector2Int), typeof(MapType) }));
        yield return new("PlayerUnitDataContainer.Deserialize",
            AccessTools.Method(typeof(PlayerUnitDataContainer), nameof(PlayerUnitDataContainer.Deserialize),
                new[] { typeof(PlayerUnitsSaveData) }));
        yield return new("ClanDataContainer.Deserialize",
            AccessTools.Method(typeof(ClanDataContainer), nameof(ClanDataContainer.Deserialize),
                new[] { typeof(ClanSaveData) }));
        yield return new("UnitQuickSlotContainer.Deserialize",
            AccessTools.Method(typeof(UnitQuickSlotContainer), nameof(UnitQuickSlotContainer.Deserialize)));
        yield return new("QuickSlotData.Deserialize",
            AccessTools.Method(typeof(QuickSlotData), nameof(QuickSlotData.Deserialize),
                new[] { typeof(QuickSlotSaveData) }));
    }

    private static string Safe(string value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Replace(' ', '_');

    internal readonly record struct Target(string Name, MethodBase Method);
}
