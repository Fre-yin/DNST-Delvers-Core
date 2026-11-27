using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// "Im Samen bleiben" for the party (SeedStayRules, the bandits' side in BanditSeedStay): a member of the
// group AI at or below seeds.stay.below walks once into the aura of the nearest seed of a party summoner and stays there until
// seeds.stay.until. The emergency share of the party's heals (GroupAiRules.EmergencyShare, 0.35) is another setting and stays.
// - The walk: one move order (GroupAiZones.OrderMove, followed by GroupAiMoveWatch) to the free cell of the aura nearest to the
//   member; the game keeps its own choice for that decision, as with a dodge. When the order ends outside the aura or the seed is
//   gone, the stay at that seed is over until the member rose above the entry share.
// - Inside the aura the member counts as held (Holds), as a member of the formation on its place: a skill on foes that reaches
//   none from where it stands is left out, its target is the nearest foe in reach, the game's walk toward one out of reach is
//   cancelled (GroupAiFormation). So it strikes what is in reach and otherwise stays. Dodging still comes first; the formation
//   does not send a staying member to its place.
// CORE_AI_SEED_STAY side=party per step change. The entry asks the share with look ahead and buffer (GroupAiLow, seed line; the
// exit stays at seeds.stay.until). Heal seeking: a member without a heal skill that finds no usable seed walks
// once into the reach of the nearest party healer instead (SeedStayRules.HealerReach) and stays there the same way;
// CORE_AI_HEAL_SEEK side=party.
internal static class GroupAiSeedStay
{
    private sealed class Stay
    {
        // The seed's GUID, or the healer's while Healer.
        internal string Seed;
        internal bool Healer;
        internal bool Ordered;
        internal bool Ended;
        internal bool InArea;
        internal string Step;
    }

    private static readonly Dictionary<string, Stay> stays = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, (int, int, int)> noSeedLogged = new(StringComparer.Ordinal);
    private static double below = SeedStayRules.BelowDefault, until = SeedStayRules.UntilDefault;
    private static int lines;

    internal static void Reset()
    {
        stays.Clear();
        noSeedLogged.Clear();
        below = SeedStayRules.Below(CoreSettings.Number(SeedStayRules.BelowKey));
        until = SeedStayRules.Until(CoreSettings.Number(SeedStayRules.UntilKey), below);
        lines = 0;
    }

    // Staying at a seed, walking there or standing in it: the formation leaves the member alone.
    internal static bool Staying(UnitEntity unit) => Current(unit) is { } stay && !stay.Ended;

    // Standing in the aura of the seed it stays at: held as a member of the formation on its place.
    internal static bool Holds(UnitEntity unit) => Current(unit) is { } stay && !stay.Ended && stay.InArea;

    // The refresh of a party member's portrait (SubUI_UnitShortCutParts.RefreshUnitStatus, WoundShortcutPatch), which the game runs
    // for every member of the group on a change of its status, stunned or not: a staying member healed up leaves at once, without a
    // decision of the fight (everyone stunned, healed to full in the aura).
    internal static bool AnyOpen => stays.Count != 0;

    internal static void OnHealth(UnitEntity unit)
    {
        if (stays.Count == 0 || unit == null) return;
        Current(unit);
    }

    // The member's stay, ended here once it is healed up to the exit share: the decisions that would end it stop when the fight
    // does (healed from 0.25 to 0.93 in the aura, no "leave").
    private static Stay Current(UnitEntity unit)
    {
        if (unit == null || stays.Count == 0) return null;
        var guid = unit.Guid.ToString();
        if (!stays.TryGetValue(guid, out var stay)) return null;
        var share = GroupAiRules.Share(CoreGame.Stat(unit, StatType.HealthHead), CoreGame.Stat(unit, StatType.HealthBody), CoreGame.Stat(unit, StatType.MaxHealthTotal));
        if (SeedStayRules.Stays(true, share, below, until)) return stay;
        stays.Remove(guid);
        if (!stay.Ended && stay.Step != "leave" && lines++ < SeedStayRules.LogCap)
        {
            var at = CoreGame.Position(unit);
            GroupAiPolicy.Log(stay.Healer
                ? SeedStayRules.HealSeekLine("party", guid, stay.Seed, "leave", share, at.x, at.y, Tick())
                : SeedStayRules.StayLine("party", guid, stay.Seed, "leave", share, at.x, at.y, Tick()));
        }
        return null;
    }

