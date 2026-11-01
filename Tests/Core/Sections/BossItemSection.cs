using DungeonSettlersDelvers.Core;
using static TestKit;

// The boss bandits' own parts: keys and stand-ins, the boss party's kit, the violet and the condition of a dropped part.
internal static class BossItemSection
{
    internal static void Run()
    {
        Check(BossItemRules.Parts.All(p => p.Key.StartsWith(CoreSaveItems.Prefix, StringComparison.Ordinal) && p.Native.StartsWith("ITEM_", StringComparison.Ordinal))
                && BossItemRules.Parts.Select(p => p.Key).Distinct().Count() == 4,
            "boss items: four own keys with Core's item prefix, each over a game item");
        Check(BossItemRules.StandIn(BossItemRules.GreatSwordKey) == "ITEM_BloodWoodGreatSword"
                && BossItemRules.StandIn(BossItemRules.ShieldKey) == "ITEM_BloodWoodShield"
                && BossItemRules.StandIn(BossItemRules.BowKey) == "ITEM_BloodWoodBow"
                && BossItemRules.StandIn(BossItemRules.StaffKey) == "ITEM_NatureBloodWoodStaff"
                && BossItemRules.StandIn("ITEM_BloodWoodBow") == null && !BossItemRules.IsBossItem(null),
            "boss items: each part is saved as its blood wood part; game keys are no boss parts");
        Check(BossItemRules.RarityValue == 3, "boss items: Legendary, the highest rarity the game knows");
        Check(BossItemText.GreatSwordName.German == ApocalypseText.Tint("Ruchloses Großschwert"),
            "boss items: the names show in Apocalypse violet");

        var kit = CoreUnitRules.Units.SelectMany(u => u.Kit.Select(k => k.Item)).Where(BossItemRules.IsBossItem).ToArray();
        Check(kit.Length == 4 && kit.Distinct().Count() == 4, "boss items: the boss party wears all four parts, each once");
        var blade = CoreUnitRules.Units.First(u => u.Role == "blade");
        Check(CoreUnitRules.DropItems(blade).Contains(BossItemRules.GreatSwordKey), "boss items: undressing the blade drops its greatsword");

        // Colours of the game's blood wood pictures and what the confirmed preview made of them.
        Check(BossItemRules.Violet(160, 52, 48, 255) == (107, 0, 160, 255) && BossItemRules.Violet(185, 67, 57, 255) == (123, 0, 185, 255),
            "boss items: the strong reds take the violet's hue at full saturation and keep their brightness");
        Check(BossItemRules.Violet(95, 67, 56, 255) == (95, 95, 95, 255) && BossItemRules.Violet(62, 45, 46, 200) == (62, 62, 62, 200),
            "boss items: dull reds and browns turn grey with their brightness and alpha");
        Check(BossItemRules.Violet(111, 221, 77, 255) == (111, 221, 77, 255) && BossItemRules.Violet(193, 189, 161, 255) == (193, 189, 161, 255)
                && BossItemRules.Violet(34, 32, 37, 255) == (34, 32, 37, 255) && BossItemRules.Violet(160, 52, 48, 0) == (160, 52, 48, 0),
            "boss items: the crystal's green, the bow's string, the outline and clear pixels stay");

        Check(BossItemRules.Gilded(230, 180, 60, 255).Item3 > BossItemRules.Gilded(230, 180, 60, 255).Item2
                && BossItemRules.Gilded(255, 255, 255, 255) == (255, 255, 255, 255) && BossItemRules.Gilded(40, 90, 200, 255) == (40, 90, 200, 255),
            "boss items: the tooltip's gold turns violet; white and other colours stay");

        Check(CorpseRules.UndressInteraction > 39 && CorpseText.Undress.German == "Ausrüstung ausziehen" && CorpseText.Undress.English == "Strip Equipment",
            "corpses: the undress option under a type the game does not use, in Fre-yin's words");

        var shield = new[] { "GuardChance_10", "AttackSpeedPerModifier_-30", "PhysicalResistance_8", "MaxHealthTotal_5" };
        var warden = BossItemRules.Statuses(BossItemRules.ShieldKey, shield);
        Check(warden.SequenceEqual(new[] { "GuardChance_15", "AttackSpeedPerModifier_-20", "PhysicalResistance_15", "MaxHealthTotal_5" })
                && BossItemRules.Statuses(BossItemRules.BowKey, shield).SequenceEqual(shield.Append("VisionRange_3"))
                && BossItemRules.Statuses(BossItemRules.ShieldKey, new[] { "AttackSpeed_10" }).SequenceEqual(new[] { "AttackSpeed_10", "GuardChance_5", "PhysicalResistance_15" })
                && BossItemRules.Statuses(BossItemRules.ShieldKey, null).Length == 2,
            "boss items: the warden blocks 5 more than the blood wood shield, its speed malus is -20 and its physical resistance 15; the others keep the shield's values");
        Check(BossItemRules.SpecialSubtitle("Legendär Sekundäre Ausrüstung", "Legendär", "Spezielle") == "Spezielle Sekundäre Ausrüstung"
                && BossItemRules.SpecialSubtitle("<color=#f5a623>Legendary</color> Primary Equipment", "Legendary", "Special") == "<color=#f5a623>Special</color> Primary Equipment"
                && BossItemRules.SpecialSubtitle("Rare Primary Equipment", "Legendary", "Special") == "Rare Primary Equipment"
                && BossItemRules.SpecialSubtitle("Legendary Primary Equipment", null, "Special") == "Legendary Primary Equipment"
                && BossItemText.Special.German == "Spezielle" && BossItemText.Special.English == "Special",
            "boss items: the subtitle says Special in place of Legendary; any other rarity and a normal Legendary item keep the game's word");
        var descriptions = new[] { BossItemText.GreatSwordDescription, BossItemText.ShieldDescription, BossItemText.BowDescription, BossItemText.StaffDescription };
        Check(descriptions.All(d => d.AllLanguages.Count == 10 && d.AllLanguages.All(t => !t.Contains('-') && !t.Contains('–') && !t.Contains('—')))
                && BossItemText.ShieldDescription.German.StartsWith("Fällt ein anderes Gruppenmitglied", StringComparison.Ordinal)
                && BossItemText.ShieldDescription.English.Contains("another party member") && !BossItemText.ShieldDescription.German.Contains("Blockchance")
                && BossItemText.ShieldDescription.AllLanguages.All(t => t.Split('\n').Length == 3)
                && BossItemText.BowDescription.AllLanguages.All(t => t.Split('\n').Length == 2),
            "boss items: the effects in short lines with their name on top, another party member for the warden, no hyphens");

        var sword = new[] { "HitChance_-10", "PhysicalAttackPowerPerModifier_10", "AttackSpeedPerModifier_-20" };
        Check(BossItemRules.Statuses(BossItemRules.GreatSwordKey, sword).SequenceEqual(new[] { "HitChance_-5", "PhysicalAttackPowerPerModifier_15", "AttackSpeedPerModifier_-20" })
                && BossItemRules.Statuses(BossItemRules.StaffKey, sword).SequenceEqual(sword.Append("SkillEnergyCostPerModifier_25"))
                && BossItemRules.BaseAttack(BossItemRules.GreatSwordKey, 56f, 0f) == (62f, 0f) && BossItemRules.BaseAttack(BossItemRules.BowKey, 34f, 0f) == (38f, 0f)
                && BossItemRules.BaseAttack(BossItemRules.StaffKey, 0f, 42f) == (0f, 46f) && BossItemRules.BaseAttack(BossItemRules.ShieldKey, 0f, 0f) == (0f, 0f)
                && BossItemRules.StatsLine(BossItemRules.BowKey, (34f, 0f), (38f, 0f), new[] { "A_1" }, new[] { "A_1" }, new[] { "SKILL_BasicBowAttack", "SKILL_RapidFire" })
                    == "CORE_BOSS_ITEM_STATS key=" + BossItemRules.BowKey + " physical=34->38 magic=0->0 native=A_1 own=A_1 skills=SKILL_BasicBowAttack,SKILL_RapidFire",
            "boss items: the greatsword hits at -5 with 15 per modifier and 62 attack, the bow 38, the staff 46 magic; the log names base values and statuses");
        var effectNames = new[] { BossItemText.GreatSwordEffectName, BossItemText.ShieldEffectName, BossItemText.BowEffectName, BossItemText.StaffEffectName };
        var effectTexts = new[] { BossItemText.GreatSwordDescription, BossItemText.ShieldDescription, BossItemText.BowDescription, BossItemText.StaffDescription };
        Check(BossItemText.GreatSwordEffectName.German == "Aderlass" && BossItemText.GreatSwordEffectName.English == "Bloodletting"
                && BossItemText.ShieldEffectName.German == "Blutwächter" && BossItemText.BowEffectName.German == "Falkenaugen"
                && BossItemText.StaffEffectName.German == "Wildwuchs" && BossItemText.StaffEffectName.English == "Overgrowth"
                && effectNames.All(n => n.AllLanguages.All(t => t.Length > 0 && !t.Contains('#') && !t.Contains('-')))
                && effectTexts.All(d => d.AllLanguages.All(t => !t.StartsWith("#l", StringComparison.Ordinal) && !t.Contains('-')))
                && BossItemText.GreatSwordDescription.AllLanguages.All(t => t.Contains("[AFFECTER_Bleeding]")) && BossItemText.StaffDescription.AllLanguages.All(t => t.Contains("[AFFECTER_Bleeding]"))
                && BossItemRules.Parts.All(p => BossItemRules.EffectKey(p.Key).StartsWith(CoreSaveEffects.Prefix, StringComparison.Ordinal)
                    && BossItemRules.EffectKey(p.Key).Split('_').Length == 2 && BossItemRules.IsEffect(BossItemRules.EffectKey(p.Key)))
                && BossItemRules.EffectKey(BossItemRules.ShieldKey) == "AFFECTER_DelversCoreBossShield" && !BossItemRules.IsEffect("AFFECTER_Bleeding")
                && BossItemRules.EffectIcon(BossItemRules.EffectKey(BossItemRules.GreatSwordKey)) == "AFFECTER_Bleeding" && BossItemRules.EffectIcon("AFFECTER_X") == null,
            "boss items: each effect is an equipment effect under a key the save keeps out and a link reaches, its name and text apart, bleeding as the game's yellow term");
        Check(CoreTextLayout.KeepUnits("Heilung +20 %.") == "Heilung <nobr>+20 %</nobr>."
                && CoreTextLayout.KeepUnits("healing done +20%.") == "healing done <nobr>+20%</nobr>."
                && CoreTextLayout.KeepUnits("unter 35 % Leben, 12,5 % mehr") == "unter <nobr>35 %</nobr> Leben, <nobr>12,5 %</nobr> mehr"
                && CoreTextLayout.KeepUnits("치유량 +20%.") == "치유량 <nobr>+20%</nobr>." && CoreTextLayout.KeepUnits("{0}%") == "{0}%"
                && CoreTextLayout.KeepUnits("no number") == "no number" && CoreTextLayout.KeepUnits(null) == null,
            "texts: a number keeps its percent sign on the same line, with or without a space, in every language");

        Check(BossItemRules.RepairRecipeKey(BossItemRules.ShieldKey) == "RECIPE_DelversCoreBossShield"
                && BossItemRules.RepairRecipeKey("ITEM_BloodWoodShield") == "RECIPE_BloodWoodShield"
                && BossItemRules.RepairRecipeKey("ITEM_NatureBloodWoodStaff") == "RECIPE_NatureBloodWoodStaff"
                && BossItemRules.Parts.All(p => BossItemRules.RepairRecipeKey(p.Key) == p.Key.Replace("ITEM", "RECIPE"))
                && BossItemRules.RepairRecipeKey(null) == null
                && BossItemRules.RecipeLine("R", "T", true, 120f) == "CORE_BOSS_ITEM_RECIPE key=R from=T result=added workload=120",
            "boss items: each part has a repair recipe under the key the game's repair looks up, copied from its blood wood part's");

        Check(BossItemRules.Statuses(BossItemRules.StaffKey, new[] { "MaxHealthTotal_5" }).SequenceEqual(new[] { "MaxHealthTotal_5", "SkillEnergyCostPerModifier_25" })
                && BossItemRules.Statuses(BossItemRules.StaffKey, new[] { "SkillEnergyCostPerModifier_-10" }).SequenceEqual(new[] { "SkillEnergyCostPerModifier_15" })
                && BossItemRules.Statuses(BossItemRules.BowKey, Array.Empty<string>()).SequenceEqual(new[] { "VisionRange_3" })
                && BossItemRules.Statuses(BossItemRules.BowKey, new[] { "VisionRange_1" }).SequenceEqual(new[] { "VisionRange_4" }),
            "boss items: the staff costs 25 % more energy and the bow sees 3 cells further, as the game's own statuses");
        Check(BossItemRules.EffectsFor(new[] { "ITEM_BoneArmor", BossItemRules.ShieldKey, "NULL", BossItemRules.ShieldKey, BossItemRules.BowKey })
                    .SequenceEqual(new[] { BossItemRules.EffectKey(BossItemRules.ShieldKey), BossItemRules.EffectKey(BossItemRules.BowKey) })
                && BossItemRules.EffectsFor(null).Count == 0 && BossItemRules.EffectsFor(new[] { "ITEM_BloodWoodShield" }).Count == 0
                && BossItemRules.RestoredLine(1, 2) == "CORE_BOSS_ITEM_EFFECTS_RESTORED units=1 effects=2"
                && BossItemRules.OutputLine("I", "R", true) == "CORE_BOSS_ITEM_RECIPE_OUTPUT key=I recipe=R result=mapped",
            "boss items: after a load each worn boss part's effect comes back once; other items call for none");

        Check(BossEffectRules.HealWithBonus(100f, true) == 120f && BossEffectRules.HealWithBonus(100f, false) == 100f
                && BossEffectRules.HealWithBonus(0f, true) == 0f && BossEffectRules.HealWithBonus(-5f, true) == -5f
                && Math.Abs(BossEffectRules.EasedHealFactor(0.70, true) - 0.82) < 1e-9 && BossEffectRules.EasedHealFactor(0.70, false) == 0.70
                && Math.Abs(BossEffectRules.EasedHealFactor(0.5, true) - 0.7) < 1e-9 && BossEffectRules.EasedHealFactor(1.0, true) == 1.0
                && BossEffectRules.Line("overgrowth-heal", "u", "heal=10->12") == "CORE_BOSS_EFFECT name=overgrowth-heal unit=u heal=10->12",
            "boss effects: the staff's heals heal 20 % more and ignore 40 % of the bleeding wound's reduction; nothing else changes");

        var party = new[] { ("bearer", true, 0.10), ("a", true, 0.30), ("b", true, 0.20), ("c", false, 0.05), ("d", true, 0.50), ("e", true, double.NaN) };
        Check(BossEffectRules.BloodGuardTarget("bearer", party) == "b"
                && BossEffectRules.BloodGuardTarget("bearer", new[] { ("bearer", true, 0.10), ("d", true, 0.50) }) == null
                && BossEffectRules.BloodGuardTarget("bearer", new[] { ("a", true, 0.35) }) == null
                && BossEffectRules.BloodGuardTarget("bearer", new[] { ("a", true, 0.349) }) == "a"
                && BossEffectRules.BloodGuardTarget("bearer", new[] { ("c", false, 0.05) }) == null,
            "boss effects: the Blood Warden shields another living member below 35 %, the lowest first, never its bearer");
        var (sacHead, sacBody, sacShield) = BossEffectRules.Sacrifice(50, 100);
        Check(sacHead == 20 && sacBody == 40 && sacShield == 180 && BossEffectRules.Sacrifice(-5, 10).Shield == 12
                && BossEffectRules.RestHeal(30) == 15 && BossEffectRules.RestHeal(0) == 0 && BossEffectRules.RestHeal(-3) == 0
                && BossEffectRules.ShieldSeconds == 8f && BossEffectRules.BloodGuardKey.StartsWith(CoreSaveEffects.Prefix, StringComparison.Ordinal)
                && BossEffectRules.BloodGuardKey.Split('_').Length == 2,
            "boss effects: the bearer gives 40 % of head and body, the member gets three times that as an 8 s shield, half the rest heals");

        Check(BossEffectRules.DamageShare == 0.30 && BossEffectRules.BloodlettingStacks(60, 2, 6) == 2 && BossEffectRules.BloodlettingStacks(300, 2, 6) == 8
                && BossEffectRules.BloodlettingStacks(19, 2, 6) == 0 && BossEffectRules.BloodlettingStacks(20, 2, 6) == 1
                && BossEffectRules.BloodlettingStacks(0, 2, 6) == 0 && BossEffectRules.BloodlettingStacks(100, 0, 6) == 0
                && BossEffectRules.BloodlettingStacks(100, 2, 0) == 0,
            "boss effects: Bloodletting adds 30 % of a hit's damage as bleeding stacks over the bleeding's time; small hits add none");

        // Bloodletting's heal: the ledger of which bearer put which of a target's bleeding stacks.
        var ledger = new BossEffectRules.BloodLedger();
        ledger.Add("t", "sword", 4, 10);
        var mixed = ledger.Heals("t", 10, 50);
        Check(BossEffectRules.HealShare == 0.20 && mixed.Count == 1 && mixed[0].Bearer == "sword" && Math.Abs(mixed[0].Heal - 6) < 1e-9,
            "boss effects: a bleeding tick heals the bearer 20 % of its damage times the bearer's share of the stacks (6 of 10, 50 -> 6)");
        Check(ledger.Heals("other", 10, 50).Count == 0 && ledger.Heals("t", 10, 0).Count == 0,
            "boss effects: bleeding of other sources alone and a tick without damage heal nobody");
        ledger.Add("t", "sword", 3, 5);
        Check(Math.Abs(ledger.Stacks("t", "sword") - 5) < 1e-9,
            "boss effects: a hit on a target whose bleeding fell below the count (3 left of 6) first cuts the count to 3, then adds its 2");
        ledger.Add("t", "sword", 5, 5);
        Check(Math.Abs(ledger.Stacks("t", "sword") - 5) < 1e-9,
            "boss effects: a hit at the game's cap (99, nothing added) adds nothing to the count");
        ledger.Add("t", "second", 5, 9);
        var two = ledger.Heals("t", 9, 90);
        Check(two.Count == 2 && Math.Abs(two.First(h => h.Bearer == "sword").Heal - 10) < 1e-9 && Math.Abs(two.First(h => h.Bearer == "second").Heal - 8) < 1e-9,
            "boss effects: two bearers on one target each heal by their own stacks (5 and 4 of 9, 90 -> 10 and 8)");
        ledger.Sync("t", 3);
        Check(Math.Abs(ledger.Stacks("t", "sword") - 5.0 / 3) < 1e-9 && Math.Abs(ledger.Stacks("t", "second") - 4.0 / 3) < 1e-9,
            "boss effects: a bandage that cuts the bleeding below the count cuts every bearer alike (9 to 3: a third each)");
        Check(ledger.Heals("t", 0, 30).Count == 0 && ledger.Count == 0,
            "boss effects: a target whose bleeding ran out is forgotten");
        ledger.Add("t", "sword", 0, 6);
        ledger.Add("t", "sword", 0, 2);
        Check(Math.Abs(ledger.Stacks("t", "sword") - 2) < 1e-9,
            "boss effects: a new bleeding after the old one ran out starts the count anew");
        for (var i = 0; i < BossEffectRules.LedgerCap + 5; i++) ledger.Add("n" + i, "sword", 0, 1);
        Check(ledger.Count == BossEffectRules.LedgerCap && ledger.Stacks("n0", "sword") == 0 && ledger.Stacks("n" + (BossEffectRules.LedgerCap + 4), "sword") == 1,
            "boss effects: the ledger keeps at most its cap of targets and lets the first counted go");
        ledger.Clear();
        Check(ledger.Count == 0, "boss effects: the ledger empties with the campaign");

        var budget = new BossEffectRules.HealBudget();
        var first = budget.Take("sword", 7, 18);
        var second = budget.Take("sword", 7, 18);
        var third = budget.Take("sword", 7, 5);
        Check(BossEffectRules.HealCapPerSecond == 30.0 && first == 18 && second == 12 && third == 0,
            "boss effects: a bearer heals at most 30 a game second from all its bleeding targets (18 + 12, then nothing)");
        Check(budget.Take("sword", 8, 25) == 25 && budget.Take("other", 8, 30) == 30 && budget.Take("sword", 8, 0) == 0,
            "boss effects: the next second starts anew, every bearer has its own budget, no heal asked takes nothing");

        Check(BossItemRules.EffectIcon(BossItemRules.EffectKey(BossItemRules.ShieldKey)) == "AFFECTER_Barrier"
                && BossItemRules.EffectIcon(BossItemRules.EffectKey(BossItemRules.BowKey)) == null
                && BossItemRules.EffectIcon(BossItemRules.EffectKey(BossItemRules.StaffKey)) == null
                && BossItemRules.EffectIconFile(BossItemRules.EffectKey(BossItemRules.GreatSwordKey)) == "Aderlass_Effektsymbol_32x32.png"
                && BossItemRules.EffectIconFile(BossItemRules.EffectKey(BossItemRules.BowKey)) == "Falkenaugen_Effektsymbol_32x32.png"
                && BossItemRules.EffectIconFile(BossItemRules.EffectKey(BossItemRules.StaffKey)) == "Wildwuchs_Effektsymbol_32x32.png"
                && BossItemRules.EffectIconFile(BossItemRules.EffectKey(BossItemRules.ShieldKey)) == "Blutwaechter_Effektsymbol_32x32.png"
                && BossItemRules.EffectIconFile(BossEffectRules.BloodGuardKey) == "Blutwaechter_Effektsymbol_32x32.png"
                && BossItemRules.EffectIconSize == 32
                && BossItemRules.RedIcon(BossItemRules.EffectKey(BossItemRules.ShieldKey)) && !BossItemRules.RedIcon(BossItemRules.EffectKey(BossItemRules.GreatSwordKey)) && BossItemRules.RedIcon(BossEffectRules.BloodGuardKey) && BossItemRules.EffectIcon(BossEffectRules.BloodGuardKey) == "AFFECTER_Barrier"
                && BossItemRules.RedCast(131, 134, 136, 255) == (158, 105, 106, 255) && BossItemRules.WardenRed == 0.22 && BossItemRules.RedCast(34, 32, 37, 0) == (34, 32, 37, 0),
            "boss effects: the icons as Fre-yin picked, the warden's barrier with a slight red cast, own 32 pictures for all four and the shield the warden gives, the red cast only without the file");

        Check(BossEffectRules.ShieldRest(1000, 1000, 12) == 12 && BossEffectRules.ShieldRest(1002, 1000, 12) == 12
                && BossEffectRules.ShieldRest(998, 1000, 12) == 12 && BossEffectRules.ShieldRest(990, 1000, 12) == 0
                && BossEffectRules.ShieldRest(1005, 1000, 12) == 0 && BossEffectRules.ShieldRest(1000, 1000, 0) == 0
                && BossEffectRules.ShieldRest(1000, 1000, -4) == 0,
            "boss effects: the warden's rest heals only when its own shield ran out within 2 ticks of its end; broken earlier, nothing");
        Check(BossEffectRules.StaffHealSource(false, true) == "self" && BossEffectRules.StaffHealSource(true, true) == "summon"
                && BossEffectRules.StaffHealSource(true, false) == null && BossEffectRules.StaffHealSource(false, false) == null,
            "boss effects: Overgrowth counts the bearer's own heals and her summons' heals, as long as she wears the staff now");

        Check(BossItemRules.Statuses(BossItemRules.BowKey, new[] { "NULL" }).SequenceEqual(new[] { "VisionRange_3" })
                && BossItemRules.Statuses(BossItemRules.StaffKey, new[] { "NULL" }).SequenceEqual(new[] { "SkillEnergyCostPerModifier_25" })
                && BossItemRules.Statuses(BossItemRules.GreatSwordKey, new[] { "NULL" }).SequenceEqual(new[] { "NULL" })
                && BossItemRules.Statuses(BossItemRules.ShieldKey, new[] { "AttackSpeedPerModifier_-30", "PhysicalResistance_8", "GuardChance_10" })
                    .SequenceEqual(new[] { "AttackSpeedPerModifier_-20", "PhysicalResistance_15", "GuardChance_15" })
                && !BossItemRules.Statuses(BossItemRules.BowKey, new[] { "NULL", "" }).Contains("NULL"),
            "boss items: an empty status column (NULL) is replaced, never appended to, so the game reads the part's statuses");
        Check(BossEffectRules.ShieldMaxStack(999999) == 999999 && BossEffectRules.ShieldMaxStack(-1) == 999999 && BossEffectRules.ShieldMaxStack(null) == 999999
                && BossEffectRules.ShieldMaxStack(5000) == 5000
                && BossEffectRules.ShieldEndReason(1001, 1000) == "expired" && BossEffectRules.ShieldEndReason(900, 1000) == "consumed",
            "boss effects: the warden's shield keeps the barrier template's stack cap, never -1; its end reads expired or consumed");
        Check(BossItemRules.InvertsColour("SkillEnergyCostPerModifier", true) && !BossItemRules.InvertsColour("SkillEnergyCostPerModifier", false)
                && !BossItemRules.InvertsColour("GuardChance", true)
                && BossItemText.ShieldDescription.German.Contains("das Dreifache davon") && BossItemText.ShieldDescription.English.Contains("three times that amount")
                && !BossItemText.ShieldDescription.AllLanguages.Any(t => t.Contains("120")),
            "boss items: the energy cost row of a boss part shows a plus as a drawback; the warden's text says three times");

        // The bow skills of SkillTable 0.4.29: key, range, expected range with Falcon Eyes (+2 on all but the dash).
        var bowSkills = new (string Key, int Range, int Expected)[]
        {
            ("SKILL_BasicBowAttack", 5, 7), ("SKILL_RapidFire", 5, 7), ("SKILL_RapidFireAttack", 5, 7), ("SKILL_ArrowRain", 7, 9),
            ("SKILL_AnkleBreak", 1, 3), ("SKILL_SnipingShot", 8, 10), ("SKILL_HeadAim", 5, 7), ("SKILL_QuickDash", 4, 4)
        };
        Check(bowSkills.All(s => BossEffectRules.RangeWithBonus(s.Range, true, s.Key) == s.Expected)
                && bowSkills.All(s => BossEffectRules.RangeWithBonus(s.Range, false, s.Key) == s.Range)
                && BossEffectRules.RangeWithBonus(0, true, "SKILL_X") == 0 && BossEffectRules.RangeWithBonus(3, true, "SKILL_MagicX") == 5,
            "boss effects: Falcon Eyes gives +2 range to every skill the bearer holds, never to the dash or another unit's");
        // The game's cast checks (RangeCondition, CombatUtility.IsInRange(context)) pass up to the table range plus 1; with the
        // bonus the boss bow's auto attack passes at 8 and not at 9, the normal bow's at 6 and not at 7.
        var tableRange = bowSkills.First(s => s.Key == "SKILL_BasicBowAttack").Range;
        var withBonus = BossEffectRules.RangeWithBonus(tableRange, true, "SKILL_BasicBowAttack");
        Check(BossEffectRules.InCastRange(6, tableRange, BossEffectRules.CastRangeCorrection) && !BossEffectRules.InCastRange(7, tableRange, BossEffectRules.CastRangeCorrection)
                && BossEffectRules.InCastRange(8, withBonus, BossEffectRules.CastRangeCorrection) && !BossEffectRules.InCastRange(9, withBonus, BossEffectRules.CastRangeCorrection)
                && BossEffectRules.InCastRange(7, withBonus, 0) && !BossEffectRules.InCastRange(8, withBonus, 0),
            "boss effects: a cast passes the game's check up to the range plus its correction; Falcon Eyes moves that limit by the bonus, for both checks");
        Check(BossEffectRules.FalconCritBonus(4) == 0 && BossEffectRules.FalconCritBonus(5) == 3 && BossEffectRules.FalconCritBonus(7) == 9
                && BossEffectRules.FalconCritBonus(1) == 0 && BossEffectRules.FalconCritBonus(10) == 18,
            "boss effects: Falcon Eyes adds 3 % crit chance for each cell beyond 4");
        Check(LatePatchInstalls(new[] { false, true, true, true }) == 1 && LatePatchInstalls(new[] { true, true }) == 1
                && LatePatchInstalls(new[] { false, false }) == 0,
            "boss effects: the crit patch is put on once per session, at the first hook with the combat data loaded, never again on a later load or campaign");
        var failed = new LatePatchGate();
        failed.Record(false);
        var stays = !failed.ShouldTry(true) && failed.State == LatePatchState.Failed;
        failed.Reset();
        Check(stays && failed.ShouldTry(true)
                && LatePatchGate.Line("falcon-crit", "boot", LatePatchState.Installed) == "CORE_LATE_PATCH name=falcon-crit at=boot state=installed",
            "boss effects: a failed crit patch is not tried again in the session; only Core's shutdown lets it wait again");
        Check(TargetSortRules.SkipsGame(false, true) && TargetSortRules.SkipsGame(true, false) && TargetSortRules.SkipsGame(false, false)
                && !TargetSortRules.SkipsGame(true, true)
                && TargetSortRules.GoneOrder(true, false) == -1 && TargetSortRules.GoneOrder(false, true) == 1 && TargetSortRules.GoneOrder(false, false) == 0
                && TargetSortRules.Line("a", "g", "UNIT_X", 12, 3) == "CORE_TARGET_SORT_NULL side=a guid=g key=UNIT_X tick=12 count=3",
            "target sort: only a pair with a candidate gone is Core's, the gone one comes after; every whole pair is the game's own comparison");
        var sortSet = new[] { new SortCandidate("downed", false, true), new SortCandidate("gone", true, false),
            new SortCandidate("standing", false, false), new SortCandidate("gone2", true, false), new SortCandidate("downed2", false, true) };
        Check(Permutations(sortSet).All(order => SortedRight(QueueOrder(order, TargetSortRules.GoneOrder))),
            "target sort: through the game's queue (tie by insertion) every order of candidates comes out standing, downed, gone");
        Check(Permutations(sortSet.Take(3).ToArray()).Any(order => !SortedRight(QueueOrder(order, (first, second) => 0))),
            "target sort: counting a gone candidate as equal gives a wrong order for some insertion order (why it comes after)");

        var picks = new int[4];
        for (var seed = 0; seed < 4000; seed++) picks[BossItemRules.ChosenIndex(seed, 1, 3, 4)]++;
        Check(BossItemRules.ChosenIndex(12345, 1, 3, 4) == BossItemRules.ChosenIndex(12345, 1, 3, 4)
                && picks.All(n => n > 850 && n < 1150) && BossItemRules.ChosenIndex(1, 1, 1, 0) == -1
                && Enumerable.Range(0, 50).Select(f => BossItemRules.ChosenIndex(777, 1, f, 4)).Distinct().Count() == 4,
            "boss items: the floor's seed picks one of the four bosses, the same on every load, evenly over many seeds");
        var guard = CoreUnitRules.Units.First(u => u.Role == "guard");
        Check(!CoreUnitRules.DropItems(guard, true).Contains(BossItemRules.ShieldKey) && CoreUnitRules.DropItems(guard, true).Contains("ITEM_BoneClub")
                && CoreUnitRules.DropItems(guard).Contains(BossItemRules.ShieldKey)
                && CoreUnitRules.EquipInfo(guard, false, true).All(e => !BossItemRules.IsBossItem(e.Item))
                && CoreUnitRules.EquipInfo(guard, false, true).Any(e => e.Item == "ITEM_BoneClub")
                && CoreUnitRules.Units.All(u => CoreUnitRules.SpecialCount(u) == 1),
            "boss items: a boss not picked lies without its part and never drops it; the rest of its kit stays");

        var pending = new List<(string Key, int X, int Y)>
        {
            (BossItemRules.BowKey, 10, 10), (BossItemRules.GreatSwordKey, 10, 10), (BossItemRules.GreatSwordKey, 20, 20)
        };
        Check(KitDropRules.MatchDrop(pending, BossItemRules.GreatSwordKey, 11, 9) == 1 && KitDropRules.MatchDrop(pending, BossItemRules.GreatSwordKey, 19, 20) == 2
                && KitDropRules.MatchDrop(pending, BossItemRules.BowKey, 13, 7) == 0 && KitDropRules.MatchDrop(pending, BossItemRules.BowKey, 14, 10) == -1
                && KitDropRules.MatchDrop(pending, BossItemRules.ShieldKey, 10, 10) == -1,
            "boss items: a spawned part belongs to the nearest noted drop of its key, up to three cells away");
        Check(KitDropRules.DropDurability(100, 0, true) == 75 && KitDropRules.DropDurability(100, 1, true) == 95
                && KitDropRules.DropDurability(100, 7, true) == 95 && KitDropRules.DropDurability(0, 0.5, true) == 0,
            "boss items: a dropped boss part keeps 75 to 95 % of its durability, never full");
        Check(KitDropRules.DropDurability(100, 0, false) == 23 && KitDropRules.DropDurability(100, 1, false) == 34
                && KitDropRules.DropDurability(200, 0.5, false) == 57,
            "corpses: the rest of a bandit's dropped gear keeps 23 to 34 % of its durability");
    }

