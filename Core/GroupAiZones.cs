using System.Globalization;
using UnityEngine;
#if BEPINEX
using ExecutionArea = global::Refactor.Combat.ExecutionArea;
using MoveTask = global::Refactor.Task.MoveTask;
#else
using ExecutionArea = Il2CppRefactor.Combat.ExecutionArea;
using MoveTask = Il2CppRefactor.Task.MoveTask;
#endif

namespace DungeonSettlersDelvers.Core;

// Announced area skills: the cells an area skill will hit, marked for a short time per side
// ("players" for the party, "bandits" for Core's bandits). A unit of that side standing in another
// caster's zone steps out once per zone, to the nearest free cell (GroupAiRules.PickDodgeCell),
// through the game's own move order on the unit's tick queue (OrderMove). Also the area log of both AIs.
// The zones themselves are kept in the book of GroupAiZoneRules (marking, joining, expiry, lookups).
internal static partial class GroupAiZones
{
    internal const string Players = "players";
    internal const string Bandits = "bandits";
    private const int LogCap = 2000;

    private static readonly GroupAiZoneRules.Book book = new();
    private static readonly Dictionary<string, (string Text, float At)> lastArea = new(StringComparer.Ordinal);

    // The same area line of a caster and skill is logged at most once in this time.
    private const float AreaLogSeconds = 2f;

    private static int logLines;
    private static int warnings;

    internal static void Reset()
    {
        book.Reset();
        dodged.Clear();
        dodges.Clear();
        lastArea.Clear();
        logLines = warnings = 0;
    }

    // The cells the game's area of the skill covers, cast from user at the target cell.
    internal static List<Vector2Int> AreaCells(SkillTableData data, Vector2Int user, Vector2Int at)
    {
        try
        {
            var area = ExecutionArea.Create(data.AreaType).GetArea(user, at, data.AreaRadius);
            if (area == null || area.Count == 0) return null;
            var cells = new List<Vector2Int>();
            for (var i = 0; i < area.Count; i++) cells.Add(area[i]);
            return cells;
        }
        catch (Exception ex)
        {
            Warn("CORE_AI_ZONE_AREA_FAILED skill=" + data.Key + ": " + ex.Message);
            return null;
        }
    }

    // The positions and the eight cells around each, when the game names no area.
    internal static List<Vector2Int> AroundCells(List<Vector2Int> positions)
    {
        var cells = new List<Vector2Int>();
        foreach (var position in positions)
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                    cells.Add(new Vector2Int(position.x + dx, position.y + dy));
        return cells;
    }

    // The cells as (X, Y) for the rules of the zone book.
    internal static List<(int X, int Y)> Points(List<Vector2Int> cells)
    {
        var points = new List<(int X, int Y)>(cells.Count);
        foreach (var cell in cells) points.Add((cell.x, cell.y));
        return points;
    }

    internal static void Mark(string side, string caster, string skill, string flag, List<Vector2Int> cells, float until)
    {
        book.Mark(side, caster, skill, flag, Points(cells), until);
        Log($"CORE_AI_ZONE unit={caster} side={side} skill={GroupAiRules.Short(skill)} {flag} tiles={Tiles(cells)} until={until.ToString("0.0", CultureInfo.InvariantCulture)}");
    }

    // An enemy's area for the party, held until the game's execution ends (EndExecution) or Until at the latest.
    // True for a new zone, false when the execution joined the caster's zone of that area still held.
    internal static bool MarkArea(IntPtr exec, string caster, GroupAiAreaRules.Area area, List<Vector2Int> cells, float startedAt, (int X, int Y)? lane,
        float? lastHit = null, string execId = null)
        => book.MarkArea(Players, exec, caster, area, Points(cells), startedAt, lane, lastHit, execId);

    // Enemy areas held now; the ProcessFX hook leaves at once while this is 0.
    internal static int AreaZones => book.AreaZones;

    // One of an area's executions ended; the zone goes with its last one. True when the zone went.
    internal static bool EndExecution(IntPtr exec) => book.EndExecution(exec);

