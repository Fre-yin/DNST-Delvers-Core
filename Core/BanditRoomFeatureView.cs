using UnityEngine;
using UnityEngine.Tilemaps;

namespace DungeonSettlersDelvers.Core;

// The thin game wrapper of the bandit room's waterfall and wooden bridge (rules: BanditRoomFeatureRules). The parts are set
// like any item through the tool API (Place, Remove); Core keeps them in its own save block (CoreSaveData.RoomFeatures) and
// draws the shown map's parts once per map and after every change. A camp's bridge stands on two cells the floor already has as
// dirt (BanditRoomFeatureRules.GroundUnderBridges), so the game walks them by itself; the view paints the water of the cell next
// to them onto them, on the game's tilemaps. Nothing goes into the game's part of a save. Only placed parts are drawn; the terrain rules check a
// part when it is placed and warn when it does not fit.
// Game calls: IInMapDataSupplier.GetGrid and RawMapData.GetHighDecoration per cell; the game's tilemaps found among the
// scene's Tilemap objects by name, the ground one for the cell's world corner
// and the map's cell bounds, the decoration ones for the sorting.
internal static class BanditRoomFeatureView
{
    private const int MaxLines = 8;
    private const int TicksBetweenScans = 60;

    private static readonly List<GameObject> drawn = new();
    private static readonly List<(SpriteRenderer Renderer, Sprite[] Frames)> waterfalls = new();
    // Tiles the bridge painted over, to give back when the part goes on the same map.
    private static readonly List<(Tilemap Map, Vector3Int Cell, TileBase Before)> painted = new();
    private static MapType? scannedMap;
    private static int ticks;
    private static int lines;
    private static int linesSeed;
    private static int failures;

    // From the tick postfix: scans a map once when it is shown, and moves the waterfalls' frames on.
    internal static void OnGameTick()
    {
        if (failures >= 3) return;
        try
        {
            AnimateWaterfalls();
            if (++ticks < TicksBetweenScans) return;
            ticks = 0;
            var map = CoreGame.CurrentMap;
            if (scannedMap == map) return;
            scannedMap = map;
            Rebuild(sameMap: false);
        }
        catch (Exception ex) { Failed(ex); }
    }

    // ---- tool API (DelversCoreRuntime): set and take parts like any item ----

    // checkTerrain: the tool API checks the terrain; a camp's own parts were drawn with it (its bridge stands on dirt by design).
    internal static string Place(string kind, int x, int y, bool checkTerrain = true)
    {
        var data = CoreSaveStore.Current;
        var parts = data?.RoomFeatures() ?? new List<BanditRoomFeatureRules.Part>();
        var inMap = CoreGame.InMap;
        var fits = inMap != null && (!checkTerrain
            || BanditRoomFeatureRules.TerrainFits(kind, (cx, cy) => Classify(inMap, new Vector2Int(cx, cy)), x, y, +1));
        var result = BanditRoomFeatureRules.Place(parts, data != null && inMap != null, kind, (int)CoreGame.CurrentMap, x, y, fits, FloorSeed());
        if (result.StartsWith(BanditRoomFeatureRules.Placed, StringComparison.Ordinal))
        {
            data.SetRoomFeatures(parts);
            Rebuild();
            if (result == BanditRoomFeatureRules.PlacedOffTerrain)
                DelversHost.Warning($"CORE_ROOM_FEATURE_TERRAIN kind={kind} at={x},{y}: the terrain does not fit the rules, placed anyway");
        }
        DelversHost.Info($"CORE_ROOM_FEATURE_PLACE kind={kind} at={x},{y} result={result}");
        return result;
    }

    internal static bool Remove(int x, int y)
    {
        var data = CoreSaveStore.Current;
        if (data == null) return false;
        var parts = data.RoomFeatures();
        if (!BanditRoomFeatureRules.Remove(parts, (int)CoreGame.CurrentMap, x, y, FloorSeed())) return false;
        data.SetRoomFeatures(parts);
        Rebuild();
        DelversHost.Info($"CORE_ROOM_FEATURE_REMOVE at={x},{y}");
        return true;
    }

    internal static string[] List()
    {
        var parts = CoreSaveStore.Current?.RoomFeatures() ?? new List<BanditRoomFeatureRules.Part>();
        var lines = new string[parts.Count];
        for (var i = 0; i < parts.Count; i++) lines[i] = parts[i].Line;
        return lines;
    }

    // The seed of the shown dungeon floor; 0 on the other maps, which stay.
    internal static int FloorSeed()
        => CoreGame.CurrentMap == MapType.Dungeon ? CoreGame.Flow?._campaignData?.DungeonDataContainer?.GetCurrentSeed() ?? 0 : 0;

    // A new or loaded campaign: everything drawn goes.
    internal static void ResetCampaign()
    {
        Clear(sameMap: false);
        scannedMap = null;
        ticks = 0;
    }

