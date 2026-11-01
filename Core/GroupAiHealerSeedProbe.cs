using HarmonyLib;
#if BEPINEX
using Affecter = global::Refactor.Affecter;
using HealExecutionData = global::Refactor.Combat.HealExecutionData;
using HealExecutionLog = global::Refactor.Combat.HealExecutionLog;
using IExecutionData = global::Refactor.Combat.IExecutionData;
using SeedHealRoutine = global::Refactor.Combat.AffecterRelated.AFFECTER_StrangeSeedAura._Heal_d__2;
using SeedAuraRoutine = global::Refactor.Combat.AffecterRelated.AFFECTER_StrangeSeedAura.Passive_StrangeSeedHeal._HealRoutine_d__9;
using SummonedBuildingResolver = global::Refactor.Combat.AffecterRelated.SummonedBuildingResolver;
#else
using Affecter = Il2CppRefactor.Affecter;
using HealExecutionData = Il2CppRefactor.Combat.HealExecutionData;
using HealExecutionLog = Il2CppRefactor.Combat.HealExecutionLog;
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
using SeedHealRoutine = Il2CppRefactor.Combat.AffecterRelated.AFFECTER_StrangeSeedAura._Heal_d__2;
using SeedAuraRoutine = Il2CppRefactor.Combat.AffecterRelated.AFFECTER_StrangeSeedAura.Passive_StrangeSeedHeal._HealRoutine_d__9;
using SummonedBuildingResolver = Il2CppRefactor.Combat.AffecterRelated.SummonedBuildingResolver;
#endif

namespace DungeonSettlersDelvers.Core;

// Probe, plan (b): the seed keeps the game's side (a side switch after the game registered
// the building broke CombatDataHandler, 65db29b reverted); Core's aura filter lets comrades of a Core summoner through,
// yet no heal from such a seed. Per seed the probe counts allow (GroupAiHealerSeed.OnAuraTarget), yield
// (a postfix on AFFECTER_StrangeSeedAura._Heal_d__2.MoveNext that yields a heal) and applied (the heal after the game ran
// it, TickEvent.CallOnAfterProcess), with Frieren's first seeds for comparison (CORE_SEED_HEAL_PROBE). And once per seed
// a Core summoner's comrade on the seed's own cell, which the aura pushes away (CORE_SEED_PUSH side=bandit).
// Observing only; capped. Its two patch classes carry the group Seeds and are patched only while it is on (CoreProbes). The two
// entries called from Core's own patches (OnAllow from the seed's aura filter, OnAfterProcess from Lifebloom's heal hook) ask the
// flag CoreProbes.Seeds first, so a group that is off costs a field read.
internal static class GroupAiHealerSeedProbe
{
    private sealed class Counts
    {
        internal string Summoner;
        internal bool Core;
        internal int Allow, Yields, Applied;
        internal float Amount;
        internal string Logged = "";
        internal float LoggedAt = float.NegativeInfinity;
    }

    private static readonly Dictionary<string, Counts> seeds = new(StringComparer.Ordinal);
    private static readonly HashSet<string> pushed = new(StringComparer.Ordinal);
    private static int compared, lines;

    internal static void Reset()
    {
        seeds.Clear();
        pushed.Clear();
        compared = lines = 0;
    }

    // The seed's counts, made on first sight; null for a seed not watched (Frieren's beyond the first CompareSeeds).
    private static Counts Of(IEntity seed, UnitEntity summoner)
    {
        if (!CoreProbes.Seeds || seed == null) return null;
        var key = seed.Guid.ToString();
        if (seeds.TryGetValue(key, out var counts)) return counts;
        var core = summoner != null && CoreUnitRules.IsCoreUnit(summoner.Key);
        if (!core && compared >= GroupAiHealerSeedRules.CompareSeeds) return null;
        if (!core) compared++;
        if (seeds.Count >= GroupAiHealerSeedRules.HealProbeCap) return null;
        counts = new Counts { Summoner = summoner?.Guid.ToString(), Core = core };
        seeds[key] = counts;
        return counts;
    }

    private static UnitEntity SummonerOf(IEntity seed)
    {
        var inMap = CoreGame.InMap;
        return seed == null || inMap == null ? null : SummonedBuildingResolver.GetSummonerUnit(inMap, seed);
    }

