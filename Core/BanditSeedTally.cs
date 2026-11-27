namespace DungeonSettlersDelvers.Core;

// The game side of BanditSeedTallyRules, a lasting balance log in the release, not a probe: it costs a
// look every LookSeconds only while a Core seed stands and needs no patch. A measurement only: GroupAiHealerSeed reports each Core seed's first step (Seen) and its
// heals (Step), BanditProtect.OnDamage the hits on Core bandits (NoteHit), and once per LookSeconds of game time, only while a
// Core seed is known, the tick looks which seeds still stand (CORE_SEED_TALLY_END for the others) and which noted bandits were
// low and outside every aura (CORE_SEED_TALLY_HIT). No new patch: the heal step, the damage hook and the bandits' tick are Core's.
internal static class BanditSeedTally
{
    private const int SeedCap = 64;
    private const int HitCap = 64;
    private static readonly string HealerKey = CoreUnitRules.Prefix + "ForestBanditHealer";

    private sealed class Entry
    {
        internal BanditSeedTallyRules.Seed Tally;
        internal MapType Map;
        internal int Outside;
    }

    private static readonly Dictionary<string, Entry> seeds = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, UnitEntity> hits = new(StringComparer.Ordinal);
    private static float nextLook;
    private static int lines, hitLines;

    internal static void Reset()
    {
        seeds.Clear();
        hits.Clear();
        nextLook = 0f;
        lines = hitLines = 0;
    }

    internal static void Seen(string seed, UnitEntity summoner, MapType map, int x, int y)
    {
        if (seed == null || seeds.ContainsKey(seed) || seeds.Count >= SeedCap) return;
        var who = summoner?.Guid.ToString();
        seeds[seed] = new Entry { Tally = new BanditSeedTallyRules.Seed(who, GameClock.Now), Map = map };
        Log(BanditSeedTallyRules.BeginLine(seed, who, GroupAiHealerSeed.AuraCells(seed, x, y), Tick()));
    }

    // The herbalist chose to grow the seed (BanditHealerSeed): its end is an upgrade.
    internal static void Upgrading(string seed)
    {
        if (seed != null && seeds.TryGetValue(seed, out var entry)) entry.Tally.Upgraded = true;
    }

    internal static void Step(string seed, List<string> units, float amount)
    {
        if (seed != null && seeds.TryGetValue(seed, out var entry)) entry.Tally.Step(units, amount);
    }

    // A landed hit on a unit, from the damage hook; only noted, looked at in the tick.
    internal static void NoteHit(UnitEntity target)
    {
        if (seeds.Count == 0 || target == null || hits.Count >= HitCap || target.Key == HealerKey || !CoreUnitRules.IsCoreUnit(target.Key)) return;
        hits[target.Guid.ToString()] = target;
    }

    internal static void OnGameTick()
    {
        if (seeds.Count == 0) return;
        var now = GameClock.Now;
        if (now < nextLook) return;
        nextLook = now + BanditSeedTallyRules.LookSeconds;
        var standing = Standing();
        LookAtHits(standing);
        var ended = new List<string>();
        foreach (var pair in seeds)
            if (!standing.ContainsKey(pair.Key)) ended.Add(pair.Key);
        foreach (var seed in ended)
        {
            var entry = seeds[seed];
            seeds.Remove(seed);
            Log(BanditSeedTallyRules.EndLine(seed, entry.Tally, now, entry.Outside, Tick()));
        }
    }

    // The known seeds that still stand, with their map and cell; a seed whose map is no longer loaded has ended.
    private static Dictionary<string, (MapType Map, int X, int Y)> Standing()
    {
        var result = new Dictionary<string, (MapType, int, int)>(StringComparer.Ordinal);
        var maps = CoreGame.Flow?._inMapDatas;
        var looked = new HashSet<MapType>();
        foreach (var entry in seeds.Values)
        {
            if (!looked.Add(entry.Map) || maps == null || !maps.ContainsKey(entry.Map)) continue;
            var inMap = maps[entry.Map];
            var buildings = inMap?.GetCombatableBuildingsOnMap();
            for (var i = 0; buildings != null && i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null || building.IsDead || building.Key != GroupAiHealerSeedRules.SeedBuilding) continue;
                var key = building.Guid.ToString();
                if (!seeds.ContainsKey(key)) continue;
                var at = inMap.GetLogicalPosition(building.Guid);
                result[key] = (entry.Map, at.x, at.y);
            }
        }
        return result;
    }

    // Case 1 of the annoyed herbalist: a noted bandit low and outside every standing seed's aura on its map.
    private static void LookAtHits(Dictionary<string, (MapType Map, int X, int Y)> standing)
    {
        foreach (var pair in hits)
        {
            var unit = pair.Value;
            if (unit == null || unit.IsDead || !CoreGame.TryMapOfUnit(unit, out var map)) continue;
            var maps = CoreGame.Flow?._inMapDatas;
            var inMap = maps != null && maps.ContainsKey(map) ? maps[map] : null;
            if (inMap == null) continue;
            var at = inMap.GetLogicalPosition(unit.Guid);
            int count = 0;
            var inside = false;
            foreach (var seed in standing)
            {
                if (seed.Value.Map != map) continue;
                count++;
                inside |= SeedStayRules.InArea(GroupAiHealerSeed.AuraCells(seed.Key, seed.Value.X, seed.Value.Y), at.x, at.y);
            }
            var share = GroupAiRules.Share(CoreGame.Stat(unit, StatType.HealthHead), CoreGame.Stat(unit, StatType.HealthBody),
                CoreGame.Stat(unit, StatType.MaxHealthTotal));
            if (!BanditSeedTallyRules.Annoys(share, count, inside)) continue;
            foreach (var seed in standing)
                if (seed.Value.Map == map) seeds[seed.Key].Outside++;
            if (hitLines++ < BanditSeedTallyRules.LineCap) DelversHost.Info(BanditSeedTallyRules.HitLine(pair.Key, share, count, Tick()));
        }
        hits.Clear();
    }

    private static void Log(string line)
    {
        if (lines++ < BanditSeedTallyRules.LineCap) DelversHost.Info(line);
    }

    private static long Tick() => CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0;
}
