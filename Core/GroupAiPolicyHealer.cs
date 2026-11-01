namespace DungeonSettlersDelvers.Core;

// GroupAiPolicy, the healer: her rule order, the seed burst with its diagnosis line, the emergency target, Lifebloom on an
// ally and the refresh candidate, the off-GCD Lifebloom decision, and the seed and single ally casts. The fields, the
// shared helpers (InReach, Log, Tick) and the aliases of the game's types stay in GroupAiPolicy.cs.
internal static partial class GroupAiPolicy
{
    private static Choice Heal(GroupAiSituation s)
    {
        var burst = SeedBurst(s);
        if (burst != null) return burst;
        var lowest = s.LowestAlly;
        var urgent = EmergencyTarget(s);
        if (urgent != null)
        {
            var emergency = Bloom(s, urgent, "heal-emergency-lifebloom", emergency: true)
                ?? SingleAlly(s, "SKILL_HealingTouchAttack", urgent, "heal-emergency-hand")
                ?? Seed(s, EnemyNextTo(s, urgent), "heal-emergency-seed");
            if (emergency != null) return emergency;
        }
        if (lowest != null && lowest.Share < GroupAiRules.HealShare)
        {
            var bloom = Bloom(s, lowest, "heal-lifebloom", emergency: false);
            if (bloom != null) return bloom;
        }
        var refresh = Bloom(s, RefreshCandidate(s), "heal-lifebloom-refresh", emergency: false);
        if (refresh != null) return refresh;
        var anchor = SeedAnchor(s, GroupAiRules.SeedNearShare);
        if (anchor != null || s.AlliesBelow(GroupAiRules.SeedShare) >= 2)
        {
            var seed = Seed(s, anchor ?? SeedAnchor(s, 2f), "heal-seed");
            if (seed != null) return seed;
        }
        // A share of 2 counts every unit: keep a seed on the enemy with the most party members around.
        var upkeep = Seed(s, SeedAnchor(s, 2f), "heal-seed-upkeep");
        if (upkeep != null) return upkeep;
        if (GroupAiRules.HealWaits(s.AnyAllyBelow(GroupAiRules.HealShare), HealSecondsUntilFree(s)))
        {
            return NewChoice("", null, "heal-wait");
        }
        return Damage(s, "heal-damage", GroupAiRules.DamageReserve);
    }

    // What the last seed burst check found, for its diagnosis line (GroupAiRules.SeedCheckLine).
    private sealed class SeedCheck
    {
        internal int Stage;
        internal string Upgrade;
        internal bool Free;
        internal int Seeds;
        internal int Own;
        internal string Summoner;
        internal int? Distance;
        internal int? Range;
        internal float? Lowest;
        internal bool BloomFree;

        internal void Reach(int stage) => Stage = Math.Max(Stage, stage);
    }

    private static Choice SeedBurst(GroupAiSituation s)
    {
        var check = new SeedCheck();
        GroupAiSituation.Slot upgrade = null;
        foreach (var key in SeedUpgrades)
        {
            var slot = s.Find(key);
            if (slot == null) continue;
            check.Upgrade ??= slot.Key;
            if (slot.Free && slot.Data != null) { upgrade = slot; break; }
        }
        if (upgrade == null) return SeedChecked(s, check, null);
        check.Upgrade = upgrade.Key;
        check.Free = true;
        check.Reach(1);
        var buildings = s.InMap?.GetCombatableBuildingsOnMap();
        var owner = CoreGame.GuidOf(s.Owner);
        var bloom = s.Find(LifebloomRules.SkillKey);
        check.BloomFree = bloom != null && bloom.Free && Lifebloom.ChargesOf(owner) > 0;
        check.Range = upgrade.Data.Range;
        for (var i = 0; buildings != null && i < buildings.Count; i++)
        {
            var seed = buildings[i];
            if (seed == null || seed.IsDead || seed.Key != SeedBuilding) continue;
            check.Seeds++;
            // The summoner from the backing field; seed.Summoner through the wrapper never matched.
            var summoner = CoreGame.SummonerGuid(seed.Cast<IEntity>());
            if (check.Summoner == null || summoner == s.Guid) check.Summoner = summoner;
            if (summoner != s.Guid) continue;
            check.Own++;
            check.Reach(2);
            var at = s.PositionOf(seed.Cast<IEntity>());
            var distance = GroupAiSituation.Distance(s.Self.Position, at);
            if (check.Distance == null || distance < check.Distance) check.Distance = distance;
            if (!InReach(s, distance, upgrade.Data.Range)) continue;
            check.Reach(3);
            float? lowest = null;
            if (GroupAiRules.NextToSeed(at.x, at.y, s.Self.Position.x, s.Self.Position.y)) lowest = s.Self.Share;
            foreach (var ally in s.Allies)
                if (GroupAiRules.NextToSeed(at.x, at.y, ally.Position.x, ally.Position.y) && (lowest == null || ally.Share < lowest)) lowest = ally.Share;
            if (lowest == null) continue;
            check.Reach(4);
            if (check.Lowest == null || lowest < check.Lowest) check.Lowest = lowest;
            if (!GroupAiRules.SeedBurstNow(lowest, check.BloomFree)) continue;
            check.Reach(5);
            check.Lowest = lowest;
            Log(GroupAiRules.SeedBurstLine(s.Guid, seed.Guid.ToString(), upgrade.Key, lowest, Tick()));
            return SeedChecked(s, check, NewChoice(upgrade.Key, seed.Cast<IEntity>(), "heal-seed-burst"));
        }
        return SeedChecked(s, check, null);
    }

