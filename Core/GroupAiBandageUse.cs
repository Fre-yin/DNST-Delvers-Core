#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using ActCombatBehaviourTask = global::Refactor.Task.ActCombatBehaviourTask;
using TaskBase = global::Refactor.Task.Task_Base;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using ActCombatBehaviourTask = Il2CppRefactor.Task.ActCombatBehaviourTask;
using TaskBase = Il2CppRefactor.Task.Task_Base;
#endif

namespace DungeonSettlersDelvers.Core;

// C: a party unit puts a bandage on itself (GroupAiBandageRules): the salve or the herbal one by the situation, the heavy
// or light one as the last stand-in, from its bag. It goes the way UseSkillMode.UseSkill does: an ActCombatBehaviourTask with the
// item, enqueued as DirectOrderRequested plus ForceTickCombatBehaviour (Core always sends it; the game only for some skills),
// so neither the player's selection nor the control mode is touched.
// Core's own task is known by its pointer (IsOwnTask): GroupAiDirectOrderPatch does not count it as a player's order;
// a bandage the player uses with Q stays a player's order (3 s quiet). At most one per unit every CooldownSeconds, never
// while a player's order holds the unit, while it is provoked or held. CORE_AI_BANDAGE, waits too, once per change.
// The bandages on the item quick slots (Q and the Extended Hotbar's two extra slots, GroupAiBandageSlots);
// from the bag only with the Group AI option on (GroupAiBandageRules.FromBagKey, off by default);
// no AI for potions and the like, the player uses those.
internal static class GroupAiBandageUse
{
    private const int OwnCap = 64;
    private static readonly HashSet<IntPtr> own = new();
    private static readonly Dictionary<string, float> lastUse = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> lastLogged = new(StringComparer.Ordinal);
    // Orders that went out and wait for CORE_BANDAGE_USED (Bandages.Use calls OnUsed); LostSeconds later they count as lost.
    private static readonly Dictionary<string, (UnitEntity Unit, string Item, float Since)> pending = new(StringComparer.Ordinal);
    // The skill each kind is put on with.
    private static readonly Dictionary<string, string> SkillOf = new(StringComparer.Ordinal)
    {
        [GroupAiBandageRules.AidKit] = GroupAiBandageRules.AidKitSkillKey,
        [GroupAiBandageRules.Salve] = Bandages.SalveSkillKey,
        [GroupAiBandageRules.Herbal] = Bandages.HerbalSkillKey,
        [GroupAiBandageRules.Heavy] = Bandages.HeavySkillKey,
        [GroupAiBandageRules.Light] = Bandages.LightSkillKey,
    };

    internal static void Reset()
    {
        own.Clear();
        lastUse.Clear();
        lastLogged.Clear();
        pending.Clear();
        gateLogged.Clear();
    }

    internal static void OnUsed(string unit)
    {
        if (pending.Count > 0 && unit != null) pending.Remove(unit);
    }

    // Every game tick (GroupAiBanditTickPatch): an order without its bandage after LostSeconds, logged once.
    internal static void OnGameTick()
    {
        if (pending.Count == 0) return;
        var now = GameClock.Now;
        List<string> lost = null;
        foreach (var pair in pending)
            if (GameClockRules.Elapsed(now, pair.Value.Since, GroupAiBandageRules.LostSeconds)) (lost ??= new List<string>()).Add(pair.Key);
        if (lost == null) return;
        var tick = CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
        foreach (var guid in lost)
        {
            var entry = pending[guid];
            pending.Remove(guid);
            var unit = entry.Unit;
            var dead = unit == null || unit.IsDead;
            var reason = GroupAiBandageRules.LostReason(dead, !dead && GroupAiSituation.HasAffecter(unit, "AFFECTER_Deadly"),
                !dead && GroupAiSituation.HasAffecter(unit, "AFFECTER_Stun"));
            var behaviour = dead ? null : CoreGame.Component<CombatInfoComponent>(unit.Cast<IEntity>())?.GetCombatBehaviour()?.GetIl2CppType().Name;
            GroupAiPolicy.Log(GroupAiBandageRules.LostLine(guid, entry.Item, now - entry.Since, reason, behaviour, tick));
        }
    }

    internal static bool IsOwnTask(IntPtr task) => task != IntPtr.Zero && own.Contains(task);