    internal static void OnAllow(IEntity seed, UnitEntity summoner, bool allowed)
    {
        if (!CoreProbes.Seeds || !allowed) return;
        var counts = Of(seed, summoner);
        if (counts == null) return;
        counts.Allow++;
        Note(seed.Guid.ToString(), counts);
    }

    internal static void OnHealYield(SeedHealRoutine routine)
    {
        var seed = routine?.seed;
        if (seed == null || routine.__2__current?.TryCast<HealExecutionData>() == null) return;
        var counts = Of(seed, SummonerOf(seed));
        if (counts == null) return;
        counts.Yields++;
        Note(seed.Guid.ToString(), counts);
    }

    // After the game ran an execution: a heal of the seed's aura, counted with what arrived on head and body.
    internal static void OnAfterProcess(IExecutionData data)
    {
        if (!CoreProbes.Seeds || seeds.Count == 0 || data == null) return;
        var heal = data.TryCast<HealExecutionData>();
        if (heal == null) return;
        if (heal.BaseExecution?.Context?.Publisher?.TryCast<Affecter>()?.Key != GroupAiHealerSeedRules.AuraKey) return;
        var seed = heal.User;
        if (seed == null || !seeds.TryGetValue(seed.Guid.ToString(), out var counts)) return;
        double arrived = 0;
        var changes = heal.GetLog()?.TryCast<HealExecutionLog>()?._applyDatas;
        for (var i = 0; changes != null && i < changes.Count; i++)
        {
            var change = changes[i]?.TryCast<StatusApplyData>();
            if (change != null && (change.StatType == StatType.HealthHead || change.StatType == StatType.HealthBody)) arrived += change.Amount;
        }
        counts.Applied++;
        counts.Amount += (float)arrived;
        Note(seed.Guid.ToString(), counts);
    }

    // The units on the seed's own cell, which the aura pushes away: logged once per seed when one of them is a comrade of
    // the Core summoner.
    internal static void OnAuraRoutine(SeedAuraRoutine routine)
    {
        var units = routine?._units_5__2;
        var seed = routine?.__4__this?.Owner;
        if (units == null || units.Count == 0 || seed == null || pushed.Contains(seed.Guid.ToString())) return;
        var summoner = SummonerOf(seed);
        if (summoner == null || !CoreUnitRules.IsCoreUnit(summoner.Key)) return;
        for (var i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            if (unit == null || !CoreUnitRules.IsCoreUnit(unit.Key)) continue;
            pushed.Add(seed.Guid.ToString());
            if (lines++ < GroupAiHealerSeedRules.HealProbeCap)
                DelversHost.Info(GroupAiHealerSeedRules.PushLine(seed.Guid.ToString(), unit.Guid.ToString(), "bandit", Tick()));
            return;
        }
    }

    private static void Note(string seed, Counts counts)
    {
        var state = counts.Allow + "/" + counts.Yields + "/" + counts.Applied;
        var now = GameClock.Now;
        if (state == counts.Logged || !GameClockRules.Elapsed(now, counts.LoggedAt, GroupAiHealerSeedRules.HealProbeSeconds)) return;
        if (lines++ >= GroupAiHealerSeedRules.HealProbeCap) return;
        counts.Logged = state;
        counts.LoggedAt = now;
        DelversHost.Info(GroupAiHealerSeedRules.HealProbeLine(seed, counts.Summoner, counts.Core, counts.Allow, counts.Yields, counts.Applied,
            counts.Amount, Tick()));
    }

    private static long Tick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
}

[ProbeGroup(CoreProbeRules.Seeds)]
[HarmonyPatch(typeof(SeedHealRoutine), "MoveNext")]
internal static class GroupAiHealerSeedYieldPatch
{
    private static void Postfix(SeedHealRoutine __instance, bool __result)
    {
        if (!__result) return;
        try { GroupAiHealerSeedProbe.OnHealYield(__instance); }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_HEAL_PROBE_FAILED yield: " + ex.Message); }
    }
}

[ProbeGroup(CoreProbeRules.Seeds)]
[HarmonyPatch(typeof(SeedAuraRoutine), "MoveNext")]
internal static class GroupAiHealerSeedPushPatch
{
    private static void Postfix(SeedAuraRoutine __instance)
    {
        try { GroupAiHealerSeedProbe.OnAuraRoutine(__instance); }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_HEAL_PROBE_FAILED push: " + ex.Message); }
    }
}
