using DungeonSettlersDelvers.Core;
using static TestKit;

// Lifebloom: stacks, tooltip, heal paths, waits, seed bursts and the NPC variants.
internal static class LifebloomSection
{
    internal static void Run()
    {
        Stacks();
        Tooltip();
        HealPath();
        TickWaits();
        CampWakes();
        SeedBurst();
        HealerWithoutFireMissile();
        PendingHeals();
        HealAmounts();
        OffGlobalCooldown();
        NpcStatFactor();
    }

    static void Stacks()
    {
        // Lifebloom: stacks, duration, the bloom at the end, charges.
        {
            var first = LifebloomRules.Cast(default, 100, 10, out var burstFirst);
            var second = LifebloomRules.Cast(first, 105, 10, out var burstSecond);
            var late = LifebloomRules.Cast(second, 118, 10, out var burstLate);
            Check(first.Stacks == 1 && first.Until == 115 && burstFirst == 0 && second.Stacks == 2 && second.Until == 120 && burstSecond == 0
                    && late.Stacks == 3 && late.Until == 133 && burstLate == 2
                    && Math.Abs(LifebloomRules.HealFor(second, 118, 125) - 2 * 2 * LifebloomRules.PlayerPerStackDefault * 10) < 1e-9
                    && Math.Abs(LifebloomRules.Burst(late, 3) - 3 * LifebloomRules.PlayerBurstDefault * 10) < 1e-9,
                "Lifebloom: each cast adds a stack up to three and renews the duration; a late recast blooms the old stacks");
        }
        // (bloom sound a second late): the game tick blooms a buff from the moment its time is up, a game tick
        // (1/60 s) before stays running.
        Check(LifebloomRules.Expired(new LifebloomRules.State(2, 120, 10), 120) && !LifebloomRules.Expired(new LifebloomRules.State(2, 120, 10), 120 - 1 / 60.0)
                && !LifebloomRules.Expired(default, 130),
            "Lifebloom: a buff is due to bloom exactly when its time is up, checked on every game tick");
        Check(LifebloomRules.BloomReason(new LifebloomRules.State(2, 120, 10), 120, false) == "end"
                && LifebloomRules.BloomReason(new LifebloomRules.State(2, 120, 10), 110, true) == "dispel"
                && LifebloomRules.BloomReason(new LifebloomRules.State(2, 120, 10), 119.5, true) == "end"
                && LifebloomRules.BloomReason(new LifebloomRules.State(2, 120, 10), 110, false) == null
                && LifebloomRules.BloomReason(default, 110, true) == null
                && LifebloomRules.Split(30, 10, 30) == (7.5, 22.5) && LifebloomRules.Split(100, 10, 30) == (10, 30) && LifebloomRules.Split(5, 0, 0) == (0, 0),
            "Lifebloom: it blooms at the end or when dispelled; a heal splits over head and body by what each misses");
        {
            var full = LifebloomRules.Charges.Full;
            var one = LifebloomRules.Spend(full, 10, 12, out var cooldownOne);
            var two = LifebloomRules.Spend(one, 11, 12, out _);
            var none = LifebloomRules.Spend(two, 12, 12, out var cooldownNone);
            Check(full.Count == 3 && one.Count == 2 && one.NextAt == 22 && !cooldownOne && two.NextAt == 22 && none.Count == 0 && cooldownNone
                    && LifebloomRules.BlockSeconds(none, 15) == 7 && LifebloomRules.BlockSeconds(one, 15) == 0
                    && LifebloomRules.Recharge(none, 21).Count == 0 && LifebloomRules.Recharge(none, 22).Count == 1
                    && LifebloomRules.Recharge(none, 46) is { Count: 3 } back && back.NextAt == 58,
                "Lifebloom: three charges, one back every 12 s from the first spent; the game's cooldown only after the last");
        }
    }

