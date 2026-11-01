#if BEPINEX
using DamageExecutionData = global::Refactor.Combat.DamageExecutionData;
using DamageExecutionLog = global::Refactor.Combat.DamageExecutionLog;
using IExecutionData = global::Refactor.Combat.IExecutionData;
#else
using DamageExecutionData = Il2CppRefactor.Combat.DamageExecutionData;
using DamageExecutionLog = Il2CppRefactor.Combat.DamageExecutionLog;
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
#endif

namespace DungeonSettlersDelvers.Core;

// B (the vines counted as a threat only a foe whose combat target was the healer): who landed a hit
// on a party unit, kept for WindowSeconds. Fed from the same damage hook as the bandits' (GroupAiBanditDamagePatch).
internal static class GroupAiPartyHits
{
    internal const float WindowSeconds = 2f;
    private static readonly Dictionary<string, List<(string Attacker, float At)>> hits = new(StringComparer.Ordinal);

    internal static void Reset() => hits.Clear();

    internal static void OnDamage(IExecutionData data)
    {
        if (data == null || (!GroupAiSettings.Enabled && !GroupAiFormation.Active)) return;
        var damage = data.TryCast<DamageExecutionData>();
        var target = damage?.Target?.TryCast<UnitEntity>();
        if (target == null || target.Faction != FactionType.Player) return;
        var attacker = damage.User?.TryCast<UnitEntity>();
        if (attacker == null || attacker.Faction != FactionType.Enemy) return;
        // The formation counts every attack, hit or not (GroupAiFormation), with or without the group AI.
        GroupAiFormation.OnAttacked(target);
        if (!GroupAiSettings.Enabled) return;
        if ((damage.GetLog()?.TryCast<DamageExecutionLog>()?.TotalDamage ?? 0f) <= 0f) return;
        var key = target.Guid.ToString();
        var now = GameClock.Now;
        if (!hits.TryGetValue(key, out var list)) hits[key] = list = new List<(string Attacker, float At)>();
        list.RemoveAll(hit => hit.At > now || now - hit.At > WindowSeconds);
        list.Add((attacker.Guid.ToString(), now));
    }

    // Whether the enemy hit the party unit within WindowSeconds.
    internal static bool HitWithin(string unit, string enemy)
    {
        if (unit == null || enemy == null || !hits.TryGetValue(unit, out var list)) return false;
        var now = GameClock.Now;
        foreach (var (attacker, at) in list)
            if (attacker == enemy && GroupAiRules.RecentHit(now, at, WindowSeconds)) return true;
        return false;
    }
}