    // Everything of the shown map anew: its placed parts. sameMap: a part was placed or taken on the shown map, so the water it
    // painted is given back first; otherwise the map changed and its tilemaps went with it.
    private static void Rebuild(bool sameMap = true)
    {
        Clear(sameMap);
        var inMap = CoreGame.InMap;
        var tilemaps = UnityEngine.Object.FindObjectsOfType<Tilemap>();
        LogTilemaps(tilemaps);
        var ground = Named(tilemaps, BanditRoomFeatureRules.GroundTilemapName);
        if (inMap == null || ground == null) return;
        var map = (int)CoreGame.CurrentMap;
        var seed = FloorSeed();
        var placed = 0;
        foreach (var part in CoreSaveStore.Current?.RoomFeatures() ?? new List<BanditRoomFeatureRules.Part>())
        {
            if (!BanditRoomFeatureRules.Shown(part, map, seed)) continue;
            Draw(tilemaps, ground, part.Kind, new Vector2Int(part.X, part.Y), inMap);
            placed++;
        }
        Line($"CORE_BANDIT_ROOM_FEATURES map={scannedMap} placed={placed}");
    }

    private static void Draw(Tilemap[] tilemaps, Tilemap ground, string kind, Vector2Int anchor, IInMapDataSupplier inMap)
    {
        if (kind == BanditRoomFeatureRules.BridgeKind) DrawBridge(tilemaps, ground, anchor, inMap);
        else DrawWaterfall(tilemaps, ground, anchor);
    }

    // The terrain of a cell from the game: its grid flag and the high decoration of the shown map's raw data
    // (RawMapData.GetHighDecoration, same map coordinates as GetGrid).
    private static BanditRoomFeatureRules.Cell Classify(IInMapDataSupplier inMap, Vector2Int cell)
    {
        var grid = inMap.GetGrid(cell);
        if (grid == null) return BanditRoomFeatureRules.Cell.Other;
        var raw = CoreGame.Flow?._campaignData?.MapDataContainer?.GetMap(CoreGame.CurrentMap);
        var high = raw != null ? (int)raw.GetHighDecoration(cell) : 0;
        return BanditRoomFeatureRules.CellOf((int)grid.Flag, high);
    }

    private static void DrawWaterfall(Tilemap[] tilemaps, Tilemap ground, Vector2Int left)
    {
        var frames = CoreAssets.WaterfallFrames();
        if (frames == null) return;
        var renderer = Place("DelversWaterfall", frames[0], ground, left, BanditRoomFeatureRules.WaterfallCells, BanditRoomFeatureRules.WaterfallRowsAbove,
            SortingOf(tilemaps, BanditRoomFeatureRules.Layer.AboveTiles));
        waterfalls.Add((renderer, frames));
        Line($"CORE_BANDIT_ROOM_WATERFALL at={left.x},{left.y}");
    }

    private static void DrawBridge(Tilemap[] tilemaps, Tilemap ground, Vector2Int left, IInMapDataSupplier inMap)
    {
        var under = CoreAssets.BridgeUnder();
        var over = CoreAssets.BridgeOver();
        if (under == null || over == null) return;
        var water = PaintWater(tilemaps, left, inMap);
        Place("DelversBridgeUnder", under, ground, left, BanditRoomFeatureRules.BridgeCells, 0, SortingOf(tilemaps, BanditRoomFeatureRules.Layer.BelowUnits));
        Place("DelversBridgeOver", over, ground, left, BanditRoomFeatureRules.BridgeCells, 0, SortingOf(tilemaps, BanditRoomFeatureRules.Layer.AboveUnits));
        Line($"CORE_BANDIT_ROOM_BRIDGE at={left.x},{left.y} water={water}");
    }

    // The water look on the bridge's two cells: each takes every ground and decoration tile of the water cell above or below it.
    // The game's grid stays as the floor has it (dirt), so the cells stay walkable. How many cells got water.
    private static int PaintWater(Tilemap[] tilemaps, Vector2Int left, IInMapDataSupplier inMap)
    {
        var count = 0;
        foreach (var (x, y) in BanditRoomFeatureRules.BridgeWaterCells(left.x, left.y))
        {
            Vector2Int? source = null;
            foreach (var dy in new[] { -1, 1 })
                if (source == null && Classify(inMap, new Vector2Int(x, y + dy)) == BanditRoomFeatureRules.Cell.Water) source = new Vector2Int(x, y + dy);
            if (source == null) continue;
            var from = new Vector3Int(source.Value.x, source.Value.y, 0);
            var to = new Vector3Int(x, y, 0);
            foreach (var tilemap in tilemaps)
            {
                if (tilemap == null || !BanditRoomFeatureRules.PaintsWater(tilemap.name)) continue;
                var tile = tilemap.GetTile(from);
                var before = tilemap.GetTile(to);
                if (tile == before) continue;
                painted.Add((tilemap, to, before));
                tilemap.SetTile(to, tile);
            }
            count++;
        }
        return count;
    }

