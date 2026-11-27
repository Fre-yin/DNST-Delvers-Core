namespace DungeonSettlersDelvers.Core;

// The weak forest bandits (30. and): on dungeon levels 1 to 3 of the forest, a weak group may stand in for
// a normal monster group, built like players of that level would build (2026-10-24 wiki builds: "players should meet what they
// would build themselves"). Plain data, no game types: one Definition per level and class, keys
// UNIT_DelversCoreForestBanditWeak<level><class>, the class as the key's end so the bandit AI finds its kit (KitOf).
// - Each class has the first skill of its tree on level 1 and the second from level 2 on, both from row 0 of the game's skill
//   tree (8 points, no level), plus the skill its weapon brings (ItemTable Equipment_ItemDefaultSkills). Row 1 needs level 4
//   and stays out. No signature skill of the boss party (chain, Serrated Blade, Deadly Fear, Lifebloom).
// - Health and damage are Factor (0.4) of the boss party's, on every level, health on level 2 HealthByLevel's 0.3 (Core's
//   proposal after the third bandit tipped level 2 on Normal, 30 % wins against 90 and 70; the measured
//   value stays); the armour bonus is ArmorBonus of the level
//   (20, 25, 30) instead of the boss party's 50; a player's taunt holds TauntFactor (0.75) of its time, and the band breaks
//   free when its healer loses TauntBreakDrop (35 %) within the window (the boss party: 0.5 and 20 %).
// - Kit by level: wood weapons and cloth on level 1, bone weapons and thick cloth (the guard wood armour) on level 2, bone armour
//   and helmet on level 3. No boss item.
internal static class WeakBanditRules
{
    internal const string KeyPart = "ForestBanditWeak";
    internal const int Levels = 3;
    internal const float Factor = 0.4f;
    internal static readonly double[] ArmorBonusByLevel = { 20.0, 25.0, 30.0 };
    internal static readonly float[] HealthByLevel = { Factor, 0.3f, Factor };
    internal const double TauntFactor = 0.75;
    internal const double TauntBreakDrop = 0.35;

    // One class of the table: the key's end, the role (forms, settings), the goblin row it is copied from, its basic attack and
    // weapon skill, its tree skills by level (index 0 = level 1) and its kit by level.
    internal sealed class Class
    {
        internal string Suffix;
        internal string Role;
        internal string Template;
        internal string Basic;
        internal string WeaponSkill;
        internal string[] TreeSkills;
        // Other choices of the tree skills beside TreeSkills, rolled per bandit on the floor's seed
        // (WeakBanditSpawnRules.VariantOf). Each has its own unit row, the key's class end unchanged, so a saved bandit keeps its kit.
        internal string[][] Variants = Array.Empty<string[]>();
        // Skills of row 1 from level 3 on, as the water mage's toad. Explosion for the fire mage stays out until
        // Toolkit has shown its warning.
        internal string[] Level3Skills = Array.Empty<string>();
        // A variant's own level 3 skills by its number, where it does not take Level3Skills.
        internal string[][] VariantLevel3Skills = Array.Empty<string[]>();
        // The class's own row is no longer rolled, only its variants: it stays for saved bandits and the switch off (the water mage).
        internal bool OnlyVariantsRolled;
        // The passives of row 0 of the side trees in the order the levels add them (like players by the wiki;
        // 5 side points each, 6 per level, so one on level 1, two on level 2, three on level 3). Dropped and taken back the same
        // evening ("bandits are Apocalypse content and a challenge in every form"); BanditPassives registers them in the game.
        internal string[] Passives;
        internal (string Part, string Item)[][] KitByLevel;
    }

    private const string Body = CoreUnitRules.Body;
    private const string Head = CoreUnitRules.Head;
    private const string Primary = CoreUnitRules.Primary;
    private const string Secondary = CoreUnitRules.Secondary;
    private const string Brawler = "UNIT_GoblinBrawler";
    private const string Worker = "UNIT_GoblinWorker";

