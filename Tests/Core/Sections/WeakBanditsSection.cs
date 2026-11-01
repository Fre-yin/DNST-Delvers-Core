using System.Text.RegularExpressions;
using DungeonSettlersDelvers.Core;
using static TestKit;

// The weak forest bandits (WeakBanditRules): their table by level and class, beside the boss party.
internal static class WeakBanditsSection
{
    // Signature skills of the boss party that no weak bandit carries.
    static readonly string[] Signature = { BanditChainRules.SkillKey, BanditSerratedRules.SkillKey, BanditFearRules.SkillKey, LifebloomRules.SkillKey, "SKILL_GuardiansTaunt", "SKILL_DeadlyFear" };

    internal static void Run()
    {
        KeysAndLookups();
        SkillsByLevel();
        KitByLevel();
        Numbers();
        SpawnRules();
        NewClasses();
        Passives();
        Faction();
        PassivesAndEffects();
        Coin();
    }

    // The passives the table gives (2026-10-31_P4-Passive-je-Ebene): row 0 of the side trees only, one per level.
    static readonly string[] SideRowZero =
    {
        "SKILL_TwoHandGrip", "SKILL_SwordmansResolve", "SKILL_ShieldHandling", "SKILL_Bastion", "SKILL_GuardBarrier", "SKILL_Fortified",
        "SKILL_BarrierResurge", "SKILL_Ignition", "SKILL_LastCinder", "SKILL_FastHand", "SKILL_BulkShot", "SKILL_SharpShooter",
        "SKILL_InevitableBlow", "SKILL_CrimsonRecoil", "SKILL_ColdFury", "SKILL_SavageBarrier", "SKILL_Maelstrom", "SKILL_ExtraBubble",
        "SKILL_EbbAndFlow", "SKILL_InnerGrove", GroupAiHealerSeedRules.LifeBurstSkill, "SKILL_HealingTouch", "SKILL_RecklessAssault", "SKILL_Initiative"
    };

    static void Passives()
    {
        Check(WeakBanditRules.Classes.All(c => c.Passives.Length == WeakBanditRules.Levels && c.Passives.Distinct().Count() == c.Passives.Length
                    && c.Passives.All(p => Array.IndexOf(SideRowZero, p) >= 0)),
            "weak passives: three per class, none twice, all from row 0 of the side trees (5 points, no level)");
        Check(WeakBanditRules.Classes.All(c => Enumerable.Range(1, WeakBanditRules.Levels).All(level =>
                    WeakBanditRules.PassivesOf(c, level).SequenceEqual(c.Passives.Take(level))
                    && WeakBanditRules.Find(level, c.Suffix).Skills.SequenceEqual(WeakBanditRules.ActivesOf(c, level).Concat(c.Passives.Take(level).Where(BanditPassiveRules.Registrable))))),
            "weak passives: one on level 1, two on level 2, three on level 3 (6 side points per level), the row carries actives and passives");
        Check(WeakBanditRules.Classes.First(c => c.Suffix == "Healer").Passives[0] == GroupAiHealerSeedRules.LifeBurstSkill
                && WeakBanditRules.Classes.First(c => c.Suffix == "Guard").Passives.SequenceEqual(new[] { "SKILL_Fortified", "SKILL_GuardBarrier", "SKILL_Bastion" })
                && WeakBanditRules.Classes.First(c => c.Suffix == "Sword").Passives.SequenceEqual(new[] { "SKILL_SwordmansResolve", "SKILL_Fortified", "SKILL_GuardBarrier" })
                && WeakBanditRules.Classes.All(c => c.Passives.All(BanditPassiveRules.Registrable)),
            "weak passives: the herbalist's Life Burst first; the guard and the swordsman without Shield Handling, which needs the equipment a bandit lacks; every table passive registrable");
    }

    // Every weak group leaves one of the game's imperial coins, on its last bandit's cell.
    static void Coin()
    {
        Check(WeakBanditRules.CoinKey == "ITEM_ImperialCoin" && WeakBanditRules.CoinCount == 3 && WeakBanditRules.DropsCoin(true, 0) && !WeakBanditRules.DropsCoin(true, 1)
                && !WeakBanditRules.DropsCoin(false, 0) && GroupAiRules.IsGroupAlly(12) && !GroupAiRules.IsGroupAlly(13),
            "weak coin: the last weak bandit of a group (no living weak bandit within the group's 12 cells) leaves one stack of three imperial coins");
    }