    // (lethal wound, only the kit on Q, a stunned foe next to it: no decision in 15 s): why the game's
    // decision did not reach the bandage, only for a heavy or lethal wound, once per unit and reason.
    // CORE_AI_BANDAGE_GATE unit= reason=held|noenemy|gate|gcd|notcombat:<state>|quiet|cooldown stage=
    private static readonly Dictionary<string, string> gateLogged = new(StringComparer.Ordinal);

    internal static void NoteGate(UnitEntity unit, string reason)
    {
        if (unit == null) return;
        var guid = CoreGame.GuidOf(unit);
        var stage = Wounds.Stage(guid);
        if (stage < WoundRules.Stage.Heavy) return;
        var key = guid.ToString("D");
        if (gateLogged.TryGetValue(key, out var last) && last == reason) return;
        if (gateLogged.Count >= 64) gateLogged.Clear();
        gateLogged[key] = reason;
        GroupAiPolicy.Log(GroupAiRules.BandageGateLine(key, reason, stage));
    }

    // True when a bandage was ordered; the caller then leaves the decision to the game.
    internal static bool TryUse(GroupAiSituation s)
    {
        if (s?.Owner == null) return false;
        if (s.State != CombatState.InCombat || GroupAiStance.PlayerQuiet(s.Guid))
        {
            NoteGate(s.Owner, s.State != CombatState.InCombat ? "notcombat:" + s.State : "quiet");
            return false;
        }
        // In an enemy's area the step out comes first, no bandage there (GroupAiAreaRules.BandageAllowed).
        if (!GroupAiAreaRules.BandageAllowed(GroupAiZones.InEnemyArea(s.Owner, s.InMap)))
        {
            NoteGate(s.Owner, "zone");
            return false;
        }
        var unit = s.Owner;
        var entity = unit.Cast<IEntity>();
        if (CoolingDown(s.Guid))
        {
            NoteGate(unit, "cooldown");
            return false;
        }
        var guid = CoreGame.GuidOf(unit);
        var stage = Wounds.Stage(guid);
        var severe = WoundRules.SevereName(stage);
        var severeWound = severe != null;
        var fatal = stage == WoundRules.Stage.Fatal;
        var bleeding = GroupAiSituation.HasAffecter(unit, Wounds.BleedingKey);
        var pool = Pool(unit);
        var trigger = GroupAiBandageRules.Trigger(severe, bleeding || stage != WoundRules.Stage.None, HealAvailable(s, guid), pool, s.Role == GroupAiRole.Heal);
        if (trigger == null) return false;
        var tick = CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
        var stageText = WoundRules.Describe(stage);
        var nearest = s.ClosestEnemy;
        var nearestDistance = nearest?.Distance ?? int.MaxValue;
        var taunted = nearest != null && Neutral(nearest.Unit);
        var info = CoreGame.Component<CombatInfoComponent>(entity);
        if (info?.GetForceAttackTarget() != null || GroupAiMoveWatch.IsHeld(unit))
        {
            Note(s.Guid, "wait:held|-", GroupAiBandageRules.Line(s.Guid, null, trigger, pool, stageText, bleeding, nearestDistance, taunted, "wait:held", null, null,
                CoreSettings.GroupAiBandagesFromBag, new GroupAiBandageRules.BagCounts(-1, -1, -1, -1, -1), tick, false));
            return false;
        }
        var bag = Bag(entity);
        var counts = Counts(bag);
        // The bandages on the item quick slots (Q, extra 1 and 2); from the bag only with the option on.
        var fromBag = CoreSettings.GroupAiBandagesFromBag;
        var slotKinds = GroupAiBandageSlots.Kinds(s);
        var slots = GroupAiBandageRules.Slots(slotKinds);
        var item = GroupAiBandageRules.Pick(GroupAiBandageRules.Want(severeWound, pool, fatal),
            kind => GroupAiBandageRules.Ready(kind, slotKinds, bag.TryGetValue(kind, out var found) ? found.Count : 0, fromBag),
            stage != WoundRules.Stage.None);
        if (item == null)
        {
            var none = GroupAiBandageRules.NoneReason(counts.Bandages + (fatal ? counts.Kits : 0), fromBag);
            Note(s.Guid, none + "|-", GroupAiBandageRules.Line(s.Guid, null, trigger, pool, stageText, bleeding, nearestDistance, taunted, none, null, slots, fromBag, counts, tick, fatal));
            return false;
        }
        var from = GroupAiBandageRules.From(item, slotKinds);
        var safety = GroupAiBandageRules.Safety(nearestDistance, taunted, pool);
        if (safety != "use")
        {
            Note(s.Guid, safety + "|" + item, GroupAiBandageRules.Line(s.Guid, item, trigger, pool, stageText, bleeding, nearestDistance, taunted, safety, from, slots, fromBag, counts, tick, fatal));
            return false;
        }
        var failed = Dress(unit, s.InMap, s.Guid, item, bag.TryGetValue(item, out var picked) ? picked.First : null);
        if (failed != null)
        {
            Note(s.Guid, failed + "|" + item, GroupAiBandageRules.Line(s.Guid, item, trigger, pool, stageText, bleeding, nearestDistance, taunted, failed, from, slots, fromBag, counts, tick, fatal));
            return false;
        }
        GroupAiPolicy.Log(GroupAiBandageRules.Line(s.Guid, item, trigger, pool, stageText, bleeding, nearestDistance, taunted, "use", from, slots, fromBag, counts, tick, fatal));
        return true;
    }

