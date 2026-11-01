using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// Where the forest bandits' camp may stand on a generated floor, pure so the
// offline tests cover it. The floor is the game's room graph: rooms and corridors are nodes, a room
// links only to corridors and a corridor to its two rooms (DungeonPreRoomData.ConnectedRooms).
// - The mandatory path runs from the entry room (type 0) to the portal room (type 1); a node lies on
//   it when it lies on a shortest path: dist(entry, x) + dist(x, portal) = dist(entry, portal).
// - Candidates are combat rooms with random spawns and at least MinEnemyCells monster cells. Never:
//   an elite room, a room holding the Alpha Acid Slogel, a quest room (NPCSpawnConditionTable), a
//   room one corridor away from the Alpha's room, a room on the mandatory path.
// - Rank 1: dead end (one corridor) whose corridor is at least longCorridor tiles long (0: every
//   dead end); rank 2: the other dead ends; rank 3: side rooms off the path. The pick is the lowest
//   rank, then the longest and narrowest access corridor, then a hash of seed and room id.
// - A graph without a single room-corridor link, or without a way from the entry to the portal, is no
//   result (Problem "empty" or "no-path"): no room is picked (the first probe in the game
//   saw the rooms without their corridors).
// BanditCampProbe logs it; step 2 of the camp places the bandits with the same rules.
internal static class BanditCampRules
{
    // The game's RoomType values.
    internal const int EntryType = 0;
    internal const int PortalType = 1;
    internal const int CombatType = 2;
    internal const int EliteType = 96;
    internal const int CorridorType = 99;

    internal const int MinEnemyCells = 4;
    internal const string RandomSpawn = "RANDOMSPAWN";
    internal const string AlphaKey = "UNIT_AlphaAcidSlogel";

    internal sealed class Node
    {
        internal string Id = "";
        internal string Key = "";
        internal int Type;
        internal List<string> Links = new();
        internal List<(int X, int Y)> Cells = new();
        internal string[] Monsters = Array.Empty<string>();
        internal int EnemyCells;
    }

    internal sealed class Room
    {
        internal Node Node;
        internal int Degree;
        internal bool OnPath;
        // Null for a candidate, else why the room cannot hold the camp.
        internal string Reason;
        internal int Rank;
        internal string CorridorKey = "-";
        internal int CorridorLength;
        internal int CorridorWidth;
    }

    internal sealed class Result
    {
        internal string Entry;
        internal string Portal;
        internal int PathLength = -1;
        internal int Corridors;
        internal string Alpha;
        internal List<Room> Rooms = new();
        internal Room Picked;
        internal int Candidates;
        // The rooms' corridor links together.
        internal int Links;
        // Null for a usable graph, else "empty" or "no-path"; then nothing is picked.
        internal string Problem;
    }

