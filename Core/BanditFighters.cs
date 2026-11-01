namespace DungeonSettlersDelvers.Core;

// GroupAiBandits, the fighters: the blade (Deadly Fear, then its damage skills), the guard (shield, Iron Clad, the
// charge, the slowing ring) and the archer (Head Aim, Arrow Rain, Ankle Break) choose their skill; the bricks they
// share (Damage, Basic, Formation) stay in GroupAiBandits.cs.
internal static partial class GroupAiBandits
{
    internal const float ShieldUpShare = 0.6f;
    internal const int MeleeReach = 2;

    private static int slowMinTargets = GroupAiRules.SlowMinTargetsDefault;
    private static readonly HashSet<string> ironCladFight = new(StringComparer.Ordinal);

    private static Choice Blade(Situation s)
    {
        var protect = Protect(s, "blade");
        if (protect != null) return protect;
        // Deadly Fear only to set up a big hit (BanditFearRules); Serrated Blade took Blade Grinding's
        // place, Sweeping Cleave left the kit.
        var fear = Fear(s);
        if (fear != null) return fear;
        return Damage(s, s.Protecting ? "blade-protect" : "blade", BanditKitKeys.Slay, BanditSerratedRules.SkillKey, BanditKitKeys.DestructiveSplit,
            // A weak blade's greatsword sweep (WeakBanditRules); the boss party has none.
            WeakBanditKitKeys.WhirlwindSlash);
    }

    private static readonly Dictionary<string, string> fearSkipLogged = new(StringComparer.Ordinal);

    private static Choice Fear(Situation s)
    {
        var slot = s.Find(BanditFearRules.SkillKey);
        if (slot == null || !slot.Free || slot.Data == null) return null;
        var target = s.Intended;
        var unit = target?.TryCast<UnitEntity>();
        var inRange = target != null && GroupAiRules.InRange(GroupAiSituation.Distance(s.Self.Position, s.PositionOf(target)), slot.Data.Range);
        var affecters = unit != null ? CoreGame.Affecters(unit) : null;
        double? soonest = null;
        foreach (var key in BanditFearRules.BigSkills)
        {
            var big = s.Find(key);
            soonest = BanditFearRules.Soonest(soonest, BanditFearRules.BigReadyIn(big != null && big.Data != null, big?.Free == true, big?.State == "cd", big?.CooldownLeft ?? 0f));
        }
        var skip = BanditFearRules.Skip(target != null, unit != null && (unit.IsDead || IsDowned(unit)), inRange,
            affecters != null && affecters.Has(BanditFearRules.FlawKey), soonest);
        if (skip == null)
        {
            fearSkipLogged.Remove(s.Guid);
            return NewChoice(slot.Key, target, BanditFearRules.Rule);
        }
        // One line per blade and change of reason.
        if (!fearSkipLogged.TryGetValue(s.Guid, out var last) || last != skip)
        {
            if (fearSkipLogged.Count >= 64) fearSkipLogged.Clear();
            fearSkipLogged[s.Guid] = skip;
            Log(BanditFearRules.SkipLine(s.Guid, target?.Guid.ToString(), skip, soonest, Tick()));
        }
        return null;
    }

    private static Choice Guard(Situation s)
    {
        var protect = Protect(s, "guard");
        if (protect != null) return protect;
        // The offensive pull and its CC chain when the herbalist is left alone (BanditOffensive).
        var offensive = OffensiveChain(s) ?? (s.HealerAttacker == null ? OffensivePull(s) : null);
        if (offensive != null) return offensive;
        var stun = StunAfterPull(s) ?? StunAttacker(s);
        if (stun != null) return stun;

        var ring = SlowRing(s);
        if (ring != null) return ring;
        var self = s.Self.Unit.Cast<IEntity>();
        var shieldUp = s.Find(BanditKitKeys.ShieldUp);
        if (shieldUp != null && shieldUp.Free && s.State == CombatState.InCombat && s.Self.Share < ShieldUpShare && !BuffRunning(s, shieldUp))
        {
            return NewChoice(shieldUp.Key, self, "guard-shieldup");
        }
        // Iron Clad once per fight, a fight being the unit and its combat target.
        var ironClad = s.Find(BanditKitKeys.IronClad);
        var fight = s.Guid + "|" + (s.CombatTarget != null ? s.CombatTarget.Guid.ToString() : "-");
        if (ironClad != null && ironClad.Free && s.State == CombatState.InCombat && !BuffRunning(s, ironClad) && ironCladFight.Add(fight))
        {
            return NewChoice(ironClad.Key, self, "guard-ironclad");
        }
        var charge = s.Find(StunSkill);
        var intended = s.Intended;
        if (charge != null && charge.Free && charge.Data != null && intended != null)
        {
            var distance = GroupAiSituation.Distance(s.Self.Position, s.PositionOf(intended));
            if (distance >= MeleeReach && GroupAiRules.InRange(distance, charge.Data.Range))
            {
                return NewChoice(charge.Key, intended, "guard-stuncharge");
            }
        }
        return Damage(s, s.Protecting ? "guard-protect" : "guard", BanditKitKeys.Hammering, BanditKitKeys.WildFlurry);
    }

