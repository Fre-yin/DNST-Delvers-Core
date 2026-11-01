using System.Collections;
using System.Globalization;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Brings a dead party member back (taken over from the toolkit's
// UnitRevive, tested there, research artifacts\recherche-wiederbeleben-20261005).
// The game has no revive of its own; Core uses its map-transfer path on the corpse:
// ExtractUnitFrom, SetDead(false), InsertUnitTo. That rebuilds what the death removed (occupancy,
// party lists, combat, vision, tick registration, passives). Health, the permanent traits (from
// ReviveSnapshots) and the view come back from Core. It runs over a few frames:
// health on the corpse, the corpse's view removed, the unit inserted again, everything read back.
// A failure puts the corpse back as it was. Revives per character follow the combat difficulty.
internal static class ReviveService
{
    internal const string AfterViewRemoved = "after-view-removed";

    // Reason codes, agreed with the Modding Toolkit.
    internal const string NoCampaign = "no-campaign";
    internal const string Busy = "busy";
    internal const string NotFound = "not-found";
    internal const string Alive = "alive";
    internal const string NotPlayer = "not-player";
    internal const string NotOnMap = "not-on-map";
    internal const string Buried = "buried";
    internal const string Carried = "carried";
    internal const string DeathPending = "death-pending";
    internal const string CellOccupied = "cell-occupied";
    internal const string NobodyAwake = "nobody-awake";
    internal const string NoRevivesLeft = "no-revives-left";
    internal const string NoSnapshot = "no-snapshot";
    internal const string Error = "error";
    internal const string Test = "test";

    private enum Phase { Idle, Vitals, Dispose, Verify }

    private static Phase phase = Phase.Idle;
    private static Guid reviveGuid;
    private static MapType reviveMap;
    private static Vector2Int reviveCell;
    // Where the body lay; a failed reinsertion puts the corpse back here, not on a neighbour cell.
    private static Vector2Int corpseCell;
    private static ReviveRules.ReviveCellKind reviveCellKind;
    private static int expectedPlayers;
    private static float phaseDeadline;
    private static List<TraitStack> restoreTraits = new();
    private static List<TraitStack> restoreStay = new();
    private static float beforeHead, beforeBody;
    private static bool viewDisposed;
    private static string injectedFailure;
    private static readonly Dictionary<Guid, int> testRevivesLeft = new();

    internal static bool Running => phase != Phase.Idle;
    internal static string LastResult { get; private set; } = "";

    // Hook for the revival sickness; called once the unit stands again.
    internal static Action<UnitEntity> AfterRevive { get; set; }

    internal static void ResetLifecycle()
    {
        phase = Phase.Idle;
        LastResult = "";
        injectedFailure = null;
        testRevivesLeft.Clear();
    }

    internal static void ResetCampaign()
    {
        phase = Phase.Idle;
        injectedFailure = null;
        testRevivesLeft.Clear();
    }

    // -1 means unlimited.
    internal static int RevivesLeft(Guid guid)
    {
        if (testRevivesLeft.TryGetValue(guid, out var forced)) return forced;
        var used = CoreSaveStore.Current?.RevivesUsed(guid) ?? 0;
        return ReviveRules.RevivesLeft(CoreGame.CombatLevel(), used);
    }

    internal static void SetRevivesLeftForTest(Guid guid, int left) => testRevivesLeft[guid] = Math.Max(0, left);

    internal static void InjectFailureForTest(string where) => injectedFailure = where;

    // Null when the corpse may be revived, otherwise a reason code.
    internal static string Blocker(Guid guid)
    {
        if (Running) return Busy;
        if (!CoreGame.CampaignReady || CoreSaveStore.Current == null) return NoCampaign;
        var unit = CoreGame.FindUnit(guid);
        if (unit == null) return NotFound;
        if (!unit.IsDead) return Alive;
        if (unit.Faction != FactionType.Player) return NotPlayer;
        if (!IsPartyMemberHere(guid)) return NotOnMap;
        try
        {
            if (CoreGame.Flow._campaignData.GraveOwnerContainer.HasGrave(unit.Guid)) return Buried;
        }
        catch (Exception) { return Error; }
        if (CoreGame.Component<CarryComponent>(unit.Cast<IEntity>())?.IsCarried() == true) return Carried;
        if (!DeathProcessed(unit)) return DeathPending;
        // A living unit on the body's cell (usually the caster, who walks onto it to cast) only
        // blocks when no neighbour cell is free either (ReviveCellFor).
        try { if (ReviveCellFor(unit, out _) == null) return CellOccupied; }
        catch (Exception) { return Error; }
        if (CoreGame.CurrentMap == MapType.Dungeon && !SomeoneAwake(guid)) return NobodyAwake;
        if (RevivesLeft(guid) == 0) return NoRevivesLeft;
        if (CoreSaveStore.Current.Snapshot(guid).Count == 0) return NoSnapshot;
        return null;
    }

    // Null when the revive has started; it finishes over the next frames (LastResult).
    internal static string Start(Guid guid)
    {
        var blocker = Blocker(guid);
        if (blocker != null) return blocker;
        var unit = CoreGame.FindUnit(guid);
        reviveGuid = guid;
        reviveMap = CoreGame.CurrentMap;
        corpseCell = CoreGame.Position(unit);
        reviveCell = ReviveCellFor(unit, out reviveCellKind) ?? corpseCell;
        expectedPlayers = CoreGame.PlayerUnits(true).Count;
        restoreTraits = new List<TraitStack>(CoreSaveStore.Current.Snapshot(guid));
        restoreStay = reviveMap == MapType.Dungeon ? StayEffects(guid) : new List<TraitStack>();
        beforeHead = CoreGame.Stat(unit, StatType.HealthHead);
        beforeBody = CoreGame.Stat(unit, StatType.HealthBody);
        viewDisposed = false;
        LastResult = "";
        DelversHost.Info($"CORE_REVIVE_REQUEST unit={guid:D} map={reviveMap} cell={reviveCell} body={corpseCell}"
            + $" revivedCell={ReviveRules.Describe(reviveCellKind)} traits={restoreTraits.Count}"
            + " beforeHead=" + beforeHead.ToString("0.00", CultureInfo.InvariantCulture)
            + " beforeBody=" + beforeBody.ToString("0.00", CultureInfo.InvariantCulture));

        // Paused during the change, the player resumes; not in Apocalypse, which has no pause.
        if (!ApocalypseTime.Applies) CoreGame.Enqueue(new SetGameSpeedRequested(GameSpeed.Pause).Cast<IEventData>());
        try { CoreGame.Flow._eventProcessor.SelectProcessor.Clear(); }
        catch (Exception) { /* Nothing selected: nothing to clear. */ }

        // Health on the still dead corpse; the game's death only fires on a change to <= 0.
        var entity = unit.Cast<IEntity>();
        SetHealth(unit, entity, StatType.HealthHead, StatType.MaxHealthHead);
        SetHealth(unit, entity, StatType.HealthBody, StatType.MaxHealthBody);
        phase = Phase.Vitals;
        phaseDeadline = Time.unscaledTime + 5f;
        DelversHost.StartCoroutine(Run());
        return null;
    }

    // 10 % of the full maximum, not of one Core lowered for a down before the death.
    private static void SetHealth(UnitEntity unit, IEntity entity, StatType stat, StatType max)
    {
        var target = Math.Max(1f, ReviveSickness.FullMaxHealth(unit, max) * (float)ReviveRules.StartHealthShare);
        CoreGame.Apply(reviveMap, new StatusApplyData(entity, stat, target - CoreGame.Stat(unit, stat), StatusApplyPersistence.Normal).Cast<IApplyData>());
    }

    private static IEnumerator Run()
    {
        while (Running)
        {
            yield return null;
            try { Step(); }
            catch (Exception ex) { Finish(false, Error, ex); }
        }
    }

    private static void Step()
    {
        var unit = CoreGame.FindUnit(reviveGuid);
        switch (phase)
        {
            case Phase.Vitals:
                // Health read back on the corpse, then the corpse's view removed like the game's
                // own EntityEventHandler.DespawnUnit.
                if (unit == null) { Finish(false, NotFound); return; }
                if (!(CoreGame.Stat(unit, StatType.HealthHead) > 0f))
                {
                    if (Time.unscaledTime > phaseDeadline) Finish(false, Error, new InvalidOperationException("health could not be set"));
                    return;
                }
                var entity = unit.Cast<IEntity>();
                CoreGame.Flow._objectLoader.EntityLoader.EnqueueDispose(entity);
                viewDisposed = true;
                View(UpdateType.EntityUnfocused, entity);
                View(UpdateType.EntityDespawned, entity);
                View(UpdateType.PlayerUnitDespawned, entity);
                phase = Phase.Dispose;
                phaseDeadline = Time.unscaledTime + 3f;
                return;

            case Phase.Dispose:
                if (unit == null) { Finish(false, NotFound); return; }
                if (HasView(unit))
                {
                    if (Time.unscaledTime > phaseDeadline) Finish(false, Error, new InvalidOperationException("the corpse's view was not removed"));
                    return;
                }
                if (injectedFailure == AfterViewRemoved)
                {
                    injectedFailure = null;
                    Finish(false, Test);
                    return;
                }
                Reinsert(unit);
                phase = Phase.Verify;
                phaseDeadline = Time.unscaledTime + 10f;
                return;

            case Phase.Verify:
                var problem = VerifyProblem();
                if (problem == null) Finish(true, null);
                else if (Time.unscaledTime > phaseDeadline) Finish(false, Error, new InvalidOperationException("read-back failed: " + problem));
                return;
        }
    }

    // One synchronous block, never repeated automatically: the game's transfer pair rebuilds the
    // runtime data; SetDead(false) in between makes InsertUnitTo take its living branch.
    private static void Reinsert(UnitEntity unit)
    {
        var flow = CoreGame.Flow;
        var helper = flow._campaignHelperSet.EntityLifecycleHelper;
        var component = CoreGame.Component<EntityInfoComponent>(unit.Cast<IEntity>())
                        ?? throw new InvalidOperationException("EntityInfoComponent missing");
        var info = helper.ExtractUnitFrom(reviveMap, unit);
        try
        {
            if (info.Unit == null || CoreGame.GuidOf(info.Unit) != reviveGuid)
                throw new InvalidOperationException("extracting returned another unit");
            component.SetDead(false);
            helper.InsertUnitTo(reviveMap, info, reviveCell);
        }
        catch (Exception)
        {
            // Back into the map as a corpse, where it lay: InsertUnitTo's dead branch.
            try
            {
                component.SetDead(true);
                helper.InsertUnitTo(reviveMap, info, corpseCell);
                DelversHost.Warning($"CORE_REVIVE_ROLLBACK unit={reviveGuid:D} data=corpse");
            }
            catch (Exception inner) { DelversHost.Error($"CORE_REVIVE_ROLLBACK_FAILED unit={reviveGuid:D}: {inner.Message}"); }
            throw;
        }

        // View like the game's own recruiting.
        var entity = unit.Cast<IEntity>();
        flow._objectLoader.EntityLoader.EnqueueActivate(entity);
        View(UpdateType.EntityFocused, entity);
        View(UpdateType.EntitySpawned, entity);
        View(UpdateType.PlayerUnitSpawned, entity);
        flow._viewDispatcher.AddViewUpdateCall(new ViewAnimationData(UpdateType.PlayAnimation, reviveMap, entity,
            AnimationType.IsDead, AnimationParamType.Bool_False, 0).Cast<IViewUpdateData>());
        View(UpdateType.EntityStatusChanged, entity);

        // The traits the death removed, and in the dungeon the stay level a comrade has.
        foreach (var trait in restoreTraits)
            CoreGame.Apply(reviveMap, new AffecterApplyData(entity, trait.Key, trait.Stacks, 0f, false, null).Cast<IApplyData>());
        foreach (var stay in restoreStay)
            CoreGame.Apply(reviveMap, new AffecterApplyData(entity, stay.Key, stay.Stacks, 0f, false, null).Cast<IApplyData>());
        DelversHost.Info($"CORE_REVIVE_INSERTED unit={reviveGuid:D} traits={restoreTraits.Count} stay={restoreStay.Count}");
        try { AfterRevive?.Invoke(unit); }
        catch (Exception ex) { DelversHost.Warning("CORE_REVIVE_AFTER_FAILED " + ex.Message); }
    }

    private static string VerifyProblem()
    {
        var unit = CoreGame.FindUnit(reviveGuid);
        if (unit == null) return "unit not found";
        if (unit.IsDead) return "still dead";
        if (!(CoreGame.Stat(unit, StatType.HealthHead) > 0f)) return "no health";
        if (SafePosition(unit) != reviveCell) return "another position";
        if (CoreGame.PlayerUnits(true).Count != expectedPlayers) return "party size changed";
        var living = 0;
        foreach (var member in CoreGame.PlayerUnits(false)) if (CoreGame.GuidOf(member) == reviveGuid) living++;
        if (living != 1) return "not exactly once among the living";
        var affecters = CoreGame.Affecters(unit);
        foreach (var trait in restoreTraits)
            if (affecters?.Has(trait.Key) != true) return "trait missing: " + trait.Key;
        foreach (var stay in restoreStay)
            if (affecters?.Has(stay.Key) != true) return "stay level missing: " + stay.Key;
        if (!HasView(unit)) return "no view";
        return null;
    }

    private static void Finish(bool ok, string code, Exception error = null)
    {
        var guid = reviveGuid;
        if (ok)
        {
            var data = CoreSaveStore.Current;
            if (data != null) data.SetRevivesUsed(guid, data.RevivesUsed(guid) + 1);
            if (testRevivesLeft.TryGetValue(guid, out var left) && left > 0) testRevivesLeft[guid] = left - 1;
            LastResult = "ok:" + guid.ToString("D");
            Wounds.ClearAll(CoreGame.FindUnit(guid), "scroll");
            Wounds.Close(guid, "revive");
            DelversHost.Info($"CORE_REVIVE_RESULT unit={guid:D} ok=true cell={reviveCell} revivedCell={ReviveRules.Describe(reviveCellKind)}"
                + $" traits={restoreTraits.Count} revivesLeft={RevivesLeft(guid)}");
        }
        else
        {
            DelversHost.Error($"CORE_REVIVE_RESULT unit={guid:D} ok=false phase={phase} reason={code}{(error != null ? " " + error.Message : "")}");
            try { RestoreCorpse(); }
            catch (Exception ex) { DelversHost.Error($"CORE_REVIVE_ROLLBACK_FAILED unit={guid:D}: {ex.Message}"); }
            LastResult = "failed:" + guid.ToString("D") + ":" + code;
        }
        phase = Phase.Idle;
    }

    // A failed revive leaves the corpse as it was: health back and the body visible again. The health is set on the status
    // itself (StatusComponent.SetValue), not through the game's status apply: there a player unit's head falling from above 0 to
    // 0 is a head hit, whether dead or not, and gives near-death (SevereDamage, Deadly, the head refilled to DeadlyRecovery,
    // 80 %), as showed on the rolled back corpse.
    private static void RestoreCorpse()
    {
        var unit = CoreGame.FindUnit(reviveGuid);
        if (unit == null || !unit.IsDead) return;
        var entity = unit.Cast<IEntity>();
        var status = CoreGame.Component<StatusComponent>(entity) ?? throw new InvalidOperationException("StatusComponent missing");
        status.SetValue(StatType.HealthHead, beforeHead);
        status.SetValue(StatType.HealthBody, beforeBody);
        if (viewDisposed && !HasView(unit))
        {
            CoreGame.Flow._objectLoader.EntityLoader.EnqueueActivate(entity);
            View(UpdateType.EntitySpawned, entity);
            View(UpdateType.EntityDead, entity);
        }
        DelversHost.Warning($"CORE_REVIVE_ROLLBACK unit={reviveGuid:D} view={viewDisposed.ToString().ToLowerInvariant()}");
    }

    private static bool IsPartyMemberHere(Guid guid)
    {
        foreach (var member in CoreGame.PlayerUnits(true)) if (CoreGame.GuidOf(member) == guid) return true;
        return false;
    }

    private static bool SomeoneAwake(Guid except)
    {
        foreach (var member in CoreGame.PlayerUnits(false))
            if (CoreGame.GuidOf(member) != except && CoreGame.Affecters(member)?.Has(ReviveSnapshots.DeadlyKey) != true) return true;
        return false;
    }

    private static Vector2Int? SafePosition(UnitEntity unit)
    {
        try { return CoreGame.Position(unit); }
        catch (Exception) { return null; }
    }

    // The cell the revived character gets (ReviveRules.ChooseReviveCell): the body's cell when no
    // living unit stands on it, otherwise the first free neighbour. Free means movable, not blocked
    // and no living unit. Null when no cell is free.
    internal static Vector2Int? ReviveCellFor(UnitEntity body, out ReviveRules.ReviveCellKind kind)
    {
        var cell = CoreGame.Position(body);
        var probe = new CellProbe(CoreGame.InMap);
        kind = ReviveRules.ChooseReviveCell(cell.x, cell.y, !probe.HasLiving(cell.x, cell.y), probe.IsFree, out var x, out var y);
        return kind == ReviveRules.ReviveCellKind.None ? null : new Vector2Int(x, y);
    }

    // For the scroll's log line before the revive starts: where the character would stand.
    internal static string DescribeReviveCell(Guid guid)
    {
        try
        {
            var unit = CoreGame.FindUnit(guid);
            if (unit == null) return " revivedCell=none";
            var cell = ReviveCellFor(unit, out var kind);
            return $" revive={(cell.HasValue ? cell.Value.ToString() : "none")} revivedCell={ReviveRules.Describe(kind)}";
        }
        catch (Exception ex) { return " revivedCell=error-" + ex.GetType().Name; }
    }

    // Living units by cell, read once per check; passed to ReviveRules as a method group. A lambda as a
    // MatchEvaluator and one in ToDictionary broke with CS0656 (NullableAttribute, missing from the IL2CPP
    // reference set), so none here.
    private sealed class CellProbe
    {
        private readonly IInMapDataSupplier inMap;
        private readonly HashSet<long> living = new();

        internal CellProbe(IInMapDataSupplier inMap)
        {
            this.inMap = inMap;
            foreach (var other in CoreGame.MapUnits())
            {
                if (other.IsDead) continue;
                var at = SafePosition(other);
                if (at.HasValue) living.Add(Key(at.Value.x, at.Value.y));
            }
        }

        internal bool HasLiving(int x, int y) => living.Contains(Key(x, y));

        internal bool IsFree(int x, int y)
        {
            if (inMap == null || HasLiving(x, y)) return false;
            var cell = new Vector2Int(x, y);
            try { return inMap.IsMovable(cell) && !inMap.IsBlocked(cell); }
            catch (Exception) { return false; }
        }

        private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }

    // The death is processed when no queued game event of this unit is left; otherwise a late
    // death event would unregister the revived unit or remove its restored traits.
    private static bool DeathProcessed(UnitEntity unit)
    {
        var handler = CoreGame.TickHandler(CoreGame.CurrentMap);
        if (handler == null) return false;
        var entities = Related(unit);
        return !handler.HasRelatedEvent(handler._eventQueue, entities) && !handler.HasRelatedEvent(handler._forceQueue, entities);
    }

    private static Il2CppSystem.Collections.Generic.IReadOnlyCollection<IEntity> Related(UnitEntity unit)
    {
        var list = new Il2CppSystem.Collections.Generic.List<IEntity>();
        list.Add(unit.Cast<IEntity>());
        return list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<IEntity>>();
    }

    // Details for the log line of a refused scroll click (diagnosis on DS_B.0.4.29):
    // who stands on the body's cell, or which game queue still holds an event of the body.
    internal static string DescribeBlocker(string blocker, Guid guid, UnitEntity caster)
    {
        try
        {
            var unit = CoreGame.FindUnit(guid);
            if (unit == null) return string.Empty;
            if (blocker == CellOccupied)
            {
                var cell = CoreGame.Position(unit);
                var casterGuid = CoreGame.GuidOf(caster);
                // cell-occupied now means: someone stands on the body and no neighbour is free.
                foreach (var other in CoreGame.MapUnits())
                    if (!other.IsDead && SafePosition(other) == cell)
                    {
                        var by = CoreGame.GuidOf(other);
                        return $" by={by:D} self={(by == casterGuid ? "yes" : "no")} neighbours=none-free";
                    }
                return " neighbours=none-free";
            }
            if (blocker == DeathPending)
            {
                var handler = CoreGame.TickHandler(CoreGame.CurrentMap);
                if (handler == null) return " handler=none";
                var entities = Related(unit);
                return $" events={handler._eventQueue?.Count ?? -1} forced={handler._forceQueue?.Count ?? -1}"
                    + $" relatedEvent={(handler.HasRelatedEvent(handler._eventQueue, entities) ? "yes" : "no")}"
                    + $" relatedForced={(handler.HasRelatedEvent(handler._forceQueue, entities) ? "yes" : "no")}";
            }
        }
        catch (Exception ex) { return " detail-error=" + ex.GetType().Name; }
        return string.Empty;
    }

    // The dungeon stay level comes only on entering; the revived unit takes it from a comrade.
    private static List<TraitStack> StayEffects(Guid guid)
    {
        foreach (var member in CoreGame.PlayerUnits(false))
        {
            if (CoreGame.GuidOf(member) == guid || member.IsSummonEntity()) continue;
            var affecters = CoreGame.Affecters(member);
            var keys = affecters?.GetAllByCategory(AffecterCategory.DungeonStay);
            if (keys == null || keys.Count == 0) continue;
            var result = new List<TraitStack>();
            for (var i = 0; i < keys.Count; i++) result.Add(new TraitStack(keys[i], Math.Max(1, affecters.GetStack(keys[i]))));
            return result;
        }
        return new List<TraitStack>();
    }

    private static bool HasView(UnitEntity unit)
        => CoreGame.Flow._viewContainer.EntityObjectContainer._activatedObjects.ContainsKey(unit.Guid);

    private static void View(UpdateType type, IEntity entity)
        => CoreGame.Flow._viewDispatcher.AddViewUpdateCall(new ViewEntityData(type, reviveMap, entity).Cast<IViewUpdateData>());
}
