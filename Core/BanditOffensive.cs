namespace DungeonSettlersDelvers.Core;

// The guard's offensive pull and CC chain, and the archer's focus on the pulled one (BanditOffensiveRules). Damage on any
// bandit and the last hit on the herbalist come from GroupAiBanditDamagePatch (OnDamage). Logs: CORE_AI_BANDIT_PULL
// reason=offensive, CORE_AI_BANDIT_FOCUS, the chain as rule=guard-offensive-wait-root|guard-offensive-stun|
// guard-offensive-hammer, skips as CORE_AI_BANDIT_PULL_SKIP reason=offensive:busy|noclass|cooldown|range.
internal static partial class GroupAiBandits
{
    // Damage on any bandit by attacker (DamageWindowSeconds) and the last landed hit on each herbalist (game seconds).
    private static readonly List<(string Attacker, float Amount, float At)> banditDamage = new();
    private static readonly Dictionary<string, float> lastHealerHit = new(StringComparer.Ordinal);

    // Per guard: the pulled unit, the chain's step and until when the step may still come; the last offensive pull.
    private static readonly Dictionary<string, (string Target, BanditOffensiveRules.Step Step, double Until)> offensiveChain = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, double> lastOffensive = new(StringComparer.Ordinal);
    // The archers' focus: the pulled unit until when (one at a time for the camp).
    private static (string Target, double Until) archerFocus;
    private static readonly HashSet<string> waitLogged = new(StringComparer.Ordinal);
    // When each guard's fight began (game seconds); out of combat the entry goes.
    private static readonly Dictionary<string, double> offensiveFightStart = new(StringComparer.Ordinal);

    private static double offensiveIdle = BanditOffensiveRules.IdleDefault;
    private static double offensiveLock = BanditOffensiveRules.CooldownDefault;
    private static double offensiveFocus = BanditOffensiveRules.FocusDefault;

    private static void ResetOffensive()
    {
        banditDamage.Clear();
        lastHealerHit.Clear();
        offensiveChain.Clear();
        lastOffensive.Clear();
        archerFocus = default;
        waitLogged.Clear();
        offensiveFightStart.Clear();
        offensiveIdle = BanditOffensiveRules.Seconds(CoreSettings.Number(BanditOffensiveRules.IdleKey), BanditOffensiveRules.IdleDefault, 60);
        offensiveLock = BanditOffensiveRules.Seconds(CoreSettings.Number(BanditOffensiveRules.CooldownKey), BanditOffensiveRules.CooldownDefault, 300);
        offensiveFocus = BanditOffensiveRules.Seconds(CoreSettings.Number(BanditOffensiveRules.FocusKey), BanditOffensiveRules.FocusDefault, 30);
    }

    // From OnDamage: a landed hit by a player unit on a bandit; on the herbalist also its time.
    private static void NoteOffensiveDamage(string attacker, float amount, bool onHealer, string healerGuid)
    {
        var now = GameClock.Now;
        banditDamage.RemoveAll(hit => hit.At > now || now - hit.At > BanditOffensiveRules.DamageWindowSeconds);
        if (banditDamage.Count < 512) banditDamage.Add((attacker, amount, now));
        if (onHealer) lastHealerHit[healerGuid] = now;
    }

    private static float DamageBy(string attacker)
    {
        var now = GameClock.Now;
        var sum = 0f;
        foreach (var hit in banditDamage)
            if (hit.Attacker == attacker && hit.At <= now && now - hit.At <= BanditOffensiveRules.DamageWindowSeconds) sum += hit.Amount;
        return sum;
    }

    // The offensive pull of a guard with the chain, or null.
    private static Choice OffensivePull(Situation s)
    {
        var pull = s.Find(PullSkill);
        if (pull == null || s.Healer == null) return null;
        var now = GameNow();
        if (s.State != CombatState.InCombat)
        {
            offensiveFightStart.Remove(s.Guid);
            return null;
        }
        if (!offensiveFightStart.TryGetValue(s.Guid, out var fightStart)) offensiveFightStart[s.Guid] = fightStart = now;
        var sinceHit = lastHealerHit.TryGetValue(s.Healer.Guid, out var at) ? GameClock.Now - at : double.PositiveInfinity;
        var idle = BanditOffensiveRules.Idle(sinceHit, now - fightStart);
        var since = lastOffensive.TryGetValue(s.Guid, out var last) ? now - last : double.PositiveInfinity;
        if (!BanditOffensiveRules.May(idle, offensiveIdle, since, offensiveLock, s.Taunter != null))
        {
            if (s.Taunter == null) NotePullSkip(s, idle < offensiveIdle ? "offensive:busy" : "offensive:cooldown");
            return null;
        }
        if (!pull.Free || pull.Data == null)
        {
            NotePullSkip(s, "cooldown");
            return null;
        }
        var tier1 = new List<(string Guid, float Damage, int Distance)>();
        var tier2 = new List<(string Guid, float Damage, int Distance)>();
        var classes = new Dictionary<string, (string Class, Figure Figure)>(StringComparer.Ordinal);
        var anyClass = false;
        foreach (var hostile in s.Hostiles)
        {
            if (!hostile.Player || (s.Downed != null && s.Downed.Contains(hostile))) continue;
            var cls = ClassOf(hostile.Unit);
            var tier = BanditOffensiveRules.TierOf(cls);
            if (tier == 0) continue;
            anyClass = true;
            if (hostile.Distance < PullMinDistance || !GroupAiRules.InRange(hostile.Distance, pull.Data.Range)) continue;
            (tier == 1 ? tier1 : tier2).Add((hostile.Guid, DamageBy(hostile.Guid), hostile.Distance));
            classes[hostile.Guid] = (cls, hostile);
        }
        // Tier 2 only when no tier 1 unit stands within reach.
        var pick = BanditOffensiveRules.Pick(tier1) ?? BanditOffensiveRules.Pick(tier2);
        if (pick == null)
        {
            NotePullSkip(s, anyClass ? "offensive:range" : "offensive:noclass");
            return null;
        }
        var (pickClass, figure) = classes[pick];
        lastOffensive[s.Guid] = now;
        offensiveChain[s.Guid] = (pick, BanditOffensiveRules.Step.WaitRoot, now + GroupAiRules.GuardComboSeconds);
        ownPulls[s.Guid] = (pick, now + GroupAiRules.OwnPullSeconds);
        archerFocus = (pick, now + offensiveFocus);
        Log(BanditOffensiveRules.PullLine(s.Guid, pick, pickClass, double.IsInfinity(idle) ? 999 : idle, DamageBy(pick), Tick()));
        Log(BanditOffensiveRules.FocusLine("archers", pick, archerFocus.Until, Tick()));
        return NewChoice(pull.Key, figure.Unit.Cast<IEntity>(), "guard-offensive-pull");
    }

