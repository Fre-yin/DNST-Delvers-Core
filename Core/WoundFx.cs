using UnityEngine;
#if BEPINEX
using CreateVFXRequest = global::Refactor.FX.CreateVFXRequest;
using IFXRequest = global::Refactor.FX.IFXRequest;
using SetOwnerVFXRequest = global::Refactor.FX.SetOwnerVFXRequest;
using SetVFXLocalPositionRequest = global::Refactor.FX.SetVFXLocalPositionRequest;
using StopVFXRequest = global::Refactor.FX.StopVFXRequest;
using VFXID = global::Refactor.FX.VFXID;
#else
using CreateVFXRequest = Il2CppRefactor.FX.CreateVFXRequest;
using IFXRequest = Il2CppRefactor.FX.IFXRequest;
using SetOwnerVFXRequest = Il2CppRefactor.FX.SetOwnerVFXRequest;
using SetVFXLocalPositionRequest = Il2CppRefactor.FX.SetVFXLocalPositionRequest;
using StopVFXRequest = Il2CppRefactor.FX.StopVFXRequest;
using VFXID = Il2CppRefactor.FX.VFXID;
#endif

namespace DungeonSettlersDelvers.Core;

// The bleeding wound on the body (the wound shows at the unit in every stage, a
// sparse blood trail only for a lethal wound). Today the trail starts at a heavy wound (WoundRules.LeavesTrail).
// Wounds.Tick calls OnTick every second for a unit with an open wound and EndTick once after all
// units. It plays the game's own effects through the game's view dispatcher, the way the game does:
// - the pop: VFXID.Bleeding, the game's short bleeding pop (a red splash with the bleeding icon,
//   AffecterFX_Bleeding.OnBleedFX), at most once in PopMinSeconds when the wound drains; the mode
//   "loop" plays the same;
// - bound to the unit at the game's height (0, (SpriteHeight - 1) * 0.4; constants from
//   GameAssembly .rdata), a unit lying at near-death at the middle of the body: at the game's height
//   the effect floated above the lying unit (probe);
// - the trail: the ground stains of WoundRules.GroundStain, not bound. The game's stains stay
//   until stopped, so each lives WoundRules.TrailStainSeconds of game time (timed, Expire) and a
//   unit keeps at most WoundRules.TrailMaxStains, the oldest is stopped;
// - each request as ViewFXRequestData(UpdateType.FXRequested, map, request), as in
//   EntityEventHandler.RemoveHUD.
// Bleeding_Always looks like a pool; bound to the unit at foot
// height (SetOwnerVFXRequest) it walked along. It is never bound to a unit any more: on the body only the short pop
// (mode Pop, the default; "loop" is kept as a name for the Toolkit and plays the same), on the ground small stains at
// every stage and the pool from a deep wound on, laid at the cell without an owner, each for WoundRules.SpotSeconds
// (BloodSpot, Spot).
// Taken back: the
// short pop at body height stays bound to the unit (Show) as the game's own; only what lies on the ground is unbound.
// The small red scratches sit bound on the body, one to three
// by stage, renewed while the wound drains (BodyMarks); on the ground only the pool (deep on) and the lethal trail.
// The author's sorting of the assets: on the body the pop Bleeding (01) and Bleeding_Always (02, the marks); on the
// ground HitBloodGround_red_B and _C (04, 05) for the pool and the trail; HitBloodGround_red (03) is not used. Number,
// lifetime and height of the marks stay as.
// The height is the game's own: AffecterFX_Bleeding.OnBleedFX sets (0, (SpriteHeight - 1) * 0.4).
// 's pool under the unit was the Bleeding_Always picture, which
// looks like a pool, not this height.
// Nothing is saved: the effects are view only. The mode is a switch for the Toolkit
// (DelversCoreRuntime.SetWoundFxModeForTest) until the author has chosen; the probe stays as well
// (DelversCoreRuntime.PlayWoundFxForTest).
internal static class WoundFx
{
    internal enum Kind { Drop, Center, Ground, Always, Spot, Body, Lying }
    internal enum Mode { Off, Pop, Loop }