    internal static bool IsAreaExecution(IntPtr exec) => book.IsAreaExecution(exec);

    // The execution that opened the held zone of this caster (its CORE_AI_ZONE line), or null when none is held.
    internal static string ZoneExecOf(string caster) => book.ZoneExecOf(caster, GameClock.Now);

    // The latest end of the enemy areas that hold now and contain one of these cells, 0 when none does (GroupAiZoneLock).
    internal static float UntilCrossed(List<(int X, int Y)> cells) => book.UntilCrossed(cells, GameClock.Now);

    // Whether a cell lies in an enemy area that holds now (GroupAiZoneLock).
    internal static bool InEnemyAreaCell(int x, int y) => book.InEnemyAreaCell(x, y, GameClock.Now);

    // Whether a unit stands in an enemy area now (no bandage there).
    internal static bool InEnemyArea(UnitEntity unit, IInMapDataSupplier inMap)
    {
        if (unit == null || inMap == null || book.AreaZones == 0) return false;
        var at = inMap.GetLogicalPosition(unit.Guid);
        return book.InEnemyAreaCell(at.x, at.y, GameClock.Now);
    }

    internal static void LogArea(string side, string caster, SkillTableData data, Vector2Int from, Vector2Int at, int allies)
    {
        var cells = AreaCells(data, from, at);
        var text = $"CORE_AI_AREA unit={caster} skill={GroupAiRules.Short(data.Key)} from={from.x},{from.y} target={at.x},{at.y} cells={(cells == null ? "-" : Tiles(cells))} allies={allies.ToString(CultureInfo.InvariantCulture)} side={side}";
        var key = caster + "|" + data.Key;
        var now = GameClock.Now;
        if (lastArea.TryGetValue(key, out var last) && last.Text == text && !GameClockRules.Elapsed(now, last.At, AreaLogSeconds)) return;
        lastArea[key] = (text, now);
        Log(text);
    }

    private static string Tiles(List<Vector2Int> cells)
    {
        var seen = new HashSet<long>();
        var parts = new List<string>();
        foreach (var cell in cells)
            if (seen.Add(GroupAiRules.CellKey(cell.x, cell.y)))
                parts.Add(cell.x.ToString(CultureInfo.InvariantCulture) + "," + cell.y.ToString(CultureInfo.InvariantCulture));
        return string.Join("|", parts);
    }

    internal static void Clear(string caster, string skill) => book.Clear(caster, skill);

    // GroupAiBanditMoves.Order: a move order of the game for the unit.
    internal static bool OrderMove(UnitEntity unit, Vector2Int to, string reason)
        => unit != null && CoreGame.TryMapOfUnit(unit, out var map) && OrderMove(unit, map, to, reason);

    // (none of 106 bandit move orders arrived): DirectOrderRequested is
    // the player's click (Control.MoveOrder) and moves player units; the game moves its own NPCs with
    // UnitOrderRequested (EntityEventHandler: chase, patrol), which TaskSystem.NPCUnitOrder sets at once.
    // Every order is followed to its end (GroupAiMoveWatch).
    private static bool OrderMove(UnitEntity unit, MapType map, Vector2Int to, string reason)
    {
        var handler = CoreGame.TickHandler(map);
        if (handler == null) return false;
        var player = unit.Faction == FactionType.Player;
        var entity = unit.Cast<IEntity>();
        var task = new MoveTask(to);
        handler.EnqueueEvent(player
            ? new DirectOrderRequested(entity, task).Cast<IEventData>()
            : new UnitOrderRequested(entity, task).Cast<IEventData>());
        GroupAiMoveWatch.Start(unit, player, map, to, reason, player ? "direct" : "npc", task.Pointer);
        return true;
    }

    private static void Log(string line)
    {
        if (logLines++ < LogCap) DelversHost.Info(line);
    }

    private static void Warn(string line)
    {
        if (warnings++ < GroupAiRules.WarningCap) DelversHost.Warning(line);
    }
}
