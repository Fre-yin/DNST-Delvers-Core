using HarmonyLib;
#if BEPINEX
using AffecterExecutionData = global::Refactor.Combat.AffecterExecutionData;
using DamageExecutionData = global::Refactor.Combat.DamageExecutionData;
using DamageExecutionLog = global::Refactor.Combat.DamageExecutionLog;
using IExecutionData = global::Refactor.Combat.IExecutionData;
using RuntimeMapData = global::Refactor.Runtime.RuntimeMapData;
#else
using AffecterExecutionData = Il2CppRefactor.Combat.AffecterExecutionData;
using DamageExecutionData = Il2CppRefactor.Combat.DamageExecutionData;
using DamageExecutionLog = Il2CppRefactor.Combat.DamageExecutionLog;
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
using RuntimeMapData = Il2CppRefactor.Runtime.RuntimeMapData;
#endif

namespace DungeonSettlersDelvers.Core;

// The summon probe for the Toolkit: what a summon of a given owner is to the game. Side, distance to its owner,
// the game's relation to a party unit and to the owner, and the hits it landed on the party and on Core's units, counted from
// the moment the probe was first asked. Test campaigns only; the damage hook is patched only with the probe group Effects on.
internal static class SummonProbe
{
    private static readonly Dictionary<string, (int Party, int Core)> hits = new(StringComparer.Ordinal);
    internal static bool On;

    internal static void Reset()
    {
        On = false;
        effectLines.Clear();
        hits.Clear();
    }

    private static readonly Dictionary<string, int> effectLines = new(StringComparer.Ordinal);

    internal static void OnDamage(IExecutionData data)
    {
        var affecter = data?.TryCast<AffecterExecutionData>();
        if (affecter != null)
        {
            OnAffecter(affecter);
            return;
        }
        var damage = data?.TryCast<DamageExecutionData>();
        var user = damage?.User;
        if (user == null || DelversSummons.OwnerOf(user).Length == 0) return;
        var amount = damage.GetLog()?.TryCast<DamageExecutionLog>()?.TotalDamage ?? 0f;
        if (amount <= 0f) return;
        var target = damage.Target?.TryCast<UnitEntity>();
        if (target == null) return;
        Effect(user, target.Cast<IEntity>(), "damage", amount);
        var id = user.Guid.ToString();
        hits.TryGetValue(id, out var count);
        if (target.Faction == FactionType.Player) count.Party++;
        else if (CoreUnitRules.IsCoreUnit(target.Key)) count.Core++;
        hits[id] = count;
    }

    // An affecter a summon puts on someone (BubbleSplash: AFFECTER_Barrier on an ally of the bubbleling's).
    private static void OnAffecter(AffecterExecutionData affecter)
    {
        var user = affecter.User;
        var target = affecter.Target;
        if (user == null || target == null || DelversSummons.OwnerOf(user).Length == 0) return;
        Effect(user, target, "affecter:" + (affecter.Key ?? "-"), 0f);
    }

    // A summon's damage to itself (Bubble Tackle, Bubble Splash) is no effect on anyone. A Bubble Splash comes when a bubbleling
    // dies (Passive_BubbleSplash.OnDead); its line names the execution's user, which for a splash set off by a kill can be the
    // killer, so a bandit's bubbleling may stand on the line of a party bubbleling's splash (runs 20261031-200645 to -201923).
    private static void Effect(IEntity user, IEntity target, string kind, float amount)
    {
        var id = user.Guid.ToString();
        if (id == target.Guid.ToString()) return;
        effectLines.TryGetValue(id, out var written);
        if (written >= SummonSideRules.EffectCap || (written == 0 && effectLines.Count >= SummonSideRules.EffectSummons)) return;
        effectLines[id] = written + 1;
        var owner = DelversSummons.OwnerUnitOf(user);
        var ownerKind = owner == null ? "none" : CoreUnitRules.IsCoreUnit(owner.Key) ? "core" : "party";
        DelversHost.Info(SummonSideRules.EffectLine(id, ownerKind, target.Guid.ToString(), SideOf(target), kind, amount));
    }

    // party, party-summon, bandit, bandit-summon or other.
    private static string SideOf(IEntity target)
    {
        var unit = target.TryCast<UnitEntity>();
        if (unit == null) return "other";
        if (unit.Faction == FactionType.Player) return "party";
        if (CoreUnitRules.IsCoreUnit(unit.Key)) return "bandit";
        if (DelversSummons.OwnerOf(target).Length == 0) return "other";
        var owner = DelversSummons.OwnerUnitOf(target);
        return owner == null ? "other" : CoreUnitRules.IsCoreUnit(owner.Key) ? "bandit-summon" : owner.Faction == FactionType.Player ? "party-summon" : "other";
    }

