using DungeonSettlersDelvers.Core;
using static TestKit;

// Rules read from the source: native lists, notice keys, known traps, inventory readers, Harmony argument binding. The table and the
// reader are Tests/Shared/SourceRules.cs, the same in the tests of Core and of Frieren; this section runs them and adds what only Core
// can say: the table's rules over every source folder, Core's own value for the stripped Unity method, the registered notice row,
// and the probes of the two rules whose cases are worth more than one snippet.
internal static class SourceRulesSection
{
    internal static void Run()
    {
        TableRules();
        NullableLambdaProbe();
        HarmonyBindsByName();
        VfxEntriesHaveOneCaller();
        GlobalCooldownWarningIgnoresCap();
        DiagnosticSwitchRatchet();
    }

    // The warning for a global cooldown on a bandit is the tripwire of the removed branch: it is written straight to the host log, not
    // through Warn, which stops after WarningCap lines, so it also shows when the cap is spent.
    static void GlobalCooldownWarningIgnoresCap()
    {
        SourceFile banditFile = null;
        foreach (var file in SourceTree.Current.Files(SourceFolders.Core))
            if (file.Name == "GroupAiBandits.cs") banditFile = file;
        if (banditFile == null) return;
        var start = banditFile.Code.IndexOf("static void NoteGlobalCooldown(", StringComparison.Ordinal);
        var end = start < 0 ? -1 : banditFile.Code.IndexOf("\n    }", start, StringComparison.Ordinal);
        var body = start < 0 || end < 0 ? "" : banditFile.Code.Substring(start, end - start);
        Check(body.Length > 0 && body.Contains("DelversHost.Warning(", StringComparison.Ordinal) && !body.Contains(" Warn(", StringComparison.Ordinal),
            "bandits: the global cooldown warning goes to the host log directly, past the shared warning cap");
    }

    // The two functional entries into VFXView.CreateVFX (the view for the body blood, the new effect for the bandits' tint) are called
    // from one patch class, CoreVfxPatches, and from nowhere else: no probe class carries them.
    static void VfxEntriesHaveOneCaller()
    {
        var files = SourceTree.Current.Files(SourceFolders.Core);
        if (files.Count == 0) return;
        var callers = new List<string>();
        foreach (var file in files)
        {
            AddCallers(callers, file, "WoundBodyArt.SeeView(");
            AddCallers(callers, file, "BanditTint.OnVfx(");
        }
        Check(callers.Count == 2 && callers.Contains("WoundBodyArt.SeeView( in CoreVfxPatches.cs") && callers.Contains("BanditTint.OnVfx( in CoreVfxPatches.cs"),
            "VFX entries: WoundBodyArt.SeeView and BanditTint.OnVfx each have one caller, CoreVfxPatches");
    }

    // One entry per call of the member in the file's code (comments and strings are blank there).
    static void AddCallers(List<string> callers, SourceFile file, string call)
    {
        for (var at = file.Code.IndexOf(call, StringComparison.Ordinal); at >= 0; at = file.Code.IndexOf(call, at + call.Length, StringComparison.Ordinal))
            callers.Add(call + " in " + file.Name);
    }

    // Every rule of the table over the folders it names, with its probe, and the guards around the table.
    static void TableRules() => SourceRuleChecks.Run(Check, CoreCondition);

    // What a rule needs beyond its hits: the trap rule for the stripped Unity method stands on Core's own colour text, and the notice rule
    // on the registered row of the burial notice.
    static bool CoreCondition(SourceRule rule) => rule.Id switch
    {
        "stripped-unity" => BanditBossBarRules.ColourText(1f, 0.5f, 0f, 1f) == "FF8000FF",
        "notice-key" => SourceTree.Current.Context.RegisteredNoticeRows.Contains("(ReviveText.BurialNoticeKey,", StringComparison.Ordinal),
        _ => true
    };

    static void NullableLambdaProbe()
    {
        Check(SourceRuleReader.HitCount("nullable-lambda", "=> rows.SelectMany(row => Problems(row.Key));") == 1
                && SourceRuleReader.HitCount("nullable-lambda", "var d = items.ToDictionary(item => item.Key);") == 1
                && SourceRuleReader.HitCount("nullable-lambda", "Regex.Replace(text, pattern, match => match.Value);") == 1
                && SourceRuleReader.HitCount("nullable-lambda", "var e = new MatchEvaluator(Keep);") == 1
                && SourceRuleReader.HitCount("nullable-lambda", "var any = list.Any(item => item != null);") == 0
                && SourceRuleReader.HitCount("nullable-lambda", "hits.RemoveAll(hit => hit.At > now);") == 0
                && SourceRuleReader.HitCount("nullable-lambda", "foreach (var (key, text) in rows) Use(key, text);") == 0,
            "trap: the lambda rule finds SelectMany, ToDictionary and a MatchEvaluator, and leaves other lambdas and loops alone");
    }

    static void HarmonyBindsByName()
    {
        // Harmony patches bind game arguments by name, never by position (__0, __1...): DS_B.0.4.29
        // inserted a parameter into UseSkillMode.GetPossibleTarget, and the index binding (__2) then broke
        // Core under Melon with an IL compile error. The rule itself, over every folder, is in the table.
        Check(SourceRuleReader.IndexBoundArguments("private static void Postfix(ActiveSkill_Base __1, Vector2Int __2, ref ICombatTargetSelector __result)").Count == 2
                && SourceRuleReader.IndexBoundArguments("private static bool Prefix(GameResourceLoader __instance, string key, ref Sprite __result)\n"
                    + "private static Exception Finalizer(Exception __exception)").Count == 0,
            "the argument rule finds index-bound patch parameters and accepts __instance, __result and named ones");
    }

    // The rule for the compile-time probe switches stands at a number of allowed hits that only goes down: that many is green, one
    // more is red, and a rule without allowed hits takes none. The reader finds the constants, also one named ...Diagnostic.
    static void DiagnosticSwitchRatchet()
    {
        var rule = SourceRules.ById("diagnostic-switch");
        var two = rule with { AllowedHits = 2 };
        Check(rule.AllowedHits >= 0 && SourceRuleChecks.Within(two, 2) && !SourceRuleChecks.Within(two, 3) && SourceRuleChecks.Within(two, 0)
                && SourceRuleChecks.Within(rule with { AllowedHits = 0 }, 0) && !SourceRuleChecks.Within(rule with { AllowedHits = 0 }, 1),
            "diagnostic switch rule: the allowed hits are green, one more is red, and no allowed hits takes none");
        Check(SourceRuleChecks.HitsText(two, new List<SourceHit>()) == "0 of 2 allowed"
                && SourceRuleChecks.HitsText(rule with { AllowedHits = 0 }, new List<SourceHit>()) == "",
            "diagnostic switch rule: a run prints the count against the allowed number");
        Check(SourceRuleReader.HitCount("diagnostic-switch", "internal const bool Diagnostic = true;\nprivate const bool HudDiagnostic = false;") == 2
                && SourceRuleReader.HitCount("diagnostic-switch", "internal const bool DiagnosticsX = true;\ninternal static bool Diagnostic => true;\nconst int Diagnostic = 1;") == 0
                && SourceRuleReader.HitCount("diagnostic-switch", "// internal const bool Diagnostic = true;\nvar t = \"const bool Diagnostic\";") == 0,
            "diagnostic switch rule: finds const bool Diagnostic and HudDiagnostic, leaves other names, other kinds and comments alone");
    }
}