    // The guard's chain after an offensive pull: wait for the root, stun, Hammering; the slow ring follows by its own rule.
    private static Choice OffensiveChain(Situation s)
    {
        if (!offensiveChain.TryGetValue(s.Guid, out var chain)) return null;
        var now = GameNow();
        var target = s.Hostiles.Find(h => h.Guid == chain.Target);
        if (s.Taunter != null || now > chain.Until || target == null || (s.Downed != null && s.Downed.Contains(target)))
        {
            offensiveChain.Remove(s.Guid);
            return null;
        }
        var entity = target.Unit.Cast<IEntity>();
        while (chain.Step != BanditOffensiveRules.Step.Done)
        {
            switch (chain.Step)
            {
                case BanditOffensiveRules.Step.WaitRoot:
                    if (CoreGame.Affecters(target.Unit)?.Has(BanditOffensiveRules.RootKey) == true)
                    {
                        if (waitLogged.Add(s.Guid + "|" + chain.Target))
                            Log($"CORE_AI_BANDIT_RULE unit={s.Guid} rule=guard-offensive-wait-root target={chain.Target} tick={Tick()}");
                        return NewChoice("", entity, "guard-offensive-wait-root");
                    }
                    break;
                case BanditOffensiveRules.Step.Stun:
                    var stun = s.Find(StunSkill);
                    if (stun != null && stun.Free && stun.Data != null && GroupAiRules.InRange(target.Distance, stun.Data.Range))
                    {
                        offensiveChain[s.Guid] = (chain.Target, BanditOffensiveRules.Step.Hammer, now + GroupAiRules.GuardComboSeconds);
                        return NewChoice(stun.Key, entity, "guard-offensive-stun");
                    }
                    break;
                case BanditOffensiveRules.Step.Hammer:
                    var hammer = s.Find(BanditKitKeys.Hammering);
                    if (hammer != null && hammer.Free && hammer.Data != null && GroupAiRules.InRange(target.Distance, hammer.Data.Range))
                    {
                        offensiveChain.Remove(s.Guid);
                        return NewChoice(hammer.Key, entity, "guard-offensive-hammer");
                    }
                    break;
            }
            chain.Step = BanditOffensiveRules.After(chain.Step);
        }
        offensiveChain.Remove(s.Guid);
        return null;
    }

    // The archer's focus on the pulled unit for FocusSeconds: the unit becomes the archer's focus (Intended) unless it is
    // taunted, the unit fell or left the sniping shot's reach.
    private static void ArcherFocus(Situation s)
    {
        if (archerFocus.Target == null || s.Taunter != null || GameNow() > archerFocus.Until) return;
        var target = s.Hostiles.Find(h => h.Guid == archerFocus.Target);
        var reach = s.Find(BanditKitKeys.SnipingShot)?.Data?.Range ?? 8;
        if (target == null || (s.Downed != null && s.Downed.Contains(target)) || target.Distance > reach) return;
        s.Focus = target.Unit.Cast<IEntity>();
    }

    // The player unit's class by its weapon (BanditOffensiveRules.ClassOf, tier 1 or 2), null for none of the author's weapons.
    private static string ClassOf(UnitEntity unit)
    {
        if (unit == null) return null;
        var sheet = DataSheetManager.Instance;
        if (sheet == null) return null;
        foreach (var key in CorpseKeep.EquippedKeys(unit.Cast<IEntity>()))
        {
            var data = sheet.GetItemData(key);
            if (data == null || !data.IsWeapon) continue;
            var cls = BanditOffensiveRules.ClassOf((int)data.ItemSubType);
            if (cls != null) return cls;
        }
        return null;
    }
}
