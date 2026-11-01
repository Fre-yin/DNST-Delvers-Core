using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// The herbalist's Strange Seed (GroupAiHealerSeedRules). Thrown at the foe with the most
// comrades below SeedShare right next to it, in her exact range (she never walks up); her own seed grown into the
// mucus sprayer once it has UpgradeSeconds left, from its aura passive (GroupAiHealerSeed.SecondsLeft). The Life Burst
// is the seed's passive: it heals when her seed dies (GroupAiHealerSeed sets its heal).
internal static partial class GroupAiBandits
{
    private static Choice HealerSeed(Situation s)
    {
        if (CoreGame.TryMapOfUnit(s.Unit, out var map)) GroupAiHealerSeed.CheckLifeBurst(s.Unit, map);
        var slot = s.Find(GroupAiHealerSeedRules.SeedSkill);
        if (slot == null || slot.Data == null || !slot.Free || s.Hostiles.Count == 0) return null;
        var foes = new List<(int Index, int Distance, int Below)>();
        for (var i = 0; i < s.Hostiles.Count; i++)
        {
            var foe = s.Hostiles[i];
            if (foe.Distance > slot.Data.Range) continue;
            var below = GroupAiHealerSeedRules.Below(s.Self.Share) && GroupAiSituation.Distance(foe.Position, s.Self.Position) <= 1 ? 1 : 0;
            foreach (var ally in s.Allies)
                if (GroupAiHealerSeedRules.Below(ally.Share) && GroupAiSituation.Distance(foe.Position, ally.Position) <= 1) below++;
            foes.Add((i, foe.Distance, below));
        }
        var pick = GroupAiHealerSeedRules.PickAnchor(foes);
        if (pick < 0) return null;
        var target = s.Hostiles[pick];
        var count = 0;
        foreach (var foe in foes) if (foe.Index == pick) count = foe.Below;
        Log(GroupAiHealerSeedRules.SeedLine(s.Guid, target.Guid, count, "healer-seed", Tick()));
        return NewChoice(slot.Key, target.Unit.Cast<IEntity>(), "healer-seed");
    }

    // burstOnly: the Life Burst alone (SeedBurst), before her heals; the upgrade shortly before the seed ends comes later.
    private static Choice HealerSeedUpgrade(Situation s, bool burstOnly = false)
    {
        var slot = s.Find(GroupAiHealerSeedRules.SprayerSkill);
        var sprayerFree = slot != null && slot.Data != null && slot.Free;
        var bloom = s.Find(SeedStayRules.BloomSkill);
        var bloomFree = bloom != null && bloom.Data != null && bloom.Free;
        if (!sprayerFree && !bloomFree) return null;
        var buildings = s.InMap?.GetCombatableBuildingsOnMap();
        for (var i = 0; buildings != null && i < buildings.Count; i++)
        {
            var seed = buildings[i];
            if (seed == null || seed.IsDead || seed.Key != GroupAiHealerSeedRules.SeedBuilding) continue;
            var entity = seed.Cast<IEntity>();
            if (CoreGame.SummonerGuid(entity) != s.Guid) continue;
            var at = s.PositionOf(entity);
            var distance = GroupAiSituation.Distance(s.Self.Position, at);
            var burst = SeedBurst(s, entity, at, distance, sprayerFree, bloomFree);
            if (burst != null) return burst;
            if (burstOnly || !sprayerFree) continue;
            var left = GroupAiHealerSeed.SecondsLeft(seed.Guid.ToString(), out var life, out var counter);
            var inRange = distance <= slot.Data.Range;
            var due = inRange && GroupAiHealerSeedRules.UpgradeDue(left);
            NoteSeedUpgrade(s.Guid, seed.Guid.ToString(), left, life, counter, GroupAiHealerSeedRules.UpgradeResult(inRange, life >= 0f, due));
            if (!inRange) continue;
            if (due) return NewChoice(slot.Key, entity, "healer-seed-upgrade");
        }
        return null;
    }

