using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// GroupAiBandits, the weak bandits' new classes (WeakBanditRules), built on the bricks the boss party's rules
// share (Protect, Formation, Damage, Basic):
// - the spear: like the blade, its stance (Twin Thrust) first, then the opening strike and its spear's thrust;
// - the swordsman: like the blade, the charge to a foe two to five cells away (the gapcloser), the counter stance when a melee
//   hit landed on it within CounterSeconds, then the sweep;
// - the water mage: behind the front like the archer; the embracing wave on the most hurt bandit of its group below WaveShare,
//   else, from level 3, the toad (it eats a bubbling), else one bubbling held beside it, else the tidal mark on the foes;
// - the fire mage: behind the front like the archer; its staff's missiles as a stance, then the echo and the incineration.
internal static partial class GroupAiBandits
{
    internal const float WaveShare = 0.6f;
    internal const float CounterSeconds = 2f;

    private static readonly Dictionary<string, float> meleeHitAt = new(StringComparer.Ordinal);

    private static Choice Spear(Situation s)
        => Protect(s, "spear") ?? Damage(s, s.Protecting ? "spear-protect" : "spear",
            WeakBanditKitKeys.TwinThrust, WeakBanditKitKeys.OpeningStrike, WeakBanditKitKeys.PiercingThrust);

    private static Choice Sword(Situation s)
    {
        var protect = Protect(s, "sword");
        if (protect != null) return protect;
        var charge = s.Find(WeakBanditKitKeys.ChargeAttack);
        var intended = s.Intended;
        if (charge != null && charge.Free && charge.Data != null && intended != null)
        {
            var distance = GroupAiSituation.Distance(s.Self.Position, s.PositionOf(intended));
            if (distance >= MeleeReach && GroupAiRules.InRange(distance, charge.Data.Range)) return NewChoice(charge.Key, intended, "sword-charge");
        }
        var counter = s.Find(WeakBanditKitKeys.SwordCounter);
        if (counter != null && counter.Free && !BuffRunning(s, counter) && meleeHitAt.TryGetValue(s.Guid, out var hit)
            && !GameClock.Elapsed(hit, CounterSeconds))
            return NewChoice(counter.Key, s.Self.Unit.Cast<IEntity>(), "sword-counter");
        return Damage(s, s.Protecting ? "sword-protect" : "sword", WeakBanditKitKeys.SweepingCleave);
    }

    private static Choice WaterMage(Situation s)
    {
        var formation = Formation(s, ArcherBehind);
        if (formation != null) return formation;
        var (bubblings, toads) = OwnSummons(s);
        // Level 3: the toad eats a bubbling and goes before the wave unless a bandit is critical (WeakBanditMageRules.ToadFirst);
        // the next decision replaces the bubbling, and the wave may spend that one.
        var lowest = s.Self.Share;
        foreach (var ally in s.Allies) lowest = Math.Min(lowest, ally.Share);
        var toadFirst = s.Hostiles.Count > 0 && WeakBanditMageRules.ToadFirst(WeakBanditMageRules.WantsToad(toads, bubblings), lowest);
        var toad = toadFirst ? WaterMageSummon(s, WeakBanditKitKeys.SummonBubbleToad, "watermage-toad") : null;
        if (toad != null) return toad;
        // The wave spends one of the mage's bubblings (SkillTable: CreatureKey UNIT_Bubbleling), so it comes before a new one.
        var wave = s.Find(WeakBanditKitKeys.EmbracingWave);
        if (wave != null && wave.Free && wave.Data != null)
        {
            Figure low = s.Self.Share < WaveShare ? s.Self : null;
            foreach (var ally in s.Allies)
                if (ally.Share < WaveShare && (low == null || ally.Share < low.Share)
                    && GroupAiRules.InRange(GroupAiSituation.Distance(s.Self.Position, ally.Position), wave.Data.Range)) low = ally;
            if (low != null) return NewChoice(wave.Key, low.Unit.Cast<IEntity>(), "watermage-wave");
        }
        if (s.Hostiles.Count == 0) return Damage(s, "watermage", WeakBanditKitKeys.TidalMark);
        // A critical bandit took the wave first; the toad still comes when one is left to it.
        toad = !toadFirst && WeakBanditMageRules.WantsToad(toads, bubblings) ? WaterMageSummon(s, WeakBanditKitKeys.SummonBubbleToad, "watermage-toad") : null;
        if (toad != null) return toad;
        var bubbling = WeakBanditMageRules.WantsBubbling(bubblings) ? WaterMageSummon(s, WeakBanditKitKeys.SummonBubbling, "watermage-bubbling") : null;
        return bubbling ?? Damage(s, "watermage", WeakBanditKitKeys.TidalMark);
    }

    private static (int Bubblings, int Toads) OwnSummons(Situation s)
    {
        int bubblings = 0, toads = 0;
        foreach (var summon in DelversSummons.SummonsOf(s.Guid))
        {
            var key = summon.TryCast<UnitEntity>()?.Key;
            if (key == WeakBanditKitKeys.Bubbling) bubblings++;
            else if (key == WeakBanditKitKeys.BubbleToad) toads++;
        }
        return (bubblings, toads);
    }

    // A summon of the mage's (WeakBanditMageRules), cast on a free cell beside it toward its closest foe; null when the skill is
    // not free or no cell is.
    private static Choice WaterMageSummon(Situation s, string skill, string rule)
    {
        var slot = s.Find(skill);
        if (slot == null || !slot.Free || slot.Data == null) return null;
        Figure closest = null;
        foreach (var h in s.Hostiles)
            if (s.Downed == null || !s.Downed.Contains(h))
                if (closest == null || h.Distance < closest.Distance) closest = h;
        if (closest == null) return null;
        var taken = Occupied(s);
        taken.Add((s.Self.Position.x, s.Self.Position.y));
        foreach (var (x, y) in WeakBanditMageRules.BubblingCells((s.Self.Position.x, s.Self.Position.y), (closest.Position.x, closest.Position.y), taken))
        {
            if (!Walkable(s, x, y)) continue;
            var choice = NewChoice(slot.Key, s.Self.Unit.Cast<IEntity>(), rule);
            choice.Cell = new Vector2Int(x, y);
            return choice;
        }
        return null;
    }

    private static Choice FireMage(Situation s)
        => Formation(s, ArcherBehind) ?? Damage(s, "firemage", WeakBanditKitKeys.FireMissile, WeakBanditKitKeys.BlazingEcho, WeakBanditKitKeys.Incinerate);

    // A landed hit on a swordsman by a foe next to it (BanditProtect.OnDamage): its counter stance may answer.
    internal static void NoteMeleeHit(UnitEntity target, UnitEntity attacker)
    {
        if (target == null || attacker == null || KitOf(target) != Kit.Sword) return;
        var inMap = CoreGame.InMap;
        if (inMap == null) return;
        var a = inMap.GetLogicalPosition(target.Guid);
        var b = inMap.GetLogicalPosition(attacker.Guid);
        if (GroupAiSituation.Distance(a, b) > 1) return;
        if (meleeHitAt.Count >= 64) meleeHitAt.Clear();
        meleeHitAt[target.Guid.ToString()] = GameClock.Now;
    }
}
