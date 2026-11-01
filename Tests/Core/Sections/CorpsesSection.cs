using DungeonSettlersDelvers.Core;
using static TestKit;

// Corpses: the burial question and the equipment on a body.
internal static class CorpsesSection
{
    internal static void Run()
    {
        BurialWarning();
        BurialConfirmation();
        UndressedCorpse();
        LeaveWarningCount();
        LeaveWarningOncePerHook();
        EquipmentStaysOnBody();
        UndressOptionOutsideProbes();
    }

    // The undress option is Core's work, so it never sits in a probe class: a probe class is patched only while its group is on
    // (the found no option at any corpse).
    static void UndressOptionOutsideProbes()
    {
        var core = FindCoreSourceDirectory();
        var path = core == null ? null : Path.Combine(core, "CorpsePatches.cs");
        Check(path != null && File.Exists(path), "corpses: CorpsePatches.cs stands in Core's sources");
        if (path == null || !File.Exists(path)) return;
        var code = File.ReadAllText(path);
        var classes = System.Text.RegularExpressions.Regex.Split(code, @"(?=\n(?:\[[^\n]*\]\s*\n)*internal static class )");
        var withOption = classes.Where(part => part.Contains("CorpseKeep.AddUndressOption(", StringComparison.Ordinal)).ToList();
        Check(withOption.Count == 1 && !withOption[0].Contains("[ProbeGroup", StringComparison.Ordinal),
            "corpses: the undress option is added by one patch class, and that one is no probe class");
    }

    static void BurialWarning()
    {
        // The confirm dialog before burying a dressed party corpse, only for ClearDeadUnit, switchable.
        Check(CorpseRules.BurialWarns(true, 9, true, 2) && !CorpseRules.BurialWarns(false, 9, true, 2) && !CorpseRules.BurialWarns(true, 8, true, 2)
                && !CorpseRules.BurialWarns(true, 9, false, 2) && !CorpseRules.BurialWarns(true, 9, true, 0)
                && CorpseRules.BurialWarningKey == "corpse.burial.warning" && ReviveText.BurialNotice.AllLanguages.All(t => !string.IsNullOrWhiteSpace(t) && !t.Contains('-') && !t.Contains('\u2013') && !t.Contains('\u2014'))
                && ReviveText.MenuBurialWarning.AllLanguages.All(t => !string.IsNullOrWhiteSpace(t) && !t.Contains('-') && !t.Contains('\u2013') && !t.Contains('\u2014'))
                && CorpseRules.BurialLine("shown", "c", 2) == "CORE_CORPSE_BURIAL_WARNING result=shown corpse=c equipped=2"
                && CorpseRules.BurialLine("busy", "c", 2, "task") == "CORE_CORPSE_BURIAL_WARNING result=busy corpse=c equipped=2 way=task"
                && CorpseRules.BurialBusy(true, 10, 20) && !CorpseRules.BurialBusy(false, 10, 20) && !CorpseRules.BurialBusy(true, 10, 10 + CorpseRules.BurialQuestionSeconds),
            "burial warning: only ClearDeadUnit of a dressed party corpse with the setting on; texts in 10 languages without dashes");
    }

    static void BurialConfirmation()
    {
        // The burial question's checks on yes: same campaign, the target still on the map, the user still carries the same dressed corpse.
        Check(CorpseRules.BurialStillValid(true, true, true, true, 2) && !CorpseRules.BurialStillValid(false, true, true, true, 2)
                && !CorpseRules.BurialStillValid(true, false, true, true, 2) && !CorpseRules.BurialStillValid(true, true, false, true, 2)
                && !CorpseRules.BurialStillValid(true, true, true, false, 2) && !CorpseRules.BurialStillValid(true, true, true, true, 0),
            "burial warning: yes runs the order only in the same campaign, with the target there and the same dressed corpse carried");
    }

