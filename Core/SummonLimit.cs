using HarmonyLib;

namespace DungeonSettlersDelvers.Core;

// The map's summon cap per side (a bandit's summons as a player's). The game's
// EntityLifecycleHelper.EnqueueDeathIfSummonLimitExceeded counts every living unit of the new summon's key on the map against
// the map's cap (SKILL_SummonBubbling {4}: 4 bubblelings, SKILL_SummonBubbleToad {1}: 1 toad), whoever owns it, and those
// with the least health die; so a bandit's bubbleling or toad took the place of the party's and back (toolkit block GB13,
// the game's count alone). Its first filter is the key (the lambda <EnqueueDeathIfSummonLimitExceeded>b__0, interop name
// _EnqueueDeathIfSummonLimitExceeded_b__0); the cap per unit (2 bubblelings, 1 toad) filters by summoner after it. A postfix
// on that lambda leaves out a unit of the other side, Core's summons count among themselves and the party's among theirs; the
// cap per unit stays the game's. The lambda runs while the game walks the map's living units, and reading them again
// (GetAliveUnitsOnMap) rebuilds that very list ("Collection was modified", run 20261031-215754): so a prefix on the method
// notes Core's units first, and the lambda only reads the summoner field. Without a Core unit on the map nothing changes.
// Each death the cap queues (EnqueueLowestHealthDeaths, its pending set before and after) is logged with both sides while a
// Core unit is on the map, the game's count too (SummonLimitForTest off), so a probe sees who dies at the cap.
// Using up a bubbleling (CreatureCondition, for the toad and Embracing Wave) takes only the caster's own
// (unit.Summoner == user), so it needs nothing.
// Put on by hand (LatePatches) like the other lambdas: a missing target must never turn Core off.
internal static class SummonLimit
{
    private const string Name = "summon-limit";
    private const string Method = "EnqueueDeathIfSummonLimitExceeded";
    private const string Lambda = "_EnqueueDeathIfSummonLimitExceeded_b__0";
    private const string Deaths = "EnqueueLowestHealthDeaths";
    private static readonly LatePatchGate gate = new();
    private static readonly HashSet<string> coreUnits = new(StringComparer.OrdinalIgnoreCase);
    private static bool active;
    private static bool noted;
    private static bool summonIsCore;
    private static int lines;
    // The deaths apart from the left-out units (run 20261101-000311: those used the cap up before a later death).
    private static int deathLines;
    private static int failures;

    // Off only through SummonLimitForTest, for a probe of the game's own count.
    internal static bool On = true;

    internal static void ResetLifecycle()
    {
        gate.Reset();
        On = true;
        active = false;
        noted = false;
        coreUnits.Clear();
        lines = 0;
        deathLines = 0;
        failures = 0;
    }

    internal static void TryInstall(string at)
    {
        if (gate.State != LatePatchState.Waiting || !gate.ShouldTry(true)) return;
        gate.Record(Install());
        DelversHost.Info(LatePatchGate.Line(Name, at, gate.State));
    }