    // An effect Core started and must stop itself at its end tick.
    private readonly struct Placed
    {
        internal Il2CppSystem.Guid Fx { get; }
        internal MapType Map { get; }
        internal Guid Owner { get; }
        internal Kind Anchor { get; }
        internal bool Stain { get; }
        internal long Until { get; }

        internal Placed(Il2CppSystem.Guid fx, MapType map, Guid owner, Kind anchor, bool stain, long until)
        {
            Fx = fx;
            Map = map;
            Owner = owner;
            Anchor = anchor;
            Stain = stain;
            Until = until;
        }
    }

    private const float HeightBase = 1f;
    private const float HeightScale = 0.4f;
    private const double PopMinSeconds = 2.0;

    internal static Mode Current { get; private set; } = Mode.Pop;

    private static int counter;
    private static readonly List<Placed> timed = new();
    private static readonly Dictionary<Guid, double> sinceShown = new();
    private static readonly Dictionary<Guid, Vector2Int> lastStain = new();
    private static readonly Dictionary<Guid, (Vector2Int Cell, long Tick)> lastSpot = new();
    private static readonly Dictionary<Guid, (Vector2Int Cell, double Seconds)> still = new();
    private static readonly HashSet<Guid> announced = new();

    // A load or a new campaign: every effect Core still holds is stopped.
    internal static void ResetCampaign()
    {
        var stains = 0;
        foreach (var placed in timed)
        {
            Stop(placed);
            if (placed.Stain) stains++;
        }
        if (stains > 0) DelversHost.Info("CORE_WOUND_FX_STAINS_GONE count=" + stains + " reason=load");
        timed.Clear();
        sinceShown.Clear();
        lastStain.Clear();
        lastSpot.Clear();
        still.Clear();
        announced.Clear();
        skipLogged.Clear();
        groundLines = 0;
        WoundBodyArt.ResetCampaign();
    }

    // The wound closed: what it showed starts anew with the next wound. Stains on the ground stay
    // until their lifetime ends.
    internal static void Forget(Guid unit)
    {
        WoundBodyArt.OnWound(null, unit, WoundRules.Stage.None, false);
        sinceShown.Remove(unit);
        lastStain.Remove(unit);
        lastSpot.Remove(unit);
        still.Remove(unit);
        announced.Remove(unit);
    }

    internal static bool TryParseMode(string text, out Mode mode)
    {
        switch (text)
        {
            case "loop": mode = Mode.Loop; return true;
            case "pop": mode = Mode.Pop; return true;
            case "off": mode = Mode.Off; return true;
            default: mode = Mode.Loop; return false;
        }
    }

    internal static void SetMode(Mode mode)
    {
        Current = mode;
        sinceShown.Clear();
        announced.Clear();
    }

