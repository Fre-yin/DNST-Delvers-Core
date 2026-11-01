using DungeonSettlersDelvers.Core;
using static TestKit;

// Group AI: shouts, heal paths, target picks and excluded units.
internal static class GroupAiDecisionsSection
{
    internal static void Run()
    {
        TauntAndRain();
        NearCampAndOrderPath();
        LifebloomSlotCooldown();
        LifebloomSounds();
        HealPathAndBasicTarget();
        DummyAndVinesPicks();
        ExcludedDummy();
        BasicAttackOnly();
        GlobalCooldownWarning();
    }

    static void TauntAndRain()
    {
        // (134505): a running foreign taunt replaced by another forced target (the bandit's own chain or another
        // taunter) ends with cause replaced; an ended or own track reports nothing.
        {
            Func<double> sixSeconds = () => 6.0;
            var darius = GroupAiRules.LookAtTaunt(null, "D", 10.0, sixSeconds, 0.5, false, null);
            var byChain = GroupAiRules.LookAtTaunt(darius.Track, "P", 10.85, sixSeconds, 0.5, false, null, ownPull: true);
            var byOther = GroupAiRules.LookAtTaunt(GroupAiRules.LookAtTaunt(null, "D", 10.0, sixSeconds, 0.5, false, null).Track, "K", 11.0, sixSeconds, 0.5, false, null);
            var same = GroupAiRules.LookAtTaunt(GroupAiRules.LookAtTaunt(null, "D", 10.0, sixSeconds, 0.5, false, null).Track, "D", 11.0, sixSeconds, 0.5, false, null);
            Check(darius.Replaced == null && byChain.Replaced == "D" && byChain.Own && byOther.Replaced == "D" && byOther.Started && same.Replaced == null,
                "C34e: a replaced taunt logs its end with cause replaced");
            Check(GroupAiRules.TauntCancels("P", "D", "G") && !GroupAiRules.TauntCancels("D", "D", "G") && !GroupAiRules.TauntCancels("G", "D", "G") && !GroupAiRules.TauntCancels(null, "D", "G")
                && GroupAiRules.TauntCancelLine("G", "D", "P", 7) == "CORE_AI_BANDIT_TAUNT_CANCEL unit=G by=D aim=P tick=7",
                "C34f: a starting taunt drops a running action aimed at anyone but the taunter");
        }
        // The arrow rain on a held focus of rank 0 or 1 in reach, also alone; rank 2 or out of reach keeps the old rule.
        Check(GroupAiReachRules.RainOnFocus(0, true) && GroupAiReachRules.RainOnFocus(1, true) && !GroupAiReachRules.RainOnFocus(2, true)
                && !GroupAiReachRules.RainOnFocus(0, false) && !GroupAiReachRules.RainOnFocus(-1, true),
            "C24c: the arrow rain follows a focus of rank 0 or 1 in reach");
        Check(GroupAiReachRules.TauntWasted(2, 2) && !GroupAiReachRules.TauntWasted(2, 1) && !GroupAiReachRules.TauntWasted(0, 0),
            "C24c: Guardian's Taunt is skipped only when every foe in reach would break it at once");
    }

    static void NearCampAndOrderPath()
    {
        // A sleeping camp nearby does not count as near; only comrades in combat.
        Check(!GroupAiRules.GroupCapableNear(GroupAiRules.BandDistances(11, new[] { (3, false), (9, true) }), 8)
                && GroupAiRules.GroupCapableNear(GroupAiRules.BandDistances(11, new[] { (3, true) }), 8)
                && GroupAiRules.BandDistances(5, null).Count == 1,
            "C17j: only bandits in combat make a capable player near");
        // The input path of a player's order, nested senders joined; via= only when asked for.
        Check(GroupAiRules.OrderVia(new[] { "left-click", "trymove" }) == "left-click>trymove" && GroupAiRules.OrderVia(new string[0]) == "-"
                && GroupAiRules.ClickSender(false, true) == "left-click+shift" && GroupAiRules.ClickSender(true, false) == "left-click+alt"
                && GroupAiRules.StanceOrderLine("f", "player", "MoveTask", null, 7, "left-click>trymove") == "CORE_AI_ORDER unit=f source=player task=MoveTask forced=- via=left-click>trymove tick=7"
                && GroupAiRules.StanceOrderLine("f", "player", "MoveTask", null, 7) == "CORE_AI_ORDER unit=f source=player task=MoveTask forced=- tick=7"
                && GroupAiRules.OrderSourceLine("f", "waypoint", "MoveTask", 9) == "CORE_AI_ORDER_SOURCE unit=f via=waypoint task=MoveTask tick=9",
            "C18h: the input path of a player's order in CORE_AI_ORDER");
    }

