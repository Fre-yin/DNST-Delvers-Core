using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// What the boss parts' effects do, as rules without the game. A part's effect is on its bearer while
// the part is worn (its equipment effect, BossItemRules.EffectKey); the game side asks the bearer's effects (BossEffects).
// - Overgrowth (staff): the bearer's heals heal 20 % more, and ignore 40 % of the bleeding wound's heal reduction
//   (WoundRules.HealFactor): a factor f becomes f + 0.4 × (1 − f).
internal static class BossEffectRules
{
    internal const double StaffHealBonus = 0.20;
    internal const double StaffBleedEase = 0.40;

    internal static float HealWithBonus(float value, bool staff)
        => staff && value > 0f ? (float)(value * (1.0 + StaffHealBonus)) : value;

    // Whose staff a heal counts for: "self" for the bearer's own heal, "summon" for a heal of what the bearer summoned (the
    // Strange Seed), null when the healer does not wear the staff now. Worn now counts, not worn when
    // summoning: the game side reads the summoner's equipment effect at the heal.
    internal static string StaffHealSource(bool fromSummon, bool healerWearsNow)
        => !healerWearsNow ? null : fromSummon ? "summon" : "self";

    internal static double EasedHealFactor(double factor, bool staff)
        => staff ? factor + StaffBleedEase * (1.0 - factor) : factor;

    // ---- Blood Warden (shield) ----

    // When another party member falls below TriggerShare of its health, the bearer gives SacrificeShare of its current
    // health (head and body each) and the member gets ShieldShare of it (three times) as a shield (a Barrier effect, one stack one point, the game caps the
    // gain) for ShieldSeconds. Half of what is left of the shield when it ends
    // heals the member. Never the bearer itself (the bearer's own fall does nothing and keeps the use), once per
    // fight: the use comes back when the bearer is out of combat.
    internal const string BloodGuardKey = "AFFECTER_DelversCoreBossBloodGuard";

    // The Blood Warden's shield may go past the shields' cap. The game cuts the gain of a shield (category
    // Barrier, 801) to 2 x MaxHealthTotal less all shields there (AffecterApplier.Apply), so a full shield
    // pool took nothing of it and the shield was lost. Right around its stacks, in the same queue, the member's
    // MaxHealthTotalPerModifier goes up by this percent and down again: the cap then has room for exactly this shield, the
    // health stays (a change of the maximum does not move it), and no other shield is pushed out. 0 when it fits anyway.
    internal static float CapRaisePercent(float maxTotal, float totalPercent, float shieldsNow, int wanted)
    {
        if (!(maxTotal > 0f) || wanted <= 0 || float.IsNaN(shieldsNow)) return 0f;
        var room = 2f * maxTotal - Math.Max(0f, shieldsNow);
        if (wanted <= room) return 0f;
        var needed = (Math.Max(0f, shieldsNow) + wanted + 1f) / 2f;
        return (needed / maxTotal - 1f) * (100f + totalPercent);
    }
    internal const string BloodGuardTemplate = "AFFECTER_Barrier";
    internal const double TriggerShare = 0.35;
    internal const double SacrificeShare = 0.40;
    internal const double ShieldShare = 3.0;
    internal const float ShieldSeconds = 8f;
    internal const double RestHealShare = 0.5;

    // The member the warden shields: another living member below the trigger, the lowest share first; null for none.
    internal static string BloodGuardTarget(string bearer, IEnumerable<(string Guid, bool Alive, double Share)> members)
    {
        string best = null;
        var lowest = double.MaxValue;
        foreach (var (guid, alive, share) in members)
        {
            if (!alive || guid == bearer || double.IsNaN(share) || share >= TriggerShare || share >= lowest) continue;
            best = guid;
            lowest = share;
        }
        return best;
    }

    // What the bearer gives from head and body, and the shield's points (whole stacks).
    internal static (double Head, double Body, int Shield) Sacrifice(double head, double body)
    {
        var h = Math.Max(0.0, head) * SacrificeShare;
        var b = Math.Max(0.0, body) * SacrificeShare;
        return (h, b, (int)Math.Round((h + b) * ShieldShare));
    }