    // Every second of Core's clock for a unit with an open wound (Wounds.Tick): seconds of game time
    // since the last call, drained points in this call, the game tick.
    internal static void OnTick(MapType map, IInMapDataSupplier inMap, UnitEntity unit, Guid guid, WoundRules.Stage stage, bool lying,
        double seconds, int drained, long tick)
    {
        if (Current == Mode.Off || stage == WoundRules.Stage.None) return;
        var dispatcher = CoreGame.Flow?._viewDispatcher;
        if (dispatcher == null || inMap == null || unit == null) return;
        if (Skipped(unit)) return;
        WoundBodyArt.OnWound(unit, guid, stage, lying);
        var anchor = lying ? Kind.Center : Kind.Drop;
        // Pop and Loop alike; nothing that looks like a pool is bound to the unit.
        var since = sinceShown.TryGetValue(guid, out var last) ? last + Math.Max(0, seconds) : double.PositiveInfinity;
        if (drained > 0 && since >= PopMinSeconds)
        {
            Show(dispatcher, map, inMap, unit, anchor, VFXID.Bleeding, out _);
            since = 0;
        }
        sinceShown[guid] = since;
        var here = inMap.GetLogicalPosition(unit.Guid);
        var knownStill = still.TryGetValue(guid, out var before);
        var stood = WoundRules.StillSeconds(knownStill, knownStill ? WoundRules.Cells(here.x - before.Cell.x, here.y - before.Cell.y) : 0,
            knownStill ? before.Seconds : 0, seconds);
        still[guid] = (here, stood);
        if (drained > 0)
        {
            Spots(dispatcher, map, inMap, unit, guid, stage, stood, tick);
            // Standing, his drawn body blood is the only blood on the body (WoundBodyArt); lying, the marks
            // look like pools and lie on the ground (LyingMarks).
            if (lying && WoundRules.LyingMarksAllowed(stood, CoreSettings.PoolStillSeconds)) LyingMarks(dispatcher, map, inMap, unit, guid, stage, tick);
            Trail(dispatcher, map, inMap, unit, guid, stage, lying, tick);
        }
        if (announced.Add(guid))
            DelversHost.Info($"CORE_WOUND_FX_ACTIVE unit={guid:D} stage={WoundRules.Describe(stage)} mode={Describe(Current)} anchor={Describe(anchor)}");
    }

    // After every unit of the tick (Wounds.Tick): the body blood of a unit not ticked goes; stains past
    // their lifetime go.
    internal static void EndTick(HashSet<Guid> ticked, long tick)
    {
        WoundBodyArt.EndTick(ticked);
        Expire(tick);
    }

    // Stops the effects whose end tick has come.
    internal static void Expire(long tick)
    {
        if (timed.Count == 0) return;
        var stains = 0;
        for (var i = timed.Count - 1; i >= 0; i--)
        {
            if (timed[i].Until > tick) continue;
            Stop(timed[i]);
            if (timed[i].Stain) stains++;
            timed.RemoveAt(i);
        }
        if (stains > 0) DelversHost.Info("CORE_WOUND_FX_STAINS_GONE count=" + stains + " reason=lifetime");
    }

    // A stain or the pool where the unit bleeds, when it moved since the last one (WoundRules.BloodSpot).
    private static void Spots(ViewUpdateDispatcher dispatcher, MapType map, IInMapDataSupplier inMap, UnitEntity unit, Guid guid, WoundRules.Stage stage,
        double stood, long tick)
    {
        var cell = inMap.GetLogicalPosition(unit.Guid);
        var known = lastSpot.TryGetValue(guid, out var last);
        var since = known ? Math.Max(0, tick - last.Tick) / WoundRules.TicksPerSecond : 0;
        var kind = WoundRules.BloodSpot(stage, known, known ? WoundRules.Cells(cell.x - last.Cell.x, cell.y - last.Cell.y) : 0, stood,
            CoreSettings.PoolStillSeconds, since);
        if (kind == null) return;
        var detail = Spot(dispatcher, map, inMap, unit, guid, tick);
        if (groundLines++ < 200) DelversHost.Info(FormattableString.Invariant($"CORE_WOUND_FX_SPOT unit={guid:D} cell={cell.x},{cell.y} still={stood:0.0} {detail} tick={tick}"));
        lastSpot[guid] = (cell, tick);
    }