    static void Tooltip()
    {
        // Tooltip step 1: Lifebloom's numbers as the game's own skills show them, {n} from the row's FloatValues
        // and the stats as tags. Every {n} must have a value (the game shows "[No_Value]" otherwise); no hyphens in the new texts.
        {
            var values = LifebloomRules.TooltipValues(0.08, 0.15);
            var highest = LifebloomRules.SkillDescription.AllLanguages
                .SelectMany(text => System.Text.RegularExpressions.Regex.Matches(text, @"(?<!\{)\{(\d+)\}(?!\})").Select(m => int.Parse(m.Groups[1].Value)))
                .DefaultIfEmpty(-1).Max();
            Check(values.SequenceEqual(new[] { 8f, 15f, 3f, 15f, 3f })
                    && LifebloomRules.TooltipValues(0.125, 0.3).SequenceEqual(new[] { 12.5f, 15f, 3f, 30f, 3f })
                    && highest == 4 && highest < values.Length
                    && LifebloomRules.SkillDescription.AllLanguages.Count == 10 && LifebloomRules.SkillDescription.AllLanguages.All(text =>
                        text.Contains("[STATUS_MagicAttackPower]", StringComparison.Ordinal) && text.Contains("[STATUS_HealthTotal]", StringComparison.Ordinal)
                        && Enumerable.Range(0, 5).All(i => text.Contains("{" + i + "}", StringComparison.Ordinal)) && text.IndexOfAny(new[] { '-', '–', '—' }) < 0)
                    && LifebloomRules.SkillDescription.German.Contains("{0}% × [STATUS_MagicAttackPower]", StringComparison.Ordinal),
                "Lifebloom tooltip: {0} to {4} from the row (8 % per stack and second, 15 s, 3 stacks, 15 % bloom, 3 charges), stats as tags, no hyphens, in all ten languages");
        }
        Check(LifebloomRules.NpcPercentDefault == 0.5 && LifebloomRules.NpcPercent(1.0) == 1.0 && LifebloomRules.NpcPercent(null) == LifebloomRules.NpcPercentDefault && LifebloomRules.NpcPercent(-1) == LifebloomRules.NpcPercentDefault
                && LifebloomRules.NpcPercent(50) == LifebloomRules.NpcPercentMax && LifebloomRules.NpcPercent(2.5) == 2.5
                && Math.Abs(LifebloomRules.NpcEffectivePercent(1, 20, 10, 1) - 1.3) < 1e-9
                && Math.Abs(LifebloomRules.NpcPower(200, 2) * LifebloomRules.NpcHealPerStack - 4) < 1e-9 && LifebloomRules.NpcPower(0, 2) == 0
                && LifebloomRules.SkillNameKey == "TEXTKEY_SKILL_DelversLifebloom_NAME"
                && LifebloomRules.Name.AllLanguages.Count == 10,
            "Lifebloom: Core's enemies heal a share of the target's maximum, raised by intelligence and willpower");
    }