    // The shield's stack cap is the barrier template's (999999 for AFFECTER_Barrier): with -1, as first given, the game kept
    // only a stack or so of the requested points (shield=386 asked, the member took the hit).
    internal static int ShieldMaxStack(int? templateMax) => templateMax is > 0 ? templateMax.Value : 999999;

    // Why a shield left: "expired" within EndTicks of its end, else "consumed".
    internal static string ShieldEndReason(long nowTick, long endTick) => Math.Abs(nowTick - endTick) <= EndTicks ? "expired" : "consumed";

    internal static double RestHeal(int restStacks) => restStacks > 0 ? restStacks * RestHealShare : 0.0;

    // The shield is followed every game tick. It ran out when it is gone within EndTicks ticks of its end; gone earlier it
    // was broken, nothing is left. The rest is what was last read of its own stacks (0 before the first read); other
    // barriers on the member never count. Which barrier the game uses up first is open.
    internal const int EndTicks = 2;

    // A shield given waits AppliedGraceTicks to be seen on the member before it counts as gone: Core gives it into the game's
    // queue, and the follow can run in a tick before the game worked through that queue (the first shield
    // of a session ended "consumed" one tick after it was given, with no stacks ever read, and was kept as a shield after).
    internal const int AppliedGraceTicks = 10;

    internal static bool StillPending(bool read, long nowTick, long startTick) => !read && nowTick - startTick <= AppliedGraceTicks;

    internal static int ShieldRest(long nowTick, long endTick, int lastStacks)
        => Math.Abs(nowTick - endTick) <= EndTicks ? Math.Max(0, lastStacks) : 0;

    // ---- Bloodletting (greatsword) ----

    // A landed hit of the bearer adds DamageShare of its damage as the game's bleeding: the bleeding deals per stack and
    // second its first value, over BleedSeconds, so the stacks are DamageShare x damage / (value x
    // seconds), rounded; a hit too small for half a stack adds none. The bleeding's damage is physical, the target's
    // resistance still applies, so the share is before resistance. 30 % (was 20 %).
    internal const double DamageShare = 0.30;

    internal static int BloodlettingStacks(double damage, double perStackSecond, double seconds)
    {
        if (!(damage > 0) || !(perStackSecond > 0) || !(seconds > 0)) return 0;
        return (int)Math.Round(DamageShare * damage / (perStackSecond * seconds), MidpointRounding.AwayFromZero);
    }

    // The bleeding the greatsword put on a target heals its bearer for HealShare of what it deals. The game
    // keeps one bleeding per target, the stacks of every source added up under one duration that drops them all at once, and its
    // tick names no source (affecter steps 9, 12, 13). So the ledger counts which stacks each bearer put on
    // which target, and a tick heals every bearer HealShare x tick damage x its stacks / all stacks. Enemies of the party only:
    // on a party member the bleeding feeds the wound (Wounds), whose fatal stage shows one stack for all, so the game's stack
    // count is no measure there. (after the first check in game): 20 %, half was too strong with many
    // bleeding targets around a tank.
    internal const double HealShare = 0.20;
    internal const int LedgerCap = 512;

    // (heal parse in game: 99 stacks on one target heal 60 a second): a bearer heals at most HealCapPerSecond
    // a second, from all its bleeding targets together. The game's bleeding ticks once a second for every target in the same
    // tick, so the budget is per game second.
    internal const double HealCapPerSecond = 30.0;

    internal sealed class HealBudget
    {
        private readonly Dictionary<string, (long Second, double Used)> used = new(StringComparer.Ordinal);

        // What of the asked heal the bearer still gets in this game second.
        internal double Take(string bearer, long second, double asked)
        {
            if (bearer == null || !(asked > 0)) return 0.0;
            var spent = used.TryGetValue(bearer, out var entry) && entry.Second == second ? entry.Used : 0.0;
            var granted = Math.Min(asked, Math.Max(0.0, HealCapPerSecond - spent));
            used[bearer] = (second, spent + granted);
            return granted;
        }

        internal void Clear() => used.Clear();
    }