    static void LifebloomSlotCooldown()
    {
        // With 1 or 2 charges Core counts down to the next one; with none the game's own cooldown shows;
        // full clears it. Recharge 20 s, next charge in 7.2 s: fill 0.36, "8".
        Check(LifebloomSlotCooldownRules.Show(2, 107.2, 100, 20) is ("ours", var fill, 8f) && Math.Abs(fill - 0.36f) < 0.001f
                && LifebloomSlotCooldownRules.Show(1, 99, 100, 20) == ("ours", 0f, 0f)
                && LifebloomSlotCooldownRules.Show(0, 110, 100, 20) == ("game", 0.5f, 10f)
                && LifebloomSlotCooldownRules.Show(3, 0, 100, 20) == ("none", 0f, 0f)
                && LifebloomSlotCooldownRules.Show(2, 130, 100, 20).Fill == 1f
                && !LifebloomSlotCooldownRules.SlotClockwise(true) && LifebloomSlotCooldownRules.SlotClockwise(false)
                && Math.Abs(LifebloomSlotCooldownRules.SlotAlpha("ours", 0.8f) - 0.36f) < 0.001f
                && LifebloomSlotCooldownRules.SlotAlpha("game", 0.8f) == 0.8f && LifebloomSlotCooldownRules.SlotAlpha("none", 0.8f) == 0.8f
                && LifebloomSlotCooldownRules.ShowLine(Guid.Empty, 2, "ours", 8f, 1) == "CORE_LIFEBLOOM_CD_SHOW caster=00000000-0000-0000-0000-000000000000 charges=2 mode=ours left=8 slots=1"
                && LifebloomSlotCooldownRules.ProbeLine("SKILL_X", 1.5f, 1.5f, 1f, 0f, -1f)
                    == "CORE_LIFEBLOOM_CD_PROBE skill=SKILL_X cooldown=1.50 duration=1.50 imgCooldown=1.00 imgSkillduration=0.00 imgSkillDurationBar=-1.00",
            "C32: the Lifebloom slot counts down to the next charge with 1 or 2 left, at 0 the game's seconds and lock with the circle of the charge's time, full clears; it turns the other way round than the game's cooldowns, lighter while a charge is left");
    }

