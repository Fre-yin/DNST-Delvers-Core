using UnityEngine;
#if BEPINEX
using AffectTarget = global::Refactor.Combat.AffectTarget;
using TargetType = global::Refactor.Combat.TargetType;
#else
using AffectTarget = Il2CppRefactor.Combat.AffectTarget;
using TargetType = Il2CppRefactor.Combat.TargetType;
#endif

namespace DungeonSettlersDelvers.Core;

// GroupAiBandits, "Im Samen bleiben" (SeedStayRules): a bandit at or below seeds.stay.below walks once into the aura of the
// nearest bandit seed and stays there until seeds.stay.until. Inside it attacks a foe in reach of a free skill or its basic
// attack and otherwise holds; a dodge still comes first (Decide). The formation is not asked while it stays. When its one
// order ends outside the aura or the seed is gone, the stay ends and the kit decides again until the bandit has risen above
// the entry share. CORE_AI_SEED_STAY per step change.
internal static partial class GroupAiBandits
{
    private sealed class Stay
    {
        internal string Seed;
        internal bool Ordered;
        internal bool Ended;
        internal string Step;
    }

    private static readonly Dictionary<string, Stay> stays = new(StringComparer.Ordinal);
    private static double stayBelow = SeedStayRules.BelowDefault, stayUntil = SeedStayRules.UntilDefault;
    private static int stayLines;

    private static void ResetSeedStay()
    {
        stays.Clear();
        noSeedLogged.Clear();
        stayBelow = SeedStayRules.Below(CoreSettings.Number(SeedStayRules.BelowKey));
        stayUntil = SeedStayRules.Until(CoreSettings.Number(SeedStayRules.UntilKey), stayBelow);
        stayLines = 0;
    }

    private static Choice SeedStay(Situation s)
    {
        var staying = stays.TryGetValue(s.Guid, out var stay);
        if (staying && stay.Ended)
        {
            // Over until the bandit rose above the entry share; a new drop starts a new stay.
            if (s.Self.Share > stayBelow) stays.Remove(s.Guid);
            return null;
        }
        if (!SeedStayRules.Stays(staying, s.Self.Share, stayBelow, stayUntil))
        {
            if (staying)
            {
                stays.Remove(s.Guid);
                NoteStay(s, stay, "leave", s.Self.Position);
            }
            return null;
        }
        (int Seeds, int Bandit, int Usable) count = default;
        var seed = staying ? SeedByGuid(s, stay.Seed) : NearestSeed(s, out count);
        if (seed == null)
        {
            if (staying) End(s, stay, "gone");
            else NoteNoSeed(s, count);
            return null;
        }
        if (!staying)
        {
            stay = new Stay { Seed = seed.Guid.ToString() };
            stays[s.Guid] = stay;
        }
        var at = s.PositionOf(seed);
        var area = GroupAiHealerSeed.AuraCells(stay.Seed, at.x, at.y);
        var inArea = SeedStayRules.InArea(area, s.Self.Position.x, s.Self.Position.y);
        var cell = inArea || stay.Ordered ? null : FreeCell(s, area);
        var attack = inArea ? StayAttack(s) : null;
        var step = SeedStayRules.Step(inArea, stay.Ordered, cell != null, attack != null);
        switch (step)
        {
            case "walk":
                stay.Ordered = true;
                var tile = new Vector2Int(cell.Value.X, cell.Value.Y);
                if (!GroupAiBanditMoves.TryOrder(s.Unit, tile, "seed-stay"))
                {
                    End(s, stay, "order");
                    return null;
                }
                NoteStay(s, stay, step, tile);
                return NewChoice("", null, "seed-walk");
            case "attack":
                NoteStay(s, stay, step, s.Self.Position);
                return attack;
            case "hold":
                NoteStay(s, stay, step, s.Self.Position);
                return NewChoice("", null, SeedStayRules.HoldRule);
            default:
                End(s, stay, "end");
                return null;
        }
    }

    private static void End(Situation s, Stay stay, string step)
    {
        stay.Ended = true;
        NoteStay(s, stay, step, s.Self.Position);
    }

