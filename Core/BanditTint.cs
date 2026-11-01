using HarmonyLib;
using UnityEngine;
#if BEPINEX
using SummonedBuildingResolver = global::Refactor.Combat.AffecterRelated.SummonedBuildingResolver;
using UnitEntityHUD = global::Refactor.View.UnitEntityHUD;
using VFXView = global::Refactor.FX.VFXView;
#else
using SummonedBuildingResolver = Il2CppRefactor.Combat.AffecterRelated.SummonedBuildingResolver;
using UnitEntityHUD = Il2CppRefactor.View.UnitEntityHUD;
using VFXView = Il2CppRefactor.FX.VFXView;
#endif

namespace DungeonSettlersDelvers.Core;

// In game (BanditTintRules): the bandits' seed and sprayer buildings and their effects drawn in orange, and the units the
// bandits summon (the water mage's bubblelings; DelversSummons.IsCoreOwned), their blue turned orange (BanditTintRules.SummonPixel).
// - Buildings: every ScanSeconds the map's Strange Seeds and Mucus Sprayers with a Core summoner; all SpriteRenderers of
//   their objects are tracked until the building is gone.
// - Effects: the VFX hook (CoreVfxPatches) notes each new effect's id; when the game binds it to its owner
//   (SetVFXOwner), the area (122) and spray (128) of a bandit building and the hit (129) on the unit a Core sprayer just hit
//   (NoteHit) are tracked until the game stops them (StopVFX prefix). Nothing is assigned by position. The upgrade's
//   Evolving effect (121) gets no owner from the game and stays as it is.
// - The game's animations write the sprite every frame (the seed grows, the sprayer attacks, the effects run through their
//   sheets). After the animation, before drawing (LateUpdate of Core's runner, DelversCoreRuntime.LateFrame), each tracked
//   renderer gets the orange copy of the sprite it shows. Each copy is made once from the game's sprite (RenderTexture and
//   ReadPixels like ScrollGlow, at most CopiesPerFrame a frame) and cached. No game image is shipped or changed.
// - The game pools its effects and objects: when a tracked one stops or its building is gone, Core puts the game's sprite
//   back and lets it go, so a reused object never shows orange for a player's own seed.
internal static partial class BanditTint
{
    private static readonly Dictionary<string, Vector2> buildings = new(StringComparer.Ordinal);
    // The bandits' summoned units being tinted (DelversSummons.IsCoreOwned), with their guid for the object; a party's summons stay
    // as they are.
    private static readonly Dictionary<string, Il2CppSystem.Guid> summons = new(StringComparer.Ordinal);
    // The bandits' summons SummonSide met at their creation, where their owner is known: the scan tints them without looking the
    // owner up again (the toad and some bubblelings stayed grey, the owner lookup in the scan missed them).
    private static readonly HashSet<string> noted = new(StringComparer.Ordinal);
    private const int NotedCap = 256;

    internal static void NoteSummon(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (noted.Count >= NotedCap) noted.Clear();
        noted.Add(id);
    }

    private static int skillLines;

    // A bandit's summon the scan tracks (ScanSummons, every ScanSeconds), for the skill effects' caster (BanditTintSkill).
    internal static bool IsBanditSummon(string id) => id != null && summons.ContainsKey(id);

    // A bandit building the scan confirmed (seed, sprayer, the blooms), for the effects a building casts itself (BanditTintSkill).
    internal static bool IsBanditBuildingSeen(string id) => id != null && buildings.ContainsKey(id);
    private static readonly List<(Il2CppSystem.Guid Guid, string Target, float At)> hits = new();
    private static readonly Dictionary<string, int> pending = new(StringComparer.Ordinal);
    private static VFXView view;
    private static double? hue = BanditTintRules.DefaultHue;
    private static double plant;
    private static int lines;
    private static int summonLines;