    // "summons=<n> <entry>|…" for the owner's summons (SummonSideRules.Entry), "summons=0" for none.
    internal static string Status(string ownerGuid)
    {
        On = true;
        var inMap = CoreGame.InMap;
        UnitEntity owner = null;
        UnitEntity party = null;
        foreach (var unit in CoreGame.MapUnits())
        {
            if (SummonOwnerRules.IsOwnedBy(unit.Guid.ToString(), ownerGuid)) owner = unit;
            if (party == null && unit.Faction == FactionType.Player && !unit.IsDead) party = unit;
        }
        var entries = new List<string>();
        foreach (var summon in DelversSummons.SummonsOf(ownerGuid))
        {
            var unit = summon.TryCast<UnitEntity>();
            if (unit == null) continue;
            var cells = -1;
            if (owner != null && inMap != null)
            {
                var a = inMap.GetLogicalPosition(unit.Guid);
                var b = inMap.GetLogicalPosition(owner.Guid);
                cells = Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y));
            }
            hits.TryGetValue(unit.Guid.ToString(), out var count);
            entries.Add(SummonSideRules.Entry(unit.Guid.ToString(), unit.Key, (int)unit.Faction, (int)unit.SubFaction, unit.IsDead, cells,
                party == null ? null : FactionRelationInfo.GetRelation(summon, party.Cast<IEntity>()).ToString(),
                party != null && EntityExtensions.IsHostile(summon, party.Cast<IEntity>()), // trap-ok: probe line only, the game's own answer
                owner == null ? null : FactionRelationInfo.GetRelation(summon, owner.Cast<IEntity>()).ToString(), count.Party, count.Core,
                Registered(inMap, unit, party, owner)));
        }
        return "summons=" + entries.Count + (entries.Count == 0 ? "" : " " + string.Join("|", entries));
    }

    // The owner's target (forced, else combat) and its summons' combat targets, for SummonFollowTarget's acceptance; after each
    // "@calls/followed/why-not" of the summon's pattern (SummonFollowTarget.Seen).
    internal static string Targets(string ownerGuid)
    {
        var owner = Guid.TryParse(ownerGuid, out var guid) ? CoreGame.FindUnit(guid) : null;
        var info = owner == null ? null : CoreGame.Component<CombatInfoComponent>(owner.Cast<IEntity>());
        var own = info?.GetForceAttackTarget() ?? info?.GetCombatTarget();
        var parts = new List<string>();
        foreach (var summon in DelversSummons.SummonsOf(ownerGuid))
            parts.Add(summon.Guid.ToString() + "=" + (CoreGame.Component<CombatInfoComponent>(summon)?.GetCombatTarget()?.Guid.ToString() ?? "-")
                      + "@" + SummonFollowTarget.Seen(summon.Guid.ToString()));
        return "owner=" + (own?.Guid.ToString() ?? "-") + " " + string.Join(",", parts);
    }

    // What CombatDataHandler holds since the spawn: whether its hostile lists count the party unit and the owner as foes of the
    // summon (IsHostileTo), "party,owner" as 1/0; null when it cannot be read. They come from the pair registered at the spawn,
    // so they must match relParty and relOwner. Run 20261031-180223: _unitFactions' ValueTuple did not read through the
    // interop (numbers like 1378417888), hence the handler's own answer.
    private static string Registered(IInMapDataSupplier inMap, UnitEntity unit, UnitEntity party, UnitEntity owner)
    {
        try
        {
            var combat = inMap?.TryCast<InMapDataInterface>()?._runtimeData?.TryCast<RuntimeMapData>()?._combat;
            if (combat == null) return null;
            var toParty = party == null ? -1 : combat.IsHostileTo(unit.Guid, party.Guid) ? 1 : 0;
            var toOwner = owner == null || owner.IsDead ? -1 : combat.IsHostileTo(unit.Guid, owner.Guid) ? 1 : 0;
            return SummonSideRules.Pair(toParty, toOwner);
        }
        catch (Exception) { return null; /* probe only: unread */ }
    }
}

// After the game processed an execution: the hits of summons, while the probe runs (SummonProbe). Arguments bound by name.
// Patched only while the group Effects is on (CoreProbes); without it the status still answers, its hit count stays 0.
[ProbeGroup(CoreProbeRules.Effects)]
[HarmonyPatch(typeof(TickEvent), "CallOnAfterProcess")]
internal static class SummonProbeDamagePatch
{
    private static void Postfix(IExecutionData data)
    {
        if (!SummonProbe.On) return;
        try { SummonProbe.OnDamage(data); }
        catch (Exception ex) { ApocalypseHooks.Report("summon-probe-damage", ex); }
    }
}
