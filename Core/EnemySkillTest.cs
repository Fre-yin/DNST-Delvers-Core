using HarmonyLib;
using UnityEngine;
#if BEPINEX
using CombatPattern = global::Refactor.Combat.CombatPattern;
using GridSelector = global::GridTargetSelector;
using TargetType = global::Refactor.Combat.TargetType;
#else
using CombatPattern = Il2CppRefactor.Combat.CombatPattern;
using GridSelector = Il2Cpp.GridTargetSelector;
using TargetType = Il2CppRefactor.Combat.TargetType;
#endif

namespace DungeonSettlersDelvers.Core;

// EnemySkillTestRules in the game. Enemies decide through their pattern class (CombatPattern.Pattern, overridden per pattern;
// DecideNonPlayerCombatBehaviour calls CombatPattern.Decide, which calls it). Core patches the Pattern method of exactly the
// class the enemy uses, on the first test call for it, never before: a postfix of its own (EnemySkillTestPatch), built like
// the bandits' CoreUnitPatternPatch and running beside it for a bandit, then returns UseSkillTo(skill, target) once for that
// unit. Test campaigns only.
internal static class EnemySkillTest
{
    private sealed class Request
    {
        internal string Skill;
        internal string Target;
        internal Guid Unit;
        internal (int X, int Y)? Cell;
    }

    private static readonly Dictionary<string, Request> requests = new(StringComparer.Ordinal);
    private static readonly HashSet<string> patched = new(StringComparer.Ordinal);

    internal static void Reset() => requests.Clear();

    internal static bool Pending(string unit) => requests.Count != 0 && requests.ContainsKey(unit);

    // The API call: checks what it can now, hooks the enemy's pattern class and queues the request for its next decision.
    internal static string Queue(string unitGuid, string skill, string target)
    {
        if (!Guid.TryParse(unitGuid, out var guid)) return "nounit";
        var unit = CoreGame.FindUnit(guid);
        if (unit == null || unit.IsDead || unit.Faction == FactionType.Player) return "nounit";
        if (string.IsNullOrEmpty(skill) || DataSheetManager.Instance?.GetSkillData(skill) == null) return "noskill";
        if (!EnemySkillTestRules.TryParseTarget(target, out var targetUnit, out var cell)) return "notarget";
        var hook = Hook(unit);
        if (hook != null) return hook;
        requests[unit.Guid.ToString()] = new Request { Skill = skill, Target = target, Unit = targetUnit, Cell = cell };
        return "queued";
    }

