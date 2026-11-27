using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace DungeonSettlersDelvers.Core;

// The formation's tempo (TacticalFormationTempoRules): with the tempo button of the formation window on, outside
// a fight the members with their formation on, the leader too, walk at the speed of the slowest of them; nobody becomes faster.
// Without any patch: the faster members carry stacks of Core's hidden effect (EffectKey, not shown in the
// effect bar, kept out of the save), each taking about one point of MovementSpeed, which the game reads in every update of a move.
// The formation's look (GroupAiFormation, every 0.5 s) sets the stacks: each member's own speed is the speed read plus what its
// stacks take, the cap the slowest own speed, and a faster member gets stacks until its speed is at the cap. What a stack takes is
// measured between two looks, so the effect's kind of value (points or percent) does not matter. A fight, the button off, a member
// switched off, dead or lying, and a look of the formation without the leader's group take the stacks away; a save never holds them.
// CORE_TACTICAL_TEMPO names each change.
internal static class TacticalFormationTempo
{
    private const string Template = "AFFECTER_Slow";

    private sealed class Member
    {
        internal int Stacks;
        internal float Speed = float.NaN;
        internal float PerStack = TacticalFormationTempoRules.FirstPerStack;
    }

    private static readonly Dictionary<Guid, Member> seen = new();
    private static string lastLine;
    // A look of the formation began and no Look followed it: the next begin takes every stack away.
    private static bool pending;
    // Stacks may be on units: a release looks at the whole party once.
    private static bool mayCarry = true;

    internal static void ResetLifecycle()
    {
        seen.Clear();
        lastLine = null;
        pending = false;
        mayCarry = true;
    }

    // Core's effect row: hidden, without duration and categories (no cleanse or immunity of the game takes it), one point of
    // MovementSpeed per stack.
    internal static void Register(AffecterSheet sheet)
    {
        if (sheet?._affecterTable == null) return;
        var (ok, row) = CoreAffecterRows.Add(sheet, TacticalFormationTempoRules.EffectKey, Template,
            "Formation tempo: the faster members walk at the slowest one's speed; Core sets the stacks.", null, false,
            TacticalFormationTempoRules.MaxStacks, false, Array.Empty<string>(), false, exposed: false);
        if (!ok)
        {
            DelversHost.Warning("CORE_TACTICAL_TEMPO_REGISTER_FAILED template=" + Template);
            return;
        }
        if (row == null) return;
        row.AdjustStatsPerStack[StatType.MovementSpeed] = -1f;
        row.AdjustStatsPerStackData = new Il2CppStringArray(new[] { "MovementSpeed_-1" });
        DelversHost.Info($"CORE_TACTICAL_TEMPO_REGISTERED key={TacticalFormationTempoRules.EffectKey} template={Template} absolute={row.IsAbsolute}");
    }

    // Every look of the formation begins here; a look before it that ended without Look (no group, no leader, outside a dungeon)
    // takes the stacks away.
    internal static void BeginLook()
    {
        if (pending) Release();
        pending = true;
    }

