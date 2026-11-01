#if BEPINEX
using DamageExecutionData = global::Refactor.Combat.DamageExecutionData;
using DamageExecutionLog = global::Refactor.Combat.DamageExecutionLog;
using IExecutionData = global::Refactor.Combat.IExecutionData;
#else
using DamageExecutionData = Il2CppRefactor.Combat.DamageExecutionData;
using DamageExecutionLog = Il2CppRefactor.Combat.DamageExecutionLog;
using IExecutionData = Il2CppRefactor.Combat.IExecutionData;
#endif

namespace DungeonSettlersDelvers.Core;

// GroupAiBandits, protecting the bandits' healer: the hits on her (OnDamage), the shared protected target, the
// chain pull and the stuns that follow it, and turning a bandit's combat target (RetargetTo).
internal static partial class GroupAiBandits
{
    // Whom each bandit's own chain was last cast at, until when (game seconds; GroupAiRules.IsOwnPull).
    private static readonly Dictionary<string, (string Target, double Until)> ownPulls = new(StringComparer.Ordinal);

    // A good chain pull from far off is worth more than one from close.
    internal const int PullMinDistance = 3;

    internal const string PullSkill = BanditChainRules.SkillKey;

    // Damage on the bandits' healer per attacker, for the protection (GroupAiRules.TopAttacker), and
    // the attacker last logged per healer (CORE_AI_BANDIT_PROTECT on a change).
    private static readonly Dictionary<string, List<(string Attacker, float Amount, float At)>> healerHits = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> protectTarget = new(StringComparer.Ordinal);
    // The protected target per healer, the same for all bandits, until when it is held (game seconds).
    private static readonly Dictionary<string, (string Target, double Until)> protectHeld = new(StringComparer.Ordinal);

    internal const string StunSkill = "SKILL_StunCharge";

    // Whom the guard pulled with the chain, for the stun that follows, and whom it stunned after the pull, for
    // the slow ring (game seconds; GroupAiRules.GuardComboSeconds).
    private static readonly Dictionary<string, (string Target, double Until)> pulled = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, (string Target, double Until)> stunnedAfterPull = new(StringComparer.Ordinal);

    // A player unit attacks the bandits' healer: everyone but the healer turns on it, and whoever
    // carries the chain pulls that very unit away when it stands at least PullMinDistance off and in reach,
    // and only once it has come close to the healer and the pull takes it away from her (no damage skill, a far pull
    // even brought attackers to her faster; GroupAiRules.PullPeelsHealer).
    private static Choice Protect(Situation s, string who)
    {
        if (!ProtectHealer || s.Kit == Kit.Healer || s.Healer == null || s.HealerAttacker == null)
        {
            NotePullSkip(s, !ProtectHealer ? "off" : s.Healer == null ? "nohealer" : "noattacker");
            return null;
        }
        if (s.Taunter != null)
        {
            NotePullSkip(s, "taunted");
            return null;
        }
        var attacker = s.HealerAttacker;
        var target = s.Focus = attacker.Unit.Cast<IEntity>();
        if (watched.TryGetValue(s.Guid, out var watch)) watch.FocusGuid = attacker.Guid;
        s.Protecting = true;
        if (s.CombatTarget == null || s.CombatTarget.Guid.ToString() != attacker.Guid) RetargetTo(s, target, attacker.Guid);
        var pull = s.Find(PullSkill);
        var pullTarget = attacker;
        var skip = BanditChainRules.PullSkip(pull != null, pull?.Data != null, pull != null && pull.Free, pullTarget.Distance, PullMinDistance,
            pull?.Data != null && GroupAiRules.InRange(pullTarget.Distance, pull.Data.Range),
            GroupAiRules.PullPeelsHealer(GroupAiSituation.Distance(pullTarget.Position, s.Healer.Position),
                GroupAiSituation.Distance(s.Self.Position, s.Healer.Position)));
        if (skip != null) NotePullSkip(s, skip);
        if (skip == null)
        {
            pulled[s.Guid] = (pullTarget.Guid, GameNow() + GroupAiRules.GuardComboSeconds);
            ownPulls[s.Guid] = (pullTarget.Guid, GameNow() + GroupAiRules.OwnPullSeconds);
            return NewChoice(pull.Key, pullTarget.Unit.Cast<IEntity>(), "pull");
        }
        return null;
    }