    // On the ground at the unit's place, no owner: it stays where it fell and goes after SpotSeconds; the unit's oldest
    // goes first beyond SpotMaxPerUnit.
    private static string Spot(ViewUpdateDispatcher dispatcher, MapType map, IInMapDataSupplier inMap, UnitEntity unit, Guid owner, long tick)
    {
        var fx = GuidGenerator.GuidInt(unit.Guid, ++counter).Generate();
        // The author's sorting: on the ground only HitBloodGround_red_B and _C (04, 05); Bleeding_Always (02) sits
        // on the body, HitBloodGround_red (03) is not used.
        var vfx = (VFXID)WoundRules.GroundStain(counter);
        Send(dispatcher, map, new CreateVFXRequest(fx, vfx, inMap.GetWorldPosition(unit.Guid)).Cast<IFXRequest>());
        timed.Add(new Placed(fx, map, owner, Kind.Spot, false, WoundRules.TickAfter(tick, WoundRules.SpotSeconds)));
        var count = 0;
        foreach (var placed in timed)
            if (placed.Anchor == Kind.Spot && placed.Owner == owner) count++;
        for (var i = 0; i < timed.Count && count > WoundRules.SpotMaxPerUnit; i++)
        {
            if (timed[i].Anchor != Kind.Spot || timed[i].Owner != owner) continue;
            Stop(timed[i]);
            timed.RemoveAt(i--);
            count--;
        }
        return FormattableString.Invariant($"vfx={vfx} owner=none lifetime={WoundRules.SpotSeconds:0}");
    }

    // Small spread of the scratches over the body (local x, y added to the game's height).
    private static readonly (float X, float Y)[] MarkSpread = { (0f, 0f), (-0.14f, 0.08f), (0.12f, -0.07f) };

    // (picture 18: "that is a pool, not the bleeding itself"): a lying unit's marks (HitBloodGround_red,
    // _B, _C in turn, WoundRules.BodyMarkPicture) look like pools, so they lie on the ground where she lies, not bound to
    // her: CreateVFX at her place with the small spread, no owner. As many as the stage asks (WoundRules.BodyMarks), each
    // living SpotSeconds; they stay when she gets up or is pulled, and do not go with a closed wound.
    // CORE_WOUND_FX_GROUND kind=lying, capped.
    private static int groundLines;

    private static void LyingMarks(ViewUpdateDispatcher dispatcher, MapType map, IInMapDataSupplier inMap, UnitEntity unit, Guid owner,
        WoundRules.Stage stage, long tick)
    {
        var want = WoundRules.BodyMarks(stage);
        var have = 0;
        foreach (var placed in timed)
            if (placed.Anchor == Kind.Lying && placed.Owner == owner) have++;
        var at = inMap.GetWorldPosition(unit.Guid);
        for (var i = have; i < want; i++)
        {
            var fx = GuidGenerator.GuidInt(unit.Guid, ++counter).Generate();
            var vfx = (VFXID)WoundRules.BodyMarkPicture(counter);
            var spread = MarkSpread[i % MarkSpread.Length];
            Send(dispatcher, map, new CreateVFXRequest(fx, vfx, new Vector2(at.x + spread.X, at.y + spread.Y)).Cast<IFXRequest>());
            timed.Add(new Placed(fx, map, owner, Kind.Lying, false, WoundRules.TickAfter(tick, WoundRules.SpotSeconds)));
            if (groundLines++ < 200) DelversHost.Info(WoundRules.GroundLine(owner.ToString("D"), "lying", (int)vfx, i + 1, want, tick));
        }
    }

    private static void Trail(ViewUpdateDispatcher dispatcher, MapType map, IInMapDataSupplier inMap, UnitEntity unit, Guid guid, WoundRules.Stage stage,
        bool lying, long tick)
    {
        var cell = inMap.GetLogicalPosition(unit.Guid);
        var known = lastStain.TryGetValue(guid, out var last);
        var cells = known ? WoundRules.Cells(cell.x - last.x, cell.y - last.y) : 0;
        if (!WoundRules.LeavesTrail(stage, known, cells)) return;
        var detail = Ground(dispatcher, map, inMap, unit, guid, tick);
        // The trail's stain is the pool's picture (45, 46); its own line tells them apart (a pool at arrival
        // without a SPOT line).
        if (groundLines++ < 200)
            DelversHost.Info(FormattableString.Invariant($"CORE_WOUND_FX_TRAIL unit={guid:D} cell={cell.x},{cell.y} stage={WoundRules.Describe(stage)} lying={(lying ? 1 : 0)} first={(known ? 0 : 1)} {detail} tick={tick}"));
        lastStain[guid] = cell;
    }