    // The bandits' Life Burst (SeedStayRules.BurstNow): a comrade on the seed's cell or next to it (the cells the burst heals)
    // at or below seeds.stay.below, herself included; the seed grows at once with the skill SeedStayRules.BurstSkill names, in
    // exact range. CORE_AI_BANDIT_SEED_BURST.
    private static Choice SeedBurst(Situation s, IEntity seed, Vector2Int at, int distance, bool sprayerFree, bool bloomFree)
    {
        float? lowest = null;
        string comrade = null;
        var figures = new List<Figure>(s.Allies) { s.Self };
        foreach (var figure in figures)
        {
            if (GroupAiRules.Distance(at.x, at.y, figure.Position.x, figure.Position.y) > 1) continue;
            if (lowest != null && figure.Share >= lowest) continue;
            lowest = figure.Share;
            comrade = figure.Guid == s.Guid ? "self" : figure.Guid;
        }
        if (!SeedStayRules.BurstNow(lowest, stayBelow)) return null;
        var sprayerInRange = sprayerFree && distance <= s.Find(GroupAiHealerSeedRules.SprayerSkill).Data.Range;
        var bloomInRange = bloomFree && distance <= s.Find(SeedStayRules.BloomSkill).Data.Range;
        var skill = SeedStayRules.BurstSkill(sprayerInRange, bloomInRange);
        if (skill == null) return null;
        Log(SeedStayRules.BanditBurstLine(s.Guid, seed.Guid.ToString(), skill, lowest.Value, comrade, Tick()));
        return NewChoice(skill, seed, "healer-seed-burst");
    }

    // Test tool (GroupAiHealerSeedRules.TestSeedLine): requests per herbalist, taken at her next decision.
    private static readonly Dictionary<string, (string Target, bool Upgrade)> testSeeds = new(StringComparer.Ordinal);

    internal static void RequestTestSeed(string healer, string target, bool upgrade) => testSeeds[healer] = (target, upgrade);

    private static Choice TestSeed(Situation s)
    {
        if (testSeeds.Count == 0 || !testSeeds.TryGetValue(s.Guid, out var request)) return null;
        testSeeds.Remove(s.Guid);
        if (request.Upgrade)
        {
            var sprayer = s.Find(GroupAiHealerSeedRules.SprayerSkill);
            IEntity own = null;
            var buildings = s.InMap?.GetCombatableBuildingsOnMap();
            for (var i = 0; buildings != null && i < buildings.Count && own == null; i++)
            {
                var seed = buildings[i];
                if (seed == null || seed.IsDead || seed.Key != GroupAiHealerSeedRules.SeedBuilding) continue;
                var entity = seed.Cast<IEntity>();
                if (CoreGame.SummonerGuid(entity) == s.Guid) own = entity;
            }
            var upgradeResult = own == null ? "noseed" : GroupAiHealerSeedRules.TestSeedResult(sprayer?.Data != null, sprayer?.Free == true, true,
                sprayer?.Data != null && GroupAiSituation.Distance(s.Self.Position, s.PositionOf(own)) <= sprayer.Data.Range);
            Log(GroupAiHealerSeedRules.TestSeedLine(s.Guid, own?.Guid.ToString(), true, upgradeResult));
            return upgradeResult == "cast" ? NewChoice(sprayer.Key, own, "test-seed-upgrade") : null;
        }
        var slot = s.Find(GroupAiHealerSeedRules.SeedSkill);
        Figure target = null;
        foreach (var hostile in s.Hostiles)
            if (request.Target != null ? hostile.Guid == request.Target : target == null || hostile.Distance < target.Distance) target = hostile;
        var result = GroupAiHealerSeedRules.TestSeedResult(slot?.Data != null, slot?.Free == true, target != null,
            target != null && slot?.Data != null && target.Distance <= slot.Data.Range);
        Log(GroupAiHealerSeedRules.TestSeedLine(s.Guid, target?.Guid ?? request.Target, false, result));
        return result == "cast" ? NewChoice(slot.Key, target.Unit.Cast<IEntity>(), "test-seed") : null;
    }

    // One line per seed and result, so a waiting seed does not log every decision.
    private static readonly Dictionary<string, string> seedUpgradeNotes = new(StringComparer.Ordinal);

    private static void NoteSeedUpgrade(string unit, string seed, float left, float life, float counter, string result)
    {
        if (seedUpgradeNotes.TryGetValue(seed, out var last) && last == result) return;
        if (seedUpgradeNotes.Count >= 64) seedUpgradeNotes.Clear();
        seedUpgradeNotes[seed] = result;
        Log(GroupAiHealerSeedRules.UpgradeLine(unit, seed, left, life, counter, result, Tick()));
    }
}