    // (the guard pulls rarely: median 0 a fight): why a guard with the chain did not pull, once per
    // guard and reason. CORE_AI_BANDIT_PULL_SKIP unit= reason=off|nohealer|noattacker|taunted|nodata|cooldown|close|range|nopeel.
    private static readonly Dictionary<string, string> pullSkipLogged = new(StringComparer.Ordinal);

    private static void NotePullSkip(Situation s, string reason)
    {
        if (s == null || reason == null || s.Find(PullSkill) == null) return;
        if (pullSkipLogged.TryGetValue(s.Guid, out var last) && last == reason) return;
        if (pullSkipLogged.Count >= 64) pullSkipLogged.Clear();
        pullSkipLogged[s.Guid] = reason;
        Log(BanditChainRules.PullSkipLine(s.Guid, reason, Tick()));
    }

    private static Choice StunAttacker(Situation s)
    {
        if (!s.Protecting || s.HealerAttacker == null) return null;
        var stun = s.Find(StunSkill);
        if (stun == null || !stun.Free || stun.Data == null) return null;
        if (!GroupAiRules.InRange(s.HealerAttacker.Distance, stun.Data.Range)) return null;
        return NewChoice(stun.Key, s.HealerAttacker.Unit.Cast<IEntity>(), "guard-stun");
    }

    // The stun charge on the unit the chain just pulled, within GroupAiRules.GuardComboSeconds; the slow ring
    // follows on it (SlowRing).
    private static Choice StunAfterPull(Situation s)
    {
        if (!pulled.TryGetValue(s.Guid, out var pull)) return null;
        if (GameNow() > pull.Until)
        {
            pulled.Remove(s.Guid);
            return null;
        }
        var stun = s.Find(StunSkill);
        if (stun == null || !stun.Free || stun.Data == null) return null;
        foreach (var hostile in s.Hostiles)
        {
            if (hostile.Guid != pull.Target) continue;
            if (!GroupAiRules.InRange(hostile.Distance, stun.Data.Range)) return null;
            pulled.Remove(s.Guid);
            stunnedAfterPull[s.Guid] = (pull.Target, GameNow() + GroupAiRules.GuardComboSeconds);
            return NewChoice(stun.Key, hostile.Unit.Cast<IEntity>(), "guard-stun");
        }
        pulled.Remove(s.Guid);
        return null;
    }

    private static void RetargetTo(Situation s, IEntity target, string guid, string rule = "protect-healer")
    {
        try
        {
            var info = CoreGame.Component<CombatInfoComponent>(s.Unit.Cast<IEntity>());
            if (info != null)
            {
                CoreTargets.SetCombat(s.Unit, info, target);
                s.CombatTarget = target;
                Log($"CORE_AI_BANDIT_RETARGET unit={s.Guid} to={guid} rule={rule}");
            }
        }
        catch (Exception ex)
        {
            Warn("CORE_AI_BANDIT_RETARGET_FAILED: " + ex.Message);
        }
    }