    internal sealed class BloodLedger
    {
        private sealed class Entry
        {
            internal long Seq;
            internal readonly Dictionary<string, double> Own = new(StringComparer.Ordinal);
        }

        private readonly Dictionary<string, Entry> targets = new(StringComparer.Ordinal);
        private long seq;

        internal int Count => targets.Count;

        internal double Stacks(string target, string bearer)
            => target != null && bearer != null && targets.TryGetValue(target, out var entry) && entry.Own.TryGetValue(bearer, out var stacks) ? stacks : 0.0;

        // A bearer's hit took the target's bleeding from before to after stacks (after the game's cap).
        internal void Add(string target, string bearer, int before, int after)
        {
            if (target == null || bearer == null) return;
            Sync(target, before);
            if (after <= before) return;
            if (!targets.TryGetValue(target, out var entry))
            {
                // Full: the target counted first goes (one that died without a tick of its bleeding stays until then).
                if (targets.Count >= LedgerCap) targets.Remove(targets.OrderBy(pair => pair.Value.Seq).First().Key);
                targets[target] = entry = new Entry { Seq = ++seq };
            }
            entry.Own[bearer] = (entry.Own.TryGetValue(bearer, out var stacks) ? stacks : 0.0) + (after - before);
        }

        // The game's stacks now: none, and the target is forgotten (its bleeding ran out or was cleansed); fewer than counted
        // (a bandage, a cleanse of part), and every bearer's count is cut alike.
        internal void Sync(string target, int total)
        {
            if (target == null || !targets.TryGetValue(target, out var entry)) return;
            if (total <= 0)
            {
                targets.Remove(target);
                return;
            }
            var counted = entry.Own.Values.Sum();
            if (counted <= total) return;
            var factor = total / counted;
            foreach (var bearer in entry.Own.Keys.ToList()) entry.Own[bearer] *= factor;
        }

        // What one tick of the target's bleeding heals each bearer; the target's total stacks as the game reads them now.
        internal List<(string Bearer, double Heal)> Heals(string target, int total, double tickDamage)
        {
            Sync(target, total);
            var heals = new List<(string Bearer, double Heal)>();
            if (target == null || total <= 0 || !(tickDamage > 0) || !targets.TryGetValue(target, out var entry)) return heals;
            foreach (var pair in entry.Own)
                if (pair.Value > 0) heals.Add((pair.Key, HealShare * tickDamage * pair.Value / total));
            return heals;
        }

        internal void Forget(string target)
        {
            if (target != null) targets.Remove(target);
        }

        internal void Clear() => targets.Clear();
    }

    // ---- Falcon Eyes (bow), beyond sight ----

    // +2 range on every skill the bearer holds, the auto attack included; the dash (QuickDash) stays as it
    // is. A new skill gets the bonus by itself, only the exceptions are listed.
    internal const int RangeBonus = 2;
    internal const string DashKey = "SKILL_QuickDash";

    internal static int RangeWithBonus(int range, bool ownSkill, string skillKey)
        => !ownSkill || range < 1 || skillKey == DashKey ? range : range + RangeBonus;

    // The game's checks of a cast read the table range directly, past the range getter (its inlined read), and let a cast
    // through up to the range plus a correction of its own: RangeCondition's constant for the execution check, the
    // caller's addRange for CombatUtility.IsInRange(context). Without the bonus there, a skill reaches only 1 cell further
    // than its table range, whatever the getter says (boss bow shot from 6, never from 7 cells).
    internal const int CastRangeCorrection = 1;

    internal static bool InCastRange(int distance, int range, int correction) => distance <= range + correction;

    // Each cell of distance to the target beyond CritFrom adds CritPerCell crit chance.
    internal const int CritFrom = 4;
    internal const double CritPerCell = 3.0;

    internal static double FalconCritBonus(int distance) => distance > CritFrom ? CritPerCell * (distance - CritFrom) : 0.0;

    // CORE_BOSS_EFFECT: one effect at work, for the toolkit's fight checks.
    internal static string Line(string name, string unit, string detail)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BOSS_EFFECT name={0} unit={1} {2}", name, unit, detail);
}