    internal static readonly Class[] Classes =
    {
        new Class
        {
            // The weak guard is no boss, no guard nerf; its damage is Factor of the template's like every weak
            // bandit's, not the boss guard's CoreUnitRules.GuardDamageFactor on top.
            Suffix = "Guard", Role = "guard", Template = Brawler, Basic = "SKILL_BasicClubAttack", WeaponSkill = "SKILL_StunCharge",
            TreeSkills = new[] { "SKILL_ShieldUp", "SKILL_IronClad" }, Passives = new[] { "SKILL_Fortified", "SKILL_GuardBarrier", "SKILL_Bastion" },
            Variants = new[] { new[] { WeakBanditKitKeys.Hammering, "SKILL_IronClad" } },
            Level3Skills = new[] { WeakBanditKitKeys.VileInsult, WeakBanditKitKeys.GuardiansTaunt },
            KitByLevel = new[]
            {
                new[] { (Body, "ITEM_ThickClothArmor"), (Head, "ITEM_WoodHelmet"), (Primary, "ITEM_WoodClub"), (Secondary, "ITEM_WoodShield") },
                new[] { (Body, "ITEM_WoodArmor"), (Head, "ITEM_WoodHelmet"), (Primary, "ITEM_BoneClub"), (Secondary, "ITEM_BoneShield") },
                new[] { (Body, "ITEM_BoneArmor"), (Head, "ITEM_BoneHelmet"), (Primary, "ITEM_BoneClub"), (Secondary, "ITEM_BoneShield") }
            }
        },
        new Class
        {
            Suffix = "Blade", Role = "blade", Template = Brawler, Basic = "SKILL_BasicGreatswordAttack", WeaponSkill = "SKILL_WhirlwindSlash",
            TreeSkills = new[] { "SKILL_Slay", "SKILL_DestructiveSplit" }, Passives = new[] { "SKILL_ColdFury", "SKILL_InevitableBlow", "SKILL_Initiative" },
            KitByLevel = new[]
            {
                new[] { (Body, "ITEM_ClothArmor"), (Head, "ITEM_ClothCoif"), (Primary, "ITEM_WoodGreatSword") },
                new[] { (Body, "ITEM_ThickClothArmor"), (Head, "ITEM_ThickClothCoif"), (Primary, "ITEM_BoneGreatSword") },
                new[] { (Body, "ITEM_BoneArmor"), (Head, "ITEM_BoneHelmet"), (Primary, "ITEM_BoneGreatSword") }
            }
        },
        new Class
        {
            Suffix = "Archer", Role = "archer", Template = Worker, Basic = "SKILL_BasicBowAttack", WeaponSkill = "SKILL_RapidFire",
            TreeSkills = new[] { "SKILL_SnipingShot", "SKILL_AnkleBreak" }, Passives = new[] { "SKILL_FastHand", "SKILL_SharpShooter", "SKILL_BulkShot" },
            Variants = new[] { new[] { WeakBanditKitKeys.ArrowRain, "SKILL_AnkleBreak" } },
            KitByLevel = new[]
            {
                new[] { (Body, "ITEM_ClothArmor"), (Primary, "ITEM_WoodBow") },
                new[] { (Body, "ITEM_ThickClothArmor"), (Primary, "ITEM_BoneBow") },
                new[] { (Body, "ITEM_BoneArmor"), (Head, "ITEM_BoneHelmet"), (Primary, "ITEM_BoneBow") }
            }
        },
        new Class
        {
            // The seed is her staff's own skill and what both upgrades act on, so it comes with her (weapon and tree skill).
            // Her third passive is Ebb and Flow, not Healing Touch, whose melee heal does not suit a healer who
            // keeps her distance (and which reads the equipment a bandit lacks); after Ignition, which needs burning foes, it is the
            // next of the wiki's healers (48 %), and her seeds dying give her the cooldowns back.
            Suffix = "Healer", Role = "healer", Template = Worker, Basic = "SKILL_BasicMagicMissile", WeaponSkill = GroupAiHealerSeedRules.SeedSkill,
            TreeSkills = new[] { GroupAiHealerSeedRules.SprayerSkill, "SKILL_SolarBloom" }, Passives = new[] { GroupAiHealerSeedRules.LifeBurstSkill, "SKILL_InnerGrove", "SKILL_EbbAndFlow" },
            // The vines (more damage and hold), or the support with Healing Touch.
            Variants = new[] { new[] { GroupAiHealerSeedRules.SprayerSkill, WeakBanditKitKeys.GraspingVines }, new[] { WeakBanditKitKeys.HealingTouch, GroupAiHealerSeedRules.SprayerSkill } },
            KitByLevel = new[]
            {
                new[] { (Body, "ITEM_ClothArmor"), (Primary, "ITEM_NatureWoodStaff") },
                new[] { (Body, "ITEM_ThickClothArmor"), (Primary, "ITEM_NatureBoneStaff") },
                new[] { (Body, "ITEM_BoneArmor"), (Head, "ITEM_BoneHelmet"), (Primary, "ITEM_NatureBoneStaff") }
            }
        },
        // The new classes: spear, water mage, swordsman and fire mage, the same way.
        new Class
        {
            Suffix = "Spear", Role = "spear", Template = Brawler, Basic = WeakBanditKitKeys.SpearBasic, WeaponSkill = WeakBanditKitKeys.PiercingThrust,
            TreeSkills = new[] { WeakBanditKitKeys.OpeningStrike, WeakBanditKitKeys.TwinThrust }, Passives = new[] { "SKILL_Initiative", "SKILL_RecklessAssault", "SKILL_FastHand" },
            Variants = new[] { new[] { WeakBanditKitKeys.OpeningStrike, WeakBanditKitKeys.Anchor } },
            Level3Skills = new[] { WeakBanditKitKeys.SpearBash },
            KitByLevel = new[]
            {
                new[] { (Body, "ITEM_ClothArmor"), (Head, "ITEM_ClothCoif"), (Primary, "ITEM_WoodSpear") },
                new[] { (Body, "ITEM_ThickClothArmor"), (Head, "ITEM_ThickClothCoif"), (Primary, "ITEM_BoneSpear") },
                new[] { (Body, "ITEM_BoneArmor"), (Head, "ITEM_BoneHelmet"), (Primary, "ITEM_BoneSpear") }
            }
        },
        new Class
        {
            // Its staff's own skill, the bubbling, on every level (the summons take their owner's side, SummonSide); the
            // bubble toad on level 3 as the one skill outside row 0 (a player gets it from level 4).
            Suffix = "WaterMage", Role = "watermage", Template = Worker, Basic = WeakBanditKitKeys.MagicBasic, WeaponSkill = WeakBanditKitKeys.SummonBubbling,
            TreeSkills = new[] { WeakBanditKitKeys.TidalMark, WeakBanditKitKeys.EmbracingWave, WeakBanditKitKeys.SummonBubbleToad }, Passives = new[] { "SKILL_ExtraBubble", "SKILL_EbbAndFlow", "SKILL_Ignition" },
            // Two kinds, half and half: reserved (variant 1, today's kit without the toad) and aggressive (variant 2, the toad and the
            // dive from level 3 on, row 1 like the other classes'). A mage saved before keeps its own row and counts as reserved.
            Variants = new[] { new[] { WeakBanditKitKeys.TidalMark, WeakBanditKitKeys.EmbracingWave }, new[] { GroupAiWaterRules.GlacierDrop, WeakBanditKitKeys.TidalMark } },
            VariantLevel3Skills = new[] { Array.Empty<string>(), new[] { WeakBanditKitKeys.SummonBubbleToad, GroupAiWaterRules.WaterDive } },
            OnlyVariantsRolled = true,
            KitByLevel = new[]
            {
                new[] { (Body, "ITEM_ClothArmor"), (Primary, "ITEM_WaterWoodStaff") },
                new[] { (Body, "ITEM_ThickClothArmor"), (Primary, "ITEM_WaterBoneStaff") },
                new[] { (Body, "ITEM_BoneArmor"), (Head, "ITEM_BoneHelmet"), (Primary, "ITEM_WaterBoneStaff") }
            }
        },
        new Class
        {
            // The wood sword without a shield on level 1, a wood shield from level 2 on; the sword turns to bone only on
            // level 3.
            Suffix = "Sword", Role = "sword", Template = Brawler, Basic = WeakBanditKitKeys.SwordBasic, WeaponSkill = WeakBanditKitKeys.SweepingCleave,
            TreeSkills = new[] { WeakBanditKitKeys.ChargeAttack, WeakBanditKitKeys.SwordCounter }, Passives = new[] { "SKILL_SwordmansResolve", "SKILL_Fortified", "SKILL_GuardBarrier" },
            Variants = new[] { new[] { WeakBanditKitKeys.ChargeAttack, WeakBanditKitKeys.VerticalSlash } },
            Level3Skills = new[] { WeakBanditKitKeys.PommelStrike },
            KitByLevel = new[]
            {
                new[] { (Body, "ITEM_ClothArmor"), (Head, "ITEM_ClothCoif"), (Primary, "ITEM_WoodSword") },
                new[] { (Body, "ITEM_ThickClothArmor"), (Head, "ITEM_ThickClothCoif"), (Primary, "ITEM_WoodSword"), (Secondary, "ITEM_WoodShield") },
                new[] { (Body, "ITEM_BoneArmor"), (Head, "ITEM_BoneHelmet"), (Primary, "ITEM_BoneSword"), (Secondary, "ITEM_WoodShield") }
            }
        },
        new Class
        {
            Suffix = "FireMage", Role = "firemage", Template = Worker, Basic = WeakBanditKitKeys.MagicBasic, WeaponSkill = WeakBanditKitKeys.FireMissile,
            TreeSkills = new[] { WeakBanditKitKeys.Incinerate, WeakBanditKitKeys.BlazingEcho }, Passives = new[] { "SKILL_Ignition", "SKILL_LastCinder", "SKILL_SharpShooter" },
            Variants = new[] { new[] { WeakBanditKitKeys.Incinerate, WeakBanditKitKeys.FireCloak } },
            KitByLevel = new[]
            {
                new[] { (Body, "ITEM_ClothArmor"), (Primary, "ITEM_FireWoodStaff") },
                new[] { (Body, "ITEM_ThickClothArmor"), (Primary, "ITEM_FireBoneStaff") },
                new[] { (Body, "ITEM_BoneArmor"), (Head, "ITEM_BoneHelmet"), (Primary, "ITEM_FireBoneStaff") }
            }
        }
    };

