using HarmonyLib;
using UnityEngine;
#if BEPINEX
using VFXID = global::Refactor.FX.VFXID;
using VFXView = global::Refactor.FX.VFXView;
#else
using VFXID = Il2CppRefactor.FX.VFXID;
using VFXView = Il2CppRefactor.FX.VFXView;
#endif

namespace DungeonSettlersDelvers.Core;

// Probe 1a (GroupAiAreaProbeRules): every red area the game creates (VFXView.CreateVFX with an id ending in "Area"),
// its cell (from a party unit's cell and world position) and the party units standing in it, then its duration
// (SetVFXDuration), the same area on the same cell once per 3 s (skipped= counts the rest); every other VFX id once when
// first seen, so Toolkit can tell whether a death explosion draws an area.
// Observing only; capped per campaign. Its two patch classes, the creation and the duration hook, carry the group Areas and are
// patched only while that group is on (CoreProbes); with the group off no VFX call reaches the probe.
internal static class GroupAiAreaProbe
{
    private static readonly Dictionary<string, string> areas = new(StringComparer.Ordinal);
    private static readonly HashSet<int> seen = new();
    private static readonly Dictionary<string, (long Tick, int Skipped)> last = new(StringComparer.Ordinal);
    private static int areaLines;

    internal static void Reset()
    {
        areas.Clear();
        seen.Clear();
        last.Clear();
        areaLines = 0;
    }

    internal static void OnCreate(Il2CppSystem.Guid fx, VFXID id, Vector2 position)
    {
        var number = (int)id;
        var name = id.ToString();
        var tick = CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
        if (!GroupAiAreaProbeRules.IsArea(name))
        {
            if (seen.Count < GroupAiAreaProbeRules.SeenCap && seen.Add(number))
                DelversHost.Info(GroupAiAreaProbeRules.SeenLine(name, number, position.x, position.y, tick));
            return;
        }
        if (areaLines >= GroupAiAreaProbeRules.AreaCap) return;
        string cell = null;
        var inside = new List<string>();
        var inMap = CoreGame.InMap;
        foreach (var unit in CoreGame.PlayerUnits(false))
        {
            if (unit == null || inMap == null) continue;
            var world = inMap.GetWorldPosition(unit.Guid);
            if (cell == null)
            {
                var at = inMap.GetLogicalPosition(unit.Guid);
                var c = GroupAiAreaProbeRules.CellOf(position.x, position.y, at.x, at.y, world.x, world.y);
                cell = c.X + "," + c.Y;
            }
            if (GroupAiAreaProbeRules.Inside(world.x, world.y, position.x, position.y))
                inside.Add(GroupAiAreaProbeRules.Short(unit.Guid.ToString()));
        }
        var place = name + "@" + (cell ?? position.x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "," + position.y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        if (last.TryGetValue(place, out var before) && GroupAiAreaProbeRules.Repeat(before.Tick, tick))
        {
            last[place] = (before.Tick, before.Skipped + 1);
            return;
        }
        var skipped = before.Skipped;
        if (last.Count >= GroupAiAreaProbeRules.AreaCap) last.Clear();
        last[place] = (tick, 0);
        areaLines++;
        var key = fx.ToString();
        if (areas.Count >= GroupAiAreaProbeRules.AreaCap) areas.Clear();
        areas[key] = name;
        DelversHost.Info(GroupAiAreaProbeRules.CreateLine(GroupAiAreaProbeRules.Short(key), name, number, position.x, position.y, cell,
            string.Join("|", inside), tick, skipped));
    }

    internal static void OnDuration(Il2CppSystem.Guid fx, float duration)
    {
        if (areas.Count == 0) return;
        var key = fx.ToString();
        if (!areas.Remove(key, out var name)) return;
        var tick = CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
        DelversHost.Info(GroupAiAreaProbeRules.DurationLine(GroupAiAreaProbeRules.Short(key), name, duration, tick));
    }
}

[ProbeGroup(CoreProbeRules.Areas)]
[HarmonyPatch(typeof(VFXView), nameof(VFXView.CreateVFX))]
internal static class GroupAiAreaProbeCreatePatch
{
    private static void Postfix(Il2CppSystem.Guid guid, VFXID id, Vector2 position)
    {
        try { GroupAiAreaProbe.OnCreate(guid, id, position); }
        catch (Exception ex) { DelversHost.Warning("CORE_AI_AREA_FX_PROBE_FAILED create: " + ex.Message); }
    }
}

// The duration hook: a probe like the creation hook, patched with it while the group Areas is on.
[ProbeGroup(CoreProbeRules.Areas)]
[HarmonyPatch(typeof(VFXView), nameof(VFXView.SetVFXDuration))]
internal static class GroupAiAreaProbeDurationPatch
{
    private static void Postfix(Il2CppSystem.Guid guid, float duration)
    {
        try { GroupAiAreaProbe.OnDuration(guid, duration); }
        catch (Exception ex) { DelversHost.Warning("CORE_AI_AREA_FX_PROBE_FAILED duration: " + ex.Message); }
    }
}