    // The weakest player unit targeting the healer; the nearer one to the healer on a tie.
    // A landed hit on the bandits' healer (GroupAiBanditDamagePatch): the attacker's damage is kept for
    // ProtectWindowSeconds. Only player units and allies count.
    internal static void OnDamage(IExecutionData data)
    {
        if (data == null || !CoreUnits.Active) return;
        var damage = data.TryCast<DamageExecutionData>();
        var target = damage?.Target?.TryCast<UnitEntity>();
        if (target == null) return;
        var attacker = damage.User?.TryCast<UnitEntity>();
        if (attacker == null || attacker.Faction == FactionType.Enemy) return;
        var amount = damage.GetLog()?.TryCast<DamageExecutionLog>()?.TotalDamage ?? 0f;
        if (amount <= 0f) return;
        // The first landed hit on a bandit wakes its camp (queued for the next tick).
        BanditGroupWake.OnHit(target, attacker);
        // A weak swordsman's counter stance answers a melee hit (BanditWeakClasses).
        NoteMeleeHit(target, attacker);
        // The offensive pull: damage on any bandit by attacker, and the last hit on the herbalist.
        var kit = KitOf(target);
        if (kit != Kit.None) NoteOffensiveDamage(attacker.Guid.ToString(), amount, kit == Kit.Healer, target.Guid.ToString());
        if (!ProtectHealer || kit != Kit.Healer) return;
        var key = target.Guid.ToString();
        var now = GameClock.Now;
        if (!healerHits.TryGetValue(key, out var hits)) healerHits[key] = hits = new List<(string Attacker, float Amount, float At)>();
        hits.RemoveAll(hit => hit.At > now || now - hit.At > GroupAiRules.ProtectWindowSeconds);
        hits.Add((attacker.Guid.ToString(), amount, now));
    }

    // The hostile with the most damage on the healer in the window, if it is still among the hostiles.
    private static Figure TopHealerAttacker(Situation s, Figure healer, out float damage, out int hitters)
    {
        damage = 0f;
        hitters = 0;
        if (!healerHits.TryGetValue(healer.Guid, out var hits)) return null;
        var top = GroupAiRules.TopAttacker(hits, GameClock.Now, GroupAiRules.ProtectWindowSeconds, out damage, out hitters);
        if (top == null) return null;
        foreach (var hostile in s.Hostiles)
            if (hostile.Guid == top) return hostile;
        return null;
    }

    // The protected target (GroupAiRules.ProtectPick), shared by all bandits of the healer and held
    // ProtectHoldSeconds while it still threatens her.
    private static Figure ProtectedTarget(Situation s, Figure healer, double now)
    {
        healerHits.TryGetValue(healer.Guid, out var hits);
        var realNow = GameClock.Now;
        var candidates = new List<(string Guid, float Share, int Distance, float Damage, bool TargetsHealer)>();
        foreach (var hostile in s.Hostiles)
        {
            // Fallen last before protection: a fallen one only while it may be finished.
            if (!GroupAiRules.ProtectCandidate(hostile.Player, s.Downed != null && s.Downed.Contains(hostile), s.FinishReason != null)) continue;
            var damage = 0f;
            if (hits != null)
                foreach (var hit in hits)
                    if (hit.Attacker == hostile.Guid && hit.At <= realNow && realNow - hit.At <= GroupAiRules.ProtectWindowSeconds) damage += hit.Amount;
            candidates.Add((hostile.Guid, hostile.Share, GroupAiSituation.Distance(hostile.Position, healer.Position), damage, TargetsHealer(hostile, healer)));
        }
        protectHeld.TryGetValue(healer.Guid, out var held);
        var stillThreat = candidates.Exists(c => c.Guid == held.Target && GroupAiRules.ProtectThreat(c.Damage, c.TargetsHealer, c.Distance));
        var pick = held.Target;
        if (!GroupAiRules.KeepProtect(held.Target, held.Until, now, stillThreat))
        {
            pick = GroupAiRules.ProtectPick(candidates);
            protectHeld[healer.Guid] = (pick, pick == null ? 0.0 : now + GroupAiRules.ProtectHoldSeconds);
        }
        if (pick == null) return null;
        foreach (var hostile in s.Hostiles)
            if (hostile.Guid == pick) return hostile;
        return null;
    }

    private static bool TargetsHealer(Figure hostile, Figure healer)
    {
        if (!hostile.Player) return false;
        IEntity target = null;
        try
        {
            target = CoreGame.Component<CombatInfoComponent>(hostile.Unit.Cast<IEntity>())?.GetCombatTarget();
        }
        catch (Exception)
        {
        }
        return target != null && target.Guid.ToString() == healer.Guid;
    }
}
