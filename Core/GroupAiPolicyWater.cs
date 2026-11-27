#if BEPINEX
using AffectTarget = global::Refactor.Combat.AffectTarget;
#else
using AffectTarget = Il2CppRefactor.Combat.AffectTarget;
#endif

namespace DungeonSettlersDelvers.Core;

// GroupAiPolicy, the water mage by the player's role (GroupAiWaterRules), before the priority casts (RoleFirst): the support's wave
// that sacrifices a bubbleling for a member below the sacrifice line and its two bubblelings; the damage dealer's toad, two
// bubblelings and its Glacier Drop and Water Dive on the focus, never the wave. A summon goes out as Core's own order on a free cell
// beside the mage, Glacier Drop and Water Dive on the focus' tile (GroupAiSummonCast); the game keeps its choice for that decision
// (summoned), as with a bandage.
internal static partial class GroupAiPolicy
{
    // A summon of the mage went out in this decision (RoleFirst: the game keeps its choice).
    private static bool summoned;

    private static Choice Water(GroupAiSituation s, TacticalRole player)
    {
        var keys = new List<string>();
        foreach (var slot in s.Slots) keys.Add(slot.Key);
        if (player is not (TacticalRole.Support or TacticalRole.Damage) || !GroupAiWaterRules.IsWaterMage(keys)) return null;
        var (bubblings, toads) = OwnWaterSummons(s.Guid);
        var wave = s.Find(WeakBanditKitKeys.EmbracingWave);
        var waveFree = wave != null && wave.Free && wave.Data != null;
        var summonFree = s.Find(WeakBanditKitKeys.SummonBubbling)?.Free ?? false;
        GroupAiSituation.Figure target = null;
        GroupAiSituation.Figure focus = null;
        GroupAiWaterRules.Step step;
        if (player != TacticalRole.Damage)
        {
            target = waveFree ? WaveTarget(s, wave, figure => GroupAiLow.Share(figure.Unit, figure.Share, LowLine.Sacrifice) < GroupAiWaterRules.SacrificeShare) : null;
            step = GroupAiWaterRules.Support(waveFree, target != null, bubblings, summonFree);
        }
        else
        {
            var glacier = s.Find(GroupAiWaterRules.GlacierDrop);
            var dive = s.Find(GroupAiWaterRules.WaterDive);
            var range = Math.Max(glacier?.Data?.Range ?? 0, dive?.Data?.Range ?? 0);
            focus = EnemyFigure(s, GroupAiFocus.Choose(s, range, AffectTarget.SingleHostile, s.CombatTarget ?? s.ClosestEnemy?.Unit.Cast<IEntity>(), "water"));
            var glacierReach = glacier?.Data != null && focus != null && InReach(s, focus.Distance, glacier.Data.Range);
            var diveFoes = 0;
            if (dive?.Data != null && focus != null && InReach(s, focus.Distance, dive.Data.Range))
                foreach (var enemy in s.Enemies)
                    if (GroupAiSituation.Distance(enemy.Position, focus.Position) <= 1) diveFoes++;
            var toadFree = s.Find(WeakBanditKitKeys.SummonBubbleToad)?.Free ?? false;
            step = GroupAiWaterRules.Damage(bubblings, toads, toadFree, summonFree, glacier?.Free ?? false, glacierReach, dive?.Free ?? false, diveFoes);
            target = focus;
        }
        Choice choice = null;
        if (step == GroupAiWaterRules.Step.Wave) choice = NewChoice(wave.Key, target.Unit.Cast<IEntity>(), "water-wave");
        else if (step is GroupAiWaterRules.Step.Glacier or GroupAiWaterRules.Step.Dive)
            summoned = GroupAiSummonCast.CastAt(s, step == GroupAiWaterRules.Step.Glacier ? GroupAiWaterRules.GlacierDrop : GroupAiWaterRules.WaterDive, focus.Position);
        else if (step != GroupAiWaterRules.Step.None)
            summoned = GroupAiSummonCast.Cast(s, step == GroupAiWaterRules.Step.Toad ? WeakBanditKitKeys.SummonBubbleToad : WeakBanditKitKeys.SummonBubbling);
        if (choice != null || summoned)
            Log(GroupAiWaterRules.Line(s.Guid, TacticalRoles.Word(player), step.ToString().ToLowerInvariant(), target?.Guid, target?.Share ?? 0f,
                bubblings, toads, Tick()));
        return choice;
    }

    // The weakest member the wave reaches that the test lets through, the unit itself included.
    private static GroupAiSituation.Figure WaveTarget(GroupAiSituation s, GroupAiSituation.Slot wave, Func<GroupAiSituation.Figure, bool> needs)
    {
        GroupAiSituation.Figure pick = null;
        foreach (var figure in Party(s))
            if (needs(figure) && InReach(s, figure.Distance, wave.Data.Range) && (pick == null || figure.Share < pick.Share)) pick = figure;
        return pick;
    }

    // The mage's living bubblelings and toads (DelversSummons.SummonsOf).
    internal static (int Bubblings, int Toads) OwnWaterSummons(string owner)
    {
        int bubblings = 0, toads = 0;
        foreach (var summon in DelversSummons.SummonsOf(owner))
        {
            var key = summon.TryCast<UnitEntity>()?.Key;
            if (key == WeakBanditKitKeys.Bubbling) bubblings++;
            else if (key == WeakBanditKitKeys.BubbleToad) toads++;
        }
        return (bubblings, toads);
    }
}
