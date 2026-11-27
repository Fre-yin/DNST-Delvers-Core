#if BEPINEX
using DamageExecutionData = global::Refactor.Combat.DamageExecutionData;
using DamageExecutionLog = global::Refactor.Combat.DamageExecutionLog;
using HealExecutionData = global::Refactor.Combat.HealExecutionData;
using HealExecutionLog = global::Refactor.Combat.HealExecutionLog;
using IExecutionData = global::Refactor.Combat.IExecutionData;
#else
using DamageExecutionData = Il2CppRefactor.Combat.DamageExecutionData;
using DamageExecutionLog = Il2CppRefactor.Combat.DamageExecutionLog;
using HealExecutionData = Il2CppRefactor.Combat.HealExecutionData;
using HealExecutionLog = Il2CppRefactor.Combat.HealExecutionLog;
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
#endif

namespace DungeonSettlersDelvers.Core;

// The game side of GroupAiLowRules: the damage and heals each party member and Core bandit took, kept for the window, from the
// damage hook Core already has (GroupAiBanditDamagePatch), and per figure and line until when its buffer holds. The sites ask
// Share(unit, share, line) instead of comparing the share itself: the real share, or just under the line while the figure
// counts as below by look ahead or buffer. A site that moves the line for a figure (a bandit's seed line) passes it as own, and
// every site of that line must pass the same one: the buffer is kept per figure and line, not per threshold. CORE_AI_LOW per change of a figure's state on a line, capped. Every KeepSeconds a hit
// lets the figures go that are dead or gone and took nothing for KeepSeconds (dead bandits kept their entries
// until the next campaign).
internal static class GroupAiLow
{
    private const int ChangeCap = 64;
    private const float KeepSeconds = 10f;

    private static readonly Dictionary<string, List<GroupAiLowRules.Change>> changes = new(StringComparer.Ordinal);
    private static readonly Dictionary<(string Unit, LowLine Line), (float HoldUntil, string Reason)> states = new();
    private static GroupAiLowRules.Settings settings = GroupAiLowRules.Read(null, null, null, null, GroupAiLowRules.NetDefault);
    private static double seedBelow = SeedStayRules.BelowDefault;
    private static int lines;
    private static float nextSweep;

    // At the start of a campaign and on every change of Core's settings (GroupAiPolicy.ResetCampaign).
    internal static void Reset()
    {
        changes.Clear();
        states.Clear();
        nextSweep = 0f;
        settings = GroupAiLowRules.Read(CoreSettings.Number(GroupAiLowRules.WindowKey), CoreSettings.Number(GroupAiLowRules.HitsKey),
            CoreSettings.Number(GroupAiLowRules.AheadKey), CoreSettings.Number(GroupAiLowRules.BufferKey), CoreSettings.LowNet);
        seedBelow = SeedStayRules.Below(CoreSettings.Number(SeedStayRules.BelowKey));
        lines = 0;
    }

    // After the game processed an execution: damage or a heal on a party member or a Core bandit.
    internal static void OnProcessed(IExecutionData data)
    {
        if (data == null || (!GroupAiSettings.Enabled && !CoreUnits.Active)) return;
        var damage = data.TryCast<DamageExecutionData>();
        if (damage != null)
        {
            Note(damage.Target?.TryCast<UnitEntity>(), damage.GetLog()?.TryCast<DamageExecutionLog>()?.TotalDamage ?? 0f, false);
            return;
        }
        var heal = data.TryCast<HealExecutionData>();
        if (heal != null) Note(heal.Target?.TryCast<UnitEntity>(), heal.GetLog()?.TryCast<HealExecutionLog>()?.TotalHeal ?? 0f, true);
    }

    private static void Note(UnitEntity unit, float amount, bool heal)
    {
        if (unit == null || amount <= 0f || (unit.Faction != FactionType.Player && !CoreUnitRules.IsCoreUnit(unit.Key))) return;
        var key = unit.Guid.ToString();
        var now = GameClock.Now;
        Sweep(now);
        if (!changes.TryGetValue(key, out var list)) changes[key] = list = new List<GroupAiLowRules.Change>();
        list.RemoveAll(change => change.At > now || now - change.At > KeepSeconds);
        if (list.Count >= ChangeCap) list.RemoveAt(0);
        list.Add(new GroupAiLowRules.Change(now, amount, heal));
    }

    private static void Sweep(float now)
    {
        if (now < nextSweep && nextSweep - now <= KeepSeconds) return;
        nextSweep = now + KeepSeconds;
        List<string> gone = null;
        foreach (var pair in changes)
        {
            var last = pair.Value.Count > 0 ? pair.Value[pair.Value.Count - 1].At : float.NegativeInfinity;
            if (last <= now && now - last <= KeepSeconds) continue;
            var unit = Guid.TryParse(pair.Key, out var guid) ? CoreGame.FindUnit(guid) : null;
            if (unit == null || unit.IsDead) (gone ??= new List<string>()).Add(pair.Key);
        }
        if (gone == null) return;
        foreach (var key in gone) changes.Remove(key);
        foreach (var state in new List<(string Unit, LowLine Line)>(states.Keys))
            if (gone.Contains(state.Unit) && states[state].HoldUntil <= now) states.Remove(state);
    }

    // The share a site compares with the line: the real one, or just under the line while the figure counts as below by look ahead
    // or buffer (GroupAiLowRules.Adjusted). own: the line the site really uses when it moves the setting (a bandit's courage).
    internal static float Share(UnitEntity unit, float share, LowLine line, float? own = null)
    {
        if (unit == null) return share;
        var key = unit.Guid.ToString();
        var now = GameClock.Now;
        var threshold = own ?? GroupAiLowRules.Threshold(line, seedBelow);
        var loss = 0f;
        var hits = 0;
        if (changes.TryGetValue(key, out var list))
            loss = GroupAiLowRules.LossPerSecond(list, now, settings, CoreGame.Stat(unit, StatType.MaxHealthTotal), out hits);
        states.TryGetValue((key, line), out var state);
        var hold = state.HoldUntil;
        var reason = GroupAiLowRules.Below(share, threshold, loss, now, settings, ref hold);
        if (reason != state.Reason && lines++ < GroupAiLowRules.LogCap)
            DelversHost.Info(GroupAiLowRules.Line(key, line, reason, share, loss, hits, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
        if (reason == null && state.Reason == null) states.Remove((key, line));
        else states[(key, line)] = (hold, reason);
        return GroupAiLowRules.Adjusted(share, threshold, reason);
    }
}
