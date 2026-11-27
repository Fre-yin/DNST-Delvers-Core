#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using AffectTarget = global::Refactor.Combat.AffectTarget;
using TargetType = global::Refactor.Combat.TargetType;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using AffectTarget = Il2CppRefactor.Combat.AffectTarget;
using TargetType = Il2CppRefactor.Combat.TargetType;
#endif

namespace DungeonSettlersDelvers.Core;

// GroupAiPolicy, the target of a skill Core did not plan, a priority cast above all (when our AI acts, the game's
// AI has no say; the game sent priority Lifeblooms to summons and left many casts with no target at all). A heal on an ally goes to
// the most hurt member in its range, never to a summon of someone else; the seed to the foe in focus; a seed's upgrade to the
// unit's own seed; a skill on foes never stays without a target. A skill on the unit itself stays as it is.
internal static partial class GroupAiPolicy
{
    private static IEntity NoteOwnTarget(GroupAiSituation s, ActiveSkill_Base skill, IEntity current)
    {
        var own = OwnTarget(s, skill, current);
        if (own != null && own.Guid.ToString() != current?.Guid.ToString())
            Log("CORE_AI_OWN_TARGET unit=" + s.Guid + " skill=" + skill.Key + " target=" + own.Guid + " was=" + (current?.Guid.ToString() ?? "-"));
        return own;
    }

    private static IEntity OwnTarget(GroupAiSituation s, ActiveSkill_Base skill, IEntity current)
    {
        var data = GroupAiRoles.Sheet(skill.Key);
        if (data == null || data.TargetType == TargetType.SelfTarget) return current;
        if (Array.IndexOf(SeedUpgrades, skill.Key) >= 0) return OwnSeed(s, out var seed, out _, out _) ? seed : current;
        if (data.AffectTarget is AffectTarget.SingleAlly or AffectTarget.MultipleAlly) return MostHurtInReach(s, data.Range) ?? current;
        var hostile = skill.Key == SeedSkill || data.AffectTarget is AffectTarget.SingleHostile or AffectTarget.MultipleHostile;
        if (!hostile) return current;
        // A walk to this foe that Core called off (GroupAiChase): a foe in reach instead.
        var onFoe = EnemyFigure(s, current);
        return onFoe != null && !GroupAiChase.Cancelled(s.Self.Unit, current) ? current : ClosestInReach(s, data.Range) ?? current;
    }

    // A planned foe the unit's cast was refused on (GroupAiChase): the nearest other foe the skill reaches from here, else the plan,
    // which MarkRefused then keeps out of the next decisions.
    private static IEntity Reaim(GroupAiSituation s, ActiveSkill_Base skill, IEntity planned)
    {
        var foe = EnemyFigure(s, planned);
        if (foe == null || !GroupAiChase.Blocks(s.Guid, foe.Guid, foe.Distance, skill.Range)) return planned;
        GroupAiSituation.Figure pick = null;
        foreach (var enemy in s.Enemies)
            if (GroupAiChaseRules.InReach(enemy.Distance, skill.Range) && !GroupAiChase.Blocks(s.Guid, enemy.Guid, enemy.Distance, skill.Range)
                && (pick == null || enemy.Distance < pick.Distance)) pick = enemy;
        if (pick == null) return planned;
        Log("CORE_AI_OWN_TARGET unit=" + s.Guid + " skill=" + skill.Key + " target=" + pick.Guid + " was=" + foe.Guid);
        return pick.Unit.Cast<IEntity>();
    }

    // The most hurt party member the skill reaches, the unit itself included; of the summons only the unit's own.
    private static IEntity MostHurtInReach(GroupAiSituation s, int range)
    {
        GroupAiSituation.Figure pick = null;
        foreach (var figure in Party(s))
        {
            if (figure.Guid != s.Guid && !InReach(s, figure.Distance, range)) continue;
            var summoner = DelversSummons.OwnerOf(figure.Unit.Cast<IEntity>());
            if (!string.IsNullOrEmpty(summoner) && !string.Equals(summoner, s.Guid, StringComparison.OrdinalIgnoreCase)) continue;
            if (pick == null || figure.Share < pick.Share) pick = figure;
        }
        return pick?.Unit.Cast<IEntity>();
    }

    private static IEntity ClosestInReach(GroupAiSituation s, int range)
    {
        GroupAiSituation.Figure pick = null;
        foreach (var enemy in s.Enemies)
            if (InReach(s, enemy.Distance, range) && (pick == null || enemy.Distance < pick.Distance)) pick = enemy;
        return pick?.Unit.Cast<IEntity>();
    }
}