    // Lifebloom's heal ticks show the small heal effect the Strange Seed's aura plays on a unit it heals
    // (AffecterFX_StrangeSeedAura.OnHealFX: VFXID.Heal, bound to the unit at the game's height). Any unit on a map, the
    // herbalist too; the effect ends by itself.
    // A unit the tests keep out of the AI (GroupAiExcluded, the hidden bystander) has no active object; a
    // VFX owned by it ended in the game's "CheckActivateOrFlag" error. Its wound still counts, Core draws nothing on it.
    // CORE_WOUND_FX_SKIP once per unit.
    private static readonly HashSet<string> skipLogged = new(StringComparer.Ordinal);

    internal static bool Skipped(UnitEntity unit)
    {
        var id = unit?.Guid.ToString();
        if (!GroupAiExcluded.Has(id)) return false;
        if (skipLogged.Count < 64 && skipLogged.Add(id)) DelversHost.Info(WoundRules.FxSkipLine(id, "excluded"));
        return true;
    }

    internal static void PlayHeal(UnitEntity unit)
    {
        if (Skipped(unit)) return;
        var dispatcher = CoreGame.Flow?._viewDispatcher;
        if (dispatcher == null || unit == null || !CoreGame.TryMapOfUnit(unit, out var map)) return;
        var maps = CoreGame.Flow?._inMapDatas;
        var inMap = maps != null && maps.ContainsKey(map) ? maps[map] : null;
        if (inMap == null) return;
        Show(dispatcher, map, inMap, unit, Kind.Drop, VFXID.Heal, out _);
    }

    // ---- probe ----

    internal static bool TryParse(string text, out Kind kind)
    {
        switch (text)
        {
            case "drop": kind = Kind.Drop; return true;
            case "center": kind = Kind.Center; return true;
            case "ground": kind = Kind.Ground; return true;
            case "always": kind = Kind.Always; return true;
            case "spot": kind = Kind.Spot; return true;
            default: kind = Kind.Drop; return false;
        }
    }

    // The log line of the effect; throws when the game is not ready or the unit is not on a map.
    // "always" lays the pool and "spot" a small stain on the ground, without an owner, for SpotSeconds.
    internal static string Play(Guid unitGuid, Kind kind)
    {
        var dispatcher = CoreGame.Flow?._viewDispatcher ?? throw new InvalidOperationException("the game's view dispatcher is not ready");
        if (!TryFind(unitGuid, out var map, out var inMap, out var unit)) throw new InvalidOperationException("unit " + unitGuid.ToString("D") + " is on no map");
        var position = inMap.GetWorldPosition(unit.Guid);
        var tick = CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
        string detail;
        switch (kind)
        {
            case Kind.Ground:
                detail = Ground(dispatcher, map, inMap, unit, unitGuid, tick);
                break;
            case Kind.Always:
            case Kind.Spot:
                // The pool only on the ground, never bound to the unit.
                detail = Spot(dispatcher, map, inMap, unit, unitGuid, tick);
                break;
            default:
                detail = Show(dispatcher, map, inMap, unit, kind, VFXID.Bleeding, out _);
                break;
        }
        var line = FormattableString.Invariant(
            $"CORE_WOUND_FX kind={Describe(kind)} unit={unitGuid:D} map={map} pos=({position.x:0.00},{position.y:0.00}) {detail}");
        DelversHost.Info(line);
        return line;
    }

    // ---- the game's effects ----