    static void HealPath()
    {
        // Heal path, the band as one, finishing and moving up.
        bool Open(int x, int y) => Math.Abs(x) <= 12 && Math.Abs(y) <= 12;
        // Bound aggro: a capable player 3 cells from a comrade counts as near although 11 from her.
        var bandNear = GroupAiRules.GroupCapableNear(new[] { 11, 3 }, 8) && !GroupAiRules.GroupCapableNear(new[] { 11, 9 }, 8);
        // Heal path: out of reach with a heal free; never taunted, never attacked with a foe within 3, never within reach.
        var path = GroupAiRules.HealPath(0.5f, 0.9, true, 9, 5, false, 6, false) == "normal"
            && GroupAiRules.HealPath(0.2f, 0.9, true, 9, 5, false, 6, false) == "emergency"
            && GroupAiRules.HealPath(0.5f, 0.9, true, 4, 5, false, 6, false) == null
            && GroupAiRules.HealPath(0.5f, 0.9, false, 9, 5, false, 6, false) == null
            && GroupAiRules.HealPath(0.5f, 0.9, true, 9, 5, true, 3, false) == null
            && GroupAiRules.HealPath(0.5f, 0.9, true, 9, 5, false, 6, true) == null
            && GroupAiRules.HealPath(0.95f, 0.9, true, 9, 5, false, 6, false) == null
            && GroupAiRules.HealPathLimits("normal") == (8, 0, 2) && GroupAiRules.HealPathLimits("emergency") == (12, 1, 1);
        var finish = GroupAiRules.HealerMayFinish(true, true, true, true) && GroupAiRules.HealerMayFinish(false, false, false, false)
            && !GroupAiRules.HealerMayFinish(false, true, false, false) && !GroupAiRules.HealerMayFinish(false, false, true, false)
            && !GroupAiRules.HealerMayFinish(false, false, false, true);
        // Moving up: the lowest comrade below 90 %, else the fighting one; only when safe.
        var support = GroupAiRules.SupportTarget(new[] { ("a", 1f, true), ("b", 0.6f, false), ("c", 0.7f, false) }, 0.9) == "b"
            && GroupAiRules.SupportTarget(new[] { ("a", 1f, false), ("b", 1f, true) }, 0.9) == "b"
            && GroupAiRules.SupportTarget(new[] { ("a", 1f, false) }, 0.9) == null
            && GroupAiRules.SupportSafe(4, false, false) && !GroupAiRules.SupportSafe(3, false, false) && !GroupAiRules.SupportSafe(5, true, false);
        // A comrade fights 9 cells away and she is safe: an approach cell within 5 of him, at least 3 from the foe.
        var up = EscapeRules.Approach(0, 0, 9, 0, 5, new[] { new EscapeRules.Foe(10, 1, false) }, 8, 0, 3, Open, 3);
        // A comrade fights 25 cells away: without closer none; with it a cell 8 steps nearer, as far
        // from the foe as asked; nothing nearer when every way is blocked.
        var farFoe = new[] { new EscapeRules.Foe(26, 0, false) };
        bool Wide(int x, int y) => Math.Abs(x) <= 40 && Math.Abs(y) <= 12;
        var farNone = EscapeRules.Approach(0, 0, 25, 0, 5, farFoe, 8, 0, 2, Wide, 3);
        var farStep = EscapeRules.Approach(0, 0, 25, 0, 5, farFoe, 8, 0, 2, Wide, 3, closer: true);
        var walled = EscapeRules.Approach(0, 0, 25, 0, 5, farFoe, 8, 0, 2, (x, y) => Wide(x, y) && x <= 0, 3, closer: true);
        var far = farNone == null && farStep != null && farStep.X == 8 && farStep.Steps == 8 && Math.Max(Math.Abs(farStep.X - 26), Math.Abs(farStep.Y)) >= 2
            && walled == null;
        // Leash to the hurt comrade, not to the healthy one: the heal target pulls her off the corner.
        var hurtPull = EscapeRules.Choose(0, 0, new[] { new EscapeRules.Foe(1, 0, false) }, new[] { (-1, 1) }, 2, 3,
            (x, y) => Open(x, y) && (x, y) != (1, 0) && (x, y) != (-1, 1), 3, new[] { (0, -6) });
        Check(bandNear && path && finish && support && far
                && up != null && Math.Max(Math.Abs(up.X - 9), Math.Abs(up.Y)) <= 5 && Math.Max(Math.Abs(up.X - 10), Math.Abs(up.Y - 1)) >= 3 && up.Steps <= 8
                && hurtPull != null && hurtPull.Y < 0 && hurtPull.HealTargets >= 0,
            "herbalist: heal path to a comrade out of reach, the band counts as one, finishing only when allowed, moving up to a far fight, a fight beyond one move covered in several");
    }

    static void TickWaits()
    {
        // The AI's waits run in game ticks. The same 1 s lock holds 60 ticks whether
        // they come in 1 s (speed 1) or 0.29 s (speed 3.5) of real time; a tick that jumps back after a load ends every wait.
        var lockStart = GameClockRules.SecondsOf(6000);
        bool HeldAfter(long ticks) => !GameClockRules.Elapsed(GameClockRules.SecondsOf(6000 + ticks), lockStart, 1f);
        Check(GameClockRules.SecondsOf(60 * 5) == 5f && HeldAfter(59) && !HeldAfter(60)
                && GameClockRules.Elapsed(GameClockRules.SecondsOf(120), lockStart, 1f)
                && GameClockRules.Since(GameClockRules.SecondsOf(120), lockStart) == float.MaxValue
                && GameClockRules.Since(3f, 1f) == 2f
                && GameClockRules.Running(5f, 4f, 6f) && !GameClockRules.Running(3f, 4f, 6f) && !GameClockRules.Running(6f, 4f, 6f)
                && GroupAiRules.BuffRunning(105f, 100f, 10f) && !GroupAiRules.BuffRunning(105f, 2f, 10f) && !GroupAiRules.BuffRunning(99f, 100f, 10f)
                && GroupAiRules.HoldExpired(100f, 2f) && !GroupAiRules.HoldExpired(100f, 101f)
                && GroupAiRules.TopAttacker(new List<(string Attacker, float Amount, float At)> { ("a", 50f, 100f) }, 2f, 3f, out _, out _) == null,
            "game clock: a 1 s lock lasts 60 game ticks at any speed; a tick jumping back from 6000 to 120 ends waits, buffs and hit windows");
    }