    // A sprite whose lower left corner sits on the left cell's lower left corner, widthCells wide; rowsAbove: rows of the image
    // above that cell's row (the waterfall reaches up over wall and roof).
    private static SpriteRenderer Place(string name, Sprite sprite, Tilemap ground, Vector2Int left, int widthCells, int rowsAbove,
        (int Layer, int Order) sorting)
    {
        var go = new GameObject(name);
        drawn.Add(go);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerID = sorting.Layer;
        renderer.sortingOrder = sorting.Order;
        var corner = ground.CellToWorld(new Vector3Int(left.x, left.y, 0));
        var cell = ground.cellSize;
        var size = sprite.bounds.size;
        var lowRow = BanditRoomFeatureRules.LowestRow(rowsAbove, Mathf.RoundToInt(size.y / cell.y));
        go.transform.position = new Vector3(corner.x + widthCells * cell.x / 2f, corner.y + (lowRow * cell.y) + size.y / 2f, corner.z);
        return renderer;
    }

    // The sorting of a part from the game's own tilemap renderers: the waterfall above every tile layer
    // (BanditRoomFeatureRules.AboveAllTiles), the bridge deck right above the high decoration (the water), its rail above
    // the over decoration.
    private static (int Layer, int Order) SortingOf(Tilemap[] tilemaps, BanditRoomFeatureRules.Layer layer)
    {
        if (layer == BanditRoomFeatureRules.Layer.AboveTiles)
        {
            var layers = new List<BanditRoomFeatureRules.TileLayer>();
            foreach (var tilemap in tilemaps)
            {
                var tile = tilemap != null ? tilemap.gameObject.GetComponent<TilemapRenderer>() : null;
                if (tile == null) continue;
                layers.Add(new BanditRoomFeatureRules.TileLayer(tile.sortingLayerName, SortingLayer.GetLayerValueFromID(tile.sortingLayerID),
                    tile.sortingLayerID, tile.sortingOrder));
            }
            if (BanditRoomFeatureRules.AboveAllTiles(layers) is { } above) return (above.LayerId, above.Order);
            return (0, BanditRoomFeatureRules.FallbackOrder(layer));
        }
        var reference = Named(tilemaps, BanditRoomFeatureRules.SortingTilemapName(layer));
        var renderer = reference != null ? reference.gameObject.GetComponent<TilemapRenderer>() : null;
        if (renderer == null) return (0, BanditRoomFeatureRules.FallbackOrder(layer));
        return (renderer.sortingLayerID, renderer.sortingOrder + 1);
    }

    // The scene's tilemap whose name holds the part (case ignored), else null.
    private static Tilemap Named(Tilemap[] tilemaps, string part)
    {
        foreach (var tilemap in tilemaps)
            if (tilemap != null && tilemap.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return tilemap;
        return null;
    }

    private static bool tilemapsLogged;

    private static void LogTilemaps(Tilemap[] tilemaps)
    {
        if (tilemapsLogged) return;
        tilemapsLogged = true;
        var names = new List<string>();
        foreach (var tilemap in tilemaps)
        {
            if (tilemap == null) continue;
            var renderer = tilemap.gameObject.GetComponent<TilemapRenderer>();
            names.Add(tilemap.name + (renderer != null ? ":" + renderer.sortingLayerName + "/" + renderer.sortingOrder : ""));
        }
        DelversHost.Info("CORE_BANDIT_ROOM_TILEMAPS " + string.Join(" ", names));
    }

    private static void AnimateWaterfalls()
    {
        if (waterfalls.Count == 0) return;
        var frame = BanditRoomFeatureRules.WaterfallFrameAt((long)(GameClock.Now * 1000.0));
        foreach (var (renderer, frames) in waterfalls)
            if (renderer != null) renderer.sprite = frames[frame];
    }

    private static void Clear(bool sameMap)
    {
        foreach (var go in drawn)
            if (go != null) UnityEngine.Object.Destroy(go);
        drawn.Clear();
        waterfalls.Clear();
        if (sameMap)
            foreach (var (map, cell, before) in painted)
                if (map != null) map.SetTile(cell, before);
        painted.Clear();
    }

    private static void Line(string line)
    {
        if (BanditRoomFeatureRules.LogLineAllowed(ref lines, ref linesSeed, FloorSeed(), MaxLines)) DelversHost.Info(line);
    }

    private static void Failed(Exception ex)
    {
        failures++;
        DelversHost.Warning("CORE_BANDIT_ROOM_FEATURES_FAILED: " + ex.GetType().Name + ": " + ex.Message);
    }
}