    // The diagnosis line, when the stage changed for this caster and at most once per 2 game seconds. Written past
    // the rule line cap, which the decision lines fill early in a long block.
    private static Choice SeedChecked(GroupAiSituation s, SeedCheck check, Choice choice)
    {
        var stage = GroupAiRules.SeedCheckStages[check.Stage];
        var now = GameClock.Now;
        var last = seedChecks.TryGetValue(s.Guid, out var seen) ? seen : (Stage: (string)null, At: 0f);
        if (!GroupAiRules.SeedCheckDue(last.Stage, last.At, stage, now)) return choice;
        seedChecks[s.Guid] = (stage, now);
        DelversHost.Info(GroupAiRules.SeedCheckLine(s.Guid, stage, check.Upgrade, check.Free, check.Seeds, check.Own, check.Summoner,
            s.Guid, check.Distance, check.Range, check.Lowest, check.BloomFree, Tick()));
        return choice;
    }

    // The lowest party member below EmergencyShare, the healer herself included (GroupAiRules.EmergencyPick).
    private static GroupAiSituation.Figure EmergencyTarget(GroupAiSituation s)
        => GroupAiRules.EmergencyPick(s.LowestAlly?.Share, s.Self.Share) switch
        {
            "self" => s.Self,
            "ally" => s.LowestAlly,
            _ => null
        };

    // When each heal comes free: the game's cooldown of the slot, for Lifebloom also its two charges.
    private static List<double> HealSecondsUntilFree(GroupAiSituation s)
    {
        var seconds = new List<double>();
        foreach (var key in new[] { LifebloomRules.SkillKey, "SKILL_HealingTouchAttack", SeedSkill })
        {
            var slot = s.Find(key);
            if (slot == null) continue;
            if (slot.State == "cd") seconds.Add(slot.CooldownLeft);
            else if (key == LifebloomRules.SkillKey && slot.Free) seconds.Add(Lifebloom.SecondsUntilCharges(CoreGame.GuidOf(s.Owner), 2));
        }
        return seconds;
    }

    // Lifebloom on an ally in range: an emergency spends the last charge, otherwise one charge is
    // kept. Not while the ally already has full stacks with time left.
    private static Choice Bloom(GroupAiSituation s, GroupAiSituation.Figure ally, string rule, bool emergency)
    {
        var slot = s.Find(LifebloomRules.SkillKey);
        if (slot == null || slot.Data == null || !slot.Free || ally == null) return null;
        if (!InReach(s, ally.Distance, slot.Data.Range) || !GroupAiRules.BloomTargetUseful(ally.Share)) return null;
        var charges = Lifebloom.ChargesOf(CoreGame.GuidOf(s.Owner));
        if (charges <= 0 || (!emergency && charges < 2)) return null;
        if (Lifebloom.BuffOn(CoreGame.GuidOf(ally.Unit), out var stacks, out var secondsLeft)
            && stacks >= LifebloomRules.MaxStacks && secondsLeft > LifebloomRules.RefreshBurstBelow)
            return null;
        lifeblooms++;
        Log(GroupAiRules.LifebloomLine(s.Guid, ally.Guid, stacks, secondsLeft, charges, rule));
        return NewChoice(slot.Key, ally.Unit.Cast<IEntity>(), rule);
    }

