using System.Reflection;
using System.Text.RegularExpressions;
using DungeonSettlersDelvers.Core;
using static TestKit;

// The diagnostics groups (CoreProbeRules): the twelve keys, off by default, an unknown group off, each key switching its own group
// only, the probe attribute and the plan of what to patch or take back, and that every marked class names a known group.
internal static class CoreProbeRulesSection
{
    static readonly string[] ExpectedKeys =
    {
        "probes.commands", "probes.hud", "probes.wound", "probes.tint", "probes.bossbar",
        "probes.areas", "probes.seeds", "probes.corpse", "probes.text", "probes.oneshot", "probes.items", "probes.effects"
    };

    // Stand-ins for probe classes: one per group used, one without a mark and one with a group that does not exist.
    [ProbeGroup(CoreProbeRules.Commands)]
    sealed class CommandsProbe { }

    [ProbeGroup(CoreProbeRules.Hud)]
    sealed class HudProbe { }

    [ProbeGroup(CoreProbeRules.Hud)]
    sealed class SecondHudProbe { }

    [ProbeGroup("probes.nothing")]
    sealed class UnknownGroupProbe { }

    sealed class OrdinaryPatch { }

    // The id Core's own patches have under BepInEx (DelversCoreRuntime.BepInExPluginId); the probe classes must not share it.
    const string CoreHarmonyId = "fre-yin.dungeonsettlers.delvers.core";

    static readonly Type[] AllTypes =
    {
        typeof(OrdinaryPatch), typeof(CommandsProbe), typeof(HudProbe), typeof(UnknownGroupProbe), typeof(SecondHudProbe)
    };

    internal static void Run()
    {
        TheTable();
        Defaults();
        EachKeySwitchesItsGroupOnly();
        ValuesOfTheSettingsFile();
        TheAttributeSelectsTypes();
        ThePlanFollowsTheGroups();
        MarkedClassesNameKnownGroups();
        EveryMarkedClassIsAPatchClass();
        TheProbeClassesOfEachFile();
        TheCommandsGroupKeepsThePlayersOrder();
        TheHudGroupReadsItsFlag();
        TheWoundGroupReadsItsFlag();
        TheShellFollowsTheRules();
        TheTintProbesRunOnlyWithTheirGroup();
        TheBossBarProbeRunsOnlyWithItsGroup();
        TheFrameAndVfxProbeClassesNameTheirGroups();
        TheSeedProbesAskTheirGroupFirst();
        TheBladeAndLifeBurstProbeLines();
        TheEventProbeClassesNameTheirGroups();
        TheEventProbeEntriesAskTheirGroupFirst();
        TheCorpseGroupKeepsTheLeaveWarning();
        TheBanditProbesLeaveTheFightAlone();
        TheBandageIconPatchKeepsItsWork();
        TheBodyBloodLinesAskTheGroupWound();
        TheHealerProbesLeaveTheHealerAlone();
        TheOwnMarkLookProbesAreGone();
    }

