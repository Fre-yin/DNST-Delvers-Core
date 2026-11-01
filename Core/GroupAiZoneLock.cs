using HarmonyLib;
#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using SetSkillMoveTarget = global::Refactor.Task.SetSkillMoveTarget;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using SetSkillMoveTarget = Il2CppRefactor.Task.SetSkillMoveTarget;
#endif

namespace DungeonSettlersDelvers.Core;

// ("AI lock + net"; after the dodge Core's next choice, a melee skill or Zoltraak on the knight,
// made the game walk the party back into the phantom chain before its final blast). At the target of a party unit's skill
// (DecideTarget): while an enemy area holds and the unit stands outside it, a target it would have to walk into the area for is
// swapped for one in reach from its own cell, of the same side. Without one the choice stays and the line says so (SKIP
// reason=zone); a unit that walks in after all goes out again (GroupAiZones.WatchReturns). Self skills, targets in reach and
// units inside an area (the dodge handles them) stay as they are. The game's paths are not touched.
internal static class GroupAiZoneLock
{
    private const int LogCap = 400;
    private static int lines;
    private static readonly Dictionary<string, string> lastLogged = new(StringComparer.Ordinal);
    // Per unit: its last target choice that found nothing in reach (skill, target, end of the zone its way crosses).
    private static readonly Dictionary<string, (string Skill, string Target, float Until)> skipped = new(StringComparer.Ordinal);
    private static readonly HashSet<string> holdLogged = new(StringComparer.Ordinal);

    internal static void Reset()
    {
        lines = 0;
        lastLogged.Clear();
        skipped.Clear();
        holdLogged.Clear();
    }

    internal static IEntity Retarget(IEntity owner, ActiveSkill_Base skill, IEntity target, IInMapDataSupplier inMap)
    {
        if (owner == null) return null;
        // Every new target choice replaces the last one: only the one that found nothing stays held (ShouldHold).
        skipped.Remove(owner.Guid.ToString());
        if (GroupAiZones.AreaZones == 0 || skill == null || target == null || inMap == null) return null;
        if (target.Guid == owner.Guid) return null;
        var from = inMap.GetLogicalPosition(owner.Guid);
        if (GroupAiZones.InEnemyAreaCell(from.x, from.y)) return null;
        var range = DataSheetManager.Instance?.GetSkillData(skill.Key)?.Range ?? 1;
        var to = inMap.GetLogicalPosition(target.Guid);
        if (!GroupAiAreaRules.ApproachEntersZone(from.x, from.y, to.x, to.y, range, GroupAiZones.InEnemyAreaCell)) return null;
        var hostile = target.Faction != owner.Faction;
        var candidates = new List<UnitEntity>();
        var spots = new List<(int X, int Y)>();
        foreach (var unit in hostile ? CoreGame.MapUnits() : CoreGame.PlayerUnits(false))
        {
            if (unit == null || unit.IsDead || unit.Guid == owner.Guid) continue;
            if (hostile ? unit.Faction != FactionType.Enemy : unit.Faction != owner.Faction) continue;
            var at = inMap.GetLogicalPosition(unit.Guid);
            candidates.Add(unit);
            spots.Add((at.x, at.y));
        }
        var nearest = GroupAiZoneRules.NearestInReach(from.x, from.y, range, spots);
        UnitEntity best = nearest < 0 ? null : candidates[nearest];
        var guid = owner.Guid.ToString();
        var swapped = best?.Cast<IEntity>();
        if (swapped == null)
            skipped[guid] = (skill.Key, target.Guid.ToString(), GroupAiZones.UntilCrossed(GroupAiAreaRules.Approach(from.x, from.y, to.x, to.y, range)));
        Log(guid, GroupAiAreaRules.LockLine(guid, GroupAiRules.Short(skill.Key), target.Guid.ToString(),
            swapped != null ? swapped.Guid.ToString() : "-", swapped != null ? "swap" : "skip reason=zone"));
        return swapped;
    }

    // At the game's skill move step (SetSkillMoveTarget.ShouldCancelSkill): true makes the game take its own cancel branch (OnUpdate:
    // CombatBehaviourApplyData(owner, null) through EntityBrain.ApplyData, then Failure, the way it ends a skill whose target left
    // its reach), so the unit takes no step and decides anew. Only for a party unit whose last choice found nothing in reach, while
    // the crossed zone holds and its way still crosses it; not while a player's order goes first (GroupAiStance.PlayerQuiet).
    internal static bool ShouldHold(IEntity owner)
    {
        if (owner == null || GroupAiZones.AreaZones == 0) return false;
        var guid = owner.Guid.ToString();
        if (!skipped.TryGetValue(guid, out var s)) return false;
        var now = GameClock.Now;
        if (GroupAiZoneRules.HoldExpired(now, s.Until))
        {
            skipped.Remove(guid);
            return false;
        }
        var unit = owner.TryCast<UnitEntity>();
        if (unit == null || !GroupAiZoneRules.MayHold(unit.Faction == FactionType.Player, GroupAiExcluded.Has(guid), GroupAiGate.OpenFor(unit), GroupAiStance.PlayerQuiet(guid)))
            return false;
        var inMap = CoreGame.InMap;
        var target = Guid.TryParse(s.Target, out var targetGuid) ? CoreGame.FindUnit(targetGuid) : null;
        if (inMap == null || target == null || target.IsDead) return false;
        var from = inMap.GetLogicalPosition(owner.Guid);
        var to = inMap.GetLogicalPosition(target.Guid);
        var range = DataSheetManager.Instance?.GetSkillData(s.Skill)?.Range ?? 1;
        var enters = GroupAiAreaRules.ApproachEntersZone(from.x, from.y, to.x, to.y, range, GroupAiZones.InEnemyAreaCell);
        if (!GroupAiAreaRules.HoldApplies(true, now, s.Until, enters)) return false;
        if (holdLogged.Add(guid + "|" + s.Until.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)) && lines++ < LogCap)
            DelversHost.Info(GroupAiAreaRules.HoldLine(guid, GroupAiRules.Short(s.Skill), s.Target, s.Until));
        return true;
    }

    // Once per unit and line: the same choice comes back at every decision.
    private static void Log(string unit, string line)
    {
        if (lastLogged.TryGetValue(unit, out var last) && last == line) return;
        lastLogged[unit] = line;
        if (lines++ < LogCap) DelversHost.Info(line);
    }
}

// The hold at the game's skill move step: ShouldCancelSkill is the game's own test whether the chosen skill still makes sense;
// Core only adds GroupAiZoneLock.ShouldHold, the rest of OnUpdate runs as the game wrote it. Arguments by name.
[HarmonyPatch(typeof(SetSkillMoveTarget), "ShouldCancelSkill")]
internal static class GroupAiZoneHoldPatch
{
    private static void Postfix(SetSkillMoveTarget __instance, ref bool __result)
    {
        if (__result || GroupAiZones.AreaZones == 0) return;
        try
        {
            if (GroupAiZoneLock.ShouldHold(__instance._owner)) __result = true;
        }
        catch (Exception ex) { ApocalypseHooks.Report("zone-hold", ex); }
    }
}