    // A hurt ally whose Lifebloom is about to run out.
    private static GroupAiSituation.Figure RefreshCandidate(GroupAiSituation s)
    {
        foreach (var ally in s.Allies)
        {
            if (ally.Share < GroupAiRules.SeedNearShare && Lifebloom.BuffOn(CoreGame.GuidOf(ally.Unit), out var stacks, out var secondsLeft)
                && stacks > 0 && secondsLeft <= LifebloomRules.RefreshBurstBelow)
                return ally;
        }
        return null;
    }

    internal static Choice DecideOffGcd(GroupAiSituation s) => DecideOffGcd(s, anyRole: false, source: "offgcd-ai");

    private static Choice DecideOffGcd(GroupAiSituation s, bool anyRole, string source)
    {
        if (!anyRole && s.Role != GroupAiRole.Heal) return null;
        var lowest = s.LowestAlly;
        Choice choice = null;
        var urgent = EmergencyTarget(s);
        if (urgent != null) choice = Bloom(s, urgent, "heal-emergency-lifebloom", emergency: true);
        if (choice == null && lowest != null && lowest.Share < GroupAiRules.HealShare) choice = Bloom(s, lowest, "heal-lifebloom", emergency: false);
        if (choice == null) choice = Bloom(s, RefreshCandidate(s), "heal-lifebloom-refresh", emergency: false);
        if (choice == null) return null;
        decisions++;
        ruleCounts[choice.Rule] = ruleCounts.TryGetValue(choice.Rule, out var count) ? count + 1 : 1;
        pending[s.Guid] = choice;
        Log(GroupAiRules.OffGcdRuleLine(s.Guid, choice.Rule, choice.Skill, choice.Target.Guid.ToString(), lowest?.Guid ?? "-"));
        DelversHost.Info(LifebloomRules.OgcdLine(s.Guid, Lifebloom.ChargesOf(CoreGame.GuidOf(s.Owner)), source));
        return choice;
    }

    private static Choice SingleAlly(GroupAiSituation s, string key, GroupAiSituation.Figure ally, string rule)
    {
        var slot = s.Find(key);
        if (slot == null || slot.Data == null || !slot.Free || ally == null) return null;
        if (!InReach(s, ally.Distance, slot.Data.Range)) return null;
        return NewChoice(key, ally.Unit.Cast<IEntity>(), rule);
    }

    private static Choice Seed(GroupAiSituation s, IEntity anchor, string rule)
    {
        var slot = s.Find(SeedSkill);
        if (slot == null || slot.Data == null || !slot.Free || anchor == null) return null;
        if (!InReach(s, GroupAiSituation.Distance(s.Self.Position, s.PositionOf(anchor)), slot.Data.Range)) return null;
        return NewChoice(slot.Key, anchor, rule);
    }

    // The enemy with the most party members below the share right next to it; the nearer one on a tie.
    private static IEntity SeedAnchor(GroupAiSituation s, float share)
    {
        GroupAiSituation.Figure best = null;
        var bestCount = 0;
        foreach (var enemy in s.Enemies)
        {
            var count = s.Self.Share < share && GroupAiSituation.Distance(enemy.Position, s.Self.Position) <= 1 ? 1 : 0;
            foreach (var ally in s.Allies)
                if (ally.Share < share && GroupAiSituation.Distance(enemy.Position, ally.Position) <= 1) count++;
            if (count != 0 && (count > bestCount || (count == bestCount && enemy.Distance < best.Distance)))
            {
                best = enemy;
                bestCount = count;
            }
        }
        return best?.Unit.Cast<IEntity>();
    }

    private static IEntity EnemyNextTo(GroupAiSituation s, GroupAiSituation.Figure figure)
    {
        if (figure == null) return null;
        foreach (var enemy in s.Enemies)
            if (GroupAiSituation.Distance(enemy.Position, figure.Position) <= 1) return enemy.Unit.Cast<IEntity>();
        return null;
    }
}