    // The enemy's pattern class, patched once. Null when done, else the reason.
    private static string Hook(UnitEntity unit)
    {
        var key = DataSheetManager.Instance?.GetUnitData(unit.Key)?.CombatPattern;
        var pattern = string.IsNullOrEmpty(key) ? null : CombatDataManager.Instance?.GetPatternData(key);
        if (pattern == null) return "nopattern";
        var name = pattern.GetIl2CppType()?.FullName;
        if (string.IsNullOrEmpty(name)) return "nopattern";
        if (patched.Contains(name)) return null;
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false) ?? a.GetType("Il2Cpp" + name, false)).FirstOrDefault(t => t != null);
        var method = type == null ? null : AccessTools.DeclaredMethod(type, "Pattern");
        if (method == null) return "nohook";
        try
        {
            new Harmony(DelversCoreRuntime.BepInExPluginId + ".enemy-skill-test").Patch(method,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(EnemySkillTestPatch), nameof(EnemySkillTestPatch.Postfix))));
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_TEST_ENEMY_SKILL_HOOK_FAILED pattern=" + name + ": " + ex.Message);
            return "nohook";
        }
        patched.Add(name);
        DelversHost.Info("CORE_TEST_ENEMY_SKILL_HOOK pattern=" + name);
        return null;
    }

    // The enemy's decision: its request, once, instead of what the pattern chose.
    internal static void OnPattern(CombatPattern pattern, UnitEntity unit, IInMapDataSupplier inMap, ref CombatPattern.CombatBehaviour result)
    {
        if (unit == null || inMap == null || requests.Count == 0 || !requests.Remove(unit.Guid.ToString(), out var request)) return;
        var skills = CoreGame.Component<SkillComponent>(unit.Cast<IEntity>());
        var data = DataSheetManager.Instance?.GetSkillData(request.Skill);
        var hasSkill = data != null && skills != null && skills.HasSkill(request.Skill);
        var cooldown = hasSkill && skills.HasCooldown(request.Skill);
        var targetType = data != null ? data.TargetType : TargetType.None;
        var skillOnCell = CoreUnitRules.AimsAtCell((int)targetType);
        var aim = EnemySkillTestRules.AimOf(CoreUnitRules.AimsAtSelf((int)targetType), request.Unit, request.Cell);
        IEntity aimed = null;
        Vector2Int at = default;
        var gridFound = false;
        if (aim == EnemySkillTestRules.Aim.Unit)
        {
            aimed = CoreGame.FindUnit(request.Unit)?.Cast<IEntity>();
            if (aimed != null) at = inMap.GetLogicalPosition(aimed.Guid);
        }
        else if (aim == EnemySkillTestRules.Aim.Cell)
        {
            at = new Vector2Int(request.Cell.Value.X, request.Cell.Value.Y);
            // The grid is looked up for skills on cells only; a charge takes the unit standing on the cell.
            gridFound = skillOnCell && inMap.GetGrid(at) != null;
            if (gridFound && EnemySkillTestRules.AimsAtUnit(request.Skill))
                aimed = CoreGame.MapUnits().FirstOrDefault(u => u.Guid != unit.Guid && inMap.GetLogicalPosition(u.Guid) == at)?.Cast<IEntity>();
            // A skill on units at a cell: the unit standing there, else the building (a seed for its upgrade).
            if (!skillOnCell)
                aimed = CoreGame.MapUnits().FirstOrDefault(u => u.Guid != unit.Guid && inMap.GetLogicalPosition(u.Guid) == at)?.Cast<IEntity>()
                    ?? BuildingAt(inMap, at);
        }
        var hasTarget = EnemySkillTestRules.HasTarget(aim, skillOnCell, request.Skill, gridFound, aimed != null);
        var from = inMap.GetLogicalPosition(unit.Guid);
        var inRange = aim == EnemySkillTestRules.Aim.Self || EnemySkillTestRules.InRange(from.x, from.y, at.x, at.y, data?.Range ?? 0);
        var onUnit = aimed != null && EnemySkillTestRules.AimsAtUnit(request.Skill);
        var forcedOnCell = EnemySkillTestRules.ForcedOnCell(aim, skillOnCell, onUnit, CoreUnitCombat.IsForced(unit));
        var unitTarget = aimed != null && EnemySkillTestRules.SelectorFor(aim, skillOnCell, onUnit) == EnemySkillTestRules.SelectorKind.Unit;
        var outcome = EnemySkillTestRules.Outcome(hasSkill, cooldown, hasTarget, inRange, forcedOnCell, unitTarget);
        DelversHost.Info(EnemySkillTestRules.Line(unit.Guid.ToString(), request.Skill, request.Target, outcome));
        if (!EnemySkillTestRules.Casts(outcome)) return;
        ICombatTargetSelector selector;
        switch (EnemySkillTestRules.SelectorFor(aim, skillOnCell, onUnit))
        {
            case EnemySkillTestRules.SelectorKind.Self:
                selector = EntityTargetSelector.Self().Cast<ICombatTargetSelector>();
                break;
            case EnemySkillTestRules.SelectorKind.Cell:
                selector = GridSelector.SpecificGrid(inMap.GetGrid(at)).Cast<ICombatTargetSelector>();
                break;
            default:
                selector = EntityTargetSelector.SpecificTarget(aimed).Cast<ICombatTargetSelector>();
                break;
        }
        result = pattern.UseSkillTo(request.Skill, selector);
    }

    private static IEntity BuildingAt(IInMapDataSupplier inMap, Vector2Int at)
    {
        var buildings = inMap.GetCombatableBuildingsOnMap();
        for (var i = 0; buildings != null && i < buildings.Count; i++)
        {
            var building = buildings[i];
            if (building != null && !building.IsDead && inMap.GetLogicalPosition(building.Guid) == at) return building.Cast<IEntity>();
        }
        return null;
    }
}

// Applied at run time by EnemySkillTest.Hook to one pattern class each, never by the attribute scan. Arguments by name.
internal static class EnemySkillTestPatch
{
    internal static void Postfix(CombatPattern __instance, UnitEntity unit, IInMapDataSupplier inMapData, ref CombatPattern.CombatBehaviour __result)
    {
        try { EnemySkillTest.OnPattern(__instance, unit, inMapData, ref __result); }
        catch (Exception ex) { ApocalypseHooks.Report("enemy-skill-test", ex); }
    }
}
