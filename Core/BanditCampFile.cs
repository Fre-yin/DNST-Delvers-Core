using System.Text.Json;
using System.Text.Json.Nodes;

namespace DungeonSettlersDelvers.Core;

// Reads the bandit camp room file (format mt-raum, version 2, as the author's room editor writes it) into BanditCampRoom. Pure
// rules without the game: the text goes in, the model or the first reason it was refused comes out. The checks follow the
// toolkit's own room check (format, version, size, cells, objects inside the room); what only the toolkit's own trip needs
// (free start cells) does not apply. Cells[y][x] in the file: y = 0 the bottom row, x = 0 the left column; the model keeps
// Cells[x, y] with the same origin. The cell names are the editor's German ones.
internal static class BanditCampFile
{
    internal const string Format = "mt-raum";
    internal const int Version = 2;
    internal const int MaxSpawnName = 40;

    // The game builds a floor of 300 x 300 cells; the walls BanditCampLayout.Frame adds around the room take a roof ring on each
    // side and the rows of wall side above, so the room itself may be 296 x 294 at most.
    internal const int FloorSize = 300;
    internal const int MaxWidth = FloorSize - 2 * BanditCampLayout.RoofRing;
    internal const int MaxHeight = FloorSize - 2 * BanditCampLayout.RoofRing - BanditCampLayout.SideRows;

    private static readonly int[] Rotations = { 0, 90, 180, 270 };

    // The one table of cell names the camp knows (the editor has more kinds, such as rock and poison water; the camp is
    // drawn with these five).
    private static readonly (string Name, BanditCampCell Cell)[] CellKinds =
    {
        ("Leer", BanditCampCell.Void),
        ("Erdboden", BanditCampCell.Dirt),
        ("Gras", BanditCampCell.Grass),
        ("Hochgras", BanditCampCell.HighGrass),
        ("Wasser", BanditCampCell.Water),
    };

    // The room, or null with the first reason the text was refused.
    internal static bool TryRead(string json, out BanditCampRoom room, out string problem)
    {
        room = null;
        problem = Parse(json, out var root);
        if (problem == null) problem = Build(root, out room);
        return problem == null;
    }

    private static string Parse(string json, out JsonObject root)
    {
        root = null;
        try { root = JsonNode.Parse(json ?? "") as JsonObject; }
        catch (JsonException error) { return "not JSON: " + error.Message; }
        return root == null ? "not a JSON object" : null;
    }

    private static string Build(JsonObject root, out BanditCampRoom room)
    {
        room = null;
        var problem = HeaderProblem(root);
        if (problem != null) return problem;
        problem = ReadSize(root, out var width, out var height);
        if (problem != null) return problem;
        problem = ReadCells(root, width, height, out var cells);
        if (problem != null) return problem;
        problem = ReadList(root, "Buildings", width, height, ReadBuilding, out var buildings);
        if (problem != null) return problem;
        problem = ReadList(root, "Items", width, height, ReadItem, out var items);
        if (problem != null) return problem;
        problem = ReadList(root, "Spawns", width, height, ReadSpawn, out var spawns);
        if (problem != null) return problem;
        problem = ReadList(root, "Features", width, height, ReadFeature, out var features);
        if (problem != null) return problem;
        problem = ReadList(root, "Floors", width, height, ReadFloor, out var floors);
        if (problem != null) return problem;
        problem = ReadList(root, "FloorDecos", width, height, ReadFloor, out var floorDecos);
        if (problem != null) return problem;
        problem = ReadStart(root, width, height, out var startX, out var startY);
        if (problem != null) return problem;
        room = new BanditCampRoom(Text(root, "Name", out var name) ? name : "", cells, buildings, items, spawns, features, startX, startY,
            floors, floorDecos);
        return null;
    }

    private static string HeaderProblem(JsonObject root)
    {
        if (!Text(root, "Format", out var format) || format != Format) return $"format {format ?? "(none)"}, expected {Format}";
        if (!Int(root, "FormatVersion", out var version) || version != Version) return $"format version {(version == 0 ? "(none)" : version.ToString())}, expected {Version}";
        return null;
    }

    private static string ReadSize(JsonObject root, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (!Int(root, "Width", out width) || !Int(root, "Height", out height)) return "Width or Height missing";
        if (width < 1 || height < 1 || width > MaxWidth || height > MaxHeight) return $"size {width}x{height} outside 1x1 to {MaxWidth}x{MaxHeight}";
        return null;
    }

    private static string ReadCells(JsonObject root, int width, int height, out BanditCampCell[,] cells)
    {
        cells = null;
        if (root["Cells"] is not JsonArray rows || rows.Count != height) return $"cells do not fit {width}x{height}: need {height} rows";
        var result = new BanditCampCell[width, height];
        for (var y = 0; y < height; y++)
        {
            var problem = ReadRow(rows[y], y, width, result);
            if (problem != null) return problem;
        }
        cells = result;
        return null;
    }

    private static string ReadRow(JsonNode node, int y, int width, BanditCampCell[,] cells)
    {
        if (node is not JsonArray row || row.Count != width) return $"cells do not fit: row {y} needs {width} cells";
        for (var x = 0; x < width; x++)
        {
            var name = row[x] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "(not text)";
            if (!TryCell(name, out cells[x, y])) return $"unknown cell kind \"{name}\" at {x},{y}";
        }
        return null;
    }