    static void CampWakes()
    {
        // One bandit attacked or taunted and the camp wakes; another camp stays calm.
        var camp = new[] { ("b", 2, false, false), ("c", 4, false, false), ("d", 6, false, false), ("x", 20, false, false), ("f", 3, false, true), ("w", 3, true, false) };
        // The trigger's cause (attack or a player's taunt) does not matter: the same three wake.
        var woken = GroupAiRules.WakeGroup(camp);
        Check(woken.SequenceEqual(new[] { "b", "c", "d" }) && GroupAiRules.WakeGroup(null).Count == 0
                && GroupAiRules.GroupWakeLine("t", 3, 9) == "CORE_AI_BANDIT_GROUP_WAKE trigger=t woken=3 radius=12 tick=9"
                && GroupAiRules.GroupWakeKey == "bandits.groupWake",
            "bandits: an attacked or taunted bandit wakes its three comrades; the far camp, the fallen and the already fighting stay as they are");
        Check(GroupAiRules.WakeDelay(0) == 0.1f && GroupAiRules.WakeDelay(1) == 0.3f && Math.Abs(GroupAiRules.WakeDelay(0.5) - 0.2f) < 1e-6f
                && GroupAiRules.WakeDelay(-1) == 0.1f && GroupAiRules.WakeDelay(2) == 0.3f && GroupAiRules.WakeDelay(double.NaN) == 0.1f,
            "bandits: each woken comrade waits its own 0.1 to 0.3 s of game time");
        Check(!GroupAiRules.WakeDue(10.2f, 10.1f) && GroupAiRules.WakeDue(10.2f, 10.2f) && GroupAiRules.WakeDue(10.2f, 11f),
            "bandits: a delayed wake comes when its game time has come");
    }