    // Core's side "bandits" (CoreFactionRules): a bandit key or a summon of a bandit; the plant correction asks only the tag for the
    // side of whom it heals or names.
    static void Faction()
    {
        Check(CoreFactionRules.IsBandit(true, false) && CoreFactionRules.IsBandit(false, true) && CoreFactionRules.IsBandit(true, true)
                && !CoreFactionRules.IsBandit(false, false)
                && CoreFactionRules.SideText(true) == "bandit" && CoreFactionRules.SideText(false) == "player",
            "bandit tag: a bandit key or a summon whose owner is a bandit; a player figure, a player's summon and a game building without one are not");
        Check(CoreFactionRules.BloomStep(0, 0, true, 2) == "found" && CoreFactionRules.BloomStep(0, 0, false, -1) == "none"
                && CoreFactionRules.BloomStep(0, 3, false, -1) == "keep" && CoreFactionRules.BloomStep(0, 0, true, 1) == "keep"
                && CoreFactionRules.BloomStep(2, 0, true, 3) == "keep" && CoreFactionRules.BloomStep(4, 0, false, -1) == "keep"
                && CoreFactionRules.BloomLine("b", "found", "p", 7) == "CORE_BANDIT_BLOOM_TARGET bloom=b step=found target=p tick=7"
                && CoreFactionRules.BloomLine("b", "found", null, 7) == "CORE_BANDIT_BLOOM_TARGET bloom=b step=found target=- tick=7",
            "bandit bloom: only its first step is corrected, a target the game found or none, never the cooldown, its end or the shot");
        var seed = SourceText("GroupAiHealerSeed.cs");
        Check(seed != null && !Regex.IsMatch(seed, @"IsCoreUnit\((unit|target)\.Key\)") && Regex.Matches(seed, @"CoreFaction\.IsBandit\(").Count >= 5,
            "bandit tag: the seed's heal, the Life Burst and the sprayer line ask CoreFaction for the side of whom they heal or name");
    }

    // A bandit's listed passives the game's table lacks are registered with its own Register (BanditPassiveRules); the bandits'
    // effect log names the side of each target (BanditEffectRules).
    static void PassivesAndEffects()
    {
        var held = new HashSet<string> { "SKILL_LifeBurst" };
        var missing = BanditPassiveRules.ToRegister(new[] { "SKILL_LifeBurst", "SKILL_GuardBarrier", "", "SKILL_GuardBarrier", "SKILL_Fortified" }, held.Contains);
        Check(missing.SequenceEqual(new[] { "SKILL_GuardBarrier", "SKILL_Fortified" }) && BanditPassiveRules.ToRegister(null, held.Contains).Count == 0
                && BanditPassiveRules.Line("u1", "UNIT_X", 3, missing, new[] { "SKILL_GuardBarrier" })
                    == "CORE_BANDIT_PASSIVES unit=u1 key=UNIT_X listed=3 missing=GuardBarrier+Fortified registered=GuardBarrier",
            "bandit passives: every listed passive the game's table lacks is registered once, in order; the line names what Register left");
        Check(BanditPassiveRules.ToRegister(new[] { "SKILL_HealingTouch", "SKILL_InnerGrove" }, null).SequenceEqual(new[] { "SKILL_InnerGrove" })
                && !BanditPassiveRules.Registrable("SKILL_HealingTouch") && BanditPassiveRules.Registrable("SKILL_LifeBurst")
                && new[] { "SKILL_ShieldHandling", "SKILL_TwoHandGrip", "SKILL_FireBattleMage", "SKILL_NatureSpirit" }.All(k => !BanditPassiveRules.Registrable(k))
                && BanditPassiveRules.Registrable("SKILL_Fortified") && BanditPassiveRules.Registrable("SKILL_SwordmansResolve"),
            "bandit passives: the passives that touch the equipment a bandit lacks (Healing Touch, Shield Handling, Two Hand Grip, Fire Battle Mage, Nature Spirit) are never registered for one");
        Check(BanditEffectRules.SideOf(true, false) == "party" && BanditEffectRules.SideOf(false, true) == "bandit" && BanditEffectRules.SideOf(false, false) == "other"
                && BanditEffectRules.WrongSide("damage", "bandit") && BanditEffectRules.WrongSide("heal", "party")
                && !BanditEffectRules.WrongSide("damage", "party") && !BanditEffectRules.WrongSide("heal", "bandit")
                && BanditEffectRules.Line("u", "UNIT_A", "t", "UNIT_B", "bandit", "damage", 12.34f)
                    == "CORE_BANDIT_EFFECT user=u userKey=UNIT_A target=t targetKey=UNIT_B side=bandit kind=damage amount=12.3 wrong=1 dead=0"
                && BanditEffectRules.Line("u", "UNIT_A", "t", "UNIT_B", "bandit", "damage", 12.34f, true).EndsWith("wrong=0 dead=1", StringComparison.Ordinal),
            "bandit effects: damage on a bandit or a heal on the party is the wrong side; the line names user, target, side, kind and amount");
    }