    // Length (longer side) and width (shorter side) of the cells' bounding box.
    internal static (int Length, int Width) Size(List<(int X, int Y)> cells)
    {
        if (cells == null || cells.Count == 0) return (0, 0);
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var (x, y) in cells)
        {
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }
        var w = maxX - minX + 1;
        var h = maxY - minY + 1;
        return (Math.Max(w, h), Math.Min(w, h));
    }

    // allowMainPath, only for the seed scan's widest measure: rooms on the mandatory path may anchor the camp too (it stays a leaf).
    internal static Result Evaluate(IReadOnlyList<Node> nodes, ICollection<string> questRoomKeys, int longCorridor, int seed, bool allowMainPath = false)
    {
        var result = new Result();
        var byId = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var node in nodes)
            if (!string.IsNullOrEmpty(node.Id)) byId[node.Id] = node;
        foreach (var node in nodes)
        {
            if (node.Type == EntryType && result.Entry == null) result.Entry = node.Id;
            if (node.Type == PortalType && result.Portal == null) result.Portal = node.Id;
            if (node.Type == CorridorType) result.Corridors++;
            if (result.Alpha == null && node.Type != CorridorType && HoldsAlpha(node)) result.Alpha = node.Id;
        }
        var fromEntry = Distances(byId, result.Entry);
        var fromPortal = Distances(byId, result.Portal);
        if (result.Portal != null && fromEntry.TryGetValue(result.Portal, out var pathLength)) result.PathLength = pathLength;
        var alphaNeighbours = Neighbours(byId, result.Alpha);
        result.Problem = ProblemOf(nodes, byId, result);
        foreach (var node in nodes)
        {
            if (node.Type == CorridorType) continue;
            var room = new Room { Node = node };
            foreach (var link in node.Links)
                if (byId.TryGetValue(link, out var other) && other.Type == CorridorType)
                {
                    room.Degree++;
                    var (length, width) = Size(other.Cells);
                    if (length > room.CorridorLength || (length == room.CorridorLength && width < room.CorridorWidth))
                    {
                        room.CorridorKey = string.IsNullOrEmpty(other.Key) ? other.Id : other.Key;
                        room.CorridorLength = length;
                        room.CorridorWidth = width;
                    }
                }
            result.Links += room.Degree;
            room.OnPath = result.PathLength >= 0 && fromEntry.TryGetValue(node.Id, out var a) && fromPortal.TryGetValue(node.Id, out var b)
                && a + b == result.PathLength;
            // A room that would be a candidate on an unusable graph: "graph".
            room.Reason = ReasonOf(node, room.OnPath && !allowMainPath, node.Id == result.Alpha, alphaNeighbours.Contains(node.Id), questRoomKeys)
                ?? (result.Problem != null ? "graph" : null);
            if (room.Reason == null)
            {
                room.Rank = room.Degree != 1 ? 3 : longCorridor <= 0 || room.CorridorLength >= longCorridor ? 1 : 2;
                result.Candidates++;
            }
            result.Rooms.Add(room);
        }
        foreach (var room in result.Rooms)
        {
            if (room.Reason != null) continue;
            if (result.Picked == null || Better(room, result.Picked, seed)) result.Picked = room;
        }
        return result;
    }

    // Why the graph cannot place a camp, or null: "empty" when no room links to a corridor, "no-path"
    // when the portal cannot be reached from the entry (the mandatory path is unknown then).
    private static string ProblemOf(IReadOnlyList<Node> nodes, Dictionary<string, Node> byId, Result result)
    {
        foreach (var node in nodes)
        {
            if (node.Type == CorridorType) continue;
            foreach (var link in node.Links)
                if (byId.TryGetValue(link, out var other) && other.Type == CorridorType)
                    return result.Entry == null || result.Portal == null || result.PathLength < 0 ? "no-path" : null;
        }
        return "empty";
    }

    // Why a room cannot hold the camp, or null. The fixed exclusions come first, then the camp's needs.
    internal static string ReasonOf(Node node, bool onPath, bool alpha, bool alphaNeighbour, ICollection<string> questRoomKeys)
    {
        if (node.Type == EliteType) return "elite";
        if (alpha || HoldsAlpha(node)) return "alpha";
        if (questRoomKeys != null && !string.IsNullOrEmpty(node.Key) && questRoomKeys.Contains(node.Key)) return "quest";
        if (alphaNeighbour) return "neighbour";
        if (onPath) return "mainpath";
        if (node.Type != CombatType) return "type";
        if (node.Monsters.Length != 1 || node.Monsters[0] != RandomSpawn) return "fixed";
        // -1: the cells could not be counted; the probe keeps such a room rather than hide it.
        if (node.EnemyCells >= 0 && node.EnemyCells < MinEnemyCells) return "small";
        return null;
    }

    // Whether the room's table row names the Alpha Acid Slogel.
    internal static bool HoldsAlpha(Node node) => Array.IndexOf(node.Monsters, AlphaKey) >= 0;

    internal static bool Better(Room room, Room best, int seed)
    {
        if (room.Rank != best.Rank) return room.Rank < best.Rank;
        if (room.CorridorLength != best.CorridorLength) return room.CorridorLength > best.CorridorLength;
        if (room.CorridorWidth != best.CorridorWidth) return room.CorridorWidth < best.CorridorWidth;
        var mine = TieHash(seed, room.Node.Id);
        var theirs = TieHash(seed, best.Node.Id);
        return mine != theirs ? mine < theirs : string.CompareOrdinal(room.Node.Id, best.Node.Id) < 0;
    }

    // FNV-1a over the seed and the room id: the same floor always picks the same room.
    internal static uint TieHash(int seed, string id)
    {
        var hash = 2166136261u;
        unchecked
        {
            foreach (var b in BitConverter.GetBytes(seed)) hash = (hash ^ b) * 16777619u;
            foreach (var c in id ?? "") hash = (hash ^ c) * 16777619u;
        }
        return hash;
    }

    // Steps from the start node to every node it reaches, through rooms and corridors alike.
    private static Dictionary<string, int> Distances(Dictionary<string, Node> byId, string start)
    {
        var distances = new Dictionary<string, int>(StringComparer.Ordinal);
        if (start == null || !byId.ContainsKey(start)) return distances;
        var queue = new Queue<string>();
        distances[start] = 0;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            foreach (var link in byId[id].Links)
            {
                if (!byId.ContainsKey(link) || distances.ContainsKey(link)) continue;
                distances[link] = distances[id] + 1;
                queue.Enqueue(link);
            }
        }
        return distances;
    }

    // Rooms one corridor away from the given room (room → corridor → room).
    private static HashSet<string> Neighbours(Dictionary<string, Node> byId, string room)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (room == null || !byId.TryGetValue(room, out var node)) return result;
        foreach (var link in node.Links)
        {
            if (!byId.TryGetValue(link, out var corridor) || corridor.Type != CorridorType) continue;
            foreach (var other in corridor.Links)
                if (other != room) result.Add(other);
        }
        return result;
    }

    // ---- log lines (CORE_BANDITS_GRAPH*) ----

    // Ids and keys without spaces, so a reader can split a line at spaces: the game's "Combat - X_5"
    // becomes "Combat:X_5", any other blank "_".
    internal static string LogId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "-";
        var chars = id.Replace(" - ", ":").ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (char.IsWhiteSpace(chars[i])) chars[i] = '_';
        return new string(chars);
    }

    private static string LogIdOrNone(string id) => id == null ? "none" : LogId(id);

    internal static string HeaderLine(Result r, int region, int floor, int phase, int seed, int longCorridor)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_BANDITS_GRAPH region={0} floor={1} phase={2} seed={3} rooms={4} corridors={5} entry={6} portal={7} mainpath={8} candidates={9} picked={10} rank={11} corridor={12} long={13}",
            region, floor, phase, seed, r.Rooms.Count, r.Corridors, LogIdOrNone(r.Entry), LogIdOrNone(r.Portal), r.PathLength,
            r.Candidates, LogIdOrNone(r.Picked?.Node.Id), r.Picked?.Rank ?? 0,
            r.Picked == null ? "-" : CorridorText(r.Picked), longCorridor);

    // An unusable graph (Result.Problem): nothing was picked on this floor.
    internal static string FailedLine(Result r, int seed)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_BANDITS_GRAPH_FAILED reason={0} seed={1} rooms={2} corridors={3} links={4} entry={5} portal={6} mainpath={7}",
            r.Problem ?? "none", seed, r.Rooms.Count, r.Corridors, r.Links, LogIdOrNone(r.Entry), LogIdOrNone(r.Portal), r.PathLength);

    internal static string RoomLine(int seed, Room room)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_BANDITS_GRAPH_ROOM seed={0} id={1} key={2} type={3} degree={4} onPath={5} enemyCells={6} reason={7} rank={8} corridor={9}",
            seed, LogId(room.Node.Id), LogId(room.Node.Key), room.Node.Type, room.Degree,
            room.OnPath ? "true" : "false", room.Node.EnemyCells, room.Reason ?? "candidate", room.Rank, CorridorText(room));

    internal static string CorridorLine(int seed, int phase, string key, int length, int width, int count)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_BANDITS_GRAPH_CORRIDOR seed={0} phase={1} key={2} size={3}x{4} count={5}", seed, phase, LogId(key), length, width, count);

    // ---- the camp with its own corridor, step 1: templates and seed scan (BanditCampTemplates) ----

    private static readonly string[] DoorModes = { "simple", "manual", "hybrid" };
    private static readonly string[] RepeatModes = { "allow", "noimmediate", "norepeat" };

    // Edgar's DoorsGrid2D.DoorMode and RoomTemplateRepeatMode; "-" when unknown.
    internal static string DoorModeName(int mode) => mode >= 0 && mode < DoorModes.Length ? DoorModes[mode] : "-";

    internal static string RepeatModeName(int mode) => mode >= 0 && mode < RepeatModes.Length ? RepeatModes[mode] : "-";

    // lines: the door lines Edgar sees on the template's outline; starts: the places a door can start on
    // them (each line's length + 1); -1 when unknown.
    internal static string TemplateLine(int phase, int type, string key, int weight, int length, int width, int repeat, int doorMode,
        int doorLines, int doorStarts, int doorLength)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_BANDITS_TEMPLATE phase={0} type={1} key={2} weight={3} size={4}x{5} repeat={6} doors={7} lines={8} starts={9} doorLength={10}",
            phase, type, LogId(key), weight, length, width, RepeatModeName(repeat), DoorModeName(doorMode), doorLines, doorStarts, doorLength);

    internal static string LevelGraphLine(int phase, string name, float weight, int rooms, int connections, IDictionary<string, int> types)
    {
        var parts = new List<string>();
        if (types != null)
            foreach (var pair in types) parts.Add(pair.Key + pair.Value.ToString(CultureInfo.InvariantCulture));
        return string.Format(CultureInfo.InvariantCulture, "CORE_BANDITS_LEVELGRAPH phase={0} name={1} weight={2:0.##} rooms={3} connections={4} types={5}",
            phase, LogId(name), weight, rooms, connections, parts.Count == 0 ? "-" : string.Join("+", parts));
    }

    // One seed of the scan; the graph of a generated floor as the camp rules see it, "-" without one.
    // The game's result of a seed scan is final once it is new and succeeded, counted attempts or names a fail
    // reason; at the start of a generation the game puts an empty one there (GenerateInternal).
    internal static bool ScanResultFinal(bool changed, bool succeeded, int attempts, int failReason)
        => changed && (succeeded || attempts > 0 || failReason != 0);

    internal static string SeedScanLine(int seed, bool ok, string reason, int used, int attempts, Result graph, double seconds)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_BANDITS_SEEDSCAN seed={0} ok={1} reason={2} used={3} attempts={4} rooms={5} corridors={6} mainpath={7} candidates={8} seconds={9:0.0}",
            seed, ok ? 1 : 0, LogId(reason), used, attempts,
            graph == null ? "-" : graph.Rooms.Count.ToString(CultureInfo.InvariantCulture),
            graph == null ? "-" : graph.Corridors.ToString(CultureInfo.InvariantCulture),
            graph == null ? "-" : graph.PathLength.ToString(CultureInfo.InvariantCulture),
            graph == null ? "-" : graph.Candidates.ToString(CultureInfo.InvariantCulture), seconds);

    internal sealed class SeedScanSummary
    {
        internal int Count;
        internal int Ok;
        internal int Graphs;
        internal int PathSum;
        internal int WithCandidates;
        internal readonly SortedDictionary<string, int> Reasons = new(StringComparer.Ordinal);
        // Whether the camp would come on each generated floor under the rule in use (BanditCampBuilder.Measure): "placed" or the
        // reason it would not.
        internal readonly SortedDictionary<string, int> Camp = new(StringComparer.Ordinal);

        internal void AddCamp(string reason) => Tally(Camp, reason ?? CampPlaced);

        private static void Tally(SortedDictionary<string, int> counts, string key) => counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;

        internal void Add(bool ok, string reason, Result graph)
        {
            Count++;
            if (ok) Ok++;
            else
            {
                var key = LogId(reason);
                Reasons[key] = Reasons.TryGetValue(key, out var n) ? n + 1 : 1;
            }
            if (graph == null) return;
            Graphs++;
            PathSum += Math.Max(0, graph.PathLength);
            if (graph.Candidates > 0) WithCandidates++;
        }
    }

    internal const string CampPlaced = "placed";

    internal static string SeedScanDoneText(SeedScanSummary s)
    {
        var text = string.Format(CultureInfo.InvariantCulture, "count={0} ok={1} failed={2} reasons={3} meanMainpath={4} withCandidates={5}",
            s.Count, s.Ok, s.Count - s.Ok, CountsText(s.Reasons),
            s.Graphs == 0 ? "-" : ((double)s.PathSum / s.Graphs).ToString("0.0", CultureInfo.InvariantCulture), s.WithCandidates);
        return s.Camp.Count == 0 ? text : text + " camp=" + CountsText(s.Camp);
    }

    private static string CountsText(SortedDictionary<string, int> counts)
    {
        var parts = new List<string>();
        foreach (var pair in counts) parts.Add(pair.Key + ":" + pair.Value.ToString(CultureInfo.InvariantCulture));
        return parts.Count == 0 ? "-" : string.Join(",", parts);
    }

    private static string CorridorText(Room room)
        => room.Degree == 0 ? "-" : string.Format(CultureInfo.InvariantCulture, "{0}:{1}x{2}", LogId(room.CorridorKey), room.CorridorLength, room.CorridorWidth);
}
