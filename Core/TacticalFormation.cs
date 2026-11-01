namespace DungeonSettlersDelvers.Core;

// The tactical formation of the running campaign: the group, its leader and its places, kept in Core's
// block of the save (CoreSaveData.Formations). In a dungeon the group is the whole expedition, every living party member on
// the dungeon map; in the village it is the members selected when the window opened, so a group can be planned before it
// leaves. The campaign keeps one formation book of eight sets (TacticalFormationBook); the active set is the one that counts.
// This is the interface the group AI builds on: Group, TryGet, IsOn, PriorityCasts and the rules
// (TacticalFormationRules.Next for the turn, Place for the cells).
internal static class TacticalFormation
{
    // The party members on the shown map who can stand in a formation: alive, not summoned. Dungeon or village alike.
    internal static List<UnitEntity> MapMembers()
    {
        var result = new List<UnitEntity>();
        foreach (var unit in CoreGame.PlayerUnits(false))
            if (string.IsNullOrEmpty(CoreGame.SummonerGuid(unit.Cast<IEntity>()))) result.Add(unit);
        return result;
    }

    internal static bool InDungeon => CoreGame.CurrentMap == MapType.Dungeon;

    // The group for the window: in a dungeon the expedition; in the village the selected party members, in their order.
    internal static List<UnitEntity> Group(IReadOnlyList<UnitEntity> selected)
    {
        if (InDungeon) return MapMembers();
        var members = MapMembers();
        var result = new List<UnitEntity>();
        foreach (var unit in selected ?? Array.Empty<UnitEntity>())
            if (unit != null && members.Exists(member => member.Pointer == unit.Pointer) && !result.Exists(known => known.Pointer == unit.Pointer))
                result.Add(unit);
        return result;
    }

    internal static List<Guid> Guids(IReadOnlyList<UnitEntity> units)
    {
        var result = new List<Guid>(units.Count);
        foreach (var unit in units) result.Add(CoreGame.GuidOf(unit));
        return result;
    }

    // The active formation for the members there now: its leader and the places relative to it (TacticalFormationBook.Apply).
    // False without a campaign, without an active set or without its leader among them.
    internal static bool TryGet(IReadOnlyList<Guid> group, out Guid leader, out List<TacticalFormationRules.Slot> slots)
    {
        var book = Book();
        return TacticalFormationBook.Apply(book.Sets[book.Active], group, out leader, out slots);
    }

    // Whether the member keeps its place (the button switches it like the game's options). Off, the member
    // neither follows nor holds a place, and its place stays empty; the leader stays the middle even when switched off, and the
    // priority casts count either way. On unless the player switched it off.
    internal static bool IsOn(Guid unit) => CoreSaveStore.Current?.IsFormationOff(unit) != true;

    // On for every member when all are on, as the button shows a selection.
    internal static bool AllOn(IReadOnlyList<UnitEntity> units)
    {
        foreach (var unit in units)
            if (!IsOn(CoreGame.GuidOf(unit))) return false;
        return units.Count > 0;
    }

    // The members switched on or off together, as the game does for a selection; false without a campaign. A switch by hand
    // while asleep is the player's will: the member leaves the sleepers and does not come back on waking.
    internal static bool Switch(IReadOnlyList<UnitEntity> units, bool on)
    {
        var data = CoreSaveStore.Current;
        if (data == null || units.Count == 0) return false;
        foreach (var unit in units)
        {
            var guid = CoreGame.GuidOf(unit);
            data.SetFormationOff(guid, !on);
            if (data.IsFormationSleep(guid))
            {
                data.SetFormationSleep(guid, false);
                DelversHost.Info(TacticalFormationSleepRules.Line("hand", guid, TacticalFormationSleepRules.Step.Forget, TacticalFormationSleep.IsRestFirst(unit)));
            }
        }
        DelversHost.Info($"CORE_TACTICAL_SWITCH units={units.Count} on={on}");
        return true;
    }

    // The campaign's book, an empty one without a campaign.
    internal static TacticalFormationBook Book() => CoreSaveStore.Current?.FormationBook() ?? new TacticalFormationBook();

    // A member's priority casts: learned active skills the player chose, the first place first, empty
    // places left out. The group AI uses them in this order when they are ready and make sense; an empty list leaves it alone.
    internal static IReadOnlyList<string> PriorityCasts(Guid unit)
    {
        var result = new List<string>();
        var saved = CoreSaveStore.Current?.PriorityCasts();
        if (saved == null || !saved.TryGetValue(unit, out var keys)) return result;
        foreach (var key in keys)
            if (!string.IsNullOrEmpty(key)) result.Add(key);
        return result;
    }

    // The member's learned active skills in the game's order (SkillComponent._activeSkills), for the window's choice.
    internal static List<string> ActiveSkills(UnitEntity unit)
    {
        var result = new List<string>();
        var skills = CoreGame.Component<SkillComponent>(unit?.Cast<IEntity>())?._activeSkills;
        for (var i = 0; skills != null && i < skills.Count; i++)
            if (!string.IsNullOrEmpty(skills[i]) && !result.Contains(skills[i])) result.Add(skills[i]);
        return result;
    }

    // Whether a priority cast takes part: the skill is on for the game's auto skill, and the member
    // itself has auto skill on; the window shows the others faded. The formation helps members the player is not steering.
    internal static bool CastTakesPart(UnitEntity unit, string skill)
    {
        if (unit == null || string.IsNullOrEmpty(skill)) return false;
        var entity = unit.Cast<IEntity>();
        var info = CoreGame.Component<EntityInfoComponent>(entity);
        if (info != null && !info.IsBehaviourOption(BehaviourOption.AutoAttack)) return false;
        return CoreGame.Component<SkillComponent>(entity)?.GetAutoSkillData(skill) != false;
    }

    // The saved priority casts of every member, for the window.
    internal static IReadOnlyDictionary<Guid, string[]> AllPriorityCasts()
        => CoreSaveStore.Current?.PriorityCasts() ?? new Dictionary<Guid, string[]>();

    // The window closed: the book as the window left it goes into Core's block with the shown set active, each member's casts
    // beside it. Members who are gone (dead, dismissed) leave every set, but only while the village map is loaded: then every
    // living party member stands on a loaded map.
    internal static void Save(TacticalFormationBook book, TacticalFormationGrid grid)
    {
        var data = CoreSaveStore.Current;
        if (data == null || book == null || grid == null || grid.Leader == Guid.Empty) return;
        data.SetFormationBook(book);
        var casts = grid.AllCasts();
        data.SetPriorityCasts(grid.Members, casts);
        var living = LivingPartyMembers();
        var dropped = living == null ? 0 : data.DropFormationUnits(living.Contains);
        var set = book.Sets[book.Active];
        DelversHost.Info($"CORE_TACTICAL_SAVED set={book.Active + 1} leader={grid.Leader:D} places={set?.Places.Count ?? 0} casts={casts.Count} dropped={dropped}");
    }

    // The living party members on every loaded map; null while the village map is not loaded.
    private static HashSet<Guid> LivingPartyMembers()
    {
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null || !maps.ContainsKey(MapType.Base)) return null;
        var living = new HashSet<Guid>();
        foreach (var entry in maps)
        {
            var units = entry.Value?.GetAllPlayerUnits(false);
            for (var i = 0; units != null && i < units.Count; i++)
                if (units[i] != null) living.Add(CoreGame.GuidOf(units[i]));
        }
        return living;
    }
}