    private static bool Install()
    {
        try
        {
            var owner = DelversCoreRuntime.HarmonyOwner;
            if (owner == null) return false;
            var method = AccessTools.Method(typeof(EntityLifecycleHelper), Method);
            var lambda = AccessTools.Method(typeof(EntityLifecycleHelper.__c__DisplayClass38_0), Lambda);
            var deaths = AccessTools.Method(typeof(EntityLifecycleHelper), Deaths);
            if (method == null || lambda == null || deaths == null)
            {
                DelversHost.Warning("CORE_PATCH_LATE_FAILED name=" + Name + " error=EntityLifecycleHelper." + (method == null ? Method : lambda == null ? "__c__DisplayClass38_0." + Lambda : Deaths) + " not found");
                return false;
            }
            var harmony = new Harmony(owner);
            harmony.Patch(method, prefix: new HarmonyMethod(AccessTools.Method(typeof(SummonLimitPatch), "Before")),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(SummonLimitPatch), "After")));
            harmony.Patch(lambda, postfix: new HarmonyMethod(AccessTools.Method(typeof(SummonLimitPatch), "Key")));
            harmony.Patch(deaths, prefix: new HarmonyMethod(AccessTools.Method(typeof(SummonLimitPatch), "DeathsBefore")),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(SummonLimitPatch), "DeathsAfter")));
            return true;
        }
        catch (Exception ex)
        {
            DelversHost.Error("CORE_PATCH_LATE_FAILED name=" + Name + " error=" + ex);
            return false;
        }
    }

    // Before the game counts: Core's units on the map, and the new summon's side. Nothing to do without a Core unit.
    internal static void Before(UnitEntity summoned)
    {
        active = false;
        noted = false;
        if (summoned == null || failures >= 3) return;
        try
        {
            coreUnits.Clear();
            foreach (var unit in CoreGame.MapUnits())
                if (CoreUnitRules.IsCoreUnit(unit.Key)) coreUnits.Add(unit.Guid.ToString());
            if (coreUnits.Count == 0) return;
            summonIsCore = coreUnits.Contains(DelversSummons.SummonerGuid(summoned.Cast<IEntity>()));
            noted = true;
            active = On;
        }
        catch (Exception ex) { Fail(ex); }
    }

    internal static void After()
    {
        active = false;
        noted = false;
    }

    // The cap's pending deaths before the game picks: afterwards the new ones are those it queued.
    internal static List<string> PendingBefore(Il2CppSystem.Collections.Generic.HashSet<Il2CppSystem.Guid> pending)
    {
        if (!noted || pending == null) return null;
        var before = new List<string>();
        try
        {
            foreach (var guid in pending) before.Add(guid.ToString());
        }
        catch (Exception ex)
        {
            Fail(ex);
            return null;
        }
        return before;
    }

    // Each death the cap queued, with the new summon's side and the dying one's.
    internal static void PendingAfter(Il2CppSystem.Collections.Generic.IEnumerable<UnitEntity> candidates,
        Il2CppSystem.Collections.Generic.HashSet<Il2CppSystem.Guid> pending, List<string> before)
    {
        if (before == null || pending == null) return;
        try
        {
            var list = candidates?.TryCast<Il2CppSystem.Collections.Generic.List<UnitEntity>>();
            foreach (var guid in pending)
            {
                var id = guid.ToString();
                if (before.Contains(id)) continue;
                UnitEntity dying = null;
                for (var i = 0; list != null && i < list.Count; i++)
                    if (list[i] != null && list[i].Guid.ToString() == id) dying = list[i];
                var dyingIsCore = dying != null && coreUnits.Contains(DelversSummons.SummonerGuid(dying.Cast<IEntity>()));
                if (deathLines++ < SummonSideRules.LimitLogCap)
                    DelversHost.Info(SummonSideRules.LimitDeathLine(dying?.Key, id, summonIsCore, dyingIsCore, active));
            }
        }
        catch (Exception ex) { Fail(ex); }
    }

    // Whether a unit of the same key counts for the new summon's map cap: only on the summon's side. Reads no list.
    internal static void OnKey(UnitEntity summoned, UnitEntity unit, ref bool counts)
    {
        if (!active || !counts || summoned == null || unit == null) return;
        try
        {
            if (SummonSideRules.CountsTogether(summonIsCore, coreUnits.Contains(DelversSummons.SummonerGuid(unit.Cast<IEntity>())))) return;
            counts = false;
            if (lines++ < SummonSideRules.LimitLogCap)
                DelversHost.Info(SummonSideRules.LimitLine(summoned.Guid.ToString(), summoned.Key, unit.Guid.ToString(), summonIsCore));
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Fail(Exception ex)
    {
        failures++;
        active = false;
        ApocalypseHooks.Report(Name, ex);
    }
}

// No [HarmonyPatch]: SummonLimit puts them on by hand. The method's parameter is summonedUnit; the lambda's is unit, its
// closure holds summonedUnit.
internal static class SummonLimitPatch
{
    private static void Before(UnitEntity summonedUnit) => SummonLimit.Before(summonedUnit);

    // Should the game's method throw, the next prefix starts afresh; the lambda serves only this method.
    private static void After() => SummonLimit.After();

    private static void DeathsBefore(Il2CppSystem.Collections.Generic.HashSet<Il2CppSystem.Guid> pendingDeaths, out List<string> __state)
        => __state = SummonLimit.PendingBefore(pendingDeaths);

    private static void DeathsAfter(Il2CppSystem.Collections.Generic.IEnumerable<UnitEntity> candidates,
        Il2CppSystem.Collections.Generic.HashSet<Il2CppSystem.Guid> pendingDeaths, List<string> __state)
        => SummonLimit.PendingAfter(candidates, pendingDeaths, __state);

    private static void Key(EntityLifecycleHelper.__c__DisplayClass38_0 __instance, UnitEntity unit, ref bool __result)
        => SummonLimit.OnKey(__instance?.summonedUnit, unit, ref __result);
}