    static void SeedBurst()
    {
        // Frieren grows her seed for the Life Burst when someone next to it is below 35 %, or below
        // 60 % while Lifebloom is not free; next to it means one of the eight cells around the seed.
        Check(GroupAiRules.SeedBurstNow(0.3f, true) && GroupAiRules.SeedBurstNow(0.5f, false) && !GroupAiRules.SeedBurstNow(0.5f, true)
                && !GroupAiRules.SeedBurstNow(0.7f, false) && !GroupAiRules.SeedBurstNow(null, false)
                && GroupAiRules.NextToSeed(5, 5, 6, 6) && GroupAiRules.NextToSeed(5, 5, 5, 4) && !GroupAiRules.NextToSeed(5, 5, 5, 5)
                && !GroupAiRules.NextToSeed(5, 5, 7, 5),
            "party healer: the seed grows at once for its Life Burst when someone next to it is very low, or low without Lifebloom");
        // The summoner read from the Nullable<Guid> backing field; the GUID text matches
        // unit.Guid.ToString, an unset or empty field and a field not found read as "".
        {
            var owner = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
            Check(SummonerRules.GuidText(true, owner.ToByteArray()) == "3f2504e0-4f89-11d3-9a0c-0305e82c3301"
                    && SummonerRules.GuidText(false, owner.ToByteArray()) == "" && SummonerRules.GuidText(true, Guid.Empty.ToByteArray()) == ""
                    && SummonerRules.GuidText(true, null) == "" && SummonerRules.GuidText(true, new byte[4]) == ""
                    && SummonerRules.ValueOffset == 4 && SummonerRules.FieldUsable(0x88) && !SummonerRules.FieldUsable(0) && !SummonerRules.FieldUsable(-1)
                    && SummonerRules.FieldLine("building", 0x88) == "CORE_SUMMON_FIELD type=building offset=136 ok=1"
                    && SummonerRules.FieldLine("unit", 0) == "CORE_SUMMON_FIELD type=unit offset=0 ok=0",
                "summons: the summoner's GUID from the backing field, \"\" without one or without the field");
        }
        // The seed burst diagnosis names the stage it stopped at, once per change and at most
        // every 2 game seconds per caster.
        Check(GroupAiRules.SeedCheckStages.SequenceEqual(new[] { "slot", "seed", "range", "adjacent", "rule", "burst" })
                && GroupAiRules.SeedCheckDue(null, 0f, "slot", 0f) && !GroupAiRules.SeedCheckDue("slot", 10f, "slot", 30f)
                && !GroupAiRules.SeedCheckDue("slot", 10f, "seed", 11.9f) && GroupAiRules.SeedCheckDue("slot", 10f, "seed", 12f)
                && GroupAiRules.SeedCheckDue("slot", 10f, "seed", 3f)
                && GroupAiRules.SeedCheckLine("u", "seed", "SKILL_MucusSprayer", true, 2, 0, "", "u", null, 6, null, true, 9)
                    == "CORE_AI_SEED_BURST_CHECK unit=u stage=seed ok=0 upgrade=MucusSprayer free=1 seeds=2 own=0 summoner=- self=u dist=- range=6 lowest=- bloomFree=1 tick=9"
                && GroupAiRules.SeedCheckLine("u", "burst", "SKILL_SolarBloom", true, 1, 1, "u", "u", 3, 6, 0.3f, false, 10)
                    == "CORE_AI_SEED_BURST_CHECK unit=u stage=burst ok=1 upgrade=SolarBloom free=1 seeds=1 own=1 summoner=u self=u dist=3 range=6 lowest=0.30 bloomFree=0 tick=10"
                && GroupAiRules.SeedCheckLine("u", "slot", null, false, 0, 0, null, "u", null, null, null, false, 1).Contains("upgrade=- free=0"),
            "party healer: the seed burst check line names where it stopped, logged on a change at most every 2 game seconds");
        // The probe line shows the own and the game's check of Lifebloom side by side.
        Check(GroupAiRules.NativeProbeLine("u", "free", "check:InvalidTarget", "SKILL_BasicMagicMissile", 3)
                == "CORE_AI_NATIVE_PROBE unit=u skill=DelversLifebloom slotState=free slotNative=check:InvalidTarget chosen=BasicMagicMissile tick=3"
                && GroupAiRules.NativeSkipsCooldown && GroupAiRules.NativeStep(true, false, true, true, true, true) == "gcd"
                && GroupAiRules.NativeStep(false, false, true, true, true, true) == "nodata"
                && GroupAiRules.NativeStep(false, true, true, false, false, false) == "cd" && GroupAiRules.NativeStep(false, true, false, false, true, true) == "auto"
                && GroupAiRules.NativeStep(false, true, false, true, false, true) == "energy" && GroupAiRules.NativeStep(false, true, false, true, true, false) == "noskill"
                && GroupAiRules.NativeStep(false, true, false, true, true, true) == null
                && GroupAiRules.NativeProbeLine("u", "-", "auto", null, 4).EndsWith("chosen=- tick=4"),
            "group AI probe: slotState and slotNative of Lifebloom with the game's own choice");
    }

