using HarmonyLib;

namespace DungeonSettlersDelvers.Core;

// Core's probe classes in the game: the patch classes marked [ProbeGroup(...)] (CoreProbeRules). Core's own patch plan leaves them
// out; this class patches a marked class while its group is on in the settings and takes it back when the group goes off and in
// Shutdown. The settings are read when Core starts and at every campaign start (Refresh); a switch in the middle of a campaign
// takes effect with the next one.
// - Each class has a Harmony instance of its own, with an id of its own next to Core's, so a class that fails half way is taken
//   back whole and switching a group off touches nothing else.
// - A probe class that cannot be patched is optional: CORE_PROBE_PATCH_FAILED is a warning, Core starts all the same and the
//   class stays off until its group is switched off and on again. Core's own patch classes stay all or nothing.
// - Probe code that is called from a patch with a function of its own checks one of the flags below first, so a group that is off
//   costs a field read. The flags follow the settings and are all false before the first Refresh and after Shutdown.
internal static class CoreProbes
{
    internal const string HarmonyIdPrefix = "fre-yin.dungeonsettlers.delvers.core.probes";

    internal static bool Commands;
    internal static bool Hud;
    internal static bool Wound;
    internal static bool Tint;
    internal static bool BossBar;
    internal static bool Areas;
    internal static bool Seeds;
    internal static bool Corpse;
    internal static bool Text;
    internal static bool OneShot;
    internal static bool Items;
    internal static bool Effects;

    private static readonly object Gate = new();
    private static readonly Dictionary<Type, Harmony> Patched = new();
    private static readonly HashSet<Type> Failed = new();
    private static List<Type> probeClasses;

    // Reads the groups from the settings, sets the flags and brings the patched classes in line: the classes of groups that are
    // on are patched, the patched ones of groups that went off are taken back. Never throws; a probe never stops Core.
    internal static void Refresh()
    {
        try
        {
            lock (Gate)
            {
                var on = CoreSettings.ProbeGroupsOn();
                foreach (var group in CoreProbeRules.Groups) SetFlag(group, on.Contains(group));
                Apply(CoreProbeRules.Plan(ProbeClasses(), on, Patched.Keys, Failed));
            }
        }
        catch (Exception ex) { DelversHost.Warning("CORE_PROBE_REFRESH_FAILED: " + ex.Message); }
    }

    // All flags off, every probe class taken back. Core's Shutdown calls it; the loader's own unpatching of Core's id comes before.
    internal static void Shutdown()
    {
        lock (Gate)
        {
            foreach (var group in CoreProbeRules.Groups) SetFlag(group, false);
            foreach (var type in new List<Type>(Patched.Keys)) Unpatch(type);
            Failed.Clear();
            probeClasses = null;
        }
    }

    private static List<Type> ProbeClasses()
        => probeClasses ??= CoreProbeRules.ProbeClasses(AccessTools.GetTypesFromAssembly(typeof(CoreProbes).Assembly));

    private static void Apply(CoreProbeRules.Changes changes)
    {
        foreach (var type in changes.Unpatch) Unpatch(type);
        foreach (var type in changes.Forgive) Failed.Remove(type);
        var failures = PatchPlanRules.Apply(changes.Patch, PatchClass, _ => "-", ReportPatchFailed);
        foreach (var failure in failures) Failed.Add(failure.PatchClass);
    }

    private static void PatchClass(Type type)
    {
        var harmony = new Harmony(HarmonyIdPrefix + "." + type.FullName);
        try { harmony.CreateClassProcessor(type).Patch(); }
        catch
        {
            // Whatever the class patched before it failed goes again; the failure itself is what gets reported.
            try { harmony.UnpatchSelf(); }
            catch (Exception) { /* nothing more to take back */ }
            throw;
        }
        Patched[type] = harmony;
    }

    private static void Unpatch(Type type)
    {
        var harmony = Patched[type];
        Patched.Remove(type);
        try { harmony.UnpatchSelf(); }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_PROBE_UNPATCH_FAILED class=" + type.FullName + " group=" + CoreProbeRules.GroupOf(type)
                + " error=" + DelversCoreRuntime.DescribeInnermost(ex));
        }
    }

    private static void ReportPatchFailed(PatchPlanRules.Failure failure)
        => DelversHost.Warning("CORE_PROBE_PATCH_FAILED class=" + failure.PatchClass.FullName + " group=" + CoreProbeRules.GroupOf(failure.PatchClass)
            + " optional=1 error=" + DelversCoreRuntime.DescribeInnermost(failure.Error));

    private static void SetFlag(string group, bool on)
    {
        switch (group)
        {
            case CoreProbeRules.Commands: Commands = on; break;
            case CoreProbeRules.Hud: Hud = on; break;
            case CoreProbeRules.Wound: Wound = on; break;
            case CoreProbeRules.Tint: Tint = on; break;
            case CoreProbeRules.BossBar: BossBar = on; break;
            case CoreProbeRules.Areas: Areas = on; break;
            case CoreProbeRules.Seeds: Seeds = on; break;
            case CoreProbeRules.Corpse: Corpse = on; break;
            case CoreProbeRules.Text: Text = on; break;
            case CoreProbeRules.OneShot: OneShot = on; break;
            case CoreProbeRules.Items: Items = on; break;
            case CoreProbeRules.Effects: Effects = on; break;
        }
    }
}