    internal static string KeyOf(int level, Class c) => CoreUnitRules.Prefix + KeyPart + level + c.Suffix;

    // (for the passives taken back): every weak group always leaves the game's imperial coins, one stack of
    // CoinCount. A weak bandit drops nothing else at its death; its loot is the kit it wears, stripped from the corpse (the standard loot is that equipment, by Alt and
    // right click). It drops on the cell of the group's last bandit to die: a dying weak bandit with no other
    // living weak bandit of its group (GroupAiRules.IsGroupAlly, the group AI's own reach) leaves it. A game item, so a save
    // without Core keeps it.
    internal const string CoinKey = "ITEM_ImperialCoin";
    // Three of them as one stack, about 300 gold at a merchant; the coin's price stays the game's.
    internal const int CoinCount = 3;

    internal static bool DropsCoin(bool weak, int livingWeakOfGroup) => weak && livingWeakOfGroup == 0;

    // Bandits that fall in one blow all find no one left: the group's coins came from the first of them within this many seconds.
    internal const float CoinSeconds = 1f;

    internal static bool CoinsJustDropped(float now, float? droppedAt, int cellsApart)
        => droppedAt is float at && now >= at && now - at < CoinSeconds && GroupAiRules.IsGroupAlly(cellsApart);

    // The active skills of a class on a level: basic attack, weapon skill, then one tree skill per level up to the class's count.
    internal static string[] ActivesOf(Class c, int level)
    {
        var skills = new List<string> { c.Basic };
        if (!string.IsNullOrEmpty(c.WeaponSkill)) skills.Add(c.WeaponSkill);
        for (var i = 0; i < c.TreeSkills.Length && i < level; i++) skills.Add(c.TreeSkills[i]);
        if (level >= 3) skills.AddRange(c.Level3Skills);
        return skills.ToArray();
    }