    static void HealerWithoutFireMissile()
    {
        // The herbalist without Fire Missile heals only below 0.9 and attacks with her staff
        // when no hostile stands within 2 cells: attacker, then focus, then the nearest, in exact range.
        {
            var herbalist = CoreUnitRules.Units.First(unit => unit.Role == "healer");
            Check(!herbalist.Skills.Contains("SKILL_FireMissile") && herbalist.Skills[0] == "SKILL_BasicMagicMissile"
                    && GroupAiRules.HealerHealBelow(null) == 0.9 && GroupAiRules.HealerHealBelow(2) == 1.0 && GroupAiRules.HealerHealBelow(0.75) == 0.75
                    && GroupAiRules.HealerHeals(0.89f, 0.9) && !GroupAiRules.HealerHeals(0.9f, 0.9) && !GroupAiRules.HealerHeals(1f, 0.9)
                    && GroupAiRules.HealerBasicSafe(3) && !GroupAiRules.HealerBasicSafe(2) && !GroupAiRules.HealerBasicSafe(1)
                    && GroupAiRules.HealerBasicPick(new (string, int)[] { ("a", 7), ("f", 5), ("n", 3) }, 5) == "f"
                    && GroupAiRules.HealerBasicPick(new (string, int)[] { ("a", 4), ("n", 3) }, 5) == "a"
                    && GroupAiRules.HealerBasicPick(new (string, int)[] { ("n", 6) }, 5) == null
                    && GroupAiRules.HealerBasicPick(new (string, int)[] { (null, 2), ("n", 4) }, 5) == "n",
                "herbalist: no Fire Missile, heals below 0.9 only, staff attack when safe on attacker, focus or nearest in exact range");
        }
        // (a dedicated healer who deals damage when a window opens): she waits only for a heal
        // free within 1 s, blooms nobody above 90 %, and may heal herself in an emergency.
        Check(GroupAiRules.HealWaits(true, new[] { 0.5 }) && GroupAiRules.HealWaits(true, new[] { 4.0, 1.0 })
                && !GroupAiRules.HealWaits(true, new[] { 1.5 }) && !GroupAiRules.HealWaits(true, new[] { 0.0 }) && !GroupAiRules.HealWaits(false, new[] { 0.5 })
                && !GroupAiRules.HealWaits(true, new double[0])
                && GroupAiRules.BloomTargetUseful(0.9f) && !GroupAiRules.BloomTargetUseful(0.95f)
                && GroupAiRules.EmergencyPick(0.5f, 0.2f) == "self" && GroupAiRules.EmergencyPick(0.2f, 0.3f) == "ally"
                && GroupAiRules.EmergencyPick(null, 0.1f) == "self" && GroupAiRules.EmergencyPick(0.5f, 0.5f) == null
                && LifebloomRules.SecondsUntilCharges(new LifebloomRules.Charges(3, 0, 12), 5, 2) == 0
                && LifebloomRules.SecondsUntilCharges(new LifebloomRules.Charges(1, 10.5, 12), 10, 2) == 0.5
                && LifebloomRules.SecondsUntilCharges(new LifebloomRules.Charges(0, 11, 12), 10, 2) == 13,
            "party healer: waits only for a heal free within a second, no Lifebloom above 90 %, the emergency heal may go to herself");
    }

    static void PendingHeals()
    {
        // Effective above amount came from pairing heals first come, first served; now by the amount.
        Check(LifebloomRules.PendingMatch(new[] { 9.2, 86.6 }, 86.6) == 1 && LifebloomRules.PendingMatch(new[] { 9.2, 86.6 }, 9.2) == 0
                && LifebloomRules.PendingMatch(new[] { 9.2 }, 50) == -1 && LifebloomRules.PendingMatch(new[] { 9.2 }, 0) == 0
                && LifebloomRules.PendingMatch(new double[0], 9.2) == -1 && LifebloomRules.PendingMatch(new[] { 86.60001 }, 86.6) == 0
                && LifebloomRules.Effective(30, 48) == 78 && LifebloomRules.Effective(-1, 5) == 5,
            "Lifebloom: a processed heal finds its own pending entry by the amount, and effective is what arrived on head and body");
    }

    static void HealAmounts()
    {
        // (variant B): players 0.08 per stack and second and 0.15 at the bloom, from the settings and capped;
        // the bandits' healer heals exactly as before: tick stacks x max x percent / 100, bloom 6.25 times that per stack.
        var npcPower = LifebloomRules.NpcPower(1800, 1.3);
        var npc = LifebloomRules.Cast(default, 0, npcPower, out _, LifebloomRules.NpcHealPerStack, LifebloomRules.NpcBurstPerStack);
        var player = LifebloomRules.Cast(default, 0, 50, out _, LifebloomRules.PlayerPerStack(null), LifebloomRules.PlayerBurst(null));
        Check(Math.Abs(LifebloomRules.HealFor(npc, 0, 1) - 1800 * 1.3 / 100) < 1e-9
                && Math.Abs(LifebloomRules.Burst(npc, 1) - 0.25 * 1800 * 1.3 / 100 / 0.04) < 1e-9
                && Math.Abs(LifebloomRules.HealFor(player, 0, 1) - 0.08 * 50) < 1e-9 && Math.Abs(LifebloomRules.Burst(player, 1) - 0.15 * 50) < 1e-9
                && LifebloomRules.PlayerPerStack(0.12) == 0.12 && LifebloomRules.PlayerPerStack(9) == LifebloomRules.PlayerPerStackMax
                && LifebloomRules.PlayerPerStack(-1) == 0.08 && LifebloomRules.PlayerBurst(5) == LifebloomRules.PlayerBurstMax && LifebloomRules.PlayerBurst(null) == 0.15
                && new LifebloomRules.State(1, 10, 10).PerStack == LifebloomRules.PlayerPerStackDefault,
            "Lifebloom: players heal 0.08 per stack and 0.15 at the bloom from the settings; Core's enemies heal exactly as before");
    }