    internal static void Reset()
    {
        RestoreAll("reset");
        buildings.Clear();
        summons.Clear();
        noted.Clear();
        skipped.Clear();
        buildingTops.Clear();
        ownerLines = 0;
        sourceLines = 0;
        BanditTintSource.Reset();
        BanditTintSkill.Reset();
        skillLines = 0;
        hits.Clear();
        pending.Clear();
        view = null;
        hue = BanditTintRules.Hue(CoreSettings.Number(BanditTintRules.HueKey));
        plant = BanditTintRules.Plant(CoreSettings.Number(BanditTintRules.PlantKey));
        lines = 0;
        summonLines = 0;
        // The copies follow the settings; new ones are made from the next frame.
        copies.Clear();
        summonCopies.Clear();
        skillCopies.Clear();
        made.Clear();
        failed.Clear();
        BanditBar.Reset();
        BanditTintProbe.Reset();
        BanditBossBarProbe.Reset();
        BanditBossBar.Reset();
    }

    internal static void SeeView(VFXView seen)
    {
        if (seen != null && (view == null || view.Pointer != seen.Pointer)) view = seen;
    }

    // A Core sprayer hit this unit now (GroupAiHealerSeed.OnSprayerDamage).
    internal static void NoteHit(UnitEntity target)
    {
        if (hue == null || target == null) return;
        var now = Time.unscaledTime;
        hits.RemoveAll(h => now - h.At > BanditTintRules.HitSeconds);
        if (hits.Count < 64) hits.Add((target.Guid, target.Guid.ToString(), now));
    }

    // A new effect: only its id is remembered here. Whose it is shows when the game binds it (OnOwner); Core never guesses
    // from the position (a player's seed next to a bandit's showed an orange area).
    internal static void OnVfx(VFXView seen, Il2CppSystem.Guid fx, int id, Vector2 position)
    {
        SeeView(seen);
        if (hue == null) return;
        var key = fx.ToString();
        // A bandit's skill or affecter asked for this effect (BanditTintSkill): its caster, never a position.
        var caster = BanditTintSkill.Take(key);
        if (caster != null)
        {
            var skillTop = seen?._vfxContainer?.GetVFX(fx)?.transform;
            var skillCount = skillTop == null ? 0 : Track(skillTop, caster, key, TintKind.Skill);
            // A count of their own: skill effects come by the hundred and must not use up the buildings' lines (run 20261031-234608).
            if (skillLines++ < BanditTintRules.SkillLogCap) DelversHost.Info(BanditTintRules.Line("skill", caster, skillCount, tracked.Count, key));
            return;
        }
        if (!BanditTintRules.Watches(id)) return;
        // Area and spray: the building whose passive made this effect (BanditTintSource), never a position.
        if (BanditTintRules.IsBuildingVfx(id))
        {
            var building = BanditTintSource.Take(key);
            var bandit = building != null && IsBanditBuildingId(building);
            if (sourceLines++ < BanditTintRules.OwnerLogCap) DelversHost.Info(BanditTintRules.SourceLine(id, key, building, bandit));
            if (!bandit) return;
            var top = seen?._vfxContainer?.GetVFX(fx)?.transform;
            var count = top == null ? 0 : Track(top, building, key);
            if (count > 0 && lines++ < BanditTintRules.LogCap)
                DelversHost.Info(BanditTintRules.Line(id == BanditTintRules.AreaVfx ? "area" : "spray", building, count, tracked.Count, key));
            return;
        }
        if (pending.Count >= BanditTintRules.PendingCap) pending.Clear();
        pending[key] = id;
    }

