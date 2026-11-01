using HarmonyLib;
#if BEPINEX
using CombatUtility = global::Refactor.Combat.CombatUtility;
using VinesDelegatedRoutine = global::Refactor.Combat.SkillRelated.SKILL_ClingingVines._DelegatedRoutine_d__2;
#else
using CombatUtility = Il2CppRefactor.Combat.CombatUtility;
using VinesDelegatedRoutine = Il2CppRefactor.Combat.SkillRelated.SKILL_ClingingVines._DelegatedRoutine_d__2;
#endif

namespace DungeonSettlersDelvers.Core;

// The forest bandits' healer casts the game's own vines (SKILL_ClingingVines: row, icon, look and sound stay), but for a
// Core bandit Core narrows the targets to the caster's real foes (the vines root only enemies of
// the herbalist, never herself or her group; like BanditSlowRing, only the execution changes for bandit casters).
// Why: the delegated routine hits the units on a line cell (and per the catalog's estimate other entities too) for which EntityExtensions.IsHostile(context.User, unit)
// holds (DelegatedRoutine), IsHostile answers true for an Enemy caster against Player,
// Ally and Enemy alike (EntityExtensions.IsHostile), and the line starts on the caster's own cell
// (GetTilesInDirection, CoreLineRules.DirectionTiles). She rooted herself in 6 of 6 casts.
// State 0 builds the cell's target list (<targets>5__2) and yields a delay; state 1 walks it. Before state 1 runs, every
// unit that is no player or ally unit leaves the list (GroupAiRules.CoreVinesHits). Player casters are not touched.
// One routine per cell of the line: the skill's routine counts the line once from where the caster stood at the cast and starts
// a delegated routine per index; each delegated one reads the line again in its state 0, from where the caster stands then, and
// takes its cell by that index (List.get_Item in its MoveNext). A caster moved in between (pushed, pulled, its own step)
// can stand on a shorter line, and the cell's index lies beyond it: ArgumentOutOfRange in the routine (B, seven
// segments of one cast in a row). For a Core caster the segment reads the same line first and ends quietly when its index is
// no longer on it (SegmentGone); the cells still on the line grow as the game grows them.
internal static class BanditVines
{
    private static int lines;

    // Prefix of the routine's MoveNext.
    internal static void Narrow(VinesDelegatedRoutine routine)
    {
        if (routine == null || routine.__1__state != 1) return;
        var user = routine.context?.User?.TryCast<UnitEntity>();
        if (user == null || !CoreUnitRules.IsCoreUnit(user.Key)) return;
        var targets = routine._targets_5__2;
        if (targets == null) return;
        var skipped = 0;
        for (var i = targets.Count - 1; i >= 0; i--)
        {
            var target = targets[i];
            if (target != null && GroupAiRules.CoreVinesHits((int)target.Faction)) continue;
            targets.RemoveAt(i);
            skipped++;
        }
        if (skipped > 0 && lines++ < GroupAiBandits.LogCap)
            DelversHost.Info(GroupAiRules.VinesFilterLine(user.Guid.ToString(), skipped, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
    }

    // Before state 0 of a Core caster's segment: true when its cell is no longer on the line the game is about to read.
    internal static bool SegmentGone(VinesDelegatedRoutine routine)
    {
        if (routine == null || routine.__1__state != 0) return false;
        var context = routine.context;
        var user = context?.User?.TryCast<UnitEntity>();
        if (user == null || !CoreUnitRules.IsCoreUnit(user.Key)) return false;
        var inMap = routine.inMapData;
        var grid = context.TargetGrid;
        if (inMap == null || grid == null) return false;
        var at = inMap.GetLogicalPosition(user.Guid);
        var tiles = CombatUtility.GetTilesInDirection(inMap, at, grid.Position, GroupAiRules.VinesRange);
        var count = tiles?.Count ?? 0;
        if (!GroupAiRules.VinesSegmentGone(routine.index, count)) return false;
        if (shortLines++ < GroupAiBandits.LogCap)
            DelversHost.Info(GroupAiRules.VinesShortLine(user.Guid.ToString(), routine.index, count, at.x, at.y, grid.Position.x, grid.Position.y,
                CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
        return true;
    }

    private static int shortLines;

    internal static void Reset()
    {
        lines = 0;
        shortLines = 0;
    }
}

[HarmonyPatch(typeof(VinesDelegatedRoutine), "MoveNext")]
internal static class BanditVinesPatch
{
    private static bool Prefix(VinesDelegatedRoutine __instance, ref bool __result)
    {
        if (!CoreUnits.Active) return true;
        try
        {
            if (BanditVines.SegmentGone(__instance))
            {
                __instance.__1__state = -1;
                __result = false;
                return false;
            }
            BanditVines.Narrow(__instance);
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("bandit-vines", ex);
        }
        return true;
    }
}