    // True when this decision sent the member's one order to the seed.
    internal static bool TryWalk(GroupAiSituation s)
    {
        if (s?.Owner == null || s.Self == null) return false;
        var staying = stays.TryGetValue(s.Guid, out var stay);
        // The seed line with look ahead and buffer (GroupAiLow); the exit uses the real share.
        var low = GroupAiLow.Share(s.Self.Unit, s.Self.Share, LowLine.SeedStay);
        if (staying && stay.Ended)
        {
            // Freed only above the entry share, look ahead and buffer included (SeedStayRules.Freed).
            if (SeedStayRules.Freed(low, below)) stays.Remove(s.Guid);
            return false;
        }
        var share = staying ? s.Self.Share : SeedStayRules.EntryShare(low, s.Self.Share, until);
        if (!SeedStayRules.Stays(staying, share, below, until))
        {
            if (staying)
            {
                stays.Remove(s.Guid);
                Note(s, stay, "leave", s.Self.Position);
            }
            return false;
        }
        (int Seeds, int Party, int Usable) count = default;
        IEntity seed = null;
        GroupAiSituation.Figure healer = null;
        if (staying && stay.Healer) healer = HealerToSeek(s, stay.Seed);
        else seed = staying ? SeedByGuid(s, stay.Seed) : NearestSeed(s, out count);
        if (!staying && seed == null && SeedStayRules.SeeksHealer(HasHealSkill(s), s.Role == GroupAiRole.Heal)) healer = HealerToSeek(s, null);
        if (seed == null && healer == null)
        {
            if (staying) End(s, stay, "gone");
            else NoteNoSeed(s, count);
            return false;
        }
        if (!staying)
        {
            stay = healer != null ? new Stay { Seed = healer.Guid, Healer = true } : new Stay { Seed = seed.Guid.ToString() };
            stays[s.Guid] = stay;
        }
        List<(int X, int Y)> area;
        if (stay.Healer) area = SeedStayRules.ReachCells(healer.Position.x, healer.Position.y, SeedStayRules.HealerReach);
        else
        {
            var at = s.PositionOf(seed);
            area = GroupAiHealerSeed.AuraCells(stay.Seed, at.x, at.y);
        }
        stay.InArea = SeedStayRules.InArea(area, s.Self.Position.x, s.Self.Position.y);
        var cell = stay.InArea || stay.Ordered ? null : FreeCell(s, area);
        var step = SeedStayRules.Step(stay.InArea, stay.Ordered, cell != null, false);
        switch (step)
        {
            case "walk":
                stay.Ordered = true;
                var tile = new Vector2Int(cell.Value.X, cell.Value.Y);
                if (!GroupAiZones.OrderMove(s.Owner, tile, "seed-stay"))
                {
                    End(s, stay, "order");
                    return false;
                }
                Note(s, stay, step, tile);
                return true;
            case "hold":
                Note(s, stay, step, s.Self.Position);
                return false;
            default:
                End(s, stay, "end");
                return false;
        }
    }

    private static void End(GroupAiSituation s, Stay stay, string step)
    {
        stay.Ended = true;
        stay.InArea = false;
        Note(s, stay, step, s.Self.Position);
    }

    // The nearest living seed of a party summoner with enough life left; count: the seeds on the map, of the party, usable.
    private static IEntity NearestSeed(GroupAiSituation s, out (int Seeds, int Party, int Usable) count)
    {
        count = (0, 0, 0);
        IEntity best = null;
        var bestDistance = int.MaxValue;
        var buildings = s.InMap?.GetCombatableBuildingsOnMap();
        for (var i = 0; buildings != null && i < buildings.Count; i++)
        {
            var seed = buildings[i];
            if (seed == null || seed.IsDead || seed.Key != GroupAiHealerSeedRules.SeedBuilding) continue;
            var entity = seed.Cast<IEntity>();
            count.Seeds++;
            if (CoreFaction.IsBandit(entity) || DelversSummons.OwnerUnitOf(entity)?.Faction != FactionType.Player) continue;
            count.Party++;
            var left = GroupAiHealerSeed.SecondsLeft(seed.Guid.ToString(), out var life, out _);
            if (!SeedStayRules.SeedUsable(left, life)) continue;
            count.Usable++;
            var distance = GroupAiSituation.Distance(s.Self.Position, s.PositionOf(entity));
            if (distance >= bestDistance) continue;
            best = entity;
            bestDistance = distance;
        }
        return best;
    }

