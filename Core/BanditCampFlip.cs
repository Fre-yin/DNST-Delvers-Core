using HarmonyLib;
using UnityEngine;
#if BEPINEX
using BuildingRotation = global::Refactor.Building.BuildingRotation;
#else
using BuildingRotation = Il2CppRefactor.Building.BuildingRotation;
#endif

namespace DungeonSettlersDelvers.Core;

// The mirrored camp's pictures (the buildings themselves mirrored too). A building the room file marks FlipX
// shows its picture mirrored left to right over its footprint; the game keeps the building's own footprint, so it is the picture
// only, and without Core it shows unmirrored. The game draws a building's picture when its view is spawned (also after a load
// and from the pool): EntityObjectFactory.SetSpriteRenderer sets flipX = (rotation 270), the graphic's Init keeps its position as
// _originPivot, and ResetState returns there before and after every tween. So right after each activation of such a view the
// picture is flipped once more, moved by twice the distance from its pivot to the middle of the footprint's box, and _originPivot
// follows (the Modding Toolkit's mirror computes the same). The camp's GUIDs come from Core's block (CoreSaveData.CampFlips),
// read once per block; nothing runs per frame. Like the footprint (BanditCampFootprint) it asks only the GUID, not the floor's
// seed: the game gives every building a new GUID, so a GUID of the block marks the camp's building and no other, and picture and
// footprint are always mirrored together.
internal static class BanditCampFlip
{
    private const int MirroredRotation = 270;

    private static CoreSaveData readFrom;
    private static HashSet<Guid> flips = new();

    // The camp was furnished: its block changed.
    internal static void Reload() => readFrom = null;

    internal static void OnActivated(EntityObjectsContainer container, IEntity entity)
    {
        var data = CoreSaveStore.Current;
        if (data == null || CoreGame.CurrentMap != MapType.Dungeon) return;
        if (!ReferenceEquals(readFrom, data))
        {
            flips = data.CampFlips(out _);
            readFrom = data;
        }
        if (flips.Count == 0) return;
        var building = entity?.TryCast<BuildingEntity>();
        if (building == null || !Guid.TryParse(building.Guid.ToString(), out var guid) || !flips.Contains(guid)) return;
        Flip(container, building);
    }

    private static void Flip(EntityObjectsContainer container, BuildingEntity building)
    {
        var view = CoreGame.EntityObjectOf(container.Cast<IEntityObjectSupplier>(), building.Guid);
        var graphic = view?.Graphic;
        var renderer = graphic?._renderer;
        if (renderer == null || view.Transform == null) return;
        var inMap = CoreGame.InMap;
        var rotation = inMap.GetBuildingRotation(building.Guid);
        var wanted = (int)rotation != MirroredRotation;
        if (renderer.flipX == wanted) return;
        renderer.flipX = wanted;
        var anchor = inMap.GetLogicalPosition(building.Guid);
        var middle = BoxMiddleX(building.Key, anchor, rotation);
        if (middle == null) return;
        // The root stands on the anchor cell; the footprint box's middle lies that far from it.
        var target = view.Transform.position.x + (middle.Value - anchor.x);
        var moved = graphic._transform ?? renderer.transform;
        var shift = 2f * (target - renderer.transform.position.x);
        moved.position += new Vector3(shift, 0f, 0f);
        graphic._originPivot = new Vector2(moved.localPosition.x, graphic._originPivot.y);
    }

    // The middle x of the box around the footprint's blocked and occupied cells.
    private static float? BoxMiddleX(string key, Vector2Int anchor, BuildingRotation rotation)
    {
        var area = ConstructionCheckTool.GetBuildingAreaPositions(key, anchor, rotation);
        if (area == null) return null;
        int min = int.MaxValue, max = int.MinValue;
        foreach (var cells in new[] { area.Blocked, area.Occupied })
            for (var i = 0; cells != null && i < cells.Count; i++)
            {
                min = Math.Min(min, cells[i].x);
                max = Math.Max(max, cells[i].x);
            }
        return min == int.MaxValue ? null : (min + max) / 2f;
    }
}

// Every spawn of an entity's view, buildings' too (first activation, after a load, from the pool).
[HarmonyPatch(typeof(EntityObjectsContainer), nameof(EntityObjectsContainer.ActivateObject))]
internal static class BanditCampFlipPatch
{
    private static int failures;

    private static void Postfix(EntityObjectsContainer __instance, IEntity entity)
    {
        if (failures >= 3) return;
        try { BanditCampFlip.OnActivated(__instance, entity); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_BANDITS_CAMP_FLIP_FAILED: " + ex.Message);
        }
    }
}