    static void UndressedCorpse()
    {
        // Equipment on the body, step 2: undress a lying corpse; bandits once; the list in Core's block.
        var undressCampaign = Guid.NewGuid();
        var undressFirst = Guid.NewGuid();
        var undressSecond = Guid.NewGuid();
        var undressData = CoreSaveData.Create(undressCampaign);
        undressData.SetUndressed(undressFirst, true);
        undressData.SetUndressed(undressSecond, true);
        undressData.SetUndressed(undressFirst, true);
        var undressBack = CoreSaveData.Parse(undressData.Serialize(), undressCampaign);
        var undressKept = undressBack != null && undressBack.IsUndressed(undressFirst) && undressBack.IsUndressed(undressSecond) && undressBack.UndressedUnits().Count == 2;
        undressBack?.SetUndressed(undressFirst, false);
        undressBack?.SetUndressed(undressSecond, false);
        var undressGuard = CoreUnitRules.Units.First(undressUnit => undressUnit.Role == "guard");
        Check(undressKept && undressBack != null && !undressBack.IsUndressed(undressFirst) && undressBack.UndressedUnits().Count == 0 && !undressBack.Serialize().Contains("undressed")
                && CorpseRules.CanUndress(true, false, true, 3, false, false, true) && !CorpseRules.CanUndress(true, true, true, 3, false, false, true)
                && !CorpseRules.CanUndress(true, false, true, 0, false, false, true) && !CorpseRules.CanUndress(false, false, true, 3, false, false, true)
                && CorpseRules.CanUndress(true, false, false, 3, true, false, true) && !CorpseRules.CanUndress(true, false, false, 3, true, true, true)
                && !CorpseRules.CanUndress(true, false, false, 3, false, false, true)
                && !CorpseRules.CanUndress(true, false, true, 3, false, false, false) && !CorpseRules.CanUndress(true, false, false, 3, true, false, false)
                && CorpseRules.UndressLine("c", "u", "bandit", 3, "A+B+C") == "CORE_CORPSE_UNDRESS unit=c by=u kind=bandit items=3 keys=A+B+C"
                && CoreUnitRules.EquipInfo(undressGuard, true).TrueForAll(undressSlot => undressSlot.Item == CoreUnitRules.EmptySlot)
                && CoreUnitRules.EquipInfo(undressGuard, false).Exists(undressSlot => undressSlot.Item != CoreUnitRules.EmptySlot)
                && CorpseText.All().All(corpseText => corpseText.AllLanguages.Count == 10 && corpseText.AllLanguages.All(corpseWords => !string.IsNullOrWhiteSpace(corpseWords) && !corpseWords.Contains('-')))
                && CorpseText.Undress.German == "Ausrüstung ausziehen" && CorpseText.UndressFirst.German == "Erst ausziehen"
                && CorpseRules.LeaveWarns(1, true) && !CorpseRules.LeaveWarns(1, false) && !CorpseRules.LeaveWarns(0, true)
                && CorpseRules.LeaveWarningLine("dungeon-portal", "Dungeon", 1, 2, 77) == "CORE_CORPSE_LEAVE_WARNING hook=dungeon-portal map=Dungeon dressed=3 party=1 bandits=2 tick=77"
                && CorpseText.LeaveWarningKey.StartsWith("TEXTKEY_HelpNotice_", StringComparison.Ordinal),
            "corpse step 2: a lying corpse is undressed once; Core's block keeps the undressed bandits; texts in 10 languages");
    }

    static void LeaveWarningCount()
    {
        // Step 3, the count behind the leave notice: dressed fallen of the party plus bandit corpses not undressed. A living member and a dead
        // one without equipment (undressed) do not count; neither does a bandit corpse of another map, a gone unit or an undressed corpse.
        var dressedDead = new[] { (true, 2) };
        var undressedDead = new[] { (true, 0) };
        var living = new[] { (false, 3) };
        var mixedParty = new[] { (true, 1), (false, 4), (true, 0), (true, 3), (false, 0) };
        var mixedBandits = new[] { (true, false), (true, true), (false, false), (false, true), (true, false) };
        Check(CorpseRules.DressedFallenParty(dressedDead) == 1 && CorpseRules.DressedFallenParty(undressedDead) == 0
                && CorpseRules.DressedFallenParty(living) == 0 && CorpseRules.DressedFallenParty(mixedParty) == 2
                && CorpseRules.DressedFallenParty(Array.Empty<(bool, int)>()) == 0 && CorpseRules.DressedFallenParty(null) == 0,
            "leave warning count: a dead party member counts only while something is still in its equipment slots");
        Check(CorpseRules.DressedBanditCorpses(new[] { (true, false) }) == 1 && CorpseRules.DressedBanditCorpses(new[] { (true, true) }) == 0
                && CorpseRules.DressedBanditCorpses(new[] { (false, false) }) == 0 && CorpseRules.DressedBanditCorpses(mixedBandits) == 2
                && CorpseRules.DressedBanditCorpses(Array.Empty<(bool, bool)>()) == 0 && CorpseRules.DressedBanditCorpses(null) == 0,
            "leave warning count: a bandit corpse counts only on the left map and until it is undressed");
        Check(!CorpseRules.LeaveWarns(CorpseRules.DressedFallenParty(undressedDead) + CorpseRules.DressedBanditCorpses(new[] { (true, true) }), true)
                && CorpseRules.LeaveWarns(CorpseRules.DressedFallenParty(mixedParty) + CorpseRules.DressedBanditCorpses(mixedBandits), true)
                && !CorpseRules.LeaveWarns(CorpseRules.DressedFallenParty(mixedParty) + CorpseRules.DressedBanditCorpses(mixedBandits), false),
            "leave warning: nothing is left behind once everyone is undressed; with something left it shows on a dungeon map only");
    }