    static string SourceText(string file)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "Core", file);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        return null;
    }

    static void NewClasses()
    {
        var suffixes = WeakBanditRules.Classes.Select(c => c.Suffix).ToArray();
        Check(suffixes.SequenceEqual(new[] { "Guard", "Blade", "Archer", "Healer", "Spear", "WaterMage", "Sword", "FireMage" })
                && suffixes.All(a => suffixes.All(b => a == b || !a.EndsWith(b, StringComparison.Ordinal))),
            "weak classes: the eight classes, no key end is the end of another (KitOf reads the end)");
        var missing = new List<string>();
        foreach (var c in WeakBanditRules.Classes)
        {
            var kit = WeakBanditRules.Find(2, c.Suffix).Skills;
            foreach (var key in WeakBanditKitKeys.ForRole(c.Role))
                if (Array.IndexOf(kit, key) < 0) missing.Add(c.Role + ":" + key);
        }
        Check(missing.Count == 0, "weak classes: every key a class's rule looks up is in its level 2 kit (missing: " + string.Join(",", missing) + ")");
        var waterMage = WeakBanditRules.Classes.First(c => c.Suffix == "WaterMage");
        Check(WeakBanditRules.ActivesOf(waterMage, 1).SequenceEqual(new[] { WeakBanditKitKeys.MagicBasic, WeakBanditKitKeys.SummonBubbling, WeakBanditKitKeys.TidalMark })
                && WeakBanditRules.ActivesOf(waterMage, 2).SequenceEqual(new[] { WeakBanditKitKeys.MagicBasic, WeakBanditKitKeys.SummonBubbling, WeakBanditKitKeys.TidalMark, WeakBanditKitKeys.EmbracingWave })
                && WeakBanditRules.ActivesOf(waterMage, 3).Last() == WeakBanditKitKeys.SummonBubbleToad
                && WeakBanditRules.Definitions.Where(d => d.Skills.Contains(WeakBanditKitKeys.SummonBubbleToad)).Select(d => d.Key).SequenceEqual(new[] { WeakBanditRules.KeyOf(3, waterMage) })
                && WeakBanditRules.Definitions.Count(d => d.Skills.Contains(WeakBanditKitKeys.SummonBubbling)) == WeakBanditRules.Levels
                && WeakBanditRules.Classes.Where(c => c != waterMage).All(c => c.TreeSkills.Length == 2),
            "weak classes: the water mage's staff summons a bubbling on every level, the bubble toad joins on level 3 only, the other classes keep two tree skills");
        Check(WeakBanditRules.ActivesOf(waterMage, 1).SequenceEqual(new[] { WeakBanditKitKeys.MagicBasic, WeakBanditKitKeys.SummonBubbling, WeakBanditKitKeys.TidalMark })
                && WeakBanditRules.Find(2, "WaterMage").Skills.Contains(WeakBanditKitKeys.EmbracingWave)
                && WeakBanditRules.Find(1, "Sword").Kit.All(k => k.Part != CoreUnitRules.Secondary)
                && WeakBanditRules.Find(2, "Sword").Kit.Contains((CoreUnitRules.Secondary, "ITEM_WoodShield"))
                && WeakBanditRules.Find(1, "Sword").Kit.Contains((CoreUnitRules.Primary, "ITEM_WoodSword"))
                && WeakBanditRules.Find(2, "Sword").Kit.Contains((CoreUnitRules.Primary, "ITEM_WoodSword"))
                && WeakBanditRules.Find(3, "Sword").Kit.Contains((CoreUnitRules.Primary, "ITEM_BoneSword"))
                && WeakBanditRules.Find(3, "Sword").Kit.Contains((CoreUnitRules.Secondary, "ITEM_WoodShield")),
            "weak classes: the water mage with the tidal mark and from level 2 the wave; the swordsman without a shield on level 1, with the wood shield from level 2, the wood sword up to level 2 and the bone sword on level 3");
        var taken = new HashSet<(int, int)> { (6, 5), (5, 5) };
        var cells = WeakBanditMageRules.BubblingCells((5, 5), (9, 5), taken);
        Check(WeakBanditMageRules.WantsBubbling(0) && !WeakBanditMageRules.WantsBubbling(1) && !WeakBanditMageRules.WantsBubbling(2)
                && cells.Count == 7 && !cells.Contains((6, 5)) && cells[0] == (6, 6) && cells[1] == (6, 4) && cells[cells.Count - 1] == (4, 4)
                && cells.All(c => Math.Max(Math.Abs(c.X - 5), Math.Abs(c.Y - 5)) == 1)
                && WeakBanditMageRules.BubblingCells((0, 0), (3, 0), null)[0] == (1, 0),
            "weak classes: the water mage holds one bubbling, cast on a free cell beside it, the one nearest its closest foe first");
        Check(WeakBanditMageRules.WantsToad(0, 1) && WeakBanditMageRules.WantsToad(0, 2) && !WeakBanditMageRules.WantsToad(0, 0)
                && !WeakBanditMageRules.WantsToad(1, 1) && WeakBanditMageRules.WantsBubbling(0) && !WeakBanditMageRules.WantsToad(1, 0),
            "weak classes: on level 3 the water mage summons its toad while a bubbling stands and no toad does, then holds a bubbling beside it");
        Check(WeakBanditMageRules.ToadFirst(true, 0.55f) && WeakBanditMageRules.ToadFirst(true, 0.3f) && !WeakBanditMageRules.ToadFirst(true, 0.29f)
                && !WeakBanditMageRules.ToadFirst(false, 1f) && WeakBanditMageRules.CriticalShare == 0.3f,
            "weak classes: the toad goes before the wave unless a bandit of the group is below 30 %");
        var mage = WeakBanditRules.Find(1, "FireMage");
        Check(CoreUnitRules.MagicAttackOf(mage, 6.4f, null) == CoreUnitRules.HealerMagicDefault && CoreUnitRules.MagicAttackOf(WeakBanditRules.Find(1, "Spear"), 6.4f, null) == 6.4f,
            "weak classes: the mages cast from the herbalist's row value, the fighters keep the template's");
    }

    static void SpawnRules()
    {
        Check(WeakBanditSpawnRules.LevelOf(0, 0) == 1 && WeakBanditSpawnRules.LevelOf(0, 2) == 3 && WeakBanditSpawnRules.LevelOf(0, 3) == 0
                && WeakBanditSpawnRules.LevelOf(1, 0) == 0 && WeakBanditSpawnRules.LevelOf(0, -1) == 0
                && WeakBanditSpawnRules.SizeOf(1) == 2 && WeakBanditSpawnRules.SizeOf(2) == 3 && WeakBanditSpawnRules.SizeOf(3) == 3,
            "weak spawn: forest levels 1 to 3 only (floor index 0 to 2), groups of two on level 1 and three on levels 2 and 3");
        Check(WeakBanditSpawnRules.Replaceable(2) && !WeakBanditSpawnRules.Replaceable(3) && !WeakBanditSpawnRules.Replaceable(4)
                && !WeakBanditSpawnRules.Replaceable(0) && !WeakBanditSpawnRules.Replaceable(96) && !WeakBanditSpawnRules.Replaceable(98),
            "weak spawn: only a plain combat room (RoomType 2) takes a weak group, HardCombat, Boss, Elite and Patrol keep the game's roll");
        Check(WeakBanditSpawnRules.Percent(null) == 10.0 && WeakBanditSpawnRules.Percent(150) == 100.0 && WeakBanditSpawnRules.Percent(-5) == 0.0
                && WeakBanditSpawnRules.Percent(double.NaN) == 10.0,
            "weak spawn: 10 % by default, the setting clamped to 0..100");
        var hits = 0;
        for (var i = 0; i < 10000; i++) if (WeakBanditSpawnRules.Hit(1234, 0, 1, "ROOM_" + i, 10.0)) hits++;
        Check(hits >= 900 && hits <= 1100 && !Enumerable.Range(0, 500).Any(i => WeakBanditSpawnRules.Hit(7, 0, 0, "R" + i, 0.0))
                && Enumerable.Range(0, 500).All(i => WeakBanditSpawnRules.Hit(7, 0, 0, "R" + i, 100.0))
                && WeakBanditSpawnRules.Hit(99, 0, 2, "Combat_Middle_3", 37.0) == WeakBanditSpawnRules.Hit(99, 0, 2, "Combat_Middle_3", 37.0),
            "weak spawn: about 10 % over 10 000 rooms (" + hits + "), none at 0 %, all at 100 %, the same answer for the same room");
        var classes = WeakBanditSpawnRules.ClassesOf();
        var counts = classes.ToDictionary(c => c, _ => 0);
        var allDistinct = true;
        for (var i = 0; i < 4000; i++)
        {
            var group = WeakBanditSpawnRules.GroupOf(55, 0, 1, "ROOM_" + i, 2, classes);
            if (group.Count != 3 || group.Distinct().Count() != 3) allDistinct = false;
            foreach (var c in group) counts[c]++;
        }
        var expected = 4000 * 3.0 / classes.Count;
        Check(allDistinct && counts.Values.All(n => Math.Abs(n - expected) < expected * 0.1)
                && WeakBanditSpawnRules.GroupOf(55, 0, 0, "X", 1, classes).Count == 2
                && WeakBanditSpawnRules.GroupOf(55, 0, 1, "X", 2, classes).SequenceEqual(WeakBanditSpawnRules.GroupOf(55, 0, 1, "X", 2, classes)),
            "weak spawn: distinct classes, each about equally often, the same group for the same room");
        var keys = WeakBanditSpawnRules.KeysOf(2, new[] { "Guard", "Healer", "Nobody" });
        Check(keys.SequenceEqual(new[] { CoreUnitRules.Prefix + "ForestBanditWeak2Guard", CoreUnitRules.Prefix + "ForestBanditWeak2Healer" })
                && WeakBanditSpawnRules.RoomLine(1, 0, 1, "R", keys).StartsWith("CORE_WEAKBANDIT_ROOM seed=1 region=0 floor=1 room=R units=")
                && WeakBanditSpawnRules.FloorLine(1, 0, 1, 7, 1, 10).EndsWith("rooms=7 replaced=1 percent=10"),
            "weak spawn: the keys of a group on its level, unknown classes left out; the log lines");
    }

    static void KeysAndLookups()
    {
        var weak = WeakBanditRules.Definitions;
        Check(weak.Length == WeakBanditRules.Levels * WeakBanditRules.Classes.Length
                && weak.Select(d => d.Key).Distinct().Count() == weak.Length
                && weak.All(d => CoreUnitRules.IsCoreUnit(d.Key) && d.Key.Contains(WeakBanditRules.KeyPart))
                && weak.All(d => CoreUnitRules.TryGet(d.Key, out var found) && ReferenceEquals(found, d))
                && WeakBanditRules.Classes.All(c => weak.Count(d => d.Key.EndsWith(c.Suffix, StringComparison.Ordinal)) == WeakBanditRules.Levels),
            "weak bandits: one key per level and class, each a Core unit that TryGet finds, the class as the key's end");
        Check(CoreUnitRules.Units.Length == 4 && CoreUnitRules.Units.All(u => u.Level == 0 && !WeakBanditRules.IsWeak(u))
                && CoreUnitRules.All.Length == CoreUnitRules.Units.Length + weak.Length
                && CoreUnitRules.All.Take(4).SequenceEqual(CoreUnitRules.Units)
                && CoreUnitRules.Units.All(u => CoreUnitRules.TryGet(u.Key, out var found) && ReferenceEquals(found, u)),
            "weak bandits: the boss party stays Units, All holds it first and then the weak ones");
        Check(weak.All(d => CoreUnitRules.TraitsFor(d).Length == 0 && !CoreUnitRules.IsBossParty(d))
                && WeakBanditRules.Find(1, "Guard")?.Key == CoreUnitRules.Prefix + "ForestBanditWeak1Guard" && WeakBanditRules.Find(4, "Guard") == null,
            "weak bandits: no elite or boss trait, outside the boss party; Find by level and class");
        Check(CoreUnitRules.All.All(d => CoreUnitRules.RowValues(CoreUnitRules.TraitsFor(d)).Length > 0 && CoreUnitRules.RowValues(CoreUnitRules.SkillsFor(d, true)).Length > 0)
                && weak.All(d => CoreUnitRules.RowValues(CoreUnitRules.TraitsFor(d)).SequenceEqual(new[] { "NULL" }))
                && CoreUnitRules.RowValues(Array.Empty<string>()).SequenceEqual(new[] { "NULL" }) && CoreUnitRules.RowValues(null).SequenceEqual(new[] { "NULL" })
                && CoreUnitRules.Units.All(u => CoreUnitRules.RowValues(CoreUnitRules.TraitsFor(u)).SequenceEqual(CoreUnitRules.TraitsFor(u))),
            "weak bandits: a unit row's string lists are never empty, no traits is the game's NULL (the spawn reads DefaultTraits[0])");
    }

    static void SkillsByLevel()
    {
        foreach (var c in WeakBanditRules.Classes)
        {
            var one = WeakBanditRules.ActivesOf(c, 1);
            var two = WeakBanditRules.ActivesOf(c, 2);
            var three = WeakBanditRules.ActivesOf(c, 3);
            var lead = string.IsNullOrEmpty(c.WeaponSkill) ? new[] { c.Basic } : new[] { c.Basic, c.WeaponSkill };
            Check(one.SequenceEqual(lead.Append(c.TreeSkills[0]))
                    && two.SequenceEqual(lead.Append(c.TreeSkills[0]).Append(c.TreeSkills[1]))
                    && three.SequenceEqual(c.TreeSkills.Length > 2 ? two.Append(c.TreeSkills[2]) : two),
                "weak bandits " + c.Suffix + ": basic attack, weapon skill and the first tree skill on level 1, the second from level 2 on, level 3 like level 2 (with a third tree skill only where the table gives one)");
        }
        Check(WeakBanditRules.Definitions.All(d => !d.Skills.Intersect(Signature).Any() && !d.Lifebloom),
            "weak bandits: no signature skill of the boss party and no Lifebloom");
        var healer = WeakBanditRules.Find(1, "Healer");
        Check(healer.Role == "healer" && healer.Skills.Contains(GroupAiHealerSeedRules.SeedSkill) && healer.Skills.Contains(GroupAiHealerSeedRules.SprayerSkill)
                && CoreUnitRules.HumanFormsFor(healer).SequenceEqual(CoreUnitRules.HealerHumanForms),
            "weak bandits: the herbalist has her seed and its sprayer from level 1 and takes the female forms");
    }

    static void KitByLevel()
    {
        Check(WeakBanditRules.Definitions.All(d => d.Kit.All(k => !BossItemRules.IsBossItem(k.Item)) && CoreUnitRules.SpecialCount(d) == 0),
            "weak bandits: no boss item in any kit");
        Check(WeakBanditRules.Classes.All(c => c.KitByLevel.Length == WeakBanditRules.Levels)
                && WeakBanditRules.Definitions.Where(d => d.Level == 1).All(d => d.Kit.All(k => !k.Item.Contains("Bone")))
                && WeakBanditRules.Definitions.Where(d => d.Level == 3).All(d => d.Kit.Contains((CoreUnitRules.Body, "ITEM_BoneArmor")) && d.Kit.Contains((CoreUnitRules.Head, "ITEM_BoneHelmet")))
                && WeakBanditRules.Definitions.All(d => d.Kit.All(k => Array.IndexOf(CoreUnitRules.EquipSlots, k.Part) >= 0) && d.Kit.Select(k => k.Part).Distinct().Count() == d.Kit.Length),
            "weak bandits: no bone on level 1, bone armour and helmet on level 3, every part a slot of the game and none twice");
    }

    // The measured Normal values are Apocalypse's (Downfall's) standard, the steps below follow the game's.
    static void BanditDifficultyRows()
    {
        var rows = BanditDifficultyRules.Table;
        Check(rows.Select(r => r.Level).SequenceEqual(new[] { 0, 1, 2, 3, 4 }) && rows.Select(r => r.Key).Distinct().Count() == rows.Length
                && rows.All(r => r.Key.StartsWith(CoreSaveEffects.Prefix, StringComparison.Ordinal) && BanditDifficultyRules.IsKey(r.Key)),
            "bandit difficulty: one Core row per combat difficulty, Easy to Downfall, each with its own key that a save never keeps");
        Check(BanditDifficultyRules.StatsOf(BanditDifficultyRules.RowOf(4)).SequenceEqual(new[]
                {
                    "PhysicalAttackPowerPerModifier_-60", "MagicAttackPowerPerModifier_-60", "CriticalChance_10", "CriticalDamageBonus_50", "DodgeChance_10"
                }),
            "bandit difficulty: Downfall keeps the standard's attack and magic -60 and gets the party's Downfall values, crit +10, crit damage +50, dodge +10");
        Check(BanditDifficultyRules.RowOf(5) == BanditDifficultyRules.RowOf(4) && BanditDifficultyRules.RowOf(-1) == null,
            "bandit difficulty: above Downfall Downfall's row, an unknown level none");
        var allowed = new[] { BanditDifficultyRules.AttackStat, BanditDifficultyRules.MagicStat, BanditDifficultyRules.HealthStat };
        var party = new[] { BanditDifficultyRules.CritStat, BanditDifficultyRules.CritDamageStat, BanditDifficultyRules.DodgeStat };
        Check(rows.All(r => BanditDifficultyRules.StatsOf(r).All(s => allowed.Contains(s[..s.LastIndexOf('_')])
                    || (r.Level == BanditDifficultyRules.Downfall && party.Contains(s[..s.LastIndexOf('_')])))),
            "bandit difficulty: no row has speed or move speed; only Downfall's has crit, crit damage and dodge, the party's own Downfall values");
        Check(rows.Where(r => r.Level < BanditDifficultyRules.Downfall).All(r => r.Extra.Length == 0)
                && BanditDifficultyRules.RowOf(4).Attack == -60f && BanditDifficultyRules.RowOf(4).Health == 0f,
            "bandit difficulty: the steps below Downfall keep only attack, magic and health");
        // The game's attack factor (Easy 0.15, Normal 0.4, Hard 1, VeryHard 1.25, Downfall 1.5) over Downfall's, times the standard.
        var game = new[] { 0.15, 0.4, 1.0, 1.25, 1.5 };
        var health = new[] { 1.0, 1.0, 1.0, 1.25, 1.5 };
        Check(rows.All(r => Math.Abs((1 + r.Attack / 100.0) - 0.4 * game[r.Level] / 1.5) < 0.006)
                && rows.All(r => Math.Abs((1 + r.Health / 100.0) - health[r.Level] / 1.5) < 0.006),
            "bandit difficulty: attack 0.4 times the game's step over Downfall's, health the game's step over Downfall's, rounded");
        Check(rows.Zip(rows.Skip(1), (a, b) => a.Attack < b.Attack && a.Health <= b.Health).All(x => x),
            "bandit difficulty: each step weaker than the one above it");
        Check(BanditDifficultyRules.GameKeyOf(2) == null && BanditDifficultyRules.GameKeyOf(4) == "AFFECTER_MonsterDifficultyDownfall"
                && BanditDifficultyRules.GameKeyOf(1) == BanditDifficultyRules.Template,
            "bandit difficulty: nothing of the game's to take away on Hard, Downfall's row on Downfall, the rows made from the game's Normal row");
        Check(BanditDifficultyRules.SwapLine("UNIT_X", "spawn", 4, "AFFECTER_MonsterDifficultyDownfall", BanditDifficultyRules.RowOf(4).Key, "Dungeon")
                == "CORE_BANDIT_DIFFICULTY unit=UNIT_X reason=spawn level=4 removed=AFFECTER_MonsterDifficultyDownfall given=AFFECTER_DelversCoreBanditDifficultyDownfall map=Dungeon",
            "bandit difficulty: the swap line names the unit, the level, what went and what came");
        // A camp boss stood at 6120 of 4100 after the swap to a row with less health.
        Check(BanditDifficultyRules.AfterTouch(6120f, 4100f) == 4100f && BanditDifficultyRules.AfterTouch(9180f, 6120f) == 6120f
                && Math.Abs(BanditDifficultyRules.AfterTouch(4100f, 4100f) - 4100f) < 0.02f && BanditDifficultyRules.HealthTouch < 0f
                && BanditDifficultyRules.HealthTouch > -0.1f,
            "bandit difficulty: behind the swap the game clamps a health above the new maximum down to it, a full unit stays full");
        // Lowering left a boss above its maximum, raising left a full one at 4113 of 6120.
        Check(Math.Abs(BanditDifficultyRules.HealthAfter(3060f, 3060f, 0f, 0f, -33f) - 2050.2f) < 0.1f
                && Math.Abs(BanditDifficultyRules.HealthAfter(4100f, 4100f, -33f, -33f, 0f) - 6119.4f) < 0.5f
                && Math.Abs(BanditDifficultyRules.HealthAfter(1530f, 3060f, 0f, 0f, -33f) - 1025.1f) < 0.1f
                && Math.Abs(BanditDifficultyRules.HealthAfter(2050f, 4100f, -33f, -33f, 0f) - 3059.7f) < 0.5f
                && BanditDifficultyRules.HealthAfter(1224f, 1224f, 0f, 0f, 0f) == 1224f && BanditDifficultyRules.HealthAfter(700f, 0f, 0f, 0f, -33f) == 700f,
            "bandit difficulty: a swap keeps the share of head and body, lowering and raising (full stays full, half stays half)");
        Check(BanditDifficultyRules.HealthLine("UNIT_X", "test", 4100f, 4100f, 6120f, 4100f, 4100f, 6120f)
                == "CORE_BANDIT_DIFFICULTY_HEALTH unit=UNIT_X reason=test head=4100/4100->6120 body=4100/4100->6120",
            "bandit difficulty: the health line names head and body with their maximum before the queue runs and the health Core sends them to");
    }

    static void Numbers()
    {
        Check(WeakBanditRules.Definitions.All(d => d.HealthFactor == (d.Level == 2 ? 0.3f : 0.4f) && d.MagicFactor == 0.4f && d.TauntFactor == 0.75 && d.TauntBreakDrop == 0.35)
                && WeakBanditRules.Definitions.All(d => d.ArmorBonus == new[] { 20.0, 25.0, 30.0 }[d.Level - 1])
                && WeakBanditRules.Definitions.All(d => d.DamageFactor == 0.4f) && WeakBanditRules.Find(1, "Guard").DamageFactor == 0.4f
                && CoreUnitRules.PhysicalAttackOf(WeakBanditRules.Find(3, "Guard"), 102f) == 102f * 0.4f
                && CoreUnitRules.PhysicalAttackOf(WeakBanditRules.Find(1, "Blade"), 10f) == 4f
                && CoreUnitRules.Units.All(u => u.HealthFactor == 1f && u.MagicFactor == 1f && u.ArmorBonus == null && u.TauntFactor == null),
            "weak bandits: damage and magic 40 % of the boss party, the weak guard without the boss guard's nerf, health 40/30/40 % by level, armour 20/25/30 by level, taunt 0.75 with breaking free at 35 %; the boss party unchanged");
        BanditDifficultyRows();
        Check(GroupAiRules.IsGroupAlly(0) && GroupAiRules.IsGroupAlly(GroupAiRules.GroupWakeCells) && !GroupAiRules.IsGroupAlly(GroupAiRules.GroupWakeCells + 1),
            "weak bandits: a bandit's allies are the bandits within the reach of its own group, a group further away is none");
        var rows = CoreTextRows.Registered.Select(row => row.Key).ToList();
        Check(WeakBanditRules.Definitions.All(d => rows.Contains(CoreUnitRules.NameKey(d.Key)) && rows.Contains(CoreUnitRules.DescKey(d.Key))),
            "weak bandits: every weak key has its name and description row");
        Check(WeakBanditRules.TextRows().Count() == 2 * WeakBanditRules.Definitions.Length
                && WeakBanditRules.Name.German == "Waldbandit" && WeakBanditRules.Name.English == "Forest Bandit"
                && WeakBanditRules.Description.German == "Ein Waldbandit mit typischer Ausrüstung in schlechtem Zustand."
                && WeakBanditRules.Description.English == "A forest bandit with typical gear in poor condition."
                && WeakBanditRules.Name.AllLanguages.All(s => !string.IsNullOrWhiteSpace(s) && s.IndexOf('-') < 0)
                && WeakBanditRules.Description.AllLanguages.All(s => !string.IsNullOrWhiteSpace(s) && s.IndexOf('-') < 0),
            "weak bandits: one plain name and description for every key, ten languages, no hyphen");
    }
}