    static void OffGlobalCooldown()
    {
        // The rule the two global cooldown hooks ask. The three ways to cast Lifebloom (the player's slot, the party AI's
        // choice, the herbalist's own choice) all name the skill by its key, so the same key must skip the global cooldown for each.
        // The herbalist's key is her own constant (GroupAiBandits.LifebloomSkill), written out here as she uses it.
        var routes = new[]
        {
            (Name: "player", Key: LifebloomRules.SkillKey),
            (Name: "party AI", Key: LifebloomRules.SkillKey),
            (Name: "herbalist", Key: "SKILL_DelversLifebloom"),
        };
        foreach (var (name, key) in routes)
        {
            // While a charge is left the game's cooldown does not run: a failure by the global cooldown alone passes.
            var full = LifebloomRules.Charges.Full;
            var one = LifebloomRules.Spend(full, 10, 12, out var cooldownOne);
            var two = LifebloomRules.Spend(one, 11, 12, out var cooldownTwo);
            LifebloomRules.Spend(two, 12, 12, out var cooldownNone);
            Check(LifebloomRules.SkipsGlobalCooldown(key) && LifebloomRules.PassesCooldownCheck(key, cooldownOne)
                    && LifebloomRules.PassesCooldownCheck(key, cooldownTwo) && !LifebloomRules.PassesCooldownCheck(key, cooldownNone),
                $"Lifebloom off the global cooldown ({name}): the check passes while a charge is left and fails once the last is spent");
        }
        // Every other skill keeps the global cooldown, the healing touch whose class Lifebloom borrows included.
        Check(!LifebloomRules.SkipsGlobalCooldown(LifebloomRules.SkillTemplate) && !LifebloomRules.SkipsGlobalCooldown("SKILL_BasicMagicMissile")
                && !LifebloomRules.SkipsGlobalCooldown("") && !LifebloomRules.SkipsGlobalCooldown(null)
                && !LifebloomRules.PassesCooldownCheck(LifebloomRules.SkillTemplate, false) && !LifebloomRules.PassesCooldownCheck(null, false),
            "Lifebloom off the global cooldown: other skills, the healing touch and a publisher without a skill keep the global cooldown");
    }

    static void NpcStatFactor()
    {
        // Core's enemies, bandits.lifebloom.statfactor: missing or negative gives the default 1.0; a value is kept up to the cap,
        // which is the percent's cap (NpcPercentMax, 10). This pins today's numbers; whether the factor should have a cap of its own
        // is an open question, so nothing here changes it.
        Check(LifebloomRules.NpcStatFactorKey == "bandits.lifebloom.statfactor" && LifebloomRules.NpcStatFactorDefault == 1.0 && LifebloomRules.NpcPercentMax == 10.0
                && LifebloomRules.NpcStatFactor(null) == 1.0 && LifebloomRules.NpcStatFactor(-0.5) == 1.0
                && LifebloomRules.NpcStatFactor(0) == 0.0 && LifebloomRules.NpcStatFactor(0.25) == 0.25 && LifebloomRules.NpcStatFactor(2.5) == 2.5
                && LifebloomRules.NpcStatFactor(10) == 10.0 && LifebloomRules.NpcStatFactor(10.5) == 10.0 && LifebloomRules.NpcStatFactor(1000) == 10.0
                && Math.Abs(LifebloomRules.NpcEffectivePercent(1, 20, 10, LifebloomRules.NpcStatFactor(null)) - 1.3) < 1e-9
                && LifebloomRules.NpcEffectivePercent(1, 20, 10, LifebloomRules.NpcStatFactor(0)) == 1.0,
            "Lifebloom: the NPC stat factor defaults to 1.0, keeps a set value up to the cap of 10 and is read as the effective percent's factor");
    }
}