    // The formation's look: the counted members are the living ones of the formation with their formation on that can walk. A member
    // lying at near-death or downed gets no order of the formation and does not hold the others to its crawl
    // (after a fight a lying member's speed of 8.8 became the cap).
    internal static void Look(List<UnitEntity> party, HashSet<Guid> members, bool fight, Func<UnitEntity, bool> lying)
    {
        pending = false;
        var on = CoreSaveStore.Current?.FormationTempo == true;
        var counted = new List<(UnitEntity Unit, Guid Guid, Member State, int Stacks, float Speed)>();
        var left = new List<string>();
        foreach (var unit in party)
        {
            var guid = CoreGame.GuidOf(unit);
            if (!members.Contains(guid))
            {
                // Out of the formation that applies (taken out of the set, another set shown): its slowing goes with it.
                if (StacksOf(unit) is var outside and > 0) Set(unit, outside, 0);
                seen.Remove(guid);
                continue;
            }
            var stacks = StacksOf(unit);
            var why = !on || fight ? null : unit.IsDead ? "dead" : !TacticalFormation.IsOn(guid) ? "off" : lying(unit) ? "lying" : null;
            var speed = on && !fight && why == null ? CoreGame.Stat(unit, StatType.MovementSpeed) : float.NaN;
            if (on && !fight && why == null && !(speed > 0f)) why = "still";
            if (!on || fight || why != null)
            {
                if (why != null) left.Add(guid.ToString("D").Substring(0, 8) + ":" + why);
                if (stacks > 0) Set(unit, stacks, 0);
                seen.Remove(guid);
                continue;
            }
            if (!seen.TryGetValue(guid, out var state)) seen[guid] = state = new Member();
            state.PerStack = TacticalFormationTempoRules.PerStack(state.Stacks, state.Speed, stacks, speed, state.PerStack);
            counted.Add((unit, guid, state, stacks, speed));
        }
        var owns = new List<float>();
        foreach (var entry in counted) owns.Add(TacticalFormationTempoRules.Own(entry.Speed, entry.Stacks, entry.State.PerStack));
        var cap = TacticalFormationTempoRules.Cap(on, fight, owns);
        var slowed = new List<string>();
        foreach (var entry in counted)
        {
            var target = cap is { } value ? TacticalFormationTempoRules.Stacks(entry.Stacks, entry.Speed, value, entry.State.PerStack) : 0;
            if (target != entry.Stacks) Set(entry.Unit, entry.Stacks, target);
            entry.State.Stacks = entry.Stacks;
            entry.State.Speed = entry.Speed;
            if (target > 0) slowed.Add(entry.Guid.ToString("D").Substring(0, 8) + ":" + target);
        }
        if (slowed.Count > 0) mayCarry = true;
        // With the button off a fight changes nothing, so the line does not name it.
        var line = TacticalFormationTempoRules.Line(on, on && fight, cap, counted.Count, string.Join(",", left), string.Join(",", slowed));
        if (line != lastLine) GroupAiPolicy.Log(line);
        lastLine = line;
    }

    // For the test hook: the button, the cap of the last look and the stacks on the party's units.
    internal static string Status()
    {
        var stacks = new List<string>();
        foreach (var unit in CoreGame.PlayerUnits(false))
            if (StacksOf(unit) is var count and > 0) stacks.Add(CoreGame.GuidOf(unit).ToString("D").Substring(0, 8) + ":" + count);
        return $"on={(CoreSaveStore.Current?.FormationTempo == true ? 1 : 0)} stacks={(stacks.Count == 0 ? "-" : string.Join(",", stacks))}";
    }

    // Every stack away, from every unit of the party that may carry some, on every loaded map: the party may walk in the dungeon
    // while the player looks at the base.
    private static void Release()
    {
        seen.Clear();
        if (!mayCarry) return;
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null) return;
        foreach (var entry in maps)
            foreach (var unit in CoreGame.PlayerUnits(entry.Key, true))
                if (StacksOf(unit) is var count and > 0) Set(unit, count, 0, entry.Key);
        mayCarry = false;
        const string line = "CORE_TACTICAL_TEMPO released";
        if (lastLine != line) GroupAiPolicy.Log(line);
        lastLine = line;
    }

    private static int StacksOf(UnitEntity unit)
    {
        var affecters = CoreGame.Affecters(unit);
        return affecters != null && affecters.Has(TacticalFormationTempoRules.EffectKey) ? affecters.GetStack(TacticalFormationTempoRules.EffectKey) : 0;
    }

    // On the unit's own map (map, else the one that holds it, else the shown one).
    private static void Set(UnitEntity unit, int from, int to, MapType? map = null)
    {
        var on = map ?? (CoreGame.TryMapOfPartyMember(unit, out var found) ? found : CoreGame.CurrentMap);
        try { CoreGame.Apply(on, new AffecterApplyData(unit.Cast<IEntity>(), TacticalFormationTempoRules.EffectKey, to - from, 0f, false, null).Cast<IApplyData>()); }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_TEMPO_FAILED: " + ex.Message); }
    }
}
