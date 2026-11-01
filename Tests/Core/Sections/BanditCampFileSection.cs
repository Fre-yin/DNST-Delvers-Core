using System.Text.Json;
using DungeonSettlersDelvers.Core;
using static TestKit;

// The bandit camp room file (BanditCampFile): the author's room as the fixture, read cell for cell, and small rooms for each
// reason a file is refused.
internal static class BanditCampFileSection
{
    const string Blade = "UNIT_DelversCoreForestBanditBlade";
    const string Archer = "UNIT_DelversCoreForestBanditArcher";
    const string Guard = "UNIT_DelversCoreForestBanditGuard";
    const string Healer = "UNIT_DelversCoreForestBanditHealer";
    const string Grass3x2 = "[['Gras','Gras','Gras'],['Gras','Gras','Gras']]";
    const string StartAtEdge = "'PartyStart':{'X':2,'Y':0}";

    internal static void Run()
    {
        FixtureRoom();
        SmallRooms();
        Refusals();
        FieldRefusals();
    }

    static string ReadFixture()
    {
        var core = FindCoreSourceDirectory();
        if (core == null) return null;
        var path = Path.Combine(core, BanditCampRoomFiles.Folder, BanditCampRoomFiles.NormalFile);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    // A 3 x 2 room as the editor writes it; the pieces after the header are the test's own. Single quotes stand for double ones.
    static string Small(string cells = Grass3x2, string extra = "", string start = StartAtEdge, string head = "'Format':'mt-raum','FormatVersion':2,'Width':3,'Height':2")
        => ("{" + head + ",'Name':'Small','Cells':" + cells + (extra.Length > 0 ? "," + extra : "") + (start.Length > 0 ? "," + start : "") + "}").Replace('\'', '"');

    // The reason a text was refused, or null when it read.
    static string Problem(string json) => BanditCampFile.TryRead(json, out var room, out var problem) || room != null ? null : problem;

    static bool Refused(string json, params string[] parts)
    {
        var problem = Problem(json);
        if (problem == null) return false;
        foreach (var part in parts)
            if (!problem.Contains(part)) return false;
        return true;
    }

    static void FixtureRoom()
    {
        var text = ReadFixture();
        Check(text != null, "bandit camp file: the room file stands in Core/Rooms, where Core embeds it");
        if (text == null) return;
        Check(BanditCampFile.TryRead(text, out var room, out var problem) && problem == null && room != null, "bandit camp file: Fre-yin's room reads");
        if (room == null) return;
        Check(room.Name == "Bandit Forestcamp" && room.Width == 44 && room.Height == 20, "bandit camp file: the room is 44 x 20");
        FixtureCells(room, text);
        FixtureChecksum();
        var house = Enumerable.Range(5, 5).SelectMany(x => Enumerable.Range(1, 6).Select(y => (x, y))).Append((7, 7)).OrderBy(v => v);
        Check(room.Floors.Count == 31 && room.Floors.All(v => v.Key == "BUILDING_LogFloor") && room.FloorDecos.Count == 0
                && room.Floors.Select(v => (v.X, v.Y)).OrderBy(v => v).SequenceEqual(house),
            "bandit camp file: Fre-yin's plank floor fills the house, 5 to 9 by 1 to 6 and the door 7,7");
        MirroredFile(room);
        FixtureObjects(room);
    }

    static void FixtureCells(BanditCampRoom room, string text)
    {
        var counts = new Dictionary<BanditCampCell, int>();
        foreach (var cell in room.Cells) counts[cell] = counts.TryGetValue(cell, out var n) ? n + 1 : 1;
        Check(counts[BanditCampCell.Grass] == 403 && counts[BanditCampCell.HighGrass] == 207 && counts[BanditCampCell.Void] == 192
                && counts[BanditCampCell.Water] == 62 && counts[BanditCampCell.Dirt] == 16 && counts.Count == 5,
            "bandit camp file: the cells count 403 grass, 207 high grass, 192 void, 62 water and 16 dirt");
        Check(CellsFollowTheFile(room, text), "bandit camp file: every cell sits at x, y of the file's Cells[y][x] (y = 0 the bottom row)");
        Check(room.Cells[0, 0] == BanditCampCell.Void && room.Cells[3, 0] == BanditCampCell.Grass && room.Cells[36, 19] == BanditCampCell.Water
                && room.Cells[31, 10] == BanditCampCell.Dirt && room.Cells[32, 10] == BanditCampCell.Water && room.Cells[43, 11] == BanditCampCell.Grass,
            "bandit camp file: the bottom left corner is void, the stream starts in the top row and the bridge lies in row 10");
        // The exit two cells high like the game's doors, rows 11 and 12 open to the right edge.
        var exit = BanditCampLayout.ExitOf(room);
        Check(exit != null && exit.Value.Side == BanditCampSide.Right && exit.Value.Row == 11
                && Enumerable.Range(37, 7).All(x => room.Cells[x, 11] == BanditCampCell.Grass && room.Cells[x, 12] == BanditCampCell.Grass)
                && room.Cells[43, 10] == BanditCampCell.Void && room.Cells[43, 13] == BanditCampCell.Void,
            "bandit camp file: the exit is open in rows 11 and 12 from x 37 to the right edge, the rows around it are void");
        // The corridor (BanditCampPlacement) runs on the exit's two rows, so the camp's edge cells there stay open floor.
        var map = new bool[300, 300];
        for (var x = 0; x < 300; x++)
            for (var y = 0; y < 300; y++)
                map[x, y] = !(x >= 148 && x <= 169 && y >= 98 && y <= 115);
        var plan = BanditCampPlacement.Place(room, new BanditCampAnchor(150, 100, 167, 111), map);
        var edge = plan.Writes?.Where(w => w.X == plan.CampOriginX + 43 && (w.Y == plan.CampOriginY + 11 || w.Y == plan.CampOriginY + 12)).ToList();
        Check(plan.IsPlaced && plan.Corridor.All(cell => cell.Y == plan.ExitY || cell.Y == plan.ExitY + 1)
                && edge.Count == 2 && edge.All(w => w.Terrain.Grid == BanditCampLayout.GridGround),
            "bandit camp file: the corridor meets the exit on both its rows, the camp's edge stays open floor there");
    }

    // The embedded rooms are the approved ones (the exit two cells high; the mirrored camp as the room editor
    // wrote it; the plank floor in the house, in both): each file's checksum, first 8 hex digits of SHA256.
    const string ApprovedChecksum = "8F7B2CD7";
    const string ApprovedMirroredChecksum = "DE62B2BA";

    static string RoomPath(string file)
    {
        var core = FindCoreSourceDirectory();
        return core == null ? null : Path.Combine(core, BanditCampRoomFiles.Folder, file);
    }

    static string Checksum(string path)
        => path != null && File.Exists(path) ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))[..8] : "missing";

    static void FixtureChecksum()
    {
        var sum = Checksum(RoomPath(BanditCampRoomFiles.NormalFile));
        Check(sum == ApprovedChecksum, "bandit camp file: Core/Rooms holds the approved room (" + sum + ")");
    }

    // The mirrored file against the camp and the mirror rule (BanditCampMirror): the same cells reversed, spawns, start and parts
    // where the rule puts them, the same buildings by key, the exit on the left in rows 11 and 12.
    static void MirroredFile(BanditCampRoom room)
    {
        var path = RoomPath(BanditCampRoomFiles.MirroredFile);
        var sum = Checksum(path);
        Check(sum == ApprovedMirroredChecksum, "bandit camp file: Core/Rooms holds the approved mirrored room (" + sum + ")");
        if (path == null || !File.Exists(path)) return;
        Check(BanditCampFile.TryRead(File.ReadAllText(path), out var mirrored, out var problem), "bandit camp file: the mirrored room reads (" + problem + ")");
        if (mirrored == null) return;
        var rule = BanditCampMirror.Mirror(room);
        var cellsMirrored = Enumerable.Range(0, room.Width).All(x => Enumerable.Range(0, room.Height).All(y => mirrored.Cells[x, y] == room.Cells[room.Width - 1 - x, y]));
        Check(mirrored.Width == room.Width && mirrored.Height == room.Height && cellsMirrored, "bandit camp file: the mirrored room's cells are the camp's reversed in every row");
        Check(mirrored.Spawns.Select(v => (v.Name, v.Kind, v.X, v.Y)).SequenceEqual(rule.Spawns.Select(v => (v.Name, v.Kind, v.X, v.Y)))
                && mirrored.Features.Select(v => (v.Kind, v.X, v.Y)).SequenceEqual(rule.Features.Select(v => (v.Kind, v.X, v.Y)))
                && mirrored.PartyStartX == rule.PartyStartX && mirrored.PartyStartY == rule.PartyStartY && mirrored.Name == rule.Name,
            "bandit camp file: spawns, start, waterfall, bridge and name stand where the mirror rule puts them");
        var keys = room.Buildings.Select(v => v.Key).OrderBy(k => k, StringComparer.Ordinal);
        Check(mirrored.Buildings.Count == 137 && mirrored.Buildings.Select(v => v.Key).OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(keys)
                && mirrored.Buildings.Count(v => v.FlipX) == 15 && room.Buildings.All(v => !v.FlipX),
            "bandit camp file: the mirrored room has the camp's 137 buildings, 15 with their picture mirrored, the camp none");
        Check(mirrored.Floors.Select(v => (v.Key, v.X, v.Y)).OrderBy(v => v).SequenceEqual(rule.Floors.Select(v => (v.Key, v.X, v.Y)).OrderBy(v => v))
                && mirrored.FloorDecos.Count == 0,
            "bandit camp file: the mirrored room's floor tiles stand where the mirror rule puts them");
        var exit = BanditCampLayout.ExitOf(mirrored);
        Check(exit != null && exit.Value.Side == BanditCampSide.Left && exit.Value.Row == 11 && mirrored.Cells[0, 12] == BanditCampCell.Grass,
            "bandit camp file: the mirrored room's exit is on the left edge, rows 11 and 12");
    }

    // The same cells read straight from the file's text with the names of the editor, a second way to the same answer.
    static bool CellsFollowTheFile(BanditCampRoom room, string text)
    {
        var names = new Dictionary<string, BanditCampCell>
        {
            ["Leer"] = BanditCampCell.Void, ["Erdboden"] = BanditCampCell.Dirt, ["Gras"] = BanditCampCell.Grass,
            ["Hochgras"] = BanditCampCell.HighGrass, ["Wasser"] = BanditCampCell.Water,
        };
        using var document = JsonDocument.Parse(text);
        var y = 0;
        foreach (var row in document.RootElement.GetProperty("Cells").EnumerateArray())
        {
            var x = 0;
            foreach (var cell in row.EnumerateArray())
            {
                if (room.Cells[x, y] != names[cell.GetString()]) return false;
                x++;
            }
            y++;
        }
        return y == room.Height;
    }

    static void FixtureObjects(BanditCampRoom room)
    {
        Check(room.Buildings.Count == 137 && room.Items.Count == 0, "bandit camp file: 137 buildings and no items");
        var first = room.Buildings[0];
        Check(first.Key == "BUILDING_LogWall" && first.X == 4 && first.Y == 0 && first.Rotation == 0 && first.Faction == "Ally",
            "bandit camp file: a building keeps its key, cell, rotation and faction");
        var turned = 0;
        var neutral = 0;
        foreach (var building in room.Buildings)
        {
            if (building.Rotation != 0) turned++;
            if (building.Faction == "Neutral") neutral++;
        }
        Check(turned == 6 && neutral == 86, "bandit camp file: six buildings are turned and 86 are neutral, the rest are allies");
        Check(room.Spawns.Count == 4 && room.Spawns[0].Kind == Blade && room.Spawns[1].Kind == Archer && room.Spawns[2].Kind == Guard
                && room.Spawns[3].Kind == Healer, "bandit camp file: four spawns of the blade, archer, guard and healer kinds");
        Check(room.Spawns[0].Name == "Schl\u00e4chter" && room.Spawns[0].X == 5 && room.Spawns[0].Y == 16
                && room.Spawns[2].Name == "W\u00e4chter" && room.Spawns[2].X == 7 && room.Spawns[2].Y == 12
                && room.Spawns[1].X == 3 && room.Spawns[1].Y == 13 && room.Spawns[3].X == 4 && room.Spawns[3].Y == 9,
            "bandit camp file: a spawn keeps its name (umlauts too) and cell");
        Check(room.Features.Count == 2 && room.Features[0].Kind == "waterfall" && room.Features[0].X == 36 && room.Features[0].Y == 19
                && room.Features[1].Kind == "bridge" && room.Features[1].X == 31 && room.Features[1].Y == 10,
            "bandit camp file: the waterfall at 36,19 and the bridge at 31,10");
        Check(room.PartyStartX == 43 && room.PartyStartY == 11, "bandit camp file: the party starts at 43,11 on the right edge");
    }

    static void SmallRooms()
    {
        var plain = BanditCampFile.TryRead(Small(), out var room, out var problem);
        Check(plain && problem == null && room.Width == 3 && room.Height == 2 && room.Buildings.Count == 0 && room.Items.Count == 0
                && room.Spawns.Count == 0 && room.Features.Count == 0, "bandit camp file: lists the file leaves out are empty");
        var items = "'Items':[{'Key':'ITEM_A','X':0,'Y':1,'Amount':3},{'Key':'ITEM_B','X':1,'Y':0}]";
        var withItems = BanditCampFile.TryRead(Small(extra: items), out var stocked, out _);
        Check(withItems && stocked.Items.Count == 2 && stocked.Items[0].Count == 3 && stocked.Items[0].X == 0 && stocked.Items[0].Y == 1 && stocked.Items[1].Count == 1,
            "bandit camp file: an item's Amount is its count, one when it is left out");
        var noFaction = "'Buildings':[{'Key':'BUILDING_A','X':1,'Y':1,'Rotation':270}]";
        Check(BanditCampFile.TryRead(Small(extra: noFaction), out var built, out _) && built.Buildings[0].Rotation == 270 && built.Buildings[0].Faction == "",
            "bandit camp file: a building without a faction keeps none");
    }

    static void Refusals()
    {
        var text = ReadFixture();
        if (text != null)
        {
            Check(Refused(ReplaceOnce(text, "\"Gras\"", "\"Felsboden\""), "unknown cell kind", "Felsboden"), "bandit camp file: an unknown cell kind is refused with its name");
            Check(Refused(ReplaceOnce(text, "\"FormatVersion\": 2", "\"FormatVersion\": 1"), "version 1"), "bandit camp file: a wrong format version is refused");
            Check(Refused(ReplaceOnce(text, "\"mt-raum\"", "\"mt-other\""), "mt-other"), "bandit camp file: a wrong format is refused");
            Check(Refused(ReplaceOnce(text, "\"Width\": 44", "\"Width\": 43"), "row 0", "43"), "bandit camp file: a row longer than the width is refused");
        }
        Check(Refused(Small(cells: "[['Gras','Gras','Gras'],['Gras','Gras']]"), "row 1", "3 cells"), "bandit camp file: a row of the wrong length is refused with its row");
        Check(Refused(Small(cells: "[['Gras','Gras','Gras']]"), "2 rows"), "bandit camp file: too few rows are refused");
        Check(Refused(Small(cells: "[['Gras','Gras','Gras'],['Gras','Wasserfall','Gras']]"), "Wasserfall", "1,1"), "bandit camp file: an unknown cell kind is refused with its name and cell");
        Check(Refused(Small(head: "'Format':'mt-raum','FormatVersion':3,'Width':3,'Height':2"), "version 3"), "bandit camp file: a newer format version is refused");
        Check(Refused(Small(head: "'Format':'mt-raum','FormatVersion':2,'Width':297,'Height':2"), "297x2"), "bandit camp file: a room wider than the floor allows is refused");
        Check(Refused(Small(head: "'Format':'mt-raum','FormatVersion':2,'Width':0,'Height':2"), "0x2"), "bandit camp file: an empty room is refused");
        Check(Refused("{ not json", "not JSON") && Refused(null, "not JSON") && Refused("[]", "object"),
            "bandit camp file: no text, no JSON and no object are refused");
        Check(!BanditCampFile.TryRead(Small(start: ""), out var room, out var problem) && room == null && problem.Contains("PartyStart"), "bandit camp file: a missing party start is refused");
        Check(Refused(Small(start: "'PartyStart':{'X':3,'Y':0}"), "PartyStart", "3,0"), "bandit camp file: a party start outside the room is refused");
    }

    static void FieldRefusals()
    {
        Check(Refused(Small(extra: "'Buildings':[{'Key':'BUILDING_LogWall','X':3,'Y':0,'Rotation':0,'Faction':'Ally'}]"), "building", "BUILDING_LogWall", "3,0"),
            "bandit camp file: a building outside the room is refused with its key and cell");
        Check(Refused(Small(extra: "'Buildings':[{'Key':'BUILDING_LogWall','X':0,'Y':-1,'Rotation':0}]"), "-1"), "bandit camp file: a building below the room is refused");
        Check(Refused(Small(extra: "'Buildings':[{'Key':'BUILDING_WoodBed','X':1,'Y':1,'Rotation':45}]"), "BUILDING_WoodBed", "rotation 45"), "bandit camp file: a rotation other than 0, 90, 180 or 270 is refused");
        Check(Refused(Small(extra: "'Items':[{'Key':'ITEM_A','X':0,'Y':2}]"), "item", "ITEM_A", "0,2"), "bandit camp file: an item outside the room is refused");
        Check(Refused(Small(extra: "'Items':[{'Key':'ITEM_A','X':0,'Y':0,'Amount':0}]"), "ITEM_A", "amount"), "bandit camp file: an item without an amount is refused");
        Check(Refused(Small(extra: "'Spawns':[{'Name':'Bogen','Kind':'" + Archer + "','X':9,'Y':0}]"), "spawn Bogen", Archer, "9,0"), "bandit camp file: a spawn outside the room is refused");
        Check(Refused(Small(extra: "'Spawns':[{'Name':' ','Kind':'" + Guard + "','X':0,'Y':0}]"), "name"), "bandit camp file: a spawn without a name is refused");
        Check(Refused(Small(extra: "'Features':[{'Kind':'bridge','X':0,'Y':5}]"), "bridge", "0,5"), "bandit camp file: a feature outside the room is refused");
        Check(Refused(Small(extra: "'Features':[{'Kind':'tower','X':0,'Y':0}]"), "tower"), "bandit camp file: a feature of an unknown kind is refused");
        Check(Refused(Small(extra: "'Buildings':{}"), "Buildings"), "bandit camp file: a list that is no list is refused");
        Check(Refused(Small(extra: "'Buildings':[{'Key':'BUILDING_A','X':'one','Y':0,'Rotation':0}]"), "Buildings[0]"), "bandit camp file: an entry with a field of the wrong type is refused with its place");
    }

    static string ReplaceOnce(string text, string from, string to)
    {
        var at = text.IndexOf(from, StringComparison.Ordinal);
        return at < 0 ? text : text.Substring(0, at) + to + text.Substring(at + from.Length);
    }
}