    static void LeaveWarningOncePerHook()
    {
        // One warning for one call of a hook: LeaveWarning shows the notice once, only behind the decision, with one log line, and each
        // of the two hooks calls it once (the game run counts the lines per click). Read from Core's source.
        var coreDirectory = FindCoreSourceDirectory();
        var keep = coreDirectory == null ? "" : File.ReadAllText(Path.Combine(coreDirectory, "CorpseKeep.cs")).Replace("\r\n", "\n");
        var start = keep.IndexOf("internal static void LeaveWarning(", StringComparison.Ordinal);
        var end = start < 0 ? -1 : keep.IndexOf("\n    }", start, StringComparison.Ordinal);
        var body = start < 0 || end < 0 ? "" : keep.Substring(start, end - start);
        var decision = body.IndexOf("CorpseRules.LeaveWarns(", StringComparison.Ordinal);
        var notice = body.IndexOf("CoreGame.Apply(", StringComparison.Ordinal);
        var callsPerFile = new List<string>();
        var callCount = 0;
        var patches = "";
        foreach (var file in coreDirectory == null ? Array.Empty<string>() : Directory.GetFiles(coreDirectory, "*.cs"))
        {
            var text = File.ReadAllText(file).Replace("\r\n", "\n");
            var calls = Occurrences(text, "CorpseKeep.LeaveWarning(");
            if (calls == 0) continue;
            callCount += calls;
            callsPerFile.Add(Path.GetFileName(file) + ":" + calls);
            if (Path.GetFileName(file) == "CorpsePatches.cs") patches = text;
        }
        Check(body.Length > 0 && Occurrences(body, "CoreGame.Apply(") == 1 && Occurrences(body, "HelpNoticeApplyData(") == 1
                && Occurrences(body, "CorpseRules.LeaveWarningLine(") == 1 && decision >= 0 && decision < notice
                && Occurrences(body, "CorpseRules.DressedFallenParty(") == 1 && Occurrences(body, "CorpseRules.DressedBanditCorpses(") == 1,
            "leave warning: one notice and one log line per call, behind the decision, counted by the rule");
        Check(callCount == 2 && callsPerFile.Count == 1 && callsPerFile[0] == "CorpsePatches.cs:2"
                && Occurrences(patches, "CorpseKeep.LeaveWarning(\"dungeon-portal\", msg.MapType);") == 1
                && Occurrences(patches, "CorpseKeep.LeaveWarning(\"portal\", msg.Map);") == 1,
            "leave warning: called once by each of the two hooks (floor portal, portal interaction), nowhere else (" + string.Join(", ", callsPerFile) + ")");
    }

    static int Occurrences(string text, string needle)
    {
        var count = 0;
        for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    static void EquipmentStaysOnBody()
    {
        // Equipment stays on the body (step 1).
        Check(CorpseRules.KeepsEquipment(true, true, true) && !CorpseRules.KeepsEquipment(false, true, true)
                && !CorpseRules.KeepsEquipment(true, false, true) && !CorpseRules.KeepsEquipment(true, true, false)
                && CorpseRules.BanditDeathLoot.Length == 0 && CorpseRules.UndressTask == 9101
                && CorpseRules.Keys(new[] { "ITEM_WoodClub", "", null, "ITEM_ClothArmor" }) == "ITEM_WoodClub+ITEM_ClothArmor"
                && CorpseRules.Keys(Array.Empty<string>()) == "-" && CorpseRules.Keys(null) == "-"
                && CorpseRules.KeepLine("u", 2, "A+B") == "CORE_CORPSE_KEEP unit=u items=2 keys=A+B"
                && CorpseRules.BanditLine("b", "UNIT_X", "Dungeon", 3) == "CORE_CORPSE_BANDIT unit=b key=UNIT_X map=Dungeon kit=3"
                && CorpseRules.StatusLine("u", true, true, true, 3, "A+B+C", false, false, false)
                    == "unit=u found=1 dead=1 player=1 items=3 keys=A+B+C carried=0 undressed=0 listed=0"
                && CorpseRules.StatusLine("x", false, false, false, 0, "-", false, false, false) == "unit=x found=0"
                && CorpseRules.ProbeOptionLine("unit", "a", "b", true, false, 0, "Carry", 211, 7, true, "Carry \"X\"\n", null)
                    == "CORE_CORPSE_PROBE_OPTION where=unit user=a target=b dead=1 carries=0 index=0 type=Carry input=211 task=7 possible=1 text=\"Carry 'X' \" invalid=\"\""
                && CorpseRules.ProbeLeaveLine("portal", "Dungeon", 2, 1, 500) == "CORE_CORPSE_PROBE_LEAVE hook=portal map=Dungeon dead=2 dressed=1 tick=500",
            "corpse: a dying party member keeps its equipment (not on retirement), bandits drop no loot; lines for the Toolkit and the probe");
    }
}