    // Within the unit's cooldown since its last order (in combat or in the aftercare).
    internal static bool CoolingDown(string guid)
        => lastUse.TryGetValue(guid, out var last) && !GameClockRules.Elapsed(GameClock.Now, last, GroupAiBandageRules.CooldownSeconds);

    // The kind from the unit's bag put on the unit itself as Core's own task; null when ordered, else why not. The order starts
    // the unit's cooldown and is followed until CORE_BANDAGE_USED (else CORE_AI_BANDAGE_LOST).
    internal static string Dress(UnitEntity unit, IInMapDataSupplier inMap, string guid, string kind, ItemHolder holder)
    {
        var failed = Order(unit, inMap, holder, SkillOf[kind]);
        if (failed != null) return failed;
        var now = GameClock.Now;
        lastUse[guid] = now;
        if (pending.Count >= OwnCap) pending.Clear();
        pending[guid] = (unit, kind, now);
        lastLogged.Remove(guid);
        return null;
    }

    // The weaker of head and body, as a share of its maximum.
    internal static float Pool(UnitEntity unit)
    {
        var head = Share(CoreGame.Stat(unit, StatType.HealthHead), CoreGame.Stat(unit, StatType.MaxHealthHead));
        var body = Share(CoreGame.Stat(unit, StatType.HealthBody), CoreGame.Stat(unit, StatType.MaxHealthBody));
        return Math.Min(head, body);
    }

    private static float Share(float value, float max) => max > 0f ? Math.Max(0f, value) / max : 1f;

    // "Everything else on cooldown": for the healer her Lifebloom without a charge and the Strange Seed not free; for the
    // others the healer's Lifebloom without a charge and fewer than 2 stacks on them; without a healer nothing is free.
    private static bool HealAvailable(GroupAiSituation s, Guid guid)
    {
        if (s.Role == GroupAiRole.Heal)
            return Lifebloom.ChargesOf(guid) > 0 || (s.Find("SKILL_StrangeSeed")?.Free ?? false);
        foreach (var ally in s.Allies)
        {
            if (GroupAiRoles.Known(ally.Guid) != GroupAiRole.Heal || ally.Unit == null || ally.Unit.IsDead) continue;
            if (Lifebloom.ChargesOf(CoreGame.GuidOf(ally.Unit)) > 0) return true;
            return Lifebloom.BuffOn(guid, out var stacks, out _) && stacks >= 2;
        }
        return false;
    }

    // A foe that cannot strike now: taunted (its forced target set) or held (stunned, rooted).
    private static bool Neutral(UnitEntity foe)
        => foe != null && (CoreGame.Component<CombatInfoComponent>(foe.Cast<IEntity>())?.GetForceAttackTarget() != null || GroupAiMoveWatch.IsHeld(foe));