    static void LifebloomSounds()
    {
        // Cast sound with every cast (no rule), bloom sound with every bloom that heals; no sound per tick
        // and none for a charge back.
        var cases = new (string Name, bool Expected, bool Actual)[]
        {
            ("bloom at the end", true, LifebloomSoundRules.BloomPlays("end", 120)),
            ("bloom by refresh", true, LifebloomSoundRules.BloomPlays("refresh", 80)),
            ("bloom by removal", true, LifebloomSoundRules.BloomPlays("dispel", 40)),
            ("heal tick", false, LifebloomSoundRules.BloomPlays(null, 12)),
            ("bloom that heals nothing", false, LifebloomSoundRules.BloomPlays("end", 0)),
        };
        foreach (var c in cases) Check(c.Expected == c.Actual, "Lifebloom sound: " + c.Name + " -> " + (c.Expected ? "plays" : "silent"));
        Check(LifebloomSoundRules.Files.Length == 2 && Array.IndexOf(LifebloomSoundRules.Files, LifebloomSoundRules.CastFile) >= 0
            && Array.IndexOf(LifebloomSoundRules.Files, LifebloomSoundRules.BloomFile) >= 0,
            "Lifebloom sound: two files, cast and bloom; none for a charge back");
        Check(LifebloomSoundRules.BloomVolumeFor(true) == LifebloomSoundRules.BloomVolume
            && LifebloomSoundRules.BloomVolumeFor(false) < LifebloomSoundRules.BloomVolumeFor(true) && LifebloomSoundRules.BloomVolumeFor(false) > 0f,
            "Lifebloom sound: the bloom is heard at every healed unit, fully at the selected one, quieter at all others");

        // Core's WAV reading: 16 bit stereo, 24 bit mono, and what it refuses.
        static byte[] Wav(short format, short channels, int rate, short bits, byte[] data)
        {
            var bytes = new List<byte>();
            void Text(string s) => bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(s));
            Text("RIFF"); bytes.AddRange(BitConverter.GetBytes(36 + data.Length)); Text("WAVE");
            Text("fmt "); bytes.AddRange(BitConverter.GetBytes(16)); bytes.AddRange(BitConverter.GetBytes(format));
            bytes.AddRange(BitConverter.GetBytes(channels)); bytes.AddRange(BitConverter.GetBytes(rate));
            bytes.AddRange(BitConverter.GetBytes(rate * channels * bits / 8)); bytes.AddRange(BitConverter.GetBytes((short)(channels * bits / 8)));
            bytes.AddRange(BitConverter.GetBytes(bits));
            Text("data"); bytes.AddRange(BitConverter.GetBytes(data.Length)); bytes.AddRange(data);
            return bytes.ToArray();
        }
        var stereo = CoreSoundRules.ReadWav(Wav(1, 2, 48000, 16, new byte[] { 0x00, 0x40, 0x00, 0xC0, 0xFF, 0x7F, 0x00, 0x80 }));
        Check(stereo.Channels == 2 && stereo.Rate == 48000 && stereo.Frames == 2
            && stereo.Samples[0] == 0.5f && stereo.Samples[1] == -0.5f && stereo.Samples[3] == -1f,
            "Core sound: a 16 bit stereo WAV reads as interleaved floats");
        var mono = CoreSoundRules.ReadWav(Wav(1, 1, 44100, 24, new byte[] { 0x00, 0x00, 0x40, 0x00, 0x00, 0xC0 }));
        Check(mono.Channels == 1 && mono.Frames == 2 && mono.Samples[0] == 0.5f && mono.Samples[1] == -0.5f,
            "Core sound: a 24 bit mono WAV reads as floats");
        static bool Refused(byte[] bytes)
        {
            try { CoreSoundRules.ReadWav(bytes); return false; }
            catch (InvalidDataException) { return true; }
        }
        Check(Refused(Wav(3, 2, 48000, 32, new byte[8])) && Refused(Wav(1, 2, 48000, 16, new byte[0])) && Refused(new byte[] { 1, 2, 3 }),
            "Core sound: float WAV, an empty WAV and no WAV are refused");
        Check(CoreSoundRules.DistanceLevel(5) == 1f && CoreSoundRules.DistanceLevel(20) == 0.5f && CoreSoundRules.DistanceLevel(40) == 0f
            && CoreSoundRules.Paused(0f) && !CoreSoundRules.Paused(1f)
            && CoreSoundRules.RunnerStale(true, 10f, 7f) && !CoreSoundRules.RunnerStale(true, 10f, 9f) && !CoreSoundRules.RunnerStale(false, 10f, 0f),
            "Core sound: full near the camera, silent far away, holds at pause; a loop without heartbeat counts as gone");
    }

    static void HealPathAndBasicTarget()
    {
        // A heal path ends arrived, in range before arriving, or aborted (comrade gone or
        // healed, move stopped short, 6 game seconds); it runs on while she still walks.
        Check(GroupAiRules.HealPathResult(true, 3, 4, false, false, false, 2f) == "arrived"
                && GroupAiRules.HealPathResult(false, 4, 4, false, false, true, 1f) == "inrange"
                && GroupAiRules.HealPathResult(false, 6, 4, false, false, true, 1f) == null
                && GroupAiRules.HealPathResult(false, 6, 4, false, false, false, 1f) == "aborted"
                && GroupAiRules.HealPathResult(false, 6, 4, false, false, true, 6f) == "aborted"
                && GroupAiRules.HealPathResult(true, 1, 4, true, false, false, 1f) == "aborted"
                && GroupAiRules.HealPathResult(false, 2, 4, false, true, true, 1f) == "aborted"
                && GroupAiRules.HealPathEndLine("h", "t", "arrived", 5, 4, 2.25f, 9) == "CORE_AI_BANDIT_HEALPATH_END unit=h target=t result=arrived steps=5 walked=4 seconds=2.3 tick=9",
            "herbalist: the heal path ends arrived, in range or aborted, with planned and walked cells");
        // b': after SKIP_DOWNED the basic attack keeps the target the skip set while it is capable;
        // focus first, the nearest only without a valid current target.
        Check(GroupAiRules.BasicTargetPick("f", "c", true, "n") == "f" && GroupAiRules.BasicTargetPick(null, "c", true, "n") == "c"
                && GroupAiRules.BasicTargetPick(null, "c", false, "n") == "n" && GroupAiRules.BasicTargetPick(null, null, false, "n") == "n"
                && GroupAiRules.BasicTargetPick(null, null, false, null) == null,
            "bandits: after a skip from a fallen unit the basic attack goes where the skip turned it (focus first)");
    }

    static void DummyAndVinesPicks()
    {
        // (12,313 AlertDTO exceptions): the game's own pick of the dummy is replaced by
        // another player unit, or dropped after a wipe (the bandit holds); a pick that is not excluded stays.
        Check(GroupAiRules.ExcludedTargetAction(false, false) == "keep" && GroupAiRules.ExcludedTargetAction(false, true) == "keep"
                && GroupAiRules.ExcludedTargetAction(true, true) == "replace" && GroupAiRules.ExcludedTargetAction(true, false) == "drop"
                && GroupAiRules.ExcludedRetargetLine("b", null, "decide", "drop", 7) == "CORE_AI_BANDIT_RETARGET unit=b to=- rule=skip-excluded source=decide action=drop tick=7"
                && GroupAiRules.ExcludedRetargetLine("b", "p", "tick", "replace", 8) == "CORE_AI_BANDIT_RETARGET unit=b to=p rule=skip-excluded source=tick action=replace tick=8",
            "bandits: the dummy as the game's pick goes to another player, or the bandit holds after a wipe");
        // The bandits' vines rooted the caster and her comrades; the game's IsHostile counts every faction
        // for an Enemy caster. Core's answer: only player and ally units; a line through a comrade is not taken.
        Check(GroupAiRules.CoreVinesHits(1) && GroupAiRules.CoreVinesHits(2) && !GroupAiRules.CoreVinesHits(3) && !GroupAiRules.CoreVinesHits(0)
                && GroupAiRules.VinesLineHits(true, 2, true) == (false, 0) && GroupAiRules.VinesLineHits(true, 2, false) == (true, 2)
                && GroupAiRules.VinesPick(new[] { GroupAiRules.VinesLineHits(true, 1, true), GroupAiRules.VinesLineHits(false, 1, false) }) == 1
                && GroupAiRules.VinesFilterLine("u", 2, 5) == "CORE_AI_BANDIT_VINES_FILTER unit=u skipped=2 tick=5"
                && GroupAiRules.PierceFilterLine("u", 6) == "CORE_AI_BANDIT_PIERCE_FILTER unit=u tick=6",
            "bandits: the healer's vines spare herself and her comrades, and a line through a comrade is left out");
        // B, (ArgumentOutOfRange in the vines' segments): a segment whose cell left the caster's line ends quietly.
        Check(GroupAiRules.VinesSegmentGone(3, 3) && GroupAiRules.VinesSegmentGone(7, 2) && !GroupAiRules.VinesSegmentGone(2, 3)
                && GroupAiRules.VinesSegmentGone(-1, 3)
                && GroupAiRules.VinesShortLine("u", 4, 2, 450, 140, 447, 143, 9) == "CORE_AI_BANDIT_VINES_SHORT unit=u index=4 tiles=2 at=450,140 cell=447,143 tick=9",
            "bandits: a vines segment beyond the line the caster has now is left out, with its line");
        // No area skill on a fallen unit while another skill is possible.
        Check(GroupAiRules.AvoidsDownedArea(1, false, true) && !GroupAiRules.AvoidsDownedArea(0, false, true)
                && !GroupAiRules.AvoidsDownedArea(2, true, true) && !GroupAiRules.AvoidsDownedArea(1, false, false)
                && GroupAiRules.AreaDownedLine("u", "SKILL_SweepingCleave", 1, 9) == "CORE_AI_BANDIT_AREA_DOWNED unit=u skill=SweepingCleave downed=1 tick=9",
            "bandits: an area that hits a fallen unit is left out while fallen units are skipped, unless nothing else is possible");
        // The per-tick net turns to the focus first, as the decision does, else to the nearest.
        Check(GroupAiRules.SkipTargetPick("f", new[] { "n", "f" }) == "f" && GroupAiRules.SkipTargetPick("x", new[] { "n", "f" }) == "n"
                && GroupAiRules.SkipTargetPick(null, new[] { "n" }) == "n" && GroupAiRules.SkipTargetPick("f", new string[0]) == null,
            "bandits: between decisions a bandit turns from a fallen target to its capable focus, else to the nearest capable unit");
    }

    static void BasicAttackOnly()
    {
        // The switch bandits.basic-only is off by default, works only in a marked test campaign and leaves no skill but the basic attack.
        Check(GroupAiRules.BasicOnlyKey == "bandits.basic-only" && GroupAiRules.BasicOnlyStartLine == "CORE_AI_BANDIT_START basic=1"
                && GroupAiRules.BasicOnlyActive(true, true) && !GroupAiRules.BasicOnlyActive(true, false)
                && !GroupAiRules.BasicOnlyActive(false, true) && !GroupAiRules.BasicOnlyActive(false, false),
            "bandits: the basic attack only switch works with its setting in a test campaign, and does nothing in any other campaign");
        // Every skill of every kit (CoreUnitRules, the keys of BanditKitKeys) leaves the decision with the switch on; none without it; the basic attack stays.
        var asked = 0;
        var kept = new List<string>();
        var lost = new List<string>();
        foreach (var unit in CoreUnitRules.Units)
        {
            var keys = new List<string>(CoreUnitRules.SkillsFor(unit, true));
            keys.AddRange(BanditKitKeys.ForRole(unit.Role));
            foreach (var key in keys)
            {
                asked++;
                var skill = GroupAiRules.DropsSkill(true, key) ? "" : key;
                var line = GroupAiRules.BanditRuleLine("b", unit.Role, GroupAiRules.BasicOnlyRule, skill, "-", 1f, 1f, 0, 0, "-", false, "InCombat", "", null, null, null, "-", 1);
                if (!line.Contains(" skill=basic ")) kept.Add(unit.Role + ":" + key);
                if (GroupAiRules.DropsSkill(false, key)) lost.Add(unit.Role + ":" + key);
            }
        }
        Check(asked >= 20 && kept.Count == 0 && lost.Count == 0 && !GroupAiRules.DropsSkill(true, "") && !GroupAiRules.DropsSkill(true, null)
                && !GroupAiRules.DropsSkill(false, "") && GroupAiRules.BasicOnlyRule == "bandit-basic-only",
            "bandits: with the basic attack only switch no rule line names a skill of any kit, without it every skill stays (kept: "
                + string.Join(",", kept) + "; lost: " + string.Join(",", lost) + ")");
    }

    static void GlobalCooldownWarning()
    {
        // (D-2): a bandit's decision with gcd=1 writes one warning line per campaign start; gcd=0 writes none and decides nothing.
        var written = false;
        var lines = 0;
        foreach (var gcd in new[] { false, false, true, true, false, true })
            if (GroupAiRules.GcdSeenDue(gcd, written))
            {
                written = true;
                lines++;
            }
        var noneWritten = false;
        var noneLines = 0;
        foreach (var gcd in new[] { false, false, false })
            if (GroupAiRules.GcdSeenDue(gcd, noneWritten))
            {
                noneWritten = true;
                noneLines++;
            }
        Check(lines == 1 && noneLines == 0 && GroupAiRules.GcdSeenDue(true, false) && !GroupAiRules.GcdSeenDue(true, true)
                && !GroupAiRules.GcdSeenDue(false, false) && GroupAiRules.GcdSeenLine == "CORE_AI_BANDIT_GCD_SEEN note=gcd-branch-reached",
            "bandits: a global cooldown on an enemy is warned about once per campaign start, no warning without one");
    }

    static void ExcludedDummy()
    {
        // The Toolkit's dummy counts nowhere for the group AI until it is let in again.
        GroupAiExcluded.Clear();
        try
        {
            Check(!GroupAiExcluded.Has("a") && GroupAiExcluded.Set("AbC", true) && GroupAiExcluded.Has("abc") && GroupAiExcluded.Count == 1
                    && !GroupAiExcluded.Set(null, true) && !GroupAiExcluded.Set("", true) && !GroupAiExcluded.Has(null) && !GroupAiExcluded.Has("abd")
                    && GroupAiExcluded.Set("abc", false) && !GroupAiExcluded.Has("AbC") && GroupAiExcluded.Count == 0,
                "group ai: a unit excluded for tests counts nowhere until it is let in again");
        }
        finally
        {
            GroupAiExcluded.Clear();
        }
    }
}