    // The nearest party healer to seek (GroupAiRoles: a heal role), alive and standing; guid: only that one (a stay under way).
    private static GroupAiSituation.Figure HealerToSeek(GroupAiSituation s, string guid)
    {
        GroupAiSituation.Figure best = null;
        foreach (var ally in s.Allies)
        {
            if (ally.Unit == null || ally.Unit.IsDead || TacticalFormation.IsDown(ally.Unit)) continue;
            if (guid != null ? ally.Guid != guid : GroupAiRoles.Known(ally.Guid) != GroupAiRole.Heal) continue;
            if (best == null || ally.Distance < best.Distance) best = ally;
        }
        return best;
    }

    // A heal skill of its own on the bar (GroupAiRules.HealSkills): it heals itself and seeks no healer.
    private static bool HasHealSkill(GroupAiSituation s)
    {
        foreach (var slot in s.Slots)
            if (slot != null && GroupAiRules.HealSkills.Contains(slot.Key)) return true;
        return GroupAiRules.HealSkills.Contains(s.BasicAttack);
    }

    private static IEntity SeedByGuid(GroupAiSituation s, string guid)
    {
        var buildings = s.InMap?.GetCombatableBuildingsOnMap();
        for (var i = 0; buildings != null && i < buildings.Count; i++)
        {
            var seed = buildings[i];
            if (seed != null && !seed.IsDead && seed.Key == GroupAiHealerSeedRules.SeedBuilding && seed.Guid.ToString() == guid)
                return seed.Cast<IEntity>();
        }
        return null;
    }

    // The free cell of the area closest to the member: no other unit on it and the game's own nearest walkable cell.
    private static (int X, int Y)? FreeCell(GroupAiSituation s, List<(int X, int Y)> area)
    {
        var taken = new HashSet<(int X, int Y)>();
        foreach (var figure in s.Allies) taken.Add((figure.Position.x, figure.Position.y));
        foreach (var figure in s.Enemies) taken.Add((figure.Position.x, figure.Position.y));
        while (true)
        {
            var cell = SeedStayRules.PickCell(area, taken, s.Self.Position.x, s.Self.Position.y);
            if (cell == null) return null;
            var tile = new Vector2Int(cell.Value.X, cell.Value.Y);
            try
            {
                if (s.InMap.GetNearMovablePosition(tile) == tile) return cell;
            }
            catch (Exception)
            {
                return null;
            }
            taken.Add(cell.Value);
        }
    }

    private static void NoteNoSeed(GroupAiSituation s, (int Seeds, int Party, int Usable) count)
    {
        if (noSeedLogged.TryGetValue(s.Guid, out var last) && last == count) return;
        if (noSeedLogged.Count >= 64) noSeedLogged.Clear();
        noSeedLogged[s.Guid] = count;
        if (lines++ < SeedStayRules.LogCap)
            GroupAiPolicy.Log(SeedStayRules.NoSeedLine("party", s.Guid, count.Seeds, count.Party, count.Usable, s.Self.Share, Tick()));
    }

    private static void Note(GroupAiSituation s, Stay stay, string step, Vector2Int at)
    {
        if (stay.Step == step) return;
        stay.Step = step;
        if (lines++ < SeedStayRules.LogCap)
            GroupAiPolicy.Log(stay.Healer
                ? SeedStayRules.HealSeekLine("party", s.Guid, stay.Seed, step, s.Self.Share, at.x, at.y, Tick())
                : SeedStayRules.StayLine("party", s.Guid, stay.Seed, step, s.Self.Share, at.x, at.y, Tick()));
    }

    private static long Tick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
}
