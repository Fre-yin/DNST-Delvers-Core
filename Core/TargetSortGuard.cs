using HarmonyLib;

namespace DungeonSettlersDelvers.Core;

// Keeps the game's ToLowestByDeadly comparison (TargetSortRules) from throwing. A prefix on the game's compiler made lambda
// (EntityTargetSelector+<>c, <ToLowestByDeadly>b__15_1, interop name _ToLowestByDeadly_b__15_1) sees before the game
// whether a candidate has no affecter component left, the one case where the game throws; then the gone candidate sorts
// after the whole one (TargetSortRules.GoneOrder) and the game's comparison does not run. Nothing is caught: every other exception passes as before. Each such NRE ended
// MainFlow.Update for its frame, so the game's ticks and views stopped (run log, 53 in one fight's end).
// Put on by hand (LatePatchGate, hooks as FalconCritInstall): the lambda's name may change with a game update, and a missing
// target must never turn Core off.
internal static class TargetSortGuard
{
    private const string Name = "target-sort";
    private const string LambdaType = "__c";
    private const string Lambda = "_ToLowestByDeadly_b__15_1";
    private const int LineCap = 30;
    private static readonly LatePatchGate gate = new();

    internal static int Count { get; private set; }

    internal static void ResetLifecycle()
    {
        gate.Reset();
        Count = 0;
    }

    internal static void TryInstall(string at)
    {
        if (!gate.ShouldTry(true)) return;
        gate.Record(Install());
        DelversHost.Info(LatePatchGate.Line(Name, at, gate.State));
    }

    private static bool Install()
    {
        try
        {
            var owner = DelversCoreRuntime.HarmonyOwner;
            if (owner == null) return false;
            var holder = AccessTools.Inner(typeof(EntityTargetSelector), LambdaType);
            var original = holder == null ? null : AccessTools.Method(holder, Lambda);
            if (original == null)
            {
                DelversHost.Warning("CORE_PATCH_LATE_FAILED name=" + Name + " error=EntityTargetSelector." + LambdaType + "." + Lambda + " not found");
                return false;
            }
            new Harmony(owner).Patch(original, prefix: new HarmonyMethod(AccessTools.Method(typeof(TargetSortGuardPatch), "Prefix")));
            return true;
        }
        catch (Exception ex)
        {
            DelversHost.Error("CORE_PATCH_LATE_FAILED name=" + Name + " error=" + ex);
            return false;
        }
    }

    // True when the game's comparison may run. Our own check failing leaves the pair to the game.
    internal static bool LetGameCompare(IEntity first, IEntity second, ref int result)
    {
        bool firstHas, secondHas;
        try
        {
            firstHas = HasReader(first);
            secondHas = HasReader(second);
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report(Name, ex);
            return true;
        }
        if (!TargetSortRules.SkipsGame(firstHas, secondHas)) return true;
        result = TargetSortRules.GoneOrder(firstHas, secondHas);
        Count++;
        if (Count <= LineCap)
        {
            // The line never decides the result: a failure here is only reported.
            try { Note(firstHas ? "b" : "a", firstHas ? second : first); }
            catch (Exception ex) { ApocalypseHooks.Report(Name, ex); }
        }
        return false;
    }

    private static bool HasReader(IEntity entity) => entity != null && CoreGame.Component<AffecterComponent>(entity) != null;

    private static void Note(string side, IEntity entity)
    {
        var guid = entity != null ? entity.Guid.ToString("D") : "-";
        var key = entity?.TryCast<UnitEntity>()?.Key ?? "-";
        var tick = CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
        DelversHost.Info(TargetSortRules.Line(side, guid, key, tick, Count));
    }
}

// No [HarmonyPatch]: TargetSortGuard puts it on by hand. The lambda's parameters are a and b (catalog).
internal static class TargetSortGuardPatch
{
    private static bool Prefix(IEntity a, IEntity b, ref int __result)
        => TargetSortGuard.LetGameCompare(a, b, ref __result);
}