    // The settings file stands in for the game's settings: a new file in a folder of its own, with the given JSON when there is some.
    static void WithSettings(string json, Action<CoreSettingsFile> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "delvers-probes-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "settings.json");
            if (json != null) File.WriteAllText(path, json);
            test(new CoreSettingsFile(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // All twelve keys, each once, each under probes., the table holds nothing else and every group constant is in it.
    static void TheTable()
    {
        Check(CoreProbeRules.Groups.SequenceEqual(ExpectedKeys), "probe groups: the table holds the twelve keys of the groups");
        Check(CoreProbeRules.Groups.Distinct().Count() == CoreProbeRules.Groups.Length
                && CoreProbeRules.Groups.All(key => key.StartsWith(CoreProbeRules.KeyPrefix, StringComparison.Ordinal) && key.Length > CoreProbeRules.KeyPrefix.Length),
            "probe groups: every key is there once and begins with probes.");
        var constants = new List<string>();
        foreach (var field in typeof(CoreProbeRules).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            if (field.IsLiteral && field.GetRawConstantValue() is string value && value.StartsWith(CoreProbeRules.KeyPrefix, StringComparison.Ordinal)
                && value != CoreProbeRules.KeyPrefix)
                constants.Add(value);
        Check(constants.OrderBy(key => key, StringComparer.Ordinal).SequenceEqual(CoreProbeRules.Groups.OrderBy(key => key, StringComparer.Ordinal)),
            "probe groups: every key constant of the rules is in the table and the table has no other key");
        Check(ExpectedKeys.All(CoreProbeRules.IsGroup) && !CoreProbeRules.IsGroup("probes.nothing") && !CoreProbeRules.IsGroup("probes.")
                && !CoreProbeRules.IsGroup("Probes.Commands") && !CoreProbeRules.IsGroup("") && !CoreProbeRules.IsGroup(null),
            "probe groups: only the twelve keys, spelled exactly, are groups");
    }

    // Without a key every group is off, through the real reader of the settings file; an unknown group is off even when asked as on.
    static void Defaults()
    {
        Check(!CoreProbeRules.DefaultOn, "probe groups: the default of every group is off");
        WithSettings(null, settings =>
        {
            Check(CoreProbeRules.Groups.All(group => !CoreProbeRules.GroupOn(group, settings.Get)) && CoreProbeRules.OnGroups(settings.Get).Count == 0,
                "probe groups: with no settings file every group is off");
        });
        WithSettings("{\"hints.enabled\": true}", settings =>
        {
            Check(CoreProbeRules.OnGroups(settings.Get).Count == 0, "probe groups: other keys in the settings file leave every group off");
        });
        Func<string, bool, bool> everythingOn = (key, fallback) => true;
        Check(!CoreProbeRules.GroupOn("probes.nothing", everythingOn) && !CoreProbeRules.GroupOn(null, everythingOn)
                && !CoreProbeRules.GroupOn("", everythingOn) && !CoreProbeRules.GroupOn("Probes.Hud", everythingOn)
                && CoreProbeRules.OnGroups(everythingOn).Count == ExpectedKeys.Length,
            "probe groups: an unknown group is off even when every key reads on, and a known one is on");
    }

    // Setting one key alone puts exactly its group on.
    static void EachKeySwitchesItsGroupOnly()
    {
        var wrong = new List<string>();
        foreach (var key in ExpectedKeys)
        {
            WithSettings("{\"" + key + "\": 1}", settings =>
            {
                var on = CoreProbeRules.OnGroups(settings.Get);
                if (on.Count != 1 || on[0] != key) wrong.Add(key);
            });
        }
        Check(wrong.Count == 0, "probe groups: each key puts its own group on and no other (" + string.Join(", ", wrong) + ")");
        WithSettings("{\"probes.hud\": 1, \"probes.corpse\": true, \"probes.wound\": 0}", settings =>
        {
            Check(CoreProbeRules.OnGroups(settings.Get).SequenceEqual(new[] { CoreProbeRules.Hud, CoreProbeRules.Corpse }),
                "probe groups: two keys on, one off: the groups on are those two, in the order of the table");
        });
    }

    // The same forms as every Core switch: 1 and true are on (number, boolean or text), 0 and false are off, anything else keeps off.
    static void ValuesOfTheSettingsFile()
    {
        WithSettings("{\"probes.commands\": 1, \"probes.hud\": true, \"probes.wound\": \"1\", \"probes.tint\": \"TRUE\", \"probes.bossbar\": 0,"
            + " \"probes.areas\": false, \"probes.seeds\": \"0\", \"probes.corpse\": 2, \"probes.text\": \"yes\"}", settings =>
        {
            Check(CoreProbeRules.OnGroups(settings.Get).SequenceEqual(new[] { CoreProbeRules.Commands, CoreProbeRules.Hud, CoreProbeRules.Wound, CoreProbeRules.Tint }),
                "probe groups: 1, true and their texts switch a group on; 0, false, another number and another text leave it off");
        });
        WithSettings("{ broken", settings =>
        {
            Check(CoreProbeRules.OnGroups(settings.Get).Count == 0, "probe groups: an unreadable settings file leaves every group off");
        });
    }

    // The attribute marks the class; the plan of Core's own classes skips marked ones, a marked class needs no known group to be skipped.
    static void TheAttributeSelectsTypes()
    {
        Check(CoreProbeRules.GroupOf(typeof(CommandsProbe)) == "probes.commands" && CoreProbeRules.GroupOf(typeof(HudProbe)) == "probes.hud"
                && CoreProbeRules.GroupOf(typeof(UnknownGroupProbe)) == "probes.nothing"
                && CoreProbeRules.GroupOf(typeof(OrdinaryPatch)) == null && CoreProbeRules.GroupOf(null) == null,
            "probe classes: the attribute names the group of a class, an ordinary class and null have none");
        Check(CoreProbeRules.WithoutProbeClasses(AllTypes).SequenceEqual(new[] { typeof(OrdinaryPatch) }),
            "probe classes: the patch plan keeps only the ordinary classes, also one with an unknown group is left out");
        Check(CoreProbeRules.ProbeClasses(AllTypes).SequenceEqual(new[] { typeof(CommandsProbe), typeof(HudProbe), typeof(UnknownGroupProbe), typeof(SecondHudProbe) }),
            "probe classes: the marked classes are found in their order");
        Check(CoreProbeRules.WithoutProbeClasses(Array.Empty<Type>()).Count == 0 && CoreProbeRules.ProbeClasses(new Type[] { null }).Count == 0
                && CoreProbeRules.WithoutProbeClasses(new Type[] { null }).Count == 1,
            "probe classes: no types, no result; null is no probe class");
    }

    // The state moves with the groups: switched on is patched once, switched off is taken back, a failed class waits for its group to go off.
    static void ThePlanFollowsTheGroups()
    {
        var probes = CoreProbeRules.ProbeClasses(AllTypes);
        var none = new HashSet<Type>();
        var hudOn = new[] { CoreProbeRules.Hud };
        var first = CoreProbeRules.Plan(probes, hudOn, none, none);
        Check(first.Patch.SequenceEqual(new[] { typeof(HudProbe), typeof(SecondHudProbe) }) && first.Unpatch.Count == 0 && first.Forgive.Count == 0,
            "probe plan: a group switched on patches its classes and no others, the unknown group's class stays off");
        var patched = new HashSet<Type> { typeof(HudProbe), typeof(SecondHudProbe) };
        var again = CoreProbeRules.Plan(probes, hudOn, patched, none);
        Check(again.Patch.Count == 0 && again.Unpatch.Count == 0, "probe plan: classes that are patched are not patched again while their group stays on");
        var off = CoreProbeRules.Plan(probes, Array.Empty<string>(), patched, none);
        Check(off.Patch.Count == 0 && off.Unpatch.SequenceEqual(new[] { typeof(HudProbe), typeof(SecondHudProbe) }),
            "probe plan: a group switched off takes its patched classes back");
        var switched = CoreProbeRules.Plan(probes, new[] { CoreProbeRules.Commands }, patched, none);
        Check(switched.Patch.SequenceEqual(new[] { typeof(CommandsProbe) }) && switched.Unpatch.Count == 2,
            "probe plan: one group off and another on in one step patches the one and takes back the other");
        var failed = new HashSet<Type> { typeof(HudProbe) };
        var waiting = CoreProbeRules.Plan(probes, hudOn, new HashSet<Type> { typeof(SecondHudProbe) }, failed);
        Check(waiting.Patch.Count == 0 && waiting.Unpatch.Count == 0 && waiting.Forgive.Count == 0,
            "probe plan: a failed class is not tried again while its group stays on");
        var forgiven = CoreProbeRules.Plan(probes, Array.Empty<string>(), none, failed);
        Check(forgiven.Forgive.SequenceEqual(new[] { typeof(HudProbe) }) && forgiven.Patch.Count == 0 && forgiven.Unpatch.Count == 0,
            "probe plan: a failed class is forgiven when its group goes off, so switching it on again tries it again");
        Check(CoreProbeRules.Plan(Array.Empty<Type>(), CoreProbeRules.Groups, none, none).Patch.Count == 0,
            "probe plan: without probe classes there is nothing to do");
    }

    // A [ProbeGroup(...)] in Core's sources names a group constant of CoreProbeRules, never a text or an unknown name.
    static void MarkedClassesNameKnownGroups()
    {
        Check(UnknownGroupArguments("[ProbeGroup(CoreProbeRules.Commands)]\ninternal static class A { }").Count == 0
                && UnknownGroupArguments("[ProbeGroup(CoreProbeRules.Nothing)]\nclass A { }").Count == 1
                && UnknownGroupArguments("[ProbeGroup(\"probes.commands\")]\nclass A { }").Count == 1
                && UnknownGroupArguments("[ProbeGroup(CoreProbeRules.Hud)] class A { } [ProbeGroup(Other.Hud)] class B { }").Count == 1
                && UnknownGroupArguments("// [ProbeGroup(CoreProbeRules.Nothing)]\nclass A { }").Count == 0,
            "probe marks: the rule finds a mark with a text or an unknown name and accepts the group constants and comments");
        var coreFiles = SourceTree.Current.Files(SourceFolders.Core);
        var offenders = new List<string>();
        foreach (var file in coreFiles)
        {
            foreach (var argument in UnknownGroupArguments(file.Raw)) offenders.Add(file.Name + ":" + argument);
        }
        Check(coreFiles.Count > 0 && offenders.Count == 0,
            "every probe class of Core carries a known group of CoreProbeRules (" + string.Join(", ", offenders) + ")");
    }

    // The probe classes of Core's files, class by class: the file, its group and the patch classes the file holds. The assignment is
    // what the QA toolkit's table of groups relies on (which log line is in which group); a class that moves to another group or
    // loses its mark turns a check red here. Each step that moves probes behind their groups adds its rows.
    static readonly (string File, string Group, string[] Classes)[] ProbeClassesByFile =
    {
        ("GroupAiOrderSource.cs", CoreProbeRules.Commands, new[]
        {
            "GroupAiOrderLeftClickPatch", "GroupAiOrderRightClickPatch", "GroupAiOrderTryMovePatch", "GroupAiOrderNearPatch",
            "GroupAiOrderMovePatch", "GroupAiOrderFormationPatch", "GroupAiOrderWaypointPatch", "GroupAiOrderEnqueuePatch"
        }),
        ("BanditTintProbe.cs", CoreProbeRules.Hud, new[]
        {
            "BanditHudProgressPatch", "BanditHudUnsetPatch", "BanditHudSkillPatch", "BanditHudReservePatch"
        }),
        ("WoundProbe.cs", CoreProbeRules.Wound, new[] { "WoundProbeStatusPatch", "WoundProbeTooltipPatch" }),
        ("TooltipTextProbe.cs", CoreProbeRules.Text, new[] { "TooltipTextProbePatch" }),
        ("BanditCampProbe.cs", CoreProbeRules.OneShot, new[]
        {
            "BanditCampProbeSetDungeonPatch", "BanditCampProbePreparedPatch", "BanditCampProbeRoomsPatch"
        }),
        ("BossItemRepairProbe.cs", CoreProbeRules.Items, new[]
        {
            "BossItemRepairProbeOrder", "BossItemRepairProbeCheckItems", "BossItemRepairProbeNeed", "BossItemRepairProbeCanRepair"
        }),
        ("BanditEffectProbe.cs", CoreProbeRules.Effects, new[] { "BanditEffectProbePatch" }),
        ("SummonProbe.cs", CoreProbeRules.Effects, new[] { "SummonProbeDamagePatch" }),
    };

    // A class of a source file with what its attributes say: the group of [ProbeGroup(CoreProbeRules.X)] (null without one) and
    // whether it is a Harmony patch class.
    sealed record ClassMarks(string Name, string Group, bool Patch);

    // The top-level classes of a source (comments and strings blanked), each with its marks. The attributes of a class are the text
    // between the closing brace of the class before and its own head.
    static List<ClassMarks> ClassesOf(string code)
    {
        var classes = new List<ClassMarks>();
        var from = 0;
        foreach (Match head in Regex.Matches(code, @"(?m)^(?:internal|public)\s+(?:static\s+|sealed\s+|abstract\s+)*class\s+(?<name>\w+)"))
        {
            var before = code.Substring(from, head.Index - from);
            var closing = before.LastIndexOf("\n}", StringComparison.Ordinal);
            var attributes = closing < 0 ? before : before.Substring(closing + 2);
            var group = Regex.Match(attributes, @"\[ProbeGroup\(\s*CoreProbeRules\.(?<name>\w+)\s*\)\]");
            var key = group.Success
                ? typeof(CoreProbeRules).GetField(group.Groups["name"].Value, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetRawConstantValue() as string
                : null;
            classes.Add(new ClassMarks(head.Groups["name"].Value, group.Success ? key ?? "?" : null, attributes.Contains("[HarmonyPatch(", StringComparison.Ordinal)));
            from = head.Index;
        }
        return classes;
    }

    // Only a Harmony patch class is marked: CoreProbes hands every marked class to Harmony, and a helper class without patches has none.
    static void EveryMarkedClassIsAPatchClass()
    {
        const string stand = "[ProbeGroup(CoreProbeRules.Hud)]\n[HarmonyPatch(typeof(A), \"M\")]\ninternal static class P { }\n\n"
            + "[ProbeGroup(CoreProbeRules.Hud)]\ninternal static class Helper { }\n\ninternal static class Plain { }\n";
        Check(ClassesOf(stand).Select(c => c.Name + ":" + c.Group + ":" + c.Patch).SequenceEqual(new[] { "P:probes.hud:True", "Helper:probes.hud:False", "Plain::False" }),
            "probe marks: the class reader sees the group and the patch attribute of each class and keeps the attributes of one class to it");
        var offenders = new List<string>();
        foreach (var file in SourceTree.Current.Files(SourceFolders.Core))
            foreach (var each in ClassesOf(file.Code))
                if (each.Group != null && !each.Patch) offenders.Add(file.Name + ":" + each.Name);
        Check(offenders.Count == 0, "every class marked [ProbeGroup] in Core is a Harmony patch class (" + string.Join(", ", offenders) + ")");
    }

    // For each file of the table: the file's patch classes are exactly the listed ones and every one of them carries its group.
    static void TheProbeClassesOfEachFile()
    {
        var files = SourceTree.Current.Files(SourceFolders.Core);
        Check(files.Count > 0, "probe classes by file: Core's sources are found");
        foreach (var row in ProbeClassesByFile)
        {
            var file = files.FirstOrDefault(each => each.Name == row.File);
            if (file == null)
            {
                Check(false, "probe classes by file: " + row.File + " stands in Core's sources");
                continue;
            }
            var patchClasses = new List<string>();
            var wrongGroup = new List<string>();
            foreach (var each in ClassesOf(file.Code))
            {
                if (!each.Patch) continue;
                patchClasses.Add(each.Name);
                if (each.Group != row.Group) wrongGroup.Add(each.Name + " in " + (each.Group ?? "no group"));
            }
            Check(patchClasses.OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(row.Classes.OrderBy(name => name, StringComparer.Ordinal)),
                "probe classes by file: " + row.File + " holds the patch classes " + string.Join(", ", row.Classes) + " and no other (" + string.Join(", ", patchClasses) + ")");
            Check(wrongGroup.Count == 0,
                "probe classes by file: every patch class of " + row.File + " carries [ProbeGroup] with " + row.Group + " (" + string.Join(", ", wrongGroup) + ")");
        }
    }

    // What the commands group switches is the log: the player's order reaches the AI's quiet time without the probe (CORE_AI_ORDER
    // is written by NotePlayerOrder, which reads no flag), and the order source's own entry point reads the flag first.
    static void TheCommandsGroupKeepsThePlayersOrder()
    {
        var core = FindCoreSourceDirectory();
        var source = Source(core, "GroupAiOrderSource.cs");
        var patches = Source(core, "GroupAiPatches.cs");
        var stance = Source(core, "GroupAiStance.cs");
        Check(source != null && patches != null && stance != null, "commands group: the order source, the patches and the stance stand in Core's sources");
        if (source == null || patches == null || stance == null) return;
        var take = PatchPlanSection.MethodBody(source, "internal static string Take(IntPtr task)") ?? "";
        Check(Regex.IsMatch(source, @"private static bool Active => CoreProbes\.Commands && GroupAiSettings\.Enabled;")
                && Regex.IsMatch(take, @"^\{\s*if \(!Active\) return null;")
                && !source.Contains("Diagnostic", StringComparison.Ordinal),
            "commands group: the order source is active only with the group on and the group AI on, and Take reads that first");
        Check(Regex.IsMatch(patches, @"GroupAiStance\.NotePlayerOrder\(unit, task\.GetIl2CppType\(\)\.Name, GroupAiOrderSource\.Take\(task\.Pointer\)\)"),
            "commands group: the direct order patch still passes a player's order to the stance, with the order source's tag when there is one");
        var note = PatchPlanSection.MethodBody(stance, "internal static void NotePlayerOrder(") ?? "";
        Check(note.Contains("LastPlayerOrder = GameClock.Now", StringComparison.Ordinal) && note.Contains("StanceOrderLine(", StringComparison.Ordinal)
                && !note.Contains("CoreProbes", StringComparison.Ordinal) && note.Contains("via ?? \"-\"", StringComparison.Ordinal),
            "commands group: the player's order starts the quiet time and writes CORE_AI_ORDER without a probe flag, via= is - without a tag");
    }

    // The HUD probes log only: each entry point that the scan or a patch calls reads the group's flag before anything else, and the
    // old constant is gone.
    static void TheHudGroupReadsItsFlag()
    {
        var source = Source(FindCoreSourceDirectory(), "BanditTintProbe.cs");
        Check(source != null, "HUD group: the tint probe file stands in Core's sources");
        if (source == null) return;
        var entries = new[]
        {
            "internal static void Set(UnitEntityHUD", "internal static void Progress(UnitEntityHUD", "internal static void Unset(UnitEntityHUD",
            "internal static void CheckActive(UnitEntityHUD", "internal static void CheckStuck(UnitEntityHUD", "internal static void Note(UnitEntityHUD"
        };
        var unguarded = new List<string>();
        foreach (var entry in entries)
        {
            var body = PatchPlanSection.MethodBody(source, entry) ?? "";
            if (!Regex.IsMatch(body, @"^\{\s*if \(!CoreProbes\.Hud\b")) unguarded.Add(entry.Substring(entry.IndexOf("void ", StringComparison.Ordinal) + 5));
        }
        Check(unguarded.Count == 0 && !source.Contains("HudDiagnostic", StringComparison.Ordinal),
            "HUD group: every HUD probe entry point reads CoreProbes.Hud first and no HudDiagnostic constant is left (" + string.Join(", ", unguarded) + ")");
    }

    // The wound probe's clock is the one entry point the campaign's second clock calls (ReviveCampaign): it reads the group's flag
    // before its own timer, and the campaign's call stays as it is.
    static void TheWoundGroupReadsItsFlag()
    {
        var core = FindCoreSourceDirectory();
        var probe = Source(core, "WoundProbe.cs");
        var campaign = Source(core, "ReviveCampaign.cs");
        Check(probe != null && campaign != null, "wound group: the wound probe and the campaign clock stand in Core's sources");
        if (probe == null || campaign == null) return;
        var clock = PatchPlanSection.MethodBody(probe, "internal static void Clock()") ?? "";
        Check(Regex.IsMatch(clock, @"^\{\s*if \(!CoreProbes\.Wound\) return;"),
            "wound group: the wound probe's clock reads CoreProbes.Wound as its first statement");
        Check(campaign.Contains("Guard(\"wound-probe\", WoundProbe.Clock);", StringComparison.Ordinal),
            "wound group: the campaign clock still calls the wound probe's clock, which decides by the flag");
    }

    // The arguments of [ProbeGroup(...)] in the source that are not CoreProbeRules.<group constant>.
    static List<string> UnknownGroupArguments(string source)
    {
        var offenders = new List<string>();
        var code = SourceText.Strip(source.Replace("\r\n", "\n"), blankStrings: true);
        foreach (Match mark in Regex.Matches(code, @"\[ProbeGroup\(\s*(?<argument>[^)]*?)\s*\)\]"))
        {
            var argument = mark.Groups["argument"].Value;
            var name = Regex.Match(argument, @"^CoreProbeRules\.(?<name>\w+)$");
            if (!name.Success || !IsGroupConstant(name.Groups["name"].Value)) offenders.Add(argument);
        }
        return offenders;
    }

    static bool IsGroupConstant(string name)
        => typeof(CoreProbeRules).GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) is { IsLiteral: true } field
            && field.GetRawConstantValue() is string value && CoreProbeRules.IsGroup(value);

    // The game shell cannot run here, so its source is read: the flags cover the twelve groups, the patch plan leaves the marked classes
    // to CoreProbes, CoreProbes patches through the plan with an id of its own, the failure is optional and written in one place,
    // and the three hooks (start, campaign start, shutdown) are in their places.
    static void TheShellFollowsTheRules()
    {
        var core = FindCoreSourceDirectory();
        var probes = Source(core, "CoreProbes.cs");
        var lifecycle = Source(core, "DelversCoreRuntimeLifecycle.cs");
        var difficulty = Source(core, "ApocalypseDifficulty.cs");
        Check(probes != null && lifecycle != null && difficulty != null, "probe shell: CoreProbes, the runtime's life cycle and the difficulty stand in Core's sources");
        if (probes == null || lifecycle == null || difficulty == null) return;
        var flagged = new List<string>();
        foreach (Match flag in Regex.Matches(PatchPlanSection.MethodBody(probes, "private static void SetFlag(") ?? "", @"case CoreProbeRules\.(?<name>\w+):"))
            if (typeof(CoreProbeRules).GetField(flag.Groups["name"].Value, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetRawConstantValue() is string key)
                flagged.Add(key);
        Check(flagged.OrderBy(key => key, StringComparer.Ordinal).SequenceEqual(CoreProbeRules.Groups.OrderBy(key => key, StringComparer.Ordinal)),
            "probe shell: CoreProbes sets one flag for each of the twelve groups");
        var refresh = PatchPlanSection.MethodBody(probes, "internal static void Refresh(") ?? "";
        var apply = PatchPlanSection.MethodBody(probes, "private static void Apply(") ?? "";
        Check(refresh.Contains("CoreSettings.ProbeGroupsOn()", StringComparison.Ordinal) && refresh.Contains("CoreProbeRules.Plan(", StringComparison.Ordinal)
                && refresh.Contains("catch (Exception", StringComparison.Ordinal)
                && apply.Contains("PatchPlanRules.Apply(", StringComparison.Ordinal),
            "probe shell: Refresh reads the groups, plans by the rules, patches through the patch plan and never throws");
        var id = Regex.Match(probes, "HarmonyIdPrefix = \"(?<id>[^\"]+)\"");
        Check(id.Success && id.Groups["id"].Value != CoreHarmonyId && Regex.Matches(probes, @"new Harmony\(").Count == 1
                && probes.Contains("HarmonyIdPrefix + \".\" + type.FullName", StringComparison.Ordinal),
            "probe shell: every probe class gets a Harmony instance with an id of its own, not Core's");
        Check(CountOf(probes, "CORE_PROBE_PATCH_FAILED class=") == 1 && probes.Contains("optional=1", StringComparison.Ordinal),
            "probe shell: a probe class that cannot be patched is an optional warning, written in one place");
        Check((PatchPlanSection.MethodBody(lifecycle, "private static void PatchAllClasses(") ?? "").Contains("CoreProbeRules.WithoutProbeClasses(", StringComparison.Ordinal),
            "probe shell: Core's patch plan leaves the probe classes out");
        var initialize = PatchPlanSection.MethodBody(lifecycle, "public static void Initialize(") ?? "";
        var shutdown = PatchPlanSection.MethodBody(lifecycle, "public static void Shutdown(") ?? "";
        var ready = initialize.IndexOf("initialized = true;", StringComparison.Ordinal);
        var refreshed = initialize.IndexOf("CoreProbes.Refresh();", StringComparison.Ordinal);
        Check(refreshed >= 0 && ready > refreshed && CountOf(shutdown, "CoreProbes.Shutdown();") == 1,
            "probe shell: the probes are read before Core is ready and taken back in Shutdown");
        var start = PatchPlanSection.MethodBody(difficulty, "private static void StartCampaignRules(") ?? "";
        Check(start.TrimStart('{', ' ', '\r', '\n', '\t').StartsWith("CoreProbes.Refresh();", StringComparison.Ordinal),
            "probe shell: every campaign start, new or loaded, reads the groups first");
    }

    // The tint's probes (the HUD scan and the building probe) are called from BanditTint.Scan only while the group Tint is on. The
    // tint, the building bars and the rest of the scan stay outside that condition.
    static void TheTintProbesRunOnlyWithTheirGroup()
    {
        var tint = CoreFile("BanditTint.cs");
        Check(tint != null, "tint probes: BanditTint stands in Core's sources");
        if (tint == null) return;
        var scan = PatchPlanSection.MethodBody(tint.Code, "private static void Scan(") ?? "";
        Check(Regex.IsMatch(scan, @"if \(CoreProbes\.Tint\)\s*ProbeHuds\(") && CountOf(scan, "ProbeHuds(") == 1,
            "tint probes: Scan reads the HUD probe only while the group Tint is on");
        Check(Regex.IsMatch(scan, @"if \(CoreProbes\.Tint\)\s*\{[^{}]*BanditTintProbe\.Building\(") && CountOf(scan, "BanditTintProbe.Building(") == 1,
            "tint probes: Scan calls the building probe only while the group Tint is on");
        Check(!tint.Code.Contains("BanditTintProbe.Diagnostic", StringComparison.Ordinal)
                && scan.Contains("BanditBar.OnTracked(", StringComparison.Ordinal) && scan.Contains("Track(top, id, null)", StringComparison.Ordinal),
            "tint probes: the tint and the building bars hang on the scan, not on the probe switch");
        var probe = CoreFile("BanditTintProbe.cs");
        var wrong = new List<string>();
        if (probe == null) wrong.Add("BanditTintProbe.cs");
        else
        {
            foreach (var signature in new[] { "internal static void Source(", "internal static void Building(", "internal static void Hud(" })
                if (!FirstStatement(PatchPlanSection.MethodBody(probe.Code, signature) ?? "").StartsWith("if (!CoreProbes.Tint", StringComparison.Ordinal)) wrong.Add(signature);
            if (probe.Code.Contains("Diagnostic", StringComparison.Ordinal)) wrong.Add("a Diagnostic switch is left");
        }
        Check(wrong.Count == 0, "tint probes: the source, building and HUD lines ask the group Tint first (" + string.Join(", ", wrong) + ")");
    }

    // The boss bar probe is called from the tint's scan every quarter second, also on the title screen; its first statement is the flag
    // of the group BossBar, so the search for the game's boss HUD never starts while the group is off. The class is a probe only: the
    // boss bar is BanditBossBar, which nothing here touches.
    static void TheBossBarProbeRunsOnlyWithItsGroup()
    {
        var probe = CoreFile("BanditBossBarProbe.cs");
        Check(probe != null, "boss bar probe: BanditBossBarProbe stands in Core's sources");
        if (probe == null) return;
        var run = PatchPlanSection.MethodBody(probe.Code, "internal static void Run(") ?? "";
        Check(FirstStatement(run).StartsWith("if (!CoreProbes.BossBar", StringComparison.Ordinal)
                && run.IndexOf("FindObjectsOfTypeAll", StringComparison.Ordinal) > run.IndexOf("CoreProbes.BossBar", StringComparison.Ordinal),
            "boss bar probe: Run asks the group BossBar first, before it looks for the boss HUD");
        Check(!probe.Code.Contains("Diagnostic", StringComparison.Ordinal) && !probe.Code.Contains("BanditBossBar.", StringComparison.Ordinal),
            "boss bar probe: no compile-time switch, and the probe does not use the boss bar's own code");
    }

    // The probe patch classes of the frame and VFX probes and the group each one is marked with. A class that is in this list and
    // loses its mark or carries another group is red; the classes of other groups are the business of their own steps.
    static readonly (string Class, string Group)[] FrameAndVfxProbeClasses =
    {
        ("GroupAiAreaProbeCreatePatch", CoreProbeRules.Areas),
        ("GroupAiAreaProbeDurationPatch", CoreProbeRules.Areas),
        ("GroupAiHealerSeedYieldPatch", CoreProbeRules.Seeds),
        ("GroupAiHealerSeedPushPatch", CoreProbeRules.Seeds),
        ("GroupAiHealerSeedProbePatch", CoreProbeRules.Seeds),
    };

    static void TheFrameAndVfxProbeClassesNameTheirGroups()
    {
        Check(MarkedClassesOf("[ProbeGroup(CoreProbeRules.Areas)]\n[HarmonyPatch(typeof(A), nameof(A.B))]\ninternal static class X { }\n"
                    + "[HarmonyPatch(typeof(A), \"C\")]\ninternal static class Unmarked { }\n[ProbeGroup(CoreProbeRules.Seeds)] sealed class Y { }")
                .SequenceEqual(new[] { ("X", CoreProbeRules.Areas), ("Y", CoreProbeRules.Seeds) }),
            "probe marks: the reader finds a marked class behind other attributes and leaves an unmarked one alone");
        var marked = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in SourceTree.Current.Files(SourceFolders.Core))
            foreach (var (name, group) in MarkedClassesOf(file.Code)) marked[name] = group;
        var wrong = new List<string>();
        foreach (var (name, group) in FrameAndVfxProbeClasses)
            if (!marked.TryGetValue(name, out var found) || found != group) wrong.Add(name);
        Check(marked.Count > 0 && wrong.Count == 0,
            "frame and VFX probes: each patch class is marked with its group (" + string.Join(", ", wrong) + ")");
    }

    // The seed probe's code that Core's own patches reach (the aura filter's allow count, Lifebloom's heal hook, the seed's Regen) asks the
    // flag of the group Seeds as its first statement; the probe classes themselves are patched only with the group.
    static void TheSeedProbesAskTheirGroupFirst()
    {
        var probe = CoreFile("GroupAiHealerSeedProbe.cs");
        var seed = CoreFile("GroupAiHealerSeed.cs");
        var lifebloom = CoreFile("LifebloomPatches.cs");
        Check(probe != null && seed != null && lifebloom != null, "seed probes: the probe, the seed and Lifebloom's patches stand in Core's sources");
        if (probe == null || seed == null || lifebloom == null) return;
        var wrong = new List<string>();
        foreach (var signature in new[] { "internal static void OnAllow(", "internal static void OnAfterProcess(", "private static Counts Of(" })
            if (!FirstStatement(PatchPlanSection.MethodBody(probe.Code, signature) ?? "").StartsWith("if (!CoreProbes.Seeds", StringComparison.Ordinal)) wrong.Add(signature);
        if (!FirstStatement(PatchPlanSection.MethodBody(seed.Code, "private static void CompareAreaWithGame(") ?? "").StartsWith("if (!CoreProbes.Seeds", StringComparison.Ordinal))
            wrong.Add("CompareAreaWithGame");
        Check(wrong.Count == 0, "seed probes: each entry that Core's own patches call asks the group Seeds first (" + string.Join(", ", wrong) + ")");
        var regen = PatchPlanSection.MethodBody(seed.Code, "internal static void OnRegen(") ?? "";
        Check(regen.Contains("CompareAreaWithGame(", StringComparison.Ordinal) && !regen.Contains("AreaOnce(", StringComparison.Ordinal)
                && CountOf(lifebloom.Code, "GroupAiHealerSeedProbe.OnAfterProcess(") == 1,
            "seed probes: Regen compares the area only through the probe's function, Lifebloom's heal hook still calls the count once");
        Check(!probe.Code.Contains("Diagnostic", StringComparison.Ordinal) && !seed.Code.Contains("Diagnostic", StringComparison.Ordinal),
            "seed probes: no compile-time switch left in the probe or the seed's code");
    }

    // The blade's static and unit probes read the group OneShot first; in the herbalist's Life Burst check only the lines about it
    // ask the group Seeds, and the registering of the passive (the work) does not.
    static void TheBladeAndLifeBurstProbeLines()
    {
        var serrated = CoreFile("BanditSerrated.cs");
        var seed = CoreFile("GroupAiHealerSeed.cs");
        Check(serrated != null && seed != null, "probe lines: the blade and the seed stand in Core's sources");
        if (serrated == null || seed == null) return;
        var wrong = new List<string>();
        foreach (var signature in new[] { "internal static void ProbeStatic(", "internal static void ProbeUnit(" })
            if (!FirstStatement(PatchPlanSection.MethodBody(serrated.Code, signature) ?? "").StartsWith("if (!CoreProbes.OneShot", StringComparison.Ordinal)) wrong.Add(signature);
        Check(wrong.Count == 0, "probe lines: the blade's static and unit probes ask the group OneShot first (" + string.Join(", ", wrong) + ")");
        var burst = PatchPlanSection.MethodBody(seed.Code, "internal static void CheckLifeBurst(") ?? "";
        var lines = Regex.Matches(burst, @"if \(CoreProbes\.Seeds\)\s*DelversHost\.Info\(|CoreProbes\.Seeds\)\s*DelversHost\.Info\(").Count;
        var registerLine = burst.Split('\n').FirstOrDefault(line => line.Contains("container.Register(entity", StringComparison.Ordinal)) ?? "";
        Check(lines == 3 && registerLine.Length > 0 && !registerLine.Contains("CoreProbes", StringComparison.Ordinal)
                && burst.Contains("DelversHost.Warning(", StringComparison.Ordinal),
            "probe lines: the Life Burst check logs its three lines only with the group Seeds, registers the passive whatever the group says and keeps its failure warning");
    }

    // The probe patch classes of the one-shot and event probes that share a file with code that has work of its own, and the
    // group each one carries. A class in this list that loses its mark or carries another group is red.
    static readonly (string Class, string Group)[] EventProbeClasses =
    {
        ("CorpseUnitOptionsProbe", CoreProbeRules.Corpse),
        ("CorpseBuildingOptionsProbe", CoreProbeRules.Corpse),
    };

    // Patch classes that sit next to probes but carry a function of their own (the warning when leaving with dressed fallen, the
    // bandages' icons): they stay outside every group, and a group that is off must not unpatch them.
    static readonly string[] PatchClassesWithoutGroup = { "CorpseLeaveDungeonProbe", "CorpseLeavePortalProbe", "BandageIconPatch" };

    static void TheEventProbeClassesNameTheirGroups()
    {
        var marked = new Dictionary<string, string>(StringComparer.Ordinal);
        var patchClasses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in SourceTree.Current.Files(SourceFolders.Core))
        {
            foreach (var (name, group) in MarkedClassesOf(file.Code)) marked[name] = group;
            foreach (var each in ClassesOf(file.Code))
                if (each.Patch) patchClasses.Add(each.Name);
        }
        var wrong = new List<string>();
        foreach (var (name, group) in EventProbeClasses)
            if (!marked.TryGetValue(name, out var found) || found != group) wrong.Add(name);
        Check(marked.Count > 0 && wrong.Count == 0, "event probes: each probe patch class is marked with its group (" + string.Join(", ", wrong) + ")");
        var grouped = new List<string>();
        foreach (var name in PatchClassesWithoutGroup)
            if (!patchClasses.Contains(name) || marked.ContainsKey(name)) grouped.Add(name);
        Check(grouped.Count == 0, "event probes: the patch classes with work of their own carry no group (" + string.Join(", ", grouped) + ")");
    }

    // The entry points of probe code that Core's own patches or the fight reach, and the flag each one reads as its first
    // statement. The patch classes of a group are patched only while it is on; these functions are called either way.
    static readonly (string File, string Signature, string Flag)[] EventProbeEntries =
    {
        ("CorpseKeep.cs", "internal static void ProbeOptions(", "Corpse"),
        ("CorpseKeep.cs", "internal static void ProbeLeave(", "Corpse"),
        ("BanditStatsProbe.cs", "internal static void Static(", "OneShot"),
        ("BanditStatsProbe.cs", "internal static void Unit(", "OneShot"),
        ("BanditBar.cs", "private static void LogState(", "OneShot"),
        ("BandageContent.cs", "internal static void ProbeIcon(", "OneShot"),
        ("WoundBodyArt.cs", "private static void Geometry(", "Wound"),
        ("WoundBodyArt.cs", "private static void Tree(", "Wound"),
        ("WoundBodyArt.cs", "private static void Place(", "Wound"),
        ("WoundBodyArt.cs", "private static void Note(", "Wound"),
        ("GroupAiHealerMagicProbe.cs", "internal static void Run(", "OneShot"),
        ("GroupAiPolicy.cs", "internal static void NoteNativeLifebloom(", "OneShot"),
        ("LifebloomSlotCooldown.cs", "internal static void Probe(", "OneShot"),
    };

    static void TheEventProbeEntriesAskTheirGroupFirst()
    {
        var wrong = new List<string>();
        foreach (var (fileName, signature, flag) in EventProbeEntries)
        {
            var file = CoreFile(fileName);
            var body = file == null ? null : PatchPlanSection.MethodBody(file.Code, signature);
            if (body == null || !FirstStatement(body).StartsWith("if (!CoreProbes." + flag + ") return;", StringComparison.Ordinal)) wrong.Add(fileName + ": " + signature);
        }
        Check(wrong.Count == 0, "event probes: each probe entry point asks its group as the first statement (" + string.Join(", ", wrong) + ")");
    }

    // The warning when leaving a dungeon map with dressed fallen is not a probe: the portal patches call it after the probe line,
    // neither reads a flag, and the probe line itself asks the group Corpse (ProbeLeave, above).
    static void TheCorpseGroupKeepsTheLeaveWarning()
    {
        var keep = CoreFile("CorpseKeep.cs");
        var patches = CoreFile("CorpsePatches.cs");
        Check(keep != null && patches != null, "corpse group: the corpse code and its patches stand in Core's sources");
        if (keep == null || patches == null) return;
        var warning = PatchPlanSection.MethodBody(keep.Code, "internal static void LeaveWarning(") ?? "";
        Check(warning.Contains("CorpseRules.LeaveWarns(", StringComparison.Ordinal) && warning.Contains("HelpNoticeApplyData(", StringComparison.Ordinal)
                && !warning.Contains("CoreProbes", StringComparison.Ordinal),
            "corpse group: the leave warning reads no probe flag");
        var wrong = new List<string>();
        foreach (var name in new[] { "CorpseLeaveDungeonProbe", "CorpseLeavePortalProbe" })
        {
            var body = PatchPlanSection.MethodBody(patches.Code, "internal static class " + name) ?? "";
            var probe = body.IndexOf("CorpseKeep.ProbeLeave(", StringComparison.Ordinal);
            var warn = body.IndexOf("CorpseKeep.LeaveWarning(", StringComparison.Ordinal);
            if (probe < 0 || warn < probe || body.Contains("CoreProbes", StringComparison.Ordinal)) wrong.Add(name);
        }
        Check(wrong.Count == 0, "corpse group: both portal patches still call the leave warning after the probe line and read no flag (" + string.Join(", ", wrong) + ")");
    }

    // The bandits' probes (the stats, the camp's room graph, the building bar's state) only write lines: the bar reads no flag
    // outside its probe, and the camp probe keeps its own key next to the group, so a run with the group OneShot on logs no floor
    // graph unless bandits.camp.probe is on as well.
    static void TheBanditProbesLeaveTheFightAlone()
    {
        var bar = CoreFile("BanditBar.cs");
        var camp = CoreFile("BanditCampProbe.cs");
        var settings = CoreFile("CoreSettings.cs");
        Check(bar != null && camp != null && settings != null, "bandit probes: the bar, the camp probe and the settings stand in Core's sources");
        if (bar == null || camp == null || settings == null) return;
        Check(CountOf(bar.Code, "CoreProbes.") == 1,
            "bandit probes: the building bar reads a flag only in its state probe");
        var rooms = PatchPlanSection.MethodBody(camp.Code, "internal static void OnAddMonsterGroup(") ?? "";
        Check(camp.Raw.Contains("internal const string EnabledKey = \"bandits.camp.probe\";", StringComparison.Ordinal)
                && camp.Code.Contains("internal static bool Enabled => testOverride ?? CoreSettings.CampProbeEnabled;", StringComparison.Ordinal)
                && Regex.IsMatch(rooms, @"if \(!Enabled \|\|") && Regex.IsMatch(settings.Code, @"CampProbeEnabled => Get\(BanditCampProbe\.EnabledKey, false\);"),
            "bandit probes: the camp probe keeps its own key, off by default, next to the group");
    }

    // The bandages' icon patch gives the game the bandages' own icons (or the placeholder from its goods); the icon probe inside it asks
    // the group OneShot, the patch itself reads no flag and stays patched with the group off.
    static void TheBandageIconPatchKeepsItsWork()
    {
        var content = CoreFile("BandageContent.cs");
        Check(content != null, "bandage icon: BandageContent stands in Core's sources");
        if (content == null) return;
        var patch = PatchPlanSection.MethodBody(content.Code, "internal static class BandageIconPatch") ?? "";
        Check(patch.Contains("BandageContent.IconFor(key)", StringComparison.Ordinal) && patch.Contains("BandageContent.ProbeIcon(__instance)", StringComparison.Ordinal)
                && patch.Contains("CoreAssets.BandageIcon(key)", StringComparison.Ordinal) && !patch.Contains("CoreProbes", StringComparison.Ordinal),
            "bandage icon: the icon patch still gives the bandages' icons and calls the probe, and reads no flag itself");
    }

    // The body blood's four log functions (geometry, tree, place and the status line) ask the group Wound first and are the only code
    // of WoundBodyArt that reads a flag: showing, sorting, painting and removing the drawing do not depend on the group.
    static void TheBodyBloodLinesAskTheGroupWound()
    {
        var art = CoreFile("WoundBodyArt.cs");
        Check(art != null, "body blood: WoundBodyArt stands in Core's sources");
        if (art == null) return;
        var work = new[] { "internal static void OnWound(", "internal static void EndTick(", "internal static void OnGameTick(", "private static void Paint(",
            "private static void Show(", "private static void Remove(", "private static List<Part> Parts(" };
        var guarded = new List<string>();
        foreach (var signature in work)
            if ((PatchPlanSection.MethodBody(art.Code, signature) ?? "?CoreProbes").Contains("CoreProbes", StringComparison.Ordinal)) guarded.Add(signature);
        Check(CountOf(art.Code, "CoreProbes.") == 4 && guarded.Count == 0,
            "body blood: only the four log functions read the group Wound, the drawing's own code reads no flag (" + string.Join(", ", guarded) + ")");
    }

    // The healer's probes (the magic probe, the game's own check of Lifebloom with the group AI off, the slot cooldown's probe line) sit
    // inside code that has work of its own: the skill hook, the Life Burst check and the SetGlobalCooldown postfix. The probes ask the
    // group OneShot in their own first statement; the hooks read no flag, so the Off-GCD Lifebloom of the game's decision and the
    // countdown on the slot stay whatever the group says.
    static void TheHealerProbesLeaveTheHealerAlone()
    {
        var patches = CoreFile("GroupAiPatches.cs");
        var policy = CoreFile("GroupAiPolicy.cs");
        var bloom = CoreFile("LifebloomPatches.cs");
        var cooldown = CoreFile("LifebloomSlotCooldown.cs");
        Check(patches != null && policy != null && bloom != null && cooldown != null,
            "healer probes: the skill hook, the policy, Lifebloom's patches and the slot cooldown stand in Core's sources");
        if (patches == null || policy == null || bloom == null || cooldown == null) return;
        var skill = PatchPlanSection.MethodBody(patches.Code, "internal static class GroupAiDecideSkillPatch") ?? "";
        Check(CountOf(patches.Code, "CoreProbes") == 0 && skill.Contains("GroupAiPolicy.NoteNativeLifebloom(__instance, __result)", StringComparison.Ordinal)
                && skill.Contains("GroupAiPolicy.NativeLifebloomOffGcd(__instance)", StringComparison.Ordinal),
            "healer probes: the skill hook reads no flag and still calls the native probe and the Off-GCD Lifebloom");
        var postfix = PatchPlanSection.MethodBody(bloom.Code, "private static void Postfix(SkillSlotUI __instance, float cooldown, float duration)") ?? "";
        Check(CountOf(bloom.Code, "CoreProbes") == 0 && postfix.Contains("LifebloomSlotCooldown.Probe(", StringComparison.Ordinal)
                && postfix.Contains("LifebloomSlotCooldown.AfterGameRefresh(__instance)", StringComparison.Ordinal),
            "healer probes: the SetGlobalCooldown postfix reads no flag and still draws the countdown after the probe call");
        Check(CountOf(policy.Code, "CoreProbes") == 1 && CountOf(cooldown.Code, "CoreProbes") == 1,
            "healer probes: the native probe and the slot cooldown's probe are the only places of their files that read a flag");
    }

    // The look probes of the wound effect's own marks are removed (they could not match: nothing ever noted a mark's id): no file,
    // no member and no call is left, the owner patch of the tint only calls the tint, and the compile-time probe switches are gone
    // from Core altogether, so the ratchet of the diagnostic-switch rule stands at no allowed hit.
    static void TheOwnMarkLookProbesAreGone()
    {
        var files = SourceTree.Current.Files(SourceFolders.Core);
        var left = new List<string>();
        foreach (var file in files)
        {
            if (file.Name == "WoundFxOwnerProbe.cs") left.Add(file.Name);
            foreach (var word in new[] { "WoundFxOwnerProbe", "LookLying", "IsOwnMark", "ownMarks" })
                if (file.Code.Contains(word, StringComparison.Ordinal)) left.Add(file.Name + ":" + word);
        }
        Check(files.Count > 0 && left.Count == 0, "wound probes: the own-mark look probes are gone from Core (" + string.Join(", ", left) + ")");
        var tint = CoreFile("BanditTint.cs");
        var owner = tint == null ? "" : PatchPlanSection.MethodBody(tint.Code, "private static void Postfix(VFXView __instance, Il2CppSystem.Guid guid, IEntity owner)") ?? "";
        Check(CountOf(owner, "try") == 1 && CountOf(owner, "BanditTint.OnOwner(__instance, guid, owner)") == 1,
            "wound probes: the owner patch of the tint only passes the owner to the tint");
        Check(SourceRules.ById("diagnostic-switch").AllowedHits == 0, "wound probes: the diagnostic-switch rule allows no hit any more");
    }

    // The classes marked [ProbeGroup(CoreProbeRules.<name>)] in a source (comments and strings blanked), each with the group's key.
    static IEnumerable<(string Name, string Group)> MarkedClassesOf(string code)
    {
        foreach (Match mark in Regex.Matches(code, @"\[ProbeGroup\(\s*CoreProbeRules\.(?<constant>\w+)\s*\)\]\s*(?:\[[^\]]*\]\s*)*(?:\w+\s+)*?class\s+(?<name>\w+)"))
        {
            var key = typeof(CoreProbeRules).GetField(mark.Groups["constant"].Value, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetRawConstantValue() as string;
            yield return (mark.Groups["name"].Value, key);
        }
    }

    // The first statement of a method body as MethodBody gives it (from the opening brace), without the brace and the blanks.
    static string FirstStatement(string body) => body.TrimStart('{', ' ', '\r', '\n', '\t');

    // A source file of Core by its name, or null.
    static SourceFile CoreFile(string name) => SourceTree.Current.Files(SourceFolders.Core).FirstOrDefault(file => file.Name == name);

    static string Source(string directory, string file)
    {
        var path = directory == null ? null : Path.Combine(directory, file);
        return path != null && File.Exists(path) ? SourceText.WithoutComments(File.ReadAllText(path)) : null;
    }

    static int CountOf(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
