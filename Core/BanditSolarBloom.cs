using HarmonyLib;
#if BEPINEX
using BloomAttackRoutine = global::Refactor.Combat.AffecterRelated.AFFECTER_SolarBloom.Passive_SolarBloomAttack._AttackRoutine_d__8;
using BloomPassive = global::Refactor.Combat.AffecterRelated.AFFECTER_SolarBloom.Passive_SolarBloomAttack;
using Execution = global::Refactor.Combat.Execution;
#else
using BloomAttackRoutine = Il2CppRefactor.Combat.AffecterRelated.AFFECTER_SolarBloom.Passive_SolarBloomAttack._AttackRoutine_d__8;
using BloomPassive = Il2CppRefactor.Combat.AffecterRelated.AFFECTER_SolarBloom.Passive_SolarBloomAttack;
using Execution = Il2CppRefactor.Combat.Execution;
#endif

namespace DungeonSettlersDelvers.Core;

// The solar bloom of a bandit shoots the party (the target choice in the plant's own routine).
// The bloom's attack routine (Passive_SolarBloomAttack.<AttackRoutine>, state 0) takes the first unit or building on its area
// for which EntityExtensions.IsHostile(bloom, it) holds; the bloom keeps its table faction Ally, so that is an Enemy unit, a
// bandit, never the party. On a found target it sets its cooldown (_timeCounter = Values[5]), yields Execution.None and goes to
// state 2; the shot (state 4) goes to <entity>5__3. For a bloom of a bandit (CoreFaction) Core takes, after that step, the
// living player or ally unit on the area nearest to the bloom instead: in place of the game's target, or with the game's own
// yield when the game found none. Without a party unit the game's target is dropped and the cooldown cleared, so the bloom
// tries again next tick. Every other bloom and step stays the game's. CORE_BANDIT_BLOOM_TARGET per change, capped. The
// sprayer and the sun heart bloom need nothing: they ask the summoner's hostility (GetHostilesOnArea), which is the party for
// a bandit (CORE_MUCUS_SPRAYER_HIT side=player only).
internal static class BanditSolarBloom
{
    private const int CooldownValue = 5;
    private static int lines;

    internal static void Reset() => lines = 0;

    internal static (int State, int Cooldown) Before(BloomAttackRoutine routine)
        => (routine.__1__state, routine.__4__this?._timeCounter ?? 0);

    internal static void After(BloomAttackRoutine routine, (int State, int Cooldown) before, ref bool moved)
    {
        var passive = routine.__4__this;
        var bloom = passive?.Owner;
        if (bloom == null || !CoreFaction.IsBandit(bloom)) return;
        var step = CoreFactionRules.BloomStep(before.State, before.Cooldown, moved, routine.__1__state);
        if (step == "keep") return;
        var target = PartyTarget(routine.inMapData, passive, bloom);
        if (step == "found")
        {
            if (target != null) routine._entity_5__3 = target;
            else
            {
                // The yield of Execution.None stays; the next MoveNext ends the routine (state -1).
                routine._entity_5__3 = null;
                routine.__1__state = -1;
                passive._timeCounter = 0;
            }
        }
        else
        {
            var values = passive.Values;
            if (target == null || values == null || values.Length <= CooldownValue) return;
            routine._entity_5__3 = target;
            passive._timeCounter = (int)values[CooldownValue];
            routine.__2__current = Execution.None();
            routine.__1__state = 2;
            moved = true;
        }
        if (lines++ < GroupAiBandits.LogCap)
            DelversHost.Info(CoreFactionRules.BloomLine(bloom.Guid.ToString(), step, target?.Guid.ToString(), CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
    }

    // The living player or ally unit on the bloom's area nearest to the bloom.
    private static IEntity PartyTarget(IInMapDataSupplier inMap, BloomPassive passive, IEntity bloom)
    {
        var area = passive._area;
        var alive = inMap?.GetAliveUnitsOnMap();
        if (area == null || alive == null) return null;
        var cells = new HashSet<(int X, int Y)>();
        for (var i = 0; i < area.Count; i++) cells.Add((area[i].x, area[i].y));
        var from = inMap.GetLogicalPosition(bloom.Guid);
        IEntity best = null;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < alive.Count; i++)
        {
            var unit = alive[i];
            if (unit == null || unit.IsDead || !GroupAiRules.CoreVinesHits((int)unit.Faction)) continue;
            var at = inMap.GetLogicalPosition(unit.Guid);
            if (!cells.Contains((at.x, at.y))) continue;
            var distance = GroupAiRules.Distance(from.x, from.y, at.x, at.y);
            if (distance >= bestDistance) continue;
            best = unit.Cast<IEntity>();
            bestDistance = distance;
        }
        return best;
    }
}

// The bloom's attack routine (BanditSolarBloom): the step it came in at, then the correction after the game's step.
[HarmonyPatch(typeof(BloomAttackRoutine), "MoveNext")]
internal static class BanditSolarBloomPatch
{
    private static void Prefix(BloomAttackRoutine __instance, out (int State, int Cooldown) __state)
    {
        __state = (-2, 0);
        if (!CoreUnits.Active) return;
        try { __state = BanditSolarBloom.Before(__instance); }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-bloom-before", ex); }
    }

    private static void Postfix(BloomAttackRoutine __instance, (int State, int Cooldown) __state, ref bool __result)
    {
        if (__state.State != 0) return;
        try { BanditSolarBloom.After(__instance, __state, ref __result); }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-bloom-after", ex); }
    }
}
