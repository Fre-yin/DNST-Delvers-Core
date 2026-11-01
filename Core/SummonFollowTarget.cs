using HarmonyLib;
#if BEPINEX
using CombatPattern = global::Refactor.Combat.CombatPattern;
using DecideNonPlayerCombatBehaviour = global::Refactor.Task.DecideNonPlayerCombatBehaviour;
using Pattern_WaterSummoned = global::Refactor.Combat.PatternRelated.Pattern_WaterSummoned;
#else
using CombatPattern = Il2CppRefactor.Combat.CombatPattern;
using DecideNonPlayerCombatBehaviour = Il2CppRefactor.Task.DecideNonPlayerCombatBehaviour;
using Pattern_WaterSummoned = Il2CppRefactor.Combat.PatternRelated.Pattern_WaterSummoned;
#endif

namespace DungeonSettlersDelvers.Core;

// SummonFollowRules in the game: a water mage's bubblelings attack their mage's target and do not choose for themselves.
// The game lets a bubbleling choose in its pattern, Pattern_WaterSummoned.Pattern: a hostile marked with AFFECTER_TidalMark in its
// circle, else the closest hostile, each as BasicAttackTo. A bandit's bubbleling is an NPC (SummonSide), and the game's
// DecideNonPlayerCombatBehaviour asks the pattern but then takes the unit's forced target, else keeps its combat target while
// that is in reach of a basic attack; the pattern's choice counts only after that. So a bandit's bubbleling stuck to its old foe
// (and GB12 near 50 %, runs 20261031-222224 to -223655). Core therefore gives the summon its owner's target as the
// summon's forced target, the game's own way of an order, at the summon's next decision after the owner's target changed, and
// clears it again when the owner has no usable target or the comfort option is off. A forced target Core did not set is an order
// of the player's and is never touched (SummonFollowRules.Forced). The game asks a summon's pattern only every few seconds (2 to 4
// times in 20 s, run 20261031-224607), so the owner's own decision (a postfix on DecideNonPlayerCombatBehaviour.OnUpdate) and
// every place where Core sets a target (CoreTargets) hand the owner's target on to the summons that decided once; events, no
// frame of Core's own. A target the game sets from outside (a taunt) reaches the summon at its next decision. The pattern also returns BasicAttackTo of the target, for a party's
// summon that the game lets choose by its pattern. Without a target of the owner's the game's choice stays.
internal static class SummonFollowTarget
{
    private static readonly Dictionary<string, string> last = new(StringComparer.Ordinal);
    // The forced target Core gave each summon (guid text), to tell it from a player's order.
    private static readonly Dictionary<string, string> forcedByCore = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, (int Calls, int Followed, string Why)> seen = new(StringComparer.Ordinal);
    // The summons that decided once, with their owner, for a change of the owner's target (OnOwnerTarget).
    private static readonly Dictionary<string, (UnitEntity Summon, UnitEntity Owner)> watched = new(StringComparer.Ordinal);
    private const int WatchCap = 64;
    private static int lines;

    internal static void Reset()
    {
        last.Clear();
        forcedByCore.Clear();
        watched.Clear();
        seen.Clear();
        lines = 0;
    }

    // For the probe (SummonProbe.Targets): how often the game asked the summon's pattern, how often it followed, and why it last
    // did not ("-", "no-target", "unusable", "option", "order": a forced target of the player's).
    internal static string Seen(string summon)
        => seen.TryGetValue(summon, out var count) ? count.Calls + "/" + count.Followed + "/" + count.Why : "0/0/-";

    private static void Note(UnitEntity unit, bool followed, string why)
    {
        if (!SummonProbe.On) return;
        var id = unit.Guid.ToString();
        if (!seen.ContainsKey(id) && seen.Count >= 256) return;
        seen.TryGetValue(id, out var count);
        seen[id] = (count.Calls + 1, count.Followed + (followed ? 1 : 0), followed ? count.Why : why);
    }

    internal static void Decide(Pattern_WaterSummoned pattern, UnitEntity unit, ref CombatPattern.CombatBehaviour result)
    {
        if (pattern == null || unit == null || unit.IsDead) return;
        var summon = unit.Cast<IEntity>();
        var owner = DelversSummons.OwnerUnitOf(summon);
        if (owner == null || owner.IsDead) return;
        var id = unit.Guid.ToString();
        if (watched.ContainsKey(id) || watched.Count < WatchCap) watched[id] = (unit, owner);
        var why = Follow(unit, owner, out var target);
        if (why != "-")
        {
            Note(unit, false, why);
            return;
        }
        Note(unit, true, "-");
        result = pattern.BasicAttackTo(EntityTargetSelector.SpecificTarget(target).Cast<ICombatTargetSelector>());
    }