    // Bound to the unit: at the game's height (Drop) or the middle of the body (Center). The caller
    // keeps fx to stop it.
    private static string Show(ViewUpdateDispatcher dispatcher, MapType map, IInMapDataSupplier inMap, UnitEntity unit, Kind anchor,
        VFXID vfx, out Il2CppSystem.Guid fx)
    {
        fx = GuidGenerator.GuidInt(unit.Guid, ++counter).Generate();
        var offset = Offset(unit, anchor, out var height);
        Send(dispatcher, map, new CreateVFXRequest(fx, vfx, inMap.GetWorldPosition(unit.Guid)).Cast<IFXRequest>());
        Send(dispatcher, map, new SetOwnerVFXRequest(fx, unit.Cast<IEntity>()).Cast<IFXRequest>());
        Send(dispatcher, map, new SetVFXLocalPositionRequest(fx, new Vector3(0f, offset, 0f)).Cast<IFXRequest>());
        return FormattableString.Invariant($"vfx={vfx} anchor={Describe(anchor)} spriteHeight={height:0.00} offset={offset:0.00}");
    }

    private static float Offset(UnitEntity unit, Kind anchor, out float height)
    {
        height = DataSheetManager.Instance?.GetUnitData(unit.Key)?.SpriteHeight ?? HeightBase;
        return anchor == Kind.Center ? 0f : (height - HeightBase) * HeightScale;
    }

    private static string Ground(ViewUpdateDispatcher dispatcher, MapType map, IInMapDataSupplier inMap, UnitEntity unit, Guid owner, long tick)
    {
        var guid = GuidGenerator.GuidInt(unit.Guid, ++counter).Generate();
        var vfx = (VFXID)WoundRules.GroundStain(counter);
        Send(dispatcher, map, new CreateVFXRequest(guid, vfx, inMap.GetWorldPosition(unit.Guid)).Cast<IFXRequest>());
        timed.Add(new Placed(guid, map, owner, Kind.Ground, true, WoundRules.TickAfter(tick, WoundRules.TrailStainSeconds)));
        var count = 0;
        foreach (var placed in timed)
            if (placed.Stain && placed.Owner == owner) count++;
        // The unit's oldest stains first: timed keeps them in the order they were placed.
        var stopped = 0;
        for (var i = 0; i < timed.Count; i++)
        {
            if (count <= WoundRules.TrailMaxStains) break;
            if (!timed[i].Stain || timed[i].Owner != owner) continue;
            Stop(timed[i]);
            timed.RemoveAt(i--);
            count--;
            stopped++;
        }
        return FormattableString.Invariant($"vfx={vfx} lifetime={WoundRules.TrailStainSeconds:0} stopped={stopped}");
    }

    // Through the current dispatcher: a stop can come after a map change or a load.
    private static void Stop(Placed placed)
    {
        try
        {
            var dispatcher = CoreGame.Flow?._viewDispatcher;
            if (dispatcher != null) Send(dispatcher, placed.Map, new StopVFXRequest(placed.Fx).Cast<IFXRequest>());
        }
        catch (Exception ex) { ApocalypseHooks.Report("wound-fx-stop", ex); }
    }

    private static void Send(ViewUpdateDispatcher dispatcher, MapType map, IFXRequest request)
        => dispatcher.AddViewUpdateCall(new ViewFXRequestData(UpdateType.FXRequested, map, request).Cast<IViewUpdateData>());

    private static bool TryFind(Guid unitGuid, out MapType map, out IInMapDataSupplier inMap, out UnitEntity unit)
    {
        map = default;
        inMap = null;
        unit = null;
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null || unitGuid == Guid.Empty) return false;
        foreach (var pair in maps)
        {
            var units = pair.Value?.GetAllPlayerUnits(true);
            for (var i = 0; units != null && i < units.Count; i++)
            {
                if (units[i] == null || CoreGame.GuidOf(units[i]) != unitGuid) continue;
                map = pair.Key;
                inMap = pair.Value;
                unit = units[i];
                return true;
            }
        }
        return false;
    }

    private static string Describe(Kind kind) => kind switch
    {
        Kind.Center => "center",
        Kind.Ground => "ground",
        Kind.Always => "always",
        Kind.Spot => "spot",
        _ => "drop"
    };

    private static string Describe(Mode mode) => mode switch
    {
        Mode.Pop => "pop",
        Mode.Off => "off",
        _ => "loop"
    };
}
