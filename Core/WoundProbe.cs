using HarmonyLib;
using UnityEngine;
#if BEPINEX
using StatusApplier = global::Refactor.Tick.DataApply.StatusApplier;
#else
using StatusApplier = Il2CppRefactor.Tick.DataApply.StatusApplier;
#endif

namespace DungeonSettlersDelvers.Core;

// W0 probe for the bleeding wound (Core 0.5.0 build 3a). Answers in game what
// reading the game's types cannot (guide lesson 19): which heals and which bleed damage reach StatusApplier.Apply,
// how the game clock runs against real time, and which tooltip the health bar opens. It logs each
// kind once (the clock once a minute) and changes nothing. It is the diagnostics group probes.wound (CoreProbeRules.Wound), off by
// default: the two patch classes below are patched only while the group is on and the clock reads the flag first. It stays in
// the code for diagnosis.
internal static class WoundProbe
{
    private static readonly HashSet<string> Logged = new(StringComparer.Ordinal);
    private static float nextClock;
    private static long lastTick = -1;
    private static float lastTime;

    internal static void ResetLifecycle()
    {
        lock (Logged) Logged.Clear();
        nextClock = 0f;
        lastTick = -1;
    }

    private static bool Once(string key)
    {
        lock (Logged) return Logged.Add(key);
    }

    // Before: the health part the change aims at. Health only; everything else returns at once.
    internal static float Before(StatusApplyData data)
    {
        var stat = data?.StatType ?? default;
        if (stat != StatType.HealthHead && stat != StatType.HealthBody) return float.NaN;
        var unit = data.Target?.TryCast<UnitEntity>();
        return unit != null ? CoreGame.Stat(unit, stat) : float.NaN;
    }

    internal static void After(StatusApplyData data, float before)
    {
        if (float.IsNaN(before)) return;
        var unit = data?.Target?.TryCast<UnitEntity>();
        if (unit == null) return;
        var stat = data.StatType;
        var after = CoreGame.Stat(unit, stat);
        var max = CoreGame.Stat(unit, stat == StatType.HealthHead ? StatType.MaxHealthHead : StatType.MaxHealthBody);
        var sign = data.Amount > 0f ? "heal" : data.Amount < 0f ? "loss" : "zero";
        var from = data.From == null ? "none" : data.From.Pointer == data.Target.Pointer ? "self" : "other";
        var player = IsPlayer(unit) ? "player" : "other";
        var bleeding = CoreGame.Affecters(unit)?.Has("AFFECTER_Bleeding") == true ? "bleeding" : "-";
        var full = before >= max - 0.01f ? "full" : "below";
        var kind = stat + "|" + sign + "|" + from + "|" + player + "|" + bleeding + "|" + full + "|" + data.Persistence;
        if (!Once("status|" + kind)) return;
        DelversHost.Info("CORE_WOUND_PROBE status stat=" + stat + " kind=" + sign + " from=" + from + " target=" + player
            + " bleeding=" + bleeding + " hp=" + Num(before) + "->" + Num(after) + "/" + Num(max)
            + " amount=" + Num(data.Amount) + " persistence=" + data.Persistence);
    }

    // Once a minute from Core's clock: game ticks against real and scaled time.
    internal static void Clock()
    {
        if (!CoreProbes.Wound) return;
        if (Time.unscaledTime < nextClock) return;
        nextClock = Time.unscaledTime + 60f;
        var time = CoreGame.Flow?._campaignData?.TimeData;
        if (time == null) return;
        var tick = time.Tick;
        var line = "CORE_WOUND_PROBE clock tick=" + tick + " day=" + time.Day + " hour=" + time.Hour + " min=" + time.Min
            + " tickPerMin=" + time.TickPerMin + " timeScale=" + Num(Time.timeScale) + " time=" + Num(Time.time);
        if (lastTick >= 0) line += " ticksPerScaledSecond=" + Num((tick - lastTick) / Math.Max(0.001f, Time.time - lastTime));
        lastTick = tick;
        lastTime = Time.time;
        DelversHost.Info(line);
    }

    internal static void Tooltip(string key)
    {
        if (key != null && Once("tooltip|" + key)) DelversHost.Info("CORE_WOUND_PROBE majorstat-tooltip key=" + key);
    }

    // Player units, refreshed once a second: the hook runs for every health change of every unit.
    private static readonly HashSet<IntPtr> players = new();
    private static float playersUntil;

    private static bool IsPlayer(UnitEntity unit)
    {
        if (Time.unscaledTime >= playersUntil)
        {
            playersUntil = Time.unscaledTime + 1f;
            players.Clear();
            foreach (var each in CoreGame.PlayerUnits(true)) players.Add(each.Pointer);
        }
        return players.Contains(unit.Pointer);
    }

    private static string Num(float value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

// StatusApplier.Apply is protected virtual and reached through DataApplyHandler, not inlined.
// __state is Harmony's own value between prefix and postfix, not a ref/out of the game method.
[ProbeGroup(CoreProbeRules.Wound)]
[HarmonyPatch(typeof(StatusApplier), nameof(StatusApplier.Apply))]
internal static class WoundProbeStatusPatch
{
    private static void Prefix(StatusApplyData data, out float __state)
    {
        __state = float.NaN;
        try { __state = WoundProbe.Before(data); }
        catch (Exception) { /* a probe never disturbs the game */ }
    }

    private static void Postfix(StatusApplyData data, float __state)
    {
        try { WoundProbe.After(data, __state); }
        catch (Exception) { /* a probe never disturbs the game */ }
    }
}

[ProbeGroup(CoreProbeRules.Wound)]
[HarmonyPatch(typeof(Tooltip_MajorStat), nameof(Tooltip_MajorStat.Show))]
internal static class WoundProbeTooltipPatch
{
    private static void Postfix(string key)
    {
        try { WoundProbe.Tooltip(key); }
        catch (Exception) { /* a probe never disturbs the game */ }
    }
}