    // The game binds an effect to its owner (VFXView.SetVFXOwner, the effect hangs under the owner's object): the seed's area
    // (122) and the sprayer's spray (128) to their building, the sprayer's hit (129) to the unit it hit. Orange only for a
    // bandit building that Core confirmed by its summoner, or for the very unit a Core sprayer hit within HitSeconds.
    internal static void OnOwner(VFXView seen, Il2CppSystem.Guid fx, IEntity owner)
    {
        if (hue == null || owner == null || pending.Count == 0) return;
        var key = fx.ToString();
        if (!pending.TryGetValue(key, out var id)) return;
        pending.Remove(key);
        var ownerId = owner.Guid.ToString();
        var now = Time.unscaledTime;
        var hit = false;
        if (id == BanditTintRules.HitVfx)
            foreach (var h in hits)
                if (h.Target == ownerId && BanditTintRules.HitRecent(now - h.At)) { hit = true; break; }
        var top = (seen ?? view)?._vfxContainer?.GetVFX(fx)?.transform;
        // Two independent proofs, no position: the owner is a confirmed bandit building, or the game hung the effect under the
        // object of one (SetVFXOwner reparents it; no area or spray was recognised by the owner alone).
        var byOwner = IsBanditBuilding(owner);
        var under = top != null && BanditTintRules.IsBuildingVfx(id) ? UnderBanditBuilding(top) : null;
        var at = BanditTintRules.OwnerKind(id, byOwner || under != null, hit);
        if (id != BanditTintRules.HitVfx && ownerLines++ < BanditTintRules.OwnerLogCap)
            DelversHost.Info(BanditTintRules.OwnerLine(id, ownerId, SafeKey(owner), byOwner, under, top?.parent?.name, at));
        if (at == null || top == null) return;
        var count = Track(top, ownerId, key);
        if (count > 0 && lines++ < BanditTintRules.LogCap) DelversHost.Info(BanditTintRules.Line(at, ownerId, count, tracked.Count, key));
    }

    private static int ownerLines;
    // The objects of the tracked bandit buildings (the scan fills it), for UnderBanditBuilding.
    private static readonly Dictionary<string, Transform> buildingTops = new(StringComparer.Ordinal);

    // The bandit building whose object holds t (t or one of its parents), or null.
    private static string UnderBanditBuilding(Transform t)
    {
        for (var at = t; at != null; at = at.parent)
            foreach (var pair in buildingTops)
                if (pair.Value != null && pair.Value.Pointer == at.Pointer) return pair.Key;
        return null;
    }

    private static string SafeKey(IEntity entity)
    {
        try { return entity?.Key; }
        catch (Exception) { return "?"; }
    }

    private static int sourceLines;

    // A building by its Guid: confirmed by the scan, else looked up on the map and checked like IsBanditBuilding.
    private static bool IsBanditBuildingId(string id)
    {
        if (buildings.ContainsKey(id)) return true;
        var list = CoreGame.InMap?.GetCombatableBuildingsOnMap();
        for (var i = 0; list != null && i < list.Count; i++)
        {
            var building = list[i];
            if (building != null && building.Cast<IEntity>().Guid.ToString() == id) return IsBanditBuilding(building.Cast<IEntity>());
        }
        return false;
    }

    // A seed or sprayer whose summoner is one of Core's units (cached per building; the scan fills the same list).
    private static bool IsBanditBuilding(IEntity entity)
    {
        if (!BanditTintRules.IsBuilding(entity.Key)) return false;
        var id = entity.Guid.ToString();
        if (buildings.ContainsKey(id)) return true;
        var inMap = CoreGame.InMap;
        if (inMap == null) return false;
        var summoner = SummonedBuildingResolver.GetSummonerUnit(inMap, entity);
        return summoner != null && CoreUnitRules.IsCoreUnit(summoner.Key);
    }
    internal static void OnStop(Il2CppSystem.Guid fx)
    {
        var key = fx.ToString();
        pending.Remove(key);
        if (tracked.Count == 0) return;
        var gone = 0;
        foreach (var pair in new List<KeyValuePair<IntPtr, Tracked>>(tracked))
        {
            if (pair.Value.Fx != key) continue;
            Restore(pair.Value);
            tracked.Remove(pair.Key);
            gone++;
        }
        if (gone > 0 && lines++ < BanditTintRules.LogCap) DelversHost.Info(BanditTintRules.Line("restore", key, gone, tracked.Count));
    }