    // Guardian's Taunt as a slowing ring (BanditSlowRing), when a player hitting the
    // healer stands in the ring or at least slowMinTargets players do; players counted by Manhattan
    // distance like the skill's area. The unit the guard stunned after its pull counts like the healer's
    // attacker (pull, stun, then the slow).
    private static Choice SlowRing(Situation s)
    {
        var ring = s.Find(GroupAiRules.SlowRingSkill);
        if (ring == null) return null;
        var here = s.Self.Position;
        var inRing = 0;
        var attackerInRing = false;
        string combo = null;
        if (stunnedAfterPull.TryGetValue(s.Guid, out var stunned))
        {
            if (GameNow() <= stunned.Until) combo = stunned.Target;
            else stunnedAfterPull.Remove(s.Guid);
        }
        foreach (var hostile in s.Hostiles)
        {
            var downed = s.Downed != null && s.Downed.Contains(hostile);
            if (!GroupAiRules.SlowRingCounts(hostile.Player, downed, GroupAiRules.Manhattan(here.x, here.y, hostile.Position.x, hostile.Position.y))) continue;
            inRing++;
            if (s.HealerAttacker != null && hostile.Guid == s.HealerAttacker.Guid) attackerInRing = true;
            if (hostile.Guid == combo) attackerInRing = true;
        }
        var reason = GroupAiRules.SlowRingReason(ring.Free, inRing, slowMinTargets, attackerInRing);
        if (reason == null) return null;
        if (combo != null) stunnedAfterPull.Remove(s.Guid);
        return NewChoice(ring.Key, s.Self.Unit.Cast<IEntity>(), "guard-slowring-" + reason);
    }

    private static Choice Archer(Situation s)
    {
        var formation = Formation(s, ArcherBehind);
        if (formation != null) return formation;
        // The unit the guard pulled offensively is its focus for a few seconds.
        ArcherFocus(s);
        Protect(s, "archer");
        if (s.Protecting && s.HealerAttacker != null && s.HealerAttacker.Distance <= 1)
        {
            var ankle = s.Find(BanditKitKeys.AnkleBreak);
            if (ankle != null && ankle.Free)
            {
                return NewChoice(ankle.Key, s.HealerAttacker.Unit.Cast<IEntity>(), "archer-ankle-protect");
            }
        }
        var headAim = s.Find(BanditKitKeys.HeadAim);
        if (headAim != null && headAim.Free && !BuffRunning(s, headAim))
        {
            return NewChoice(headAim.Key, s.Self.Unit.Cast<IEntity>(), "archer-headaim");
        }
        var intended = s.Intended;
        var rain = s.Find(BanditKitKeys.ArrowRain);
        if (rain != null && rain.Free && rain.Data != null && intended != null)
        {
            var at = s.PositionOf(intended);
            var radius = Math.Max(1, rain.Data.AreaRadius);
            // The count is a square around the target (Chebyshev); the rain hits a diamond (Manhattan 2).
            var hit = 0;
            foreach (var hostile in s.Hostiles)
                if (GroupAiSituation.Distance(at, hostile.Position) <= radius) hit++;
            // s.Hostiles holds no fallen unit while they are skipped, so hit counts standing foes only.
            if (hit >= 2 && GroupAiRules.InRange(GroupAiSituation.Distance(s.Self.Position, at), rain.Data.Range)
                && (areaIgnoresDowned || !HitsDowned(s, rain, at)))
            {
                return NewChoice(rain.Key, intended, "archer-rain");
            }
        }
        var ankleBreak = s.Find(BanditKitKeys.AnkleBreak);
        // (under Darius' taunt the archer broke Pamela's ankle next to her): taunted, only the
        // taunter (GroupAiRules.TauntAllows "attack-taunter").
        var ankleTarget = s.Taunter ?? s.ClosestHostile;
        if (ankleBreak != null && ankleBreak.Free && ankleTarget != null && ankleTarget.Distance <= 1)
        {
            return NewChoice(ankleBreak.Key, ankleTarget.Unit.Cast<IEntity>(), "archer-ankle");
        }
        return Damage(s, s.Protecting ? "archer-protect" : "archer", BanditKitKeys.SnipingShot, WeakBanditKitKeys.RapidFire);
    }
}
