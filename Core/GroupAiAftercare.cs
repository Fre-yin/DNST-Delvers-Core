namespace DungeonSettlersDelvers.Core;

// Aftercare after a fight (GroupAiAftercareRules): every TickSeconds of game time the group AI looks at the party units with an
// open wound. A unit out of combat for QuietSeconds, not down, not in bed, not held by a player's order and with the group AI's
// gate open (GroupAiGate) dresses its own wound with the bandage the rules pick from its quick slots, or from its bag with the
// option groupai.bandagesfrombag. The most urgent unit first, one order per look; the order goes the way the combat bandages go
// (GroupAiBandageUse.Dress) and shares their cooldown. CORE_AI_AFTERCARE per order or change of reason, CORE_AI_AFTERCARE_DONE
// once no party unit bleeds any more. Runs from GroupAiBanditTickPatch; without an open wound and no aftercare running it
// returns at once.
internal static class GroupAiAftercare
{
    private const string SevereDamageKey = "AFFECTER_SevereDamage";
    private static float nextLook;
    private static readonly Dictionary<Guid, float> lastCombat = new();
    private static readonly Dictionary<Guid, string> lastLogged = new();
    // Game seconds of the first order of this aftercare (-1: none running) and the bandages ordered since.
    private static float started = -1f;
    private static int used;

    internal static void Reset()
    {
        nextLook = 0f;
        lastCombat.Clear();
        lastLogged.Clear();
        started = -1f;
        used = 0;
    }

    internal static void OnGameTick()
    {
        if (!Wounds.AnyOpen && started < 0f) return;
        var now = GameClock.Now;
        if (now < nextLook) return;
        nextLook = now + GroupAiAftercareRules.TickSeconds;
        if (!GroupAiSettings.Enabled) return;
        var due = new List<(UnitEntity Unit, Guid Guid, WoundRules.Stage Stage, double Buffer)>();
        var bleeding = 0;
        foreach (var guid in Wounds.OpenUnits.ToList())
        {
            var unit = CoreGame.FindUnit(guid);
            if (unit == null || unit.IsDead || !Wounds.IsPlayer(unit)) continue;
            bleeding++;
            // The stage is a share of the whole health; a wound set before any drain tick has not seen it yet (stage=light at a
            // deep wound in the first order).
            Wounds.SeeMaxLife(unit, guid);
            if (Ready(unit, guid, now)) due.Add((unit, guid, Wounds.Stage(guid), Wounds.Buffer(guid)));
        }
        if (bleeding == 0)
        {
            if (started >= 0f) GroupAiPolicy.Log(GroupAiAftercareRules.DoneLine(now - started, used));
            started = -1f;
            used = 0;
            return;
        }
        due.Sort((a, b) => GroupAiAftercareRules.Urgency((a.Stage, a.Buffer), (b.Stage, b.Buffer)));
        foreach (var unit in due)
            if (TryDress(unit.Unit, unit.Guid, unit.Stage, unit.Buffer, now)) return;
    }

    // Out of combat long enough and free to act.
    private static bool Ready(UnitEntity unit, Guid guid, float now)
    {
        var entity = unit.Cast<IEntity>();
        if (CoreGame.Component<CombatInfoComponent>(entity)?.GetCombatState() == CombatState.InCombat)
        {
            lastCombat[guid] = now;
            return false;
        }
        if (lastCombat.TryGetValue(guid, out var at) && !GroupAiAftercareRules.Quiet(now, at)) return false;
        var key = guid.ToString("D");
        if (!GroupAiGate.OpenFor(unit) || GroupAiStance.PlayerQuiet(key) || GroupAiBandageUse.CoolingDown(key)) return false;
        var affecters = CoreGame.Affecters(unit);
        return affecters == null || (!affecters.Has(SevereDamageKey) && !affecters.Has(Wounds.BedSleepKey));
    }

    // True when a bandage was ordered.
    private static bool TryDress(UnitEntity unit, Guid guid, WoundRules.Stage stage, double buffer, float now)
    {
        var key = guid.ToString("D");
        var entity = unit.Cast<IEntity>();
        var bag = GroupAiBandageUse.Bag(entity);
        var fromBag = CoreSettings.GroupAiBandagesFromBag;
        var slotKinds = GroupAiBandageSlots.Kinds(unit, key);
        var stacks = GroupAiSituation.HasAffecter(unit, Wounds.BleedingKey);
        var item = GroupAiAftercareRules.Pick(stage, buffer, Wounds.MaxLife(guid), stacks,
            kind => GroupAiBandageRules.Ready(kind, slotKinds, bag.TryGetValue(kind, out var found) ? found.Count : 0, fromBag));
        var tick = CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
        var pool = GroupAiBandageUse.Pool(unit);
        if (item == null)
        {
            var none = GroupAiBandageRules.NoneReason(bag.Values.Sum(found => found.Count), fromBag);
            if (lastLogged.TryGetValue(guid, out var last) && last == none) return false;
            lastLogged[guid] = none;
            GroupAiPolicy.Log(GroupAiAftercareRules.Line(key, null, WoundRules.Describe(stage), buffer, pool, none, null, tick));
            return false;
        }
        var from = GroupAiBandageRules.From(item, slotKinds);
        var failed = GroupAiBandageUse.Dress(unit, CoreGame.InMap, key, item, bag.TryGetValue(item, out var picked) ? picked.First : null);
        GroupAiPolicy.Log(GroupAiAftercareRules.Line(key, item, WoundRules.Describe(stage), buffer, pool, failed ?? "use", from, tick));
        lastLogged.Remove(guid);
        if (failed != null) return false;
        if (started < 0f) started = now;
        used++;
        return true;
    }
}