    private static bool TryCell(string name, out BanditCampCell cell)
    {
        foreach (var kind in CellKinds)
        {
            if (kind.Name != name) continue;
            cell = kind.Cell;
            return true;
        }
        cell = BanditCampCell.Void;
        return false;
    }

    // Reads one entry of an object list: the object, or null with the reason in problem.
    private delegate T EntryReader<T>(JsonObject entry, int width, int height, out string problem);

    // One object list of the file; a list that is absent is empty, as the editor leaves it. An entry that is no object or
    // lacks a field is refused with its place in the list.
    private static string ReadList<T>(JsonObject root, string name, int width, int height, EntryReader<T> read, out List<T> result)
    {
        result = new List<T>();
        if (root[name] == null) return null;
        if (root[name] is not JsonArray entries) return name + " is not a list";
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index] is not JsonObject entry) return $"{name}[{index}] is not an object";
            var item = read(entry, width, height, out var problem);
            if (problem != null) return $"{name}[{index}]: {problem}";
            result.Add(item);
        }
        return null;
    }

    private static BanditCampBuilding ReadBuilding(JsonObject entry, int width, int height, out string problem)
    {
        problem = null;
        if (!Text(entry, "Key", out var key) || key.Length == 0 || !Int(entry, "X", out var x) || !Int(entry, "Y", out var y)
            || !Int(entry, "Rotation", out var rotation)) { problem = "Key, X, Y or Rotation missing"; return null; }
        if (!Inside(width, height, x, y)) { problem = Outside("building", key, x, y); return null; }
        if (Array.IndexOf(Rotations, rotation) < 0) { problem = $"building {key} at {x},{y} has rotation {rotation}, expected 0, 90, 180 or 270"; return null; }
        return new BanditCampBuilding(key, x, y, rotation, Text(entry, "Faction", out var faction) ? faction : "",
            entry["FlipX"] is JsonValue flip && flip.TryGetValue<bool>(out var flipX) && flipX);
    }

    private static BanditCampItem ReadItem(JsonObject entry, int width, int height, out string problem)
    {
        problem = null;
        if (!Text(entry, "Key", out var key) || key.Length == 0 || !Int(entry, "X", out var x) || !Int(entry, "Y", out var y))
        { problem = "Key, X or Y missing"; return null; }
        if (!Inside(width, height, x, y)) { problem = Outside("item", key, x, y); return null; }
        // The editor writes the count as Amount; absent it is one.
        var count = entry["Amount"] == null ? 1 : Int(entry, "Amount", out var amount) ? amount : 0;
        if (count < 1) { problem = $"item {key} at {x},{y} has an amount below 1"; return null; }
        return new BanditCampItem(key, x, y, count);
    }

    private static BanditCampFloor ReadFloor(JsonObject entry, int width, int height, out string problem)
    {
        problem = null;
        if (!Text(entry, "Key", out var key) || key.Length == 0 || !Int(entry, "X", out var x) || !Int(entry, "Y", out var y))
        { problem = "Key, X or Y missing"; return null; }
        if (!Inside(width, height, x, y)) { problem = Outside("floor", key, x, y); return null; }
        return new BanditCampFloor(key, x, y);
    }

    private static BanditCampSpawn ReadSpawn(JsonObject entry, int width, int height, out string problem)
    {
        problem = null;
        if (!Text(entry, "Name", out var name) || !Text(entry, "Kind", out var kind) || !Int(entry, "X", out var x) || !Int(entry, "Y", out var y))
        { problem = "Name, Kind, X or Y missing"; return null; }
        var label = name.Trim();
        if (label.Length == 0 || label.Length > MaxSpawnName) { problem = $"spawn at {x},{y} has no name or one longer than {MaxSpawnName} characters"; return null; }
        if (kind.Length == 0) { problem = $"spawn {label} at {x},{y} has no kind"; return null; }
        if (!Inside(width, height, x, y)) { problem = Outside("spawn " + label + " of kind", kind, x, y); return null; }
        return new BanditCampSpawn(name, kind, x, y);
    }

    private static BanditCampFeature ReadFeature(JsonObject entry, int width, int height, out string problem)
    {
        problem = null;
        if (!Text(entry, "Kind", out var kind) || !Int(entry, "X", out var x) || !Int(entry, "Y", out var y)) { problem = "Kind, X or Y missing"; return null; }
        if (kind != BanditRoomFeatureRules.WaterfallKind && kind != BanditRoomFeatureRules.BridgeKind) { problem = $"unknown feature kind \"{kind}\" at {x},{y}"; return null; }
        if (!Inside(width, height, x, y)) { problem = Outside("feature", kind, x, y); return null; }
        return new BanditCampFeature(kind, x, y);
    }

    // The camp's exit: the editor's party start, anywhere in the room, on its edge for the camp.
    private static string ReadStart(JsonObject root, int width, int height, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (root["PartyStart"] is not JsonObject start || !Int(start, "X", out x) || !Int(start, "Y", out y)) return "PartyStart missing";
        return Inside(width, height, x, y) ? null : $"PartyStart at {x},{y} outside the room";
    }

    private static bool Inside(int width, int height, int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

    private static string Outside(string what, string key, int x, int y) => $"{what} {key} outside the room at {x},{y}";

    private static bool Text(JsonObject node, string name, out string text)
    {
        text = null;
        return node[name] is JsonValue value && value.TryGetValue<string>(out text);
    }

    private static bool Int(JsonObject node, string name, out int number)
    {
        number = 0;
        return node[name] is JsonValue value && value.TryGetValue<int>(out number);
    }
}
