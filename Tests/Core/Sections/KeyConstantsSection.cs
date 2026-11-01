using System.Reflection;
using System.Text.RegularExpressions;
using DungeonSettlersDelvers.Core;
using static TestKit;

// The keys Core saves under: their values are fixed, and the code reaches them only through their constants.
internal static class KeyConstantsSection
{
    const string AddsFirstValue = "groupai.adds-first";
    const string NoPauseHintValue = "DelversCoreNoPause";
    const string InstantCombatHintValue = "DelversCoreInstantCombat";
    const string LifebloomTemplateValue = "AFFECTER_HealthBoost";

    internal static void Run()
    {
        SavedKeyValuesAreFixed();
        SettingKeyFindsStoredValue();
        HintRequestsUseConstants();
        GroupAiKeysStayInTheirConstants();
    }

    static void SavedKeyValuesAreFixed()
    {
        // A saved setting or hint state is found again only under the exact key it was written with, and an effect row is cloned from
        // an existing template of the game. A constant given another value would silently orphan what players already saved.
        Check(GroupAiRules.AddsFirstKey == AddsFirstValue,
            "key: the adds-first setting is saved as groupai.adds-first");
        Check(FirstHintText.NoPause == NoPauseHintValue && FirstHintText.InstantCombat == InstantCombatHintValue
                && FirstHintText.Keys.Contains(FirstHintText.NoPause) && FirstHintText.Keys.Contains(FirstHintText.InstantCombat),
            "key: the no-pause and the instant-combat hints are saved as DelversCoreNoPause and DelversCoreInstantCombat");
        Check(LifebloomRules.EffectTemplate == LifebloomTemplateValue,
            "key: the Lifebloom effect is cloned from the game's AFFECTER_HealthBoost template");
    }

    static void SettingKeyFindsStoredValue()
    {
        // The value pin above is the key itself; this reads a file written by an earlier Core through the constant.
        var directory = Path.Combine(Path.GetTempPath(), "delvers-keys-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{\"groupai.adds-first\": false}");
            Check(!new CoreSettingsFile(path).Get(GroupAiRules.AddsFirstKey, true),
                "key: a stored groupai.adds-first switch is found through GroupAiRules.AddsFirstKey");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    static void HintRequestsUseConstants()
    {
        Check(HintRequestOffenders("FirstHints.Request(FirstHintText.NoPause);", false).Count == 0
                && HintRequestOffenders("FirstHints.Request(\"DelversCoreNoPause\");", false).Count == 1
                && HintRequestOffenders("FirstHints.Request(FirstHintText.PageBreak);", false).Count == 1
                && HintRequestOffenders("// FirstHints.Request(\"x\");\nvar y = 1;", false).Count == 0
                && HintRequestOffenders("internal static void Request(string hint)\nRequest(FirstHintText.Revive);", true).Count == 0
                && HintRequestOffenders("internal static void Request(string hint)\nRequest(hint);", true).Count == 1,
            "the hint rule finds a request with a literal or a non-hint argument and accepts the FirstHintText constants");
        var coreFiles = SourceTree.Current.Files(SourceFolders.Core);
        var offenders = new List<string>();
        foreach (var file in coreFiles)
        {
            var insideFirstHints = file.Name == "FirstHints.cs";
            foreach (var argument in HintRequestOffenders(file.Raw, insideFirstHints)) offenders.Add(file.Name + ":" + argument);
        }
        Check(coreFiles.Count > 0 && offenders.Count == 0,
            "every FirstHints.Request names its hint through a FirstHintText constant (" + string.Join(", ", offenders) + ")");
    }

    static void GroupAiKeysStayInTheirConstants()
    {
        const string constant = "internal const string EnabledKey = \"groupai.enabled\";";
        Check(GroupAiLiteralOffenders(constant, "GroupAiRules.cs").Count == 0
                && GroupAiLiteralOffenders(constant, "GroupAiStanceRules.cs").Count == 0
                && GroupAiLiteralOffenders(constant, "CoreSettings.cs").Count == 1
                && GroupAiLiteralOffenders(constant, "GroupAiBandits.cs").Count == 1
                && GroupAiLiteralOffenders("internal static bool X => Get(\"groupai.adds-first\", true);", "GroupAiRules.cs").Count == 1
                && GroupAiLiteralOffenders("// the key groupai.enabled\nvar a = 1; // groupai.x", "CoreSettings.cs").Count == 0
                && GroupAiLiteralOffenders("var k = \"groupai.a\" + \"groupai.b\";", "GroupAiRules.cs").Count == 2,
            "the group AI key rule finds a groupai. literal outside a constant of a GroupAi rules file and accepts such constants and comments");
        var coreFiles = SourceTree.Current.Files(SourceFolders.Core);
        var offenders = new List<string>();
        foreach (var file in coreFiles)
            foreach (var line in GroupAiLiteralOffenders(file.Raw, file.Name)) offenders.Add(file.Name + ":" + line);
        Check(coreFiles.Count > 0 && offenders.Count == 0,
            "no groupai. key literal in Core outside a constant of a GroupAi rules file (" + string.Join(", ", offenders) + ")");
    }

    // Each FirstHints.Request call, and inside FirstHints itself each bare Request call, whose argument is not
    // FirstHintText.<name> with a name that is one of the hint constants. Result: "line: argument".
    static List<string> HintRequestOffenders(string source, bool insideFirstHints)
    {
        var code = WithoutComments(source);
        var pattern = insideFirstHints ? @"(?<![\w.])(?<!void\s)(?:FirstHints\.)?Request\((.*?)\)\s*;" : @"FirstHints\.Request\((.*?)\)\s*;";
        var offenders = new List<string>();
        foreach (Match call in Regex.Matches(code, pattern, RegexOptions.Singleline))
        {
            var argument = call.Groups[1].Value.Trim();
            var name = Regex.Match(argument, @"^FirstHintText\.(\w+)$");
            if (!name.Success || !IsHintConstant(name.Groups[1].Value)) offenders.Add(LineOf(code, call.Index) + ": " + argument);
        }
        return offenders;
    }

    static bool IsHintConstant(string name)
        => typeof(FirstHintText).GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is { IsLiteral: true } field
            && field.GetRawConstantValue() is string value && FirstHintText.Keys.Contains(value);

    // Each string literal starting with groupai. that is not the initializer of a const string in a GroupAi*Rules.cs file.
    // Result: "line: literal".
    static List<string> GroupAiLiteralOffenders(string source, string fileName)
    {
        var constantsAllowed = Regex.IsMatch(fileName, @"^GroupAi\w*Rules\.cs$");
        var code = WithoutComments(source);
        var offenders = new List<string>();
        foreach (Match literal in Regex.Matches(code, "\"groupai\\.[^\"\\r\\n]*\""))
        {
            var lineStart = code.LastIndexOf('\n', literal.Index) + 1;
            var before = code.Substring(lineStart, literal.Index - lineStart);
            if (!constantsAllowed || !Regex.IsMatch(before, @"\bconst\s+string\s+\w+\s*=\s*$")) offenders.Add(LineOf(code, literal.Index) + ": " + literal.Value);
        }
        return offenders;
    }

    // Comments blanked out, strings kept; the line breaks stay so that line numbers still match the file.
    static string WithoutComments(string source) => SourceText.WithoutComments(source);

    static int LineOf(string text, int index) => text.Take(index).Count(character => character == '\n') + 1;
}