    sealed record SortCandidate(string Name, bool Gone, bool Downed);

    // The game's queue as the catalog shows it (ComparePriorityQueue): a binary heap of (candidate, insertion number), compared
    // by the comparison and on a tie by the number, smallest first. The comparison is the guard's: the gone rule for a pair with
    // a gone candidate, else the game's own (downed after standing).
    static List<SortCandidate> QueueOrder(SortCandidate[] insertOrder, Func<bool, bool, int> goneOrder)
    {
        int Compare(SortCandidate a, SortCandidate b)
        {
            if (TargetSortRules.SkipsGame(!a.Gone, !b.Gone)) return goneOrder(!a.Gone, !b.Gone);
            return a.Downed && !b.Downed ? 1 : !a.Downed && b.Downed ? -1 : 0;
        }
        bool Less((SortCandidate C, int N) x, (SortCandidate C, int N) y)
        {
            var c = Compare(x.C, y.C);
            return c != 0 ? c < 0 : x.N < y.N;
        }
        var heap = new List<(SortCandidate C, int N)>();
        for (var n = 0; n < insertOrder.Length; n++)
        {
            heap.Add((insertOrder[n], n));
            for (var i = heap.Count - 1; i > 0 && Less(heap[i], heap[(i - 1) / 2]); i = (i - 1) / 2)
                (heap[i], heap[(i - 1) / 2]) = (heap[(i - 1) / 2], heap[i]);
        }
        var result = new List<SortCandidate>();
        while (heap.Count > 0)
        {
            result.Add(heap[0].C);
            heap[0] = heap[^1];
            heap.RemoveAt(heap.Count - 1);
            for (var i = 0; ;)
            {
                int left = 2 * i + 1, right = left + 1, least = i;
                if (left < heap.Count && Less(heap[left], heap[least])) least = left;
                if (right < heap.Count && Less(heap[right], heap[least])) least = right;
                if (least == i) break;
                (heap[i], heap[least]) = (heap[least], heap[i]);
                i = least;
            }
        }
        return result;
    }

    // Standing ones first, then downed ones, gone ones last.
    static bool SortedRight(List<SortCandidate> order)
    {
        static int Rank(SortCandidate c) => c.Gone ? 2 : c.Downed ? 1 : 0;
        for (var i = 1; i < order.Count; i++)
            if (Rank(order[i - 1]) > Rank(order[i])) return false;
        return true;
    }

    static IEnumerable<SortCandidate[]> Permutations(SortCandidate[] items)
    {
        if (items.Length <= 1) { yield return items; yield break; }
        for (var i = 0; i < items.Length; i++)
        {
            var rest = items.Where((_, j) => j != i).ToArray();
            foreach (var tail in Permutations(rest)) yield return new[] { items[i] }.Concat(tail).ToArray();
        }
    }

    // The hooks of one session in order (boot, load, main menu and a new load, ...), each with whether the combat data is loaded
    // then, run as FalconCritInstall does; the count is how often the patch was put on.
    static int LatePatchInstalls(bool[] dataReadyAtHook)
    {
        var gate = new LatePatchGate();
        var installs = 0;
        foreach (var ready in dataReadyAtHook)
        {
            if (!gate.ShouldTry(ready)) continue;
            installs++;
            gate.Record(true);
        }
        return installs;
    }
}