    // After a unit's own combat decision or a target Core set for it (CoreTargets): its watched summons get its target as their
    // forced target.
    internal static void OnOwnerTarget(UnitEntity decider)
    {
        if (watched.Count == 0 || decider == null) return;
        var deciderId = decider.Guid.ToString();
        List<string> gone = null;
        foreach (var pair in watched)
        {
            var (unit, owner) = pair.Value;
            if (unit == null || unit.IsDead || owner == null || owner.IsDead)
            {
                (gone ??= new List<string>()).Add(pair.Key);
                continue;
            }
            if (owner.Guid.ToString() == deciderId) Follow(unit, owner, out _);
        }
        if (gone != null)
            foreach (var id in gone)
            {
                watched.Remove(id);
                forcedByCore.Remove(id);
            }
    }

    // The owner's target as the summon's forced target (SummonFollowRules.Forced); "-" when it follows, else why not.
    private static string Follow(UnitEntity unit, UnitEntity owner, out IEntity target)
    {
        var summon = unit.Cast<IEntity>();
        var core = CoreUnitRules.IsCoreUnit(owner.Key);
        var party = owner.Faction == FactionType.Player;
        var option = CoreSettings.SummonsFollowTarget;
        var id = unit.Guid.ToString();
        target = core || (party && option) ? TargetOf(owner) : null;
        var follows = SummonFollowRules.Follows(core, party, option, target != null && Usable(summon, target));
        var info = CoreGame.Component<CombatInfoComponent>(summon);
        if (info == null) return "unusable";
        var forced = info.GetForceAttackTarget()?.Guid.ToString();
        forcedByCore.TryGetValue(id, out var mine);
        var to = follows ? target.Guid.ToString() : null;
        switch (SummonFollowRules.Forced(forced, mine, to))
        {
            case SummonFollowRules.ForcedStep.Order:
                return "order";
            case SummonFollowRules.ForcedStep.Set:
                info.SetForceAttackTarget(target);
                forcedByCore[id] = to;
                break;
            case SummonFollowRules.ForcedStep.Clear:
                info.SetForceAttackTarget(null);
                forcedByCore.Remove(id);
                break;
        }
        if (!follows) return !core && !(party && option) ? "option" : target == null ? "no-target" : "unusable";
        if (!last.TryGetValue(id, out var before) || before != to)
        {
            last[id] = to;
            if (lines++ < SummonFollowRules.LogCap) DelversHost.Info(SummonFollowRules.Line(id, owner.Guid.ToString(), to, core));
        }
        return "-";
    }

    // The owner's target: the one an order forced, else the one it fights.
    private static IEntity TargetOf(UnitEntity owner)
    {
        var info = CoreGame.Component<CombatInfoComponent>(owner.Cast<IEntity>());
        return info?.GetForceAttackTarget() ?? info?.GetCombatTarget();
    }

    // Alive and hostile to the summon by the game's relation (faction and sub faction).
    private static bool Usable(IEntity summon, IEntity target)
    {
        var unit = target.TryCast<UnitEntity>();
        var building = unit == null ? target.TryCast<BuildingEntity>() : null;
        var alive = unit != null ? !unit.IsDead : building != null && !building.IsDead;
        return alive && FactionRelationInfo.GetRelation(summon, target) == RelationType.Hostile;
    }
}

// A unit's own combat decision (a bandit's: the game's NPC decision): its summons follow its target at once (SummonFollowTarget).
[HarmonyPatch(typeof(DecideNonPlayerCombatBehaviour), "OnUpdate")]
internal static class SummonFollowOwnerPatch
{
    private static void Postfix(DecideNonPlayerCombatBehaviour __instance)
    {
        try { SummonFollowTarget.OnOwnerTarget(__instance?.EntityBrain?.Value?.Owner?.TryCast<UnitEntity>()); }
        catch (Exception ex) { ApocalypseHooks.Report("summon-follow-owner", ex); }
    }
}

// The bubbleling's own choice of target, turned to its owner's (SummonFollowTarget). Arguments bound by name.
[HarmonyPatch(typeof(Pattern_WaterSummoned), "Pattern")]
internal static class SummonFollowTargetPatch
{
    private static void Postfix(Pattern_WaterSummoned __instance, UnitEntity unit, ref CombatPattern.CombatBehaviour __result)
    {
        try { SummonFollowTarget.Decide(__instance, unit, ref __result); }
        catch (Exception ex) { ApocalypseHooks.Report("summon-follow", ex); }
    }
}