    // The passives of a class on a level: one per level.
    internal static string[] PassivesOf(Class c, int level)
    {
        var passives = new List<string>();
        for (var i = 0; c.Passives != null && i < c.Passives.Length && i < level; i++) passives.Add(c.Passives[i]);
        return passives.ToArray();
    }

    // What the unit row carries: the actives, then the passives a bandit can run (BanditPassiveRules.Registrable: Healing Touch
    // reads the equipment a bandit lacks, so the herbalist's third passive stays in the table but out of her row).
    internal static string[] SkillsOf(Class c, int level)
        => ActivesOf(c, level).Concat(PassivesOf(c, level).Where(BanditPassiveRules.Registrable)).ToArray();

    internal static CoreUnitRules.Definition DefinitionOf(Class c, int level) => new()
    {
        Key = KeyOf(level, c),
        Role = c.Role,
        Template = c.Template,
        Skills = SkillsOf(c, level),
        Kit = c.KitByLevel[level - 1],
        Level = level,
        DamageFactor = Factor,
        HealthFactor = HealthByLevel[level - 1],
        MagicFactor = Factor,
        ArmorBonus = ArmorBonusByLevel[level - 1],
        TauntFactor = TauntFactor,
        TauntBreakDrop = TauntBreakDrop
    };

    // Every level and class, level by level.
    internal static readonly CoreUnitRules.Definition[] Definitions = Build();

