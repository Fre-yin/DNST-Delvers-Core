namespace DungeonSettlersDelvers.Core;

// The bandit camp as the author drew it in the room editor (format mt-raum, version 2), read by BanditCampFile and turned into map
// cells by BanditCampLayout. Plain data without the game, shared by the camp's rules. Cells[x, y]: x = 0 the left column,
// y = 0 the bottom row, as in the file.
internal enum BanditCampCell { Void, Dirt, Grass, HighGrass, Water }

internal sealed class BanditCampBuilding
{
    internal BanditCampBuilding(string key, int x, int y, int rotation, string faction, bool flipX = false)
    {
        Key = key;
        X = x;
        Y = y;
        Rotation = rotation;
        Faction = faction;
        FlipX = flipX;
    }

    internal string Key { get; }
    internal int X { get; }
    internal int Y { get; }
    // 0, 90, 180 or 270.
    internal int Rotation { get; }
    // As the file has it, "Ally" or "Neutral".
    internal string Faction { get; }
    // The picture mirrored left to right (the room editor's mirror), only in the picture: the game keeps the
    // building's own footprint (BanditCampFlip).
    internal bool FlipX { get; }
}

internal sealed class BanditCampItem
{
    internal BanditCampItem(string key, int x, int y, int count)
    {
        Key = key;
        X = x;
        Y = y;
        Count = count;
    }

    internal string Key { get; }
    internal int X { get; }
    internal int Y { get; }
    internal int Count { get; }
}

// A named spawn: the bandit's name in the file and its unit key (Kind).
internal sealed class BanditCampSpawn
{
    internal BanditCampSpawn(string name, string kind, int x, int y)
    {
        Name = name;
        Kind = kind;
        X = x;
        Y = y;
    }

    internal string Name { get; }
    internal string Kind { get; }
    internal int X { get; }
    internal int Y { get; }
}

// A floor tile of the room editor's floor layer (Floors) or its decoration layer (FloorDecos): one cell, laid before the
// buildings stand on it (the plank floor in the camp's house).
internal sealed class BanditCampFloor
{
    internal BanditCampFloor(string key, int x, int y)
    {
        Key = key;
        X = x;
        Y = y;
    }

    internal string Key { get; }
    internal int X { get; }
    internal int Y { get; }
}

// A room part (BanditRoomFeatureRules): "waterfall" or "bridge" at its anchor cell, the left one.
internal sealed class BanditCampFeature
{
    internal BanditCampFeature(string kind, int x, int y)
    {
        Kind = kind;
        X = x;
        Y = y;
    }

    internal string Kind { get; }
    internal int X { get; }
    internal int Y { get; }
}

internal sealed class BanditCampRoom
{
    internal BanditCampRoom(string name, BanditCampCell[,] cells, IReadOnlyList<BanditCampBuilding> buildings, IReadOnlyList<BanditCampItem> items,
        IReadOnlyList<BanditCampSpawn> spawns, IReadOnlyList<BanditCampFeature> features, int partyStartX, int partyStartY,
        IReadOnlyList<BanditCampFloor> floors = null, IReadOnlyList<BanditCampFloor> floorDecos = null)
    {
        Floors = floors ?? Array.Empty<BanditCampFloor>();
        FloorDecos = floorDecos ?? Array.Empty<BanditCampFloor>();
        Name = name;
        Cells = cells;
        Buildings = buildings;
        Items = items;
        Spawns = spawns;
        Features = features;
        PartyStartX = partyStartX;
        PartyStartY = partyStartY;
    }

    internal string Name { get; }
    internal BanditCampCell[,] Cells { get; }
    internal int Width => Cells.GetLength(0);
    internal int Height => Cells.GetLength(1);
    internal IReadOnlyList<BanditCampBuilding> Buildings { get; }
    internal IReadOnlyList<BanditCampItem> Items { get; }
    internal IReadOnlyList<BanditCampSpawn> Spawns { get; }
    internal IReadOnlyList<BanditCampFeature> Features { get; }
    internal IReadOnlyList<BanditCampFloor> Floors { get; }
    internal IReadOnlyList<BanditCampFloor> FloorDecos { get; }
    // The party's start in the room editor, on the edge: the camp's exit (BanditCampLayout).
    internal int PartyStartX { get; }
    internal int PartyStartY { get; }
}