    // The nearest living seed of a bandit with enough life left (SeedStayRules.SeedUsable); count: the seeds on the map, of a
    // bandit, and of those usable.
    private static IEntity NearestSeed(Situation s, out (int Seeds, int Bandit, int Usable) count)
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
            if (!CoreFaction.IsBandit(entity)) continue;
            count.Bandit++;
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

    private static IEntity SeedByGuid(Situation s, string guid)
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

    // The free cell of the area closest to the bandit: no other unit on it and the game's own nearest walkable cell.
    private static (int X, int Y)? FreeCell(Situation s, List<(int X, int Y)> area)
    {
        var taken = new HashSet<(int X, int Y)>();
        foreach (var figure in s.Allies) taken.Add((figure.Position.x, figure.Position.y));
        foreach (var figure in s.Hostiles) taken.Add((figure.Position.x, figure.Position.y));
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

    // A foe in reach without a step: the closest one a free skill of the bar reaches (no ally skill, no self skill, no area
    // that may hit comrades), else the basic attack's reach; null when nothing is in reach. The herbalist first grows her seed
    // (HealerSeedUpgrade, the Life Burst included) and gives Lifebloom to the lowest below the emergency share in exact range.
    private static Choice StayAttack(Situation s)
    {
        if (s.Kit == Kit.Healer)
        {
            var lowest = s.LowestAlly;
            if (lowest == null || s.Self.Share < lowest.Share) lowest = s.Self;
            var own = HealerSeedUpgrade(s)
                ?? (lowest.Share < EmergencyShare ? SingleAlly(s, LifebloomSkill, lowest, "seed-lifebloom", withRange: true, exact: true) : null);
            if (own != null) return own;
        }
        var foes = new List<Figure>(s.Hostiles);
        foes.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        var basic = GroupAiRoles.Sheet(s.BasicAttack);
        foreach (var foe in foes)
        {
            if (s.Downed != null && s.FinishReason == null && s.Downed.Exists(d => d.Guid == foe.Guid)) continue;
            foreach (var slot in s.Slots)
            {
                if (!slot.Free || slot.Data == null || slot.Data.TargetType == TargetType.SelfTarget) continue;
                if (slot.Data.AffectTarget is AffectTarget.SingleAlly or AffectTarget.MultipleAlly) continue;
                if (GroupAiRules.CanHitAllies((int)slot.Data.AffectTarget)) continue;
                if (GroupAiRules.InRangeExact(foe.Distance, slot.Data.Range)) return NewChoice(slot.Key, foe.Unit.Cast<IEntity>(), "seed-attack");
            }
            if (basic != null && GroupAiRules.InRangeExact(foe.Distance, basic.Range)) return NewChoice("", foe.Unit.Cast<IEntity>(), "seed-attack-basic");
        }
        return null;
    }

    // A bandit low enough to stay finds no seed: why, once per bandit and count (CORE_AI_SEED_STAY step=noseed).
    private static readonly Dictionary<string, (int, int, int)> noSeedLogged = new(StringComparer.Ordinal);

    private static void NoteNoSeed(Situation s, (int Seeds, int Bandit, int Usable) count)
    {
        if (noSeedLogged.TryGetValue(s.Guid, out var last) && last == count) return;
        if (noSeedLogged.Count >= 64) noSeedLogged.Clear();
        noSeedLogged[s.Guid] = count;
        if (stayLines++ < SeedStayRules.LogCap)
            Log(SeedStayRules.NoSeedLine("bandit", s.Guid, count.Seeds, count.Bandit, count.Usable, s.Self.Share, Tick()));
    }

    private static void NoteStay(Situation s, Stay stay, string step, Vector2Int at)
    {
        if (stay.Step == step) return;
        stay.Step = step;
        if (stayLines++ < SeedStayRules.LogCap)
            Log(SeedStayRules.StayLine("bandit", s.Guid, stay.Seed, step, s.Self.Share, at.x, at.y, Tick()));
    }
}
