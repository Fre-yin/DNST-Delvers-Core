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

    // The active formation for the members there now: its middle and the places relative to it (TacticalFormationBook.Apply, the
    // set's fixed selection; the top member there now is the middle while the leader is away). The members' living summons on a
    // place of the set stand there too, under the summon's own GUID (SummonsOf); a summon is no member of the group, so a caller that
    // only moves members passes over them. False without a campaign, without an active set or without any of its members there.
    internal static bool TryGet(IReadOnlyList<Guid> group, out Guid leader, out List<TacticalFormationRules.Slot> slots)
    {
        var book = Book();
        var set = book.Sets[book.Active];
        var summons = new Dictionary<Guid, Guid>();
        if (set != null && set.Summons.Count > 0)
            foreach (var (place, unit) in SummonsOf(set.Members())) summons[place.Id] = GuidOf(unit);
        return TacticalFormationBook.Apply(set, group, out leader, out slots, summons);
    }

    // A member's role in the active formation (each formation keeps its own); Neutral when it has none.
    internal static TacticalRole RoleOf(Guid unit)
    {
        var book = Book();
        return book.Sets[book.Active]?.RoleOf(unit) ?? TacticalRole.Neutral;
    }

    // Every role of the active formation, for the group AI's look (GroupAiFormation.RoleOf); members without one are Neutral.
    internal static Dictionary<Guid, TacticalRole> ActiveRoles()
    {
        var book = Book();
        var set = book.Sets[book.Active];
        return set == null ? new Dictionary<Guid, TacticalRole>() : new Dictionary<Guid, TacticalRole>(set.Roles);
    }

    // A party member wherever it is: on the shown map, on another loaded map (the village while the group is in the dungeon, or
    // the other way round) or dead; Unit null when no loaded map holds it.
    internal static (UnitEntity Unit, MapType Map, bool Here, bool Dead) Locate(Guid member)
    {
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null) return (null, MapType.Base, false, false);
        foreach (var entry in maps)
        {
            var units = entry.Value?.GetAllPlayerUnits(true);
            for (var i = 0; units != null && i < units.Count; i++)
                if (units[i] != null && GuidOf(units[i]) == member)
                    return (units[i], entry.Key, entry.Key == CoreGame.CurrentMap, units[i].IsDead);
        }
        return (null, MapType.Base, false, false);
    }

    // Down at near-death or downed, still to be rescued: it cannot walk, its place stays reserved.
    internal static bool IsDown(UnitEntity unit)
        => unit != null && !unit.IsDead
           && WoundRules.IsLying(CoreGame.Affecters(unit)?.Has(ReviveSnapshots.DeadlyKey) == true, GroupAiBandits.IsDowned(unit));

    // The living party members on every loaded map who can stand in a formation (not summoned), with their map; the shown map's
    // first.
    internal static List<(UnitEntity Unit, MapType Map)> PartyEverywhere()
    {
        var result = new List<(UnitEntity, MapType)>();
        foreach (var unit in MapMembers()) result.Add((unit, CoreGame.CurrentMap));
        var maps = CoreGame.Flow?._inMapDatas;
        if (maps == null) return result;
        foreach (var entry in maps)
        {
            if (entry.Key == CoreGame.CurrentMap) continue;
            var units = entry.Value?.GetAllPlayerUnits(false);
            for (var i = 0; units != null && i < units.Count; i++)
                if (units[i] != null && string.IsNullOrEmpty(CoreGame.SummonerGuid(units[i].Cast<IEntity>()))) result.Add((units[i], entry.Key));
        }
        return result;
    }

    // The numbers of the living summons for the session, per summoner and kind (TacticalFormationSummons.Number).
    private static readonly Dictionary<(Guid Summoner, string Kind), Dictionary<Guid, int>> summonNumbers = new();

    // The living summons of these members on the shown map with their places: summoner, kind (the unit key) and number, the cell
    // left at 0 (TacticalFormationSummons.Place). The numbers stay while a summon lives; a new one takes the lowest free number of
    // its summoner and kind, so it stands where one that died stood. In the order they appeared.
    internal static List<(TacticalFormationSummons.Place Place, UnitEntity Unit)> SummonsOf(IReadOnlyList<Guid> group)
    {
        var result = new List<(TacticalFormationSummons.Place, UnitEntity)>();
        if (group == null || group.Count == 0) return result;
        var found = new Dictionary<(Guid, string), List<UnitEntity>>();
        var kinds = new List<(Guid, string)>();
        // Every side is searched (DelversSummons.SummonsOf): a player's bubbleling is an Ally and stands in no list of the player's
        // units (the window found no summon of the mage).
        foreach (var summoner in group)
            foreach (var entity in DelversSummons.SummonsOf(summoner.ToString("D")))
            {
                var unit = entity?.TryCast<UnitEntity>();
                var kind = unit?.Key;
                if (unit == null || string.IsNullOrEmpty(kind)) continue;
                if (!found.TryGetValue((summoner, kind), out var living))
                {
                    found[(summoner, kind)] = living = new List<UnitEntity>();
                    kinds.Add((summoner, kind));
                }
                living.Add(unit);
            }
        var gone = new List<(Guid, string)>();
        foreach (var key in summonNumbers.Keys)
            if (Contains(group, key.Summoner) && !found.ContainsKey(key)) gone.Add(key);
        foreach (var key in gone) summonNumbers.Remove(key);
        foreach (var key in kinds)
        {
            var living = found[key];
            var guids = Guids(living);
            summonNumbers.TryGetValue(key, out var known);
            var numbers = TacticalFormationSummons.Number(known, guids);
            summonNumbers[key] = numbers;
            for (var i = 0; i < living.Count; i++)
                result.Add((new TacticalFormationSummons.Place(key.Item1, key.Item2, numbers[guids[i]], 0, 0), living[i]));
        }
        return result;
    }

    // The session's summon numbers go with the campaign.
    internal static void ForgetSummons() => summonNumbers.Clear();

    private static Guid GuidOf(UnitEntity unit) => CoreGame.GuidOf(unit);

    private static bool Contains(IReadOnlyList<Guid> units, Guid unit)
    {
        foreach (var known in units)
            if (known == unit) return true;
        return false;
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
            GroupAiFormation.ForgetHand(guid);
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
    // beside it, those of every set the window showed (seen). Members who are gone (dead, dismissed) leave every set, but only
    // while the village map is loaded: then every living party member stands on a loaded map.
    internal static void Save(TacticalFormationBook book, TacticalFormationGrid grid, IReadOnlyDictionary<Guid, string[]> seen = null)
    {
        var data = CoreSaveStore.Current;
        if (data == null || book == null || grid == null) return;
        data.SetFormationBook(book);
        var casts = new Dictionary<Guid, string[]>();
        var members = new List<Guid>(grid.Members);
        foreach (var pair in seen ?? new Dictionary<Guid, string[]>())
        {
            if (!members.Contains(pair.Key)) members.Add(pair.Key);
            if (Array.Exists(pair.Value, key => !string.IsNullOrEmpty(key))) casts[pair.Key] = pair.Value;
        }
        foreach (var pair in grid.AllCasts()) casts[pair.Key] = pair.Value;
        data.SetPriorityCasts(members, casts);
        var living = LivingPartyMembers();
        var dropped = living == null ? 0 : data.DropFormationUnits(living.Contains);
        var set = book.Sets[book.Active];
        DelversHost.Info($"CORE_TACTICAL_SAVED set={book.Active + 1} leader={grid.Leader:D} places={set?.Places.Count ?? 0} summons={set?.Summons.Count ?? 0} casts={casts.Count} dropped={dropped}");
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