    // The map's bandit seeds and sprayers: new ones are tracked, gone ones let go.
    private static void Scan()
    {
        var inMap = CoreGame.InMap;
        if (CoreProbes.Tint) ProbeHuds(inMap);
        BanditBossBarProbe.Run();
        var list = inMap?.GetCombatableBuildingsOnMap();
        var alive = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; list != null && i < list.Count; i++)
        {
            var building = list[i];
            if (building == null || building.IsDead || !BanditTintRules.IsBuilding(building.Key)) continue;
            var entity = building.Cast<IEntity>();
            var id = entity.Guid.ToString();
            if (buildings.ContainsKey(id))
            {
                alive.Add(id);
                buildings[id] = inMap.GetWorldPosition(entity.Guid);
                continue;
            }
            if (CoreProbes.Tint)
            {
                var probeTop = ObjectOf(entity.Guid);
                if (probeTop != null) BanditTintProbe.Building(id, probeTop, probeTop.Find("Sprite")?.GetComponent<SpriteRenderer>());
            }
            var summoner = SummonedBuildingResolver.GetSummonerUnit(inMap, entity);
            if (summoner == null || !CoreUnitRules.IsCoreUnit(summoner.Key)) continue;
            var top = ObjectOf(entity.Guid);
            if (top == null) continue;
            alive.Add(id);
            buildings[id] = inMap.GetWorldPosition(entity.Guid);
            buildingTops[id] = top;
            var count = hue == null ? 0 : Track(top, id, null);
            if (lines++ < BanditTintRules.LogCap) DelversHost.Info(BanditTintRules.Line("building:" + building.Key, id, count, tracked.Count));
            // After the tint took the building's renderers, so the bar is never recoloured.
            BanditBar.OnTracked(id, entity, top);
        }
        foreach (var id in new List<string>(buildings.Keys))
        {
            if (alive.Contains(id)) continue;
            buildings.Remove(id);
            buildingTops.Remove(id);
            BanditBar.OnGone(id);
            var gone = 0;
            foreach (var pair in new List<KeyValuePair<IntPtr, Tracked>>(tracked))
            {
                if (pair.Value.Owner != id || pair.Value.Fx != null) continue;
                Restore(pair.Value);
                tracked.Remove(pair.Key);
                gone++;
            }
            if (lines++ < BanditTintRules.LogCap) DelversHost.Info(BanditTintRules.Line("restore", id, gone, tracked.Count));
        }
        ScanSummons();
    }

    // The bandits' summoned units: new living ones tracked under their pivot (the body), and kept while their object is there, so
    // the death on the same object (ApplyDeadAnimation: dead layer, its animation; a bubbleling's BloodType is None, no pooled FX)
    // stays orange too; once the object is gone their sprites go back (the game pools the objects; a reused one must not stay
    // orange for a player's bubbleling).
    private static void ScanSummons()
    {
        var alive = new HashSet<string>(StringComparer.Ordinal);
        foreach (var unit in CoreGame.MapUnits())
        {
            if (unit.IsDead) continue;
            var entity = unit.Cast<IEntity>();
            var id = unit.Guid.ToString();
            var known = noted.Contains(id);
            if (!known && DelversSummons.OwnerOf(entity).Length == 0) continue;
            if (summons.ContainsKey(id))
            {
                alive.Add(id);
                continue;
            }
            if (hue == null || !(known || DelversSummons.IsCoreOwned(entity)))
            {
                if (known) SkipLine(id, unit.Key, DelversSummons.OwnerOf(entity), hue == null ? "nohue" : "notcore");
                continue;
            }
            var pivot = PivotOf(unit.Guid);
            if (pivot == null)
            {
                SkipLine(id, unit.Key, DelversSummons.OwnerOf(entity), CoreGame.EntityObjectOf(view?._objectContainer, unit.Guid) == null ? "noobject" : "nopivot");
                continue;
            }
            alive.Add(id);
            summons[id] = unit.Guid;
            var count = Track(pivot, id, null, TintKind.Summon);
            if (summonLines++ < BanditTintRules.SummonLogCap) DelversHost.Info(BanditTintRules.Line("summon:" + unit.Key, id, count, tracked.Count));
        }
        foreach (var pair in summons)
            if (!alive.Contains(pair.Key) && PivotOf(pair.Value) is { } pivot && pivot.gameObject.activeInHierarchy) alive.Add(pair.Key);
        foreach (var id in new List<string>(summons.Keys))
        {
            if (alive.Contains(id)) continue;
            summons.Remove(id);
            var gone = 0;
            foreach (var pair in new List<KeyValuePair<IntPtr, Tracked>>(tracked))
            {
                if (pair.Value.Owner != id || pair.Value.Kind != TintKind.Summon) continue;
                Restore(pair.Value);
                tracked.Remove(pair.Key);
                gone++;
            }
            if (lines++ < BanditTintRules.LogCap) DelversHost.Info(BanditTintRules.Line("restore", id, gone, tracked.Count));
        }
    }

    // Probe (group Tint): a bandit's summon (noted at its creation) the scan leaves untinted for now, once per summon and reason,
    // with why (the toad and the bubblelings of later fights stayed grey without a line).
    private static readonly HashSet<string> skipped = new(StringComparer.Ordinal);

    private static void SkipLine(string id, string key, string owner, string why)
    {
        if (!CoreProbes.Tint || skipped.Count >= BanditTintRules.OwnerLogCap || !skipped.Add(id + why)) return;
        DelversHost.Info("CORE_SEED_TINT_SUMMON_SKIP id=" + id + " key=" + key + " owner=" + owner + " why=" + why);
    }

    private static Transform PivotOf(Il2CppSystem.Guid guid)
        => CoreGame.EntityObjectOf(view?._objectContainer, guid)?.Graphic?.TryCast<UnitGraphic>()?._pivotTransform;

    // An entity's object, or null (CoreGame.EntityObjectOf: the game throws for one it has not activated yet).
    internal static Transform ObjectOf(Il2CppSystem.Guid guid) => CoreGame.EntityObjectOf(view?._objectContainer, guid)?.Transform;

    // Probe (BanditTintProbe, group Tint): the HUDs of the map's units, each probed once. Scan calls it only while the group is on.
    private static void ProbeHuds(IInMapDataSupplier inMap)
    {
        var container = view?._objectContainer;
        var alive = inMap?.GetAliveUnitsOnMap();
        for (var i = 0; container != null && alive != null && i < alive.Count; i++)
        {
            var unit = alive[i];
            if (unit == null) continue;
            var hud = ObjectOf(unit.Guid)?.GetComponentInChildren<UnitEntityHUD>(true);
            if (hud == null) continue;
            BanditTintProbe.Hud(unit, hud);
            BanditHudFlowProbe.CheckStuck(hud);
            BanditHudFlowProbe.CheckActive(hud);
        }
    }
}

// The game binds an effect to its owner (VFXView.SetVFXOwner): the tint reads the owner.
[HarmonyPatch(typeof(VFXView), nameof(VFXView.SetVFXOwner))]
internal static class BanditTintOwnerPatch
{
    private static void Postfix(VFXView __instance, Il2CppSystem.Guid guid, IEntity owner)
    {
        try { BanditTint.OnOwner(__instance, guid, owner); }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_TINT_FAILED owner: " + ex.Message); }
    }
}

// The game stops an effect (VFXView.StopVFX): before it does, the tint gives the game's sprite back (OnStop).
[HarmonyPatch(typeof(VFXView), nameof(VFXView.StopVFX))]
internal static class BanditTintStopPatch
{
    private static void Prefix(Il2CppSystem.Guid guid)
    {
        try { BanditTint.OnStop(guid); }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_TINT_FAILED stop: " + ex.Message); }
    }
}