    // The bag's bandages and aid kits by kind: the first holder and how many.
    internal static Dictionary<string, (ItemHolder First, int Count)> Bag(IEntity entity)
    {
        var found = new Dictionary<string, (ItemHolder First, int Count)>(StringComparer.Ordinal);
        var bag = CoreGame.Component<InventoryComponent>(entity);
        for (var i = 0; bag != null && i < bag.GetCapacity(); i++)
        {
            var holder = bag.GetItemInfo(i);
            if (holder == null || holder.IsEmpty()) continue;
            var kind = GroupAiBandageRules.KindOf(holder.Key);
            if (kind == null) continue;
            found[kind] = found.TryGetValue(kind, out var known)
                ? (known.First, known.Count + Math.Max(1, holder.Amount))
                : (holder, Math.Max(1, holder.Amount));
        }
        return found;
    }

    private static GroupAiBandageRules.BagCounts Counts(Dictionary<string, (ItemHolder First, int Count)> bag)
    {
        int Of(string kind) => bag.TryGetValue(kind, out var found) ? found.Count : 0;
        return new GroupAiBandageRules.BagCounts(Of(GroupAiBandageRules.Salve), Of(GroupAiBandageRules.Herbal),
            Of(GroupAiBandageRules.Heavy), Of(GroupAiBandageRules.Light), Of(GroupAiBandageRules.AidKit));
    }

    // Null when ordered, else why not (failed:holder|map|handler|skill|item), so the log names the missing part.
    private static string Order(UnitEntity unit, IInMapDataSupplier inMap, ItemHolder holder, string skillKey)
    {
        if (holder == null) return "failed:holder";
        if (!CoreGame.TryMapOfUnit(unit, out var map)) return "failed:map";
        var handler = CoreGame.TickHandler(map);
        if (handler == null) return "failed:handler";
        var skill = CombatDataManager.Instance?.GetActiveSkillData(skillKey);
        if (skill == null) return "failed:skill";
        var item = ItemOf(inMap, map, holder);
        if (item == null) return "failed:item";
        Enqueue(handler, unit.Cast<IEntity>(), skill, item, true);
        return null;
    }

    // The player's own use of an item on the unit itself (AidKitStanding, Q on a standing unit): the same order, but it
    // stays a player's order (not Core's own task). Null when ordered, else why not.
    internal static string OrderOnSelf(UnitEntity unit, ItemEntity item, string skillKey)
    {
        if (unit == null || item == null) return "failed:item";
        if (!CoreGame.TryMapOfUnit(unit, out var map)) return "failed:map";
        var handler = CoreGame.TickHandler(map);
        if (handler == null) return "failed:handler";
        var skill = CombatDataManager.Instance?.GetActiveSkillData(skillKey);
        if (skill == null) return "failed:skill";
        Enqueue(handler, unit.Cast<IEntity>(), skill, item, false);
        return null;
    }

    private static void Enqueue(TickFlowHandler handler, IEntity entity, ActiveSkill_Base skill, ItemEntity item, bool ownTask)
    {
        var task = new ActCombatBehaviourTask(skill, EntityTargetSelector.Self().Cast<ICombatTargetSelector>(), item, entity);
        if (ownTask)
        {
            if (own.Count >= OwnCap) own.Clear();
            own.Add(task.Pointer);
        }
        handler.EnqueueEvent(new DirectOrderRequested(entity, task.Cast<TaskBase>()).Cast<IEventData>());
        handler.EnqueueEvent(new ForceTickCombatBehaviour(entity).Cast<IEventData>());
    }

    // The bag item as the game finds it (PlayerControlEventHandler.OnEvent(UseItemRequested) → EntityDataContainer.GetEntityOnMap):
    // the map's entities first, then the entity container behind the map data (InMapDataInterface._entityContainer). Toolkit's
    // short run on 0910B10D: InMap.GetEntity found no bag item (failed:item 6x).
    private static ItemEntity ItemOf(IInMapDataSupplier inMap, MapType map, ItemHolder holder)
    {
        var item = inMap?.GetEntity(holder.Guid)?.TryCast<ItemEntity>();
        if (item != null) return item;
        var container = inMap?.TryCast<InMapDataInterface>()?._entityContainer;
        return container?.GetEntityOnMap<ItemEntity>(map, holder.Guid);
    }

    // One line per unit and change of result, so a wait does not repeat every decision.
    private static void Note(string unit, string key, string line)
    {
        if (lastLogged.TryGetValue(unit, out var last) && last == key) return;
        lastLogged[unit] = key;
        GroupAiPolicy.Log(line);
    }
}