    private static CoreUnitRules.Definition[] Build()
    {
        var all = new List<CoreUnitRules.Definition>();
        for (var level = 1; level <= Levels; level++)
            foreach (var c in Classes)
            {
                all.Add(DefinitionOf(c, level));
                for (var i = 1; i <= c.Variants.Length; i++) all.Add(DefinitionOf(VariantOf(c, i), level));
            }
        return all.ToArray();
    }

    // Variant number (from 1) of a class: its tree skills replaced, its key with "Alt<number>" before the class's end, so KitOf still
    // reads the class. 0 is the class itself.
    internal static Class VariantOf(Class c, int number)
    {
        if (number <= 0 || number > c.Variants.Length) return c;
        return new Class
        {
            Suffix = "Alt" + number + c.Suffix, Role = c.Role, Template = c.Template, Basic = c.Basic, WeaponSkill = c.WeaponSkill,
            TreeSkills = c.Variants[number - 1], Level3Skills = number <= c.VariantLevel3Skills.Length ? c.VariantLevel3Skills[number - 1] : c.Level3Skills,
            Passives = c.Passives, KitByLevel = c.KitByLevel
        };
    }

    // The variant a roll picks: any row of the class, or only its variants.
    internal static int VariantNumber(Class c, uint roll)
        => c.OnlyVariantsRolled && c.Variants.Length > 0 ? 1 + (int)(roll % (uint)c.Variants.Length) : (int)(roll % (uint)(c.Variants.Length + 1));

    // The reserved water mage's row; the aggressive one tells by its Glacier Drop.
    internal static bool IsReservedMage(string key) => (key ?? "").EndsWith("Alt1WaterMage", StringComparison.Ordinal);

    internal static bool IsWeak(CoreUnitRules.Definition unit) => unit != null && unit.Level > 0;

    // The weak definition of a level and class, or null.
    internal static CoreUnitRules.Definition Find(int level, string suffix)
    {
        var key = KeyPart + level + suffix;
        return Array.Find(Definitions, d => d.Level == level && d.Key.EndsWith(key, StringComparison.Ordinal));
    }

    // A plain "Forest Bandit" without the role; the kit shows the difference.
    internal static readonly UniqueCandidateTranslations Name = new(
        "Forest Bandit", "숲 도적", "Bandit des bois", "Waldbandit", "Лесной бандит",
        "森林强盗", "森林強盜", "森の盗賊", "Bandido del bosque", "Bandido da floresta");

    internal static readonly UniqueCandidateTranslations Description = new(
        "A forest bandit with typical gear in poor condition.",
        "낡은 상태의 흔한 장비를 갖춘 숲 도적입니다.",
        "Un bandit des bois à l'équipement ordinaire et en mauvais état.",
        "Ein Waldbandit mit typischer Ausrüstung in schlechtem Zustand.",
        "Лесной бандит с обычным снаряжением в плохом состоянии.",
        "装备普通且破旧的森林强盗。",
        "裝備普通且破舊的森林強盜。",
        "ありふれた装備を傷んだまま身につけた森の盗賊。",
        "Un bandido del bosque con equipo típico en mal estado.",
        "Um bandido da floresta com equipamento típico em mau estado.");

    // The text rows of every weak key: the same name and description for all.
    internal static IEnumerable<(string Key, UniqueCandidateTranslations Text)> TextRows()
    {
        foreach (var d in Definitions)
        {
            yield return (CoreUnitRules.NameKey(d.Key), Name);
            yield return (CoreUnitRules.DescKey(d.Key), Description);
        }
    }
}
