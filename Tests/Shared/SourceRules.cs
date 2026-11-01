using System.Text;
using System.Text.RegularExpressions;

// The source rules of the offline tests: one table for Core, Frieren, Adapters and SelfTest, one reader that applies it to every
// folder it finds, and the checks that keep table, reader and doc in step. Compiled into both test projects, so a public export
// with only one of the packages still checks what it has.

internal enum SourceRuleKind { Line, NativeList, NoticeKey }

// One rule: what it looks for, in which folders, which files may break it, and the two snippets of its self-probe.
// TrapNumber is the row of the trap list (0: no row in it), Stage the evidence stage of the trap's origin (S, A, D, M or E).
// ExemptionKey is the name a "trap-ok" marker uses for this rule. AllowedHits is a ratchet for a rule that is not clean yet: that many
// hits still stand, one more is a failure, and the number goes down with the change that removes a hit (0: no hit stands).
internal sealed record SourceRule(string Id, string CheckName, SourceRuleKind Kind, string Pattern, string[] Folders, string[] AllowedFiles,
    int TrapNumber, char Stage, string ExemptionKey, string BadSnippet, string GoodSnippet, int AllowedHits = 0)
{
    internal bool Allows(SourceFile file) => AllowedFiles.Contains(file.Name);
}

internal static class SourceFolders
{
    internal const string Core = "Core";
    internal const string Frieren = "Frieren";
    internal const string Adapters = "Adapters";
    internal const string SelfTest = "SelfTest";
    internal const string ProjectRules = "ProjectRules.cs";

    // The four folders a canonical project must have; a public export has some of them.
    internal static readonly string[] Directories = { Core, Frieren, Adapters, SelfTest };
    internal static readonly string[] All = { Core, Frieren, Adapters, SelfTest, ProjectRules };
}

// The table. The test names are those of the earlier checks; a rule's name followed by the hits in brackets is the line a run prints.
internal static class SourceRules
{
    static readonly string[] Everywhere = SourceFolders.All;
    static readonly string[] CoreOnly = { SourceFolders.Core };
    static readonly string[] NoFiles = Array.Empty<string>();

    internal static readonly SourceRule[] All =
    {
        // A native list of IL2CPP structs is filled through DelversNativeList.AddValue; Add and Insert only for element types known not
        // to be structs (NativeListRule.NotStructs). The pattern is the call after the list's name. The self-test file that shows the
        // wrapper's own Add on a struct list is the one file allowed to call it.
        new("native-list", "native lists of IL2CPP structs are filled through DelversNativeList.AddValue, never List.Add",
            SourceRuleKind.NativeList, "(?:Add|Insert)", Everywhere, new[] { "NativeListChecks.cs" }, 1, 'M', "native list",
            "using ItemList = Il2CppSystem.Collections.Generic.List<Il2CppSystem.ValueTuple<string, int>>;\n"
            + "holder.Items ??= new ItemList();\nholder.Items.Add(new Il2CppSystem.ValueTuple<string, int>(key, 1));\n",
            "var plain = new Il2CppSystem.Collections.Generic.List<int>();\nplain.Add(1);\n"
            + "var items = new Il2CppSystem.Collections.Generic.List<Il2CppSystem.ValueTuple<string, int>>();\nDelversNativeList.AddValue(items, item);\n"),
        new("entity-object", "trap: entity objects only through CoreGame.EntityObjectOf, the game throws for one not activated yet",
            SourceRuleKind.Line, @"\.\s*GetEntityObject\s*\(", Everywhere, new[] { "CoreGame.cs" }, 2, 'M', "entity object",
            "var found = entity.GetEntityObject(guid);\n", "var found = CoreGame.EntityObjectOf(entity);\n"),
        new("sorting-layer", "trap: a sorting layer id is never tested against 0, the HUD layer's id is negative",
            SourceRuleKind.Line, @"(?i)\b(sorting\w*|\w*group\w*layer\w*)\s*(>=|<=|<|>)\s*0\b", Everywhere, NoFiles, 3, 'D', "sorting layer",
            "if (sortingLayerId >= 0) Use();\n", "if (hasSortingLayer) Use();\n"),
        new("shared-material", "trap: no write to a shared material, it changes every user of it; copy first",
            SourceRuleKind.Line, @"\.\s*sharedMaterial\s*\.\s*(Set\w+\s*\(|color\s*=|mainTexture\s*=|shader\s*=)", Everywhere, NoFiles, 4, 'E', "shared material",
            "renderer.sharedMaterial.color = tint;\n", "renderer.material.color = tint;\n"),
        new("faction-write", "trap: no faction change of a registered entity, the game keys its combat data by faction",
            SourceRuleKind.Line, @"(\._faction|\._subFaction|\.Faction|\.SubFaction)\s*=(?!=)", Everywhere, NoFiles, 5, 'A', "faction",
            "info._faction = FactionType.Enemy;\n", "if (info._faction == FactionType.Enemy) Use();\n"),
        new("is-hostile", "trap: Core decides foes by its own faction rule, never EntityExtensions.IsHostile (true for Enemy against Enemy)",
            SourceRuleKind.Line, @"\bEntityExtensions\s*\.\s*IsHostile\s*\(", Everywhere, NoFiles, 6, 'A', "IsHostile",
            "var foe = EntityExtensions.IsHostile(first, second);\n", "var foe = HostileByFaction(first, second);\n"),
        new("stripped-unity", "trap: no Unity method the game's build has stripped, ColorUtility.ToHtmlStringRGBA is missing",
            SourceRuleKind.Line, @"\bColorUtility\s*\.\s*ToHtmlStringRGBA\s*\(", Everywhere, NoFiles, 7, 'M', "stripped method",
            "var text = ColorUtility.ToHtmlStringRGBA(tint);\n", "var text = ColourText(red, green, blue, alpha);\n"),
        // Frieren's form: every identifier __<number> counts, whatever the method is called, because probes are also bound by hand.
        new("index-binding", "every Core Harmony patch binds game arguments by name",
            SourceRuleKind.Line, @"\b__\d+\b", Everywhere, NoFiles, 8, 'M', "index argument",
            "private static void Postfix(Unit __instance, int __0) { }\n",
            "private static void Postfix(Unit __instance, int amount, ref int __result) { }\n"),
        // A lambda of these kinds needs System.Runtime.CompilerServices.NullableAttribute, which the IL2CPP reference set lacks: the game
        // build stops with CS0656, the offline build against net6 does not notice. Write a loop (yield) instead. Other lambdas, also
        // over tuples or lists of tuples, build fine. Only Core is covered because only there it is shown.
        new("nullable-lambda", "trap: no lambda that needs NullableAttribute (SelectMany, ToDictionary, a MatchEvaluator), the IL2CPP references lack it",
            SourceRuleKind.Line, @"\.\s*(SelectMany|ToDictionary)\s*\([^;]*=>|\bRegex\s*\.\s*Replace\s*\([^;]*=>|\bnew\s+MatchEvaluator\b",
            CoreOnly, NoFiles, 9, 'S', "nullable lambda",
            "var byKey = items.ToDictionary(item => item.Key);\n", "var any = items.Any(item => item != null);\n"),
        // Core puts every attribute patch on at mod start, before the game's boot has loaded SystemVar and the tables; resolving the
        // target runs the type's initializer then, and it fails for the whole session. The pattern names the game types whose
        // initializer reads that data; such a patch is put on by hand (LatePatchGate).
        new("early-type-init", "trap: no attribute patch on a game type whose type initializer reads SystemVar or a table, put it on by hand later",
            SourceRuleKind.Line, @"\[\s*HarmonyPatch\s*\(\s*typeof\s*\(\s*(?:[\w.]+\.)?DamageSimulator\s*\)", Everywhere, NoFiles, 10, 'M', "early type init",
            "[HarmonyPatch(typeof(DamageSimulator), nameof(DamageSimulator.CalculateResult))]\n",
            "new Harmony(owner).Patch(original, prefix: new HarmonyMethod(prefix));\n"),
        // A string list of a unit row is never empty: the game writes "NULL" for none and its spawn reads DefaultTraits[0] without a
        // length check. Every such list of a row goes through CoreUnitRules.RowValues, which puts "NULL" in for an empty one.
        new("empty-row-list", "trap: a unit row's string list goes through RowValues, the game's spawn reads its first entry",
            SourceRuleKind.Line, @"\.\s*(DefaultTraits|DefaultSkills|OnDeathItemsAndDrops|FirstKillItemsAndDrops)\s*=\s*new\s+Il2CppStringArray\s*\((?!\s*(CoreUnitRules\s*\.\s*)?RowValues\s*\()",
            Everywhere, NoFiles, 11, 'A', "empty row list",
            "row.DefaultTraits = new Il2CppStringArray(traits);\n", "row.DefaultTraits = new Il2CppStringArray(CoreUnitRules.RowValues(traits));\n"),
        // The hold margin of an enemy area's zone (0.3 s past the last query) is GroupAiAreaRules.HoldMargin, added by HoldUntil as
        // (start + last) + margin. Another spelling can differ in the last bit of the float, and the zone's until= in the log lines moves.
        // The pattern: a sum with the literal 0.3f, or a Hold... name set to it, anywhere but in that file.
        new("hold-margin", "the hold margin of an area's zone is written only in GroupAiAreaRules, everyone else calls HoldUntil",
            SourceRuleKind.Line, @"\+\s*0\.3f\b|\bHold\w*\s*=\s*0\.3f\b", CoreOnly, new[] { "GroupAiAreaRules.cs" }, 0, '-', "hold margin",
            "var until = startedAt + last + 0.3f;\n", "var until = GroupAiAreaRules.HoldUntil(startedAt, last);\n"),
        // The rules below have no row in the trap list.
        // A probe is switched by its diagnostics group (CoreProbeRules, off by default), not by a compile-time constant that stays true in
        // a release. The rule is clean (no allowed hits): every probe sits behind its group, and a new constant of this kind is a failure.
        new("diagnostic-switch", "no const bool ...Diagnostic in Core, a probe is switched by its diagnostics group",
            SourceRuleKind.Line, @"\bconst\s+bool\s+\w*Diagnostic\b", CoreOnly, NoFiles, 0, '-', "diagnostic switch",
            "internal const bool Diagnostic = true;\n", "internal static bool Active => CoreProbes.Commands;\n", 0),
        new("inventory-reader", "no Core code builds its own equipment or inventory reader",
            SourceRuleKind.Line, @"\bnew\s+(Equipment|Inventory)Reader\b|\bAddEvents\s*\(", Everywhere, NoFiles, 0, '-', "inventory reader",
            "var reader = new InventoryReader(component);\n", "var count = inventory.Count;\n"),
        // A key Core shows through the game's common notice is one of Core's registered text rows (the probe registers Known.Key).
        new("notice-key", "every key of a common notice is a registered Core text row",
            SourceRuleKind.NoticeKey, @"UpdateType\s*\.\s*CommonNotice_Show\w+\s*,\s*([A-Za-z_][\w.]*)", CoreOnly, NoFiles, 0, '-', "notice key",
            "Show(UpdateType.CommonNotice_ShowOk, Unknown.Key);\n", "Show(UpdateType.CommonNotice_ShowOk, Known.Key);\n"),
    };

    internal static SourceRule ById(string id) => All.Single(rule => rule.Id == id);
}

// A hit of a rule: the lines it spans (a call may break across lines) and a detail for the report.
internal sealed record SourceHit(SourceFile File, int FirstLine, int LastLine, string Detail)
{
    internal string Label => File.RelativePath + ":" + FirstLine + (Detail.Length > 0 ? " " + Detail : "");
}

// What a rule may need besides the file it reads.
internal sealed record SourceContext(string RegisteredNoticeRows);

// A source file with its text in three forms of the same length: raw, without comments, and without comments and strings.
internal sealed class SourceFile
{
    internal SourceFile(string folder, string relativePath, string text)
    {
        Folder = folder;
        RelativePath = relativePath.Replace('\\', '/');
        Name = Path.GetFileName(relativePath);
        Raw = text.Replace("\r\n", "\n");
        RawLines = Raw.Split('\n');
        WithStrings = SourceText.Strip(Raw, blankStrings: false);
        Code = SourceText.Strip(Raw, blankStrings: true);
        Aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var alias in SourceText.AliasesOf(Code)) Aliases[alias.Name] = alias.Target;
    }

    internal string Folder { get; }
    internal string RelativePath { get; }
    internal string Name { get; }
    internal string Raw { get; }
    internal string[] RawLines { get; }
    internal string WithStrings { get; }
    internal string Code { get; }
    // using X = ...; of the file, and after loading also the global ones of the whole tree.
    internal Dictionary<string, string> Aliases { get; set; }

    internal int LineOf(int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < Raw.Length; i++)
            if (Raw[i] == '\n') line++;
        return line;
    }
}

// Comments and strings out of C# source, aliases out of the using lines.
internal static class SourceText
{
    static readonly Regex AliasLine = new(@"(?m)^[ \t]*(?<global>global[ \t]+)?using[ \t]+(?<name>\w+)[ \t]*=[ \t]*(?<target>[^;]+);");

    internal static string WithoutComments(string source) => Strip(source.Replace("\r\n", "\n"), blankStrings: false);

    // Comments become blanks, and with blankStrings the content of strings and chars too; line breaks and the length stay, so an index
    // and a line number mean the same in every form. The code in the holes of an interpolated string stays.
    internal static string Strip(string text, bool blankStrings)
    {
        var chars = text.ToCharArray();
        var at = 0;
        LexCode(chars, ref at, false, blankStrings);
        return new string(chars);
    }

    internal static IEnumerable<(string Name, string Target, bool Global)> AliasesOf(string code)
    {
        foreach (Match match in AliasLine.Matches(code))
            yield return (match.Groups["name"].Value, Regex.Replace(match.Groups["target"].Value, @"\s+", " ").Trim(), match.Groups["global"].Success);
    }

    static void LexCode(char[] c, ref int i, bool inHole, bool blank)
    {
        var depth = 0;
        while (i < c.Length)
        {
            var next = i + 1 < c.Length ? c[i + 1] : '\0';
            if (c[i] == '/' && next == '/')
                while (i < c.Length && c[i] != '\n') c[i++] = ' ';
            else if (c[i] == '/' && next == '*') BlankBlockComment(c, ref i);
            else if (c[i] == '"') LexString(c, ref i, blank);
            else if (c[i] == '\'') LexChar(c, ref i, blank);
            else if (inHole && c[i] == '{') { depth++; i++; }
            else if (inHole && c[i] == '}')
            {
                i++;
                if (depth == 0) return;
                depth--;
            }
            else i++;
        }
    }

    static void BlankBlockComment(char[] c, ref int i)
    {
        while (i < c.Length && !(c[i] == '*' && i + 1 < c.Length && c[i + 1] == '/'))
            BlankKeepingBreaks(c, i++, true);
        for (var closing = 0; closing < 2 && i < c.Length; closing++) c[i++] = ' ';
    }

    static void LexString(char[] c, ref int i, bool blank)
    {
        var verbatim = (i >= 1 && c[i - 1] == '@') || (i >= 2 && c[i - 1] == '$' && c[i - 2] == '@');
        var interpolated = (i >= 1 && c[i - 1] == '$') || (i >= 2 && c[i - 1] == '@' && c[i - 2] == '$');
        i++;
        while (i < c.Length)
        {
            var next = i + 1 < c.Length ? c[i + 1] : '\0';
            if (verbatim && c[i] == '"')
            {
                if (next != '"') { i++; return; }
                BlankPair(c, ref i, blank);
            }
            else if (!verbatim && c[i] == '\\')
            {
                BlankKeepingBreaks(c, i++, blank);
                if (i < c.Length && c[i] != '\n') BlankKeepingBreaks(c, i++, blank);
            }
            else if (!verbatim && c[i] == '"') { i++; return; }
            else if (!verbatim && c[i] == '\n') return;
            else if (interpolated && c[i] == '{' && next == '{') BlankPair(c, ref i, blank);
            else if (interpolated && c[i] == '}' && next == '}') BlankPair(c, ref i, blank);
            else if (interpolated && c[i] == '{') { i++; LexCode(c, ref i, true, blank); }
            else BlankKeepingBreaks(c, i++, blank);
        }
    }

    static void LexChar(char[] c, ref int i, bool blank)
    {
        // One character or an escape such as '\'' or '\u0041'; anything longer is not a char literal.
        var end = i + (i + 1 < c.Length && c[i + 1] == '\\' ? 3 : 2);
        while (end < c.Length && c[end] != '\'' && c[end] != '\n' && end - i < 12) end++;
        if (end >= c.Length || c[end] != '\'') { i++; return; }
        for (var k = i + 1; k < end; k++) BlankKeepingBreaks(c, k, blank);
        i = end + 1;
    }

    static void BlankPair(char[] c, ref int i, bool blank)
    {
        BlankKeepingBreaks(c, i++, blank);
        BlankKeepingBreaks(c, i++, blank);
    }

    static void BlankKeepingBreaks(char[] c, int i, bool blank)
    {
        if (blank && c[i] != '\n') c[i] = ' ';
    }
}

// The marker "// trap-ok: <reason>" or "// trap-ok: <trap or check name>: <reason>" on a line takes the line out of a rule, but only
// with a reason of its own words. The reason names no reference document, no unpublished work and no private path.
internal static class TrapOk
{
    const int MinReasonWords = 3;
    static readonly Regex Marker = new(@"//\s*trap-ok:(?<text>.*)$");
    static readonly Regex Keyed = new(@"^\s*(?<key>[^:]{1,40}?)\s*:\s*(?<reason>\S.*)$");
    static readonly Regex[] Forbidden =
    {
        new("(?i)\\bIS" + "IL\\b"), new("(?i)\\bDU" + "MP\\b"), new("(?i)\\bLAB" + "OR"), new("(?i)\\bLAB" + "\\b"),
        new(@"\bL-\d+\b"), new(@"[A-Za-z]:[\\/]"), new(@"(?i)\b\d{2}-[\w-]+\.md\b"), new(@"(?i)\bINDEX\.md\b"),
    };

    // The text after "trap-ok:" of a line, or null when it has no marker.
    internal static string TextOf(string line)
    {
        var match = Marker.Match(line);
        return match.Success ? match.Groups["text"].Value.Trim() : null;
    }

    // Splits the text into the trap or check name it starts with (null in the plain form) and the reason.
    internal static (string Key, string Reason) Split(string text)
    {
        var keyed = Keyed.Match(text);
        return keyed.Success && IsKnownKey(keyed.Groups["key"].Value) ? (keyed.Groups["key"].Value.Trim(), keyed.Groups["reason"].Value) : (null, text);
    }

    // Null when the marker is well formed, else what is wrong with it.
    internal static string Problem(string text)
    {
        if (Regex.Matches(Split(text).Reason, @"[\p{L}\p{N}]+").Count < MinReasonWords) return "gives no reason of its own words";
        return Forbidden.Any(pattern => pattern.IsMatch(text)) ? "names a reference document, unpublished work or a private path" : null;
    }

    internal static bool Exempts(SourceHit hit, SourceRule rule)
    {
        for (var line = hit.FirstLine; line <= hit.LastLine; line++)
            if (Accepts(hit.File.RawLines[line - 1], rule)) return true;
        return false;
    }

    static bool Accepts(string line, SourceRule rule)
    {
        var text = TextOf(line);
        if (text == null || Problem(text) != null) return false;
        var key = Split(text).Key;
        return key == null || KeyMatches(key, rule);
    }

    static bool KeyMatches(string key, SourceRule rule)
        => string.Equals(key, rule.ExemptionKey, StringComparison.OrdinalIgnoreCase) || key == rule.Id
            || (rule.TrapNumber > 0 && key == "Falle " + rule.TrapNumber);

    static bool IsKnownKey(string key) => Regex.IsMatch(key.Trim(), @"^Falle \d+$") || SourceRules.All.Any(rule => KeyMatches(key.Trim(), rule));
}

// A native list: a declaration of Il2CppSystem.Collections.Generic.List<T>, also through a using alias, and what it is filled with.
internal static class NativeListRule
{
    // Element types that are not IL2CPP structs: Add and Insert copy them correctly. A new element type has to be sorted in here.
    // Vector2Int is two ints, a plain struct without a reference field; the self-test file fills a native list of it and reads it back.
    // The last line holds the reference types Frieren fills lists with: string and Sprite are classes and IExecutionData is an interface.
    // KeyInputType is an enum, an int in the interop (ShiftTwin fills the item slots of a key with it).
    static readonly HashSet<string> NotStructs = new(StringComparer.Ordinal)
    {
        "Il2CppSystem.Object", "IEntity", "Vector2", "Vector2Int", "int", "IApplyData", "ItemTableData", "UnitTableData", "RecipeTableData",
        "DungeonPreRoomData", "TooltipValueRow", "ComponentSaveData", "RecruitCandidateData", "ItemEntity", "KeyInputType",
        "string", "Sprite", "IExecutionData"
    };

    const string ListHead = @"(?:global::)?Il2CppSystem\s*\.\s*Collections\s*\.\s*Generic\s*\.\s*List\s*<";
    static readonly Regex Head = new(@"(?<![\w.])" + ListHead);
    static readonly Regex AliasHead = new("^" + ListHead);
    static readonly Regex AfterType = new(@"\G\s+(?<name>\w+)\s*(?<end>[,)=;{])");
    static readonly Regex Assigned = new(@"\b(?<name>\w+)\s*(?:\?\?)?=\s*new\s+(?<type>(?:global::)?[\w.]+)(?<open>\s*<)?");

    // A declared list: its name, element type, the place it is declared, a method parameter or not, and whether it is a member
    // set in an object initializer.
    sealed record Declaration(string Name, string Element, int At, bool Parameter, bool Initializer);

    internal static IEnumerable<SourceHit> Find(SourceRule rule, SourceFile file)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in Declarations(file))
        {
            if (IsNotStruct(declaration.Element)) continue;
            var (start, end) = ScopeOf(file.Code, declaration);
            var call = new Regex(@"\b" + Regex.Escape(declaration.Name) + @"\s*\.\s*" + rule.Pattern + @"\s*\(");
            foreach (Match match in call.Matches(file.Code.Substring(start, end - start)))
            {
                var at = start + match.Index;
                if (found.Add(at + ":" + declaration.Name))
                    yield return new SourceHit(file, file.LineOf(at), file.LineOf(at + match.Length - 1), declaration.Name + "<" + declaration.Element + ">");
            }
        }
    }

    static bool IsNotStruct(string element)
    {
        var plain = element.Replace("global::", "");
        return NotStructs.Contains(plain) || (!plain.Contains('<') && NotStructs.Contains(plain.Substring(plain.LastIndexOf('.') + 1)));
    }

    static IEnumerable<Declaration> Declarations(SourceFile file)
    {
        var nativeAliases = file.Aliases.Where(alias => AliasHead.IsMatch(alias.Value)).ToDictionary(alias => alias.Key, alias => alias.Value);
        foreach (var declaration in Assignments(file, nativeAliases)) yield return declaration;
        foreach (var declaration in TypedByName(file)) yield return declaration;
        foreach (var declaration in TypedByAlias(file, nativeAliases)) yield return declaration;
    }

    // name = new List<T>(...), name ??= new Alias(...), also as a member in an object initializer (Tiles = new List<T>()).
    static IEnumerable<Declaration> Assignments(SourceFile file, Dictionary<string, string> nativeAliases)
    {
        var code = file.Code;
        foreach (Match match in Assigned.Matches(code))
        {
            var type = match.Groups["type"].Value;
            string element;
            var after = match.Index + match.Length;
            if (match.Groups["open"].Success && Regex.IsMatch(type, @"^(?:global::)?Il2CppSystem\.Collections\.Generic\.List$"))
            {
                element = Arguments(code, match.Index + match.Length - 1, out after);
            }
            else if (nativeAliases.TryGetValue(type, out var target)) element = Arguments(target, target.IndexOf('<'), out _);
            else continue;
            yield return new Declaration(match.Groups["name"].Value, element, match.Index, false, IsInitializerMember(code, after));
        }
    }

    // Il2CppSystem.Collections.Generic.List<T> name followed by one of , ) = ; {
    static IEnumerable<Declaration> TypedByName(SourceFile file)
    {
        foreach (Match head in Head.Matches(file.Code))
        {
            var element = Arguments(file.Code, head.Index + head.Length - 1, out var after);
            var name = AfterType.Match(file.Code, after);
            if (name.Success) yield return Typed(name, element, head.Index);
        }
    }

    // Alias name followed by the same.
    static IEnumerable<Declaration> TypedByAlias(SourceFile file, Dictionary<string, string> nativeAliases)
    {
        foreach (var alias in nativeAliases)
            foreach (Match match in Regex.Matches(file.Code, @"(?<![\w.])" + Regex.Escape(alias.Key) + @"\s+(?<name>\w+)\s*(?<end>[,)=;{])"))
                yield return Typed(match, Arguments(alias.Value, alias.Value.IndexOf('<'), out _), match.Index);
    }

    static Declaration Typed(Match match, string element, int at)
        => new(match.Groups["name"].Value, element, at, ",)".Contains(match.Groups["end"].Value[0]), false);

    // The text between the angle brackets that open at index open; after is the index behind the closing one.
    static string Arguments(string text, int open, out int after)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '<') depth++;
            else if (text[i] == '>' && --depth == 0)
            {
                after = i + 1;
                return Regex.Replace(text.Substring(open + 1, i - open - 1), @"\s+", " ").Trim();
            }
        }
        after = text.Length;
        return "";
    }

    // After new List<T>(...) comes , or } when the list is a member of an object initializer, else ; or something else.
    static bool IsInitializerMember(string code, int index)
    {
        var at = SkipSpace(code, index);
        if (at < code.Length && code[at] == '(') at = SkipSpace(code, MatchingClose(code, at, '(', ')') + 1);
        if (at < code.Length && code[at] == '{') at = SkipSpace(code, MatchingClose(code, at, '{', '}') + 1);
        return at < code.Length && (code[at] == ',' || code[at] == '}');
    }

    static int SkipSpace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        return index;
    }

    static int MatchingClose(string text, int open, char opening, char closing)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == opening) depth++;
            else if (text[i] == closing && --depth == 0) return i;
        }
        return text.Length - 1;
    }

    // From the declaration to the end of the block it is declared in; a parameter to the end of its method; a member of an object
    // initializer to the end of the block around the statement.
    static (int Start, int End) ScopeOf(string code, Declaration declaration)
    {
        if (!declaration.Initializer) return (declaration.At, ScopeEnd(code, declaration.At, declaration.Parameter));
        var block = EnclosingBrace(code, declaration.At, 1);
        return block < 0 ? (declaration.At, code.Length) : (block + 1, ScopeEnd(code, block + 1, false));
    }

    static int ScopeEnd(string code, int from, bool parameter)
    {
        var depth = 0;
        var seenBrace = false;
        for (var i = from; i < code.Length; i++)
        {
            if (code[i] == '{') { depth++; seenBrace = true; }
            else if (code[i] == '}')
            {
                depth--;
                if (depth < 0) return i;
                if (parameter && depth == 0) return i + 1;
            }
            else if (code[i] == ';' && parameter && depth == 0 && !seenBrace) return i + 1;
        }
        return code.Length;
    }

    // The index of the opening brace that encloses index, skipping the innermost skip ones; -1 when there is none.
    static int EnclosingBrace(string code, int index, int skip)
    {
        var depth = 0;
        for (var i = index - 1; i >= 0; i--)
        {
            if (code[i] == '}') depth++;
            else if (code[i] == '{')
            {
                if (depth > 0) depth--;
                else if (skip-- == 0) return i;
            }
        }
        return -1;
    }
}

// The reader: applies a rule to a file.
internal static class SourceRuleReader
{
    // Every hit of the rule in the file, exempted or not.
    internal static IEnumerable<SourceHit> Find(SourceRule rule, SourceFile file, SourceContext context)
        => rule.Kind switch
        {
            SourceRuleKind.NativeList => NativeListRule.Find(rule, file),
            SourceRuleKind.NoticeKey => UnregisteredNotices(rule, file, context),
            _ => LineHits(rule, file),
        };

    // The hits the files leave after the markers took their lines out.
    internal static List<SourceHit> Violations(SourceRule rule, IEnumerable<SourceFile> files, SourceContext context)
    {
        var violations = new List<SourceHit>();
        foreach (var file in files)
            if (!rule.Allows(file))
                violations.AddRange(Find(rule, file, context).Where(hit => !TrapOk.Exempts(hit, rule)));
        return violations;
    }

    // Hits of a rule in a snippet of source, markers taken into account.
    internal static int HitCount(string ruleId, string source)
    {
        var rule = SourceRules.ById(ruleId);
        return Violations(rule, new[] { new SourceFile(rule.Folders[0], "Probe.cs", source) }, ProbeContext).Count;
    }

    // The lines a rule hits in a snippet.
    internal static List<int> HitLines(string ruleId, string source)
    {
        var rule = SourceRules.ById(ruleId);
        return Violations(rule, new[] { new SourceFile(rule.Folders[0], "Probe.cs", source) }, ProbeContext).Select(hit => hit.FirstLine).ToList();
    }

    // Each identifier __<number> of the source, the arguments a Harmony patch binds by position.
    internal static List<string> IndexBoundArguments(string source)
    {
        var rule = SourceRules.ById("index-binding");
        var file = new SourceFile(rule.Folders[0], "Probe.cs", source);
        return Regex.Matches(file.Code, rule.Pattern).Select(match => match.Value).ToList();
    }

    internal static readonly SourceContext ProbeContext = new("(Known.Key,");

    static IEnumerable<SourceHit> LineHits(SourceRule rule, SourceFile file)
    {
        foreach (Match match in Regex.Matches(file.Code, rule.Pattern))
            yield return new SourceHit(file, file.LineOf(match.Index), file.LineOf(match.Index + match.Length - 1), "");
    }

    static IEnumerable<SourceHit> UnregisteredNotices(SourceRule rule, SourceFile file, SourceContext context)
    {
        foreach (Match match in Regex.Matches(file.Code, rule.Pattern))
            if (!context.RegisteredNoticeRows.Contains("(" + match.Groups[1].Value + ",", StringComparison.Ordinal))
                yield return new SourceHit(file, file.LineOf(match.Index), file.LineOf(match.Index + match.Length - 1), match.Groups[1].Value);
    }
}

// The source folders of the project the tests run in, read once.
internal sealed class SourceTree
{
    static readonly Lazy<SourceTree> current = new(Load);
    static readonly string[] SkippedDirectories = { "bin", "obj", "Tests" };

    readonly Dictionary<string, List<SourceFile>> files;

    SourceTree(string root, Dictionary<string, List<SourceFile>> files)
    {
        Root = root;
        this.files = files;
        Context = new SourceContext(NoticeRows());
    }

    internal static SourceTree Current => current.Value;

    // The directory with the source folders, or null when none is found above the test program.
    internal string Root { get; }
    // The canonical project has Delvers.Canonical.props; a public export does not.
    internal bool Canonical => Root != null && File.Exists(Path.Combine(Root, "Delvers.Canonical.props"));
    internal SourceContext Context { get; }

    internal IReadOnlyList<SourceFile> Files(string folder) => files.TryGetValue(folder, out var list) ? list : new List<SourceFile>();

    internal IEnumerable<SourceFile> Files(IEnumerable<string> folders) => folders.SelectMany(folder => Files(folder));

    internal int Count(IEnumerable<string> folders) => folders.Sum(folder => Files(folder).Count);

    // The registered text rows of Core, which a notice key has to be one of.
    string NoticeRows() => Files(SourceFolders.Core).FirstOrDefault(file => file.Name == "CoreTextRows.cs")?.WithStrings ?? "";

    // The root is the first directory above the test program that holds a project of one of the packages, so a public export with
    // only Core, or only Frieren, finds it as well.
    static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Core", "DungeonSettlersDelvers.Core.csproj"))
                || File.Exists(Path.Combine(directory.FullName, "Frieren", "DungeonSettlersDelvers.Frieren.csproj"))
                || File.Exists(Path.Combine(directory.FullName, "SelfTest", "DungeonSettlersDelvers.SelfTest.csproj")))
                return directory.FullName;
        return null;
    }

    static SourceTree Load()
    {
        var root = FindRoot();
        var files = new Dictionary<string, List<SourceFile>>();
        if (root != null)
            foreach (var folder in SourceFolders.All)
                files[folder] = ReadFolder(root, folder);
        ShareGlobalAliases(files.Values.SelectMany(list => list).ToList());
        return new SourceTree(root, files);
    }

    static List<SourceFile> ReadFolder(string root, string folder)
    {
        var path = Path.Combine(root, folder);
        if (File.Exists(path)) return new List<SourceFile> { new(folder, folder, File.ReadAllText(path)) };
        if (!Directory.Exists(path)) return new List<SourceFile>();
        return Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(relative => !relative.Split('/').SkipLast(1).Any(segment => SkippedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            .OrderBy(relative => relative, StringComparer.Ordinal)
            .Select(relative => new SourceFile(folder, relative, File.ReadAllText(Path.Combine(root, relative))))
            .ToList();
    }

    // A global using alias holds for every file, so a file that uses it without declaring it still resolves it.
    static void ShareGlobalAliases(List<SourceFile> all)
    {
        var global = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in all)
            foreach (var alias in SourceText.AliasesOf(file.Code).Where(alias => alias.Global))
                global[alias.Name] = alias.Target;
        foreach (var file in all)
        {
            var merged = new Dictionary<string, string>(global, StringComparer.Ordinal);
            foreach (var alias in file.Aliases) merged[alias.Key] = alias.Value;
            file.Aliases = merged;
        }
    }
}

// The checks around the table, run by Core's and by Frieren's tests in the same form. check(condition, name) is the test's own Check.
internal static class SourceRuleChecks
{
    internal static void Run(Action<bool, string> check, Func<SourceRule, bool> extra = null, IReadOnlyDictionary<string, string> names = null)
    {
        var tree = SourceTree.Current;
        TableIsConsistent(check);
        FoldersAreFound(check, tree);
        foreach (var rule in SourceRules.All)
            RuleHolds(check, tree, rule, extra, names);
        foreach (var rule in SourceRules.All)
            check(Probe(rule), "source rule probe: " + rule.Id + " finds its bad snippet and leaves its good one alone");
        check(ReaderIgnoresCommentsAndStrings(), "source reader: comments and strings are not code, a call over a line break is found, line numbers stay");
        check(MarkerNeedsReason(), "source reader: a trap-ok marker takes its line out only with a reason of its own words and the right name");
        MarkersAreWellFormed(check, tree);
        TrapListMatchesTable(check, tree);
    }

    static void TableIsConsistent(Action<bool, string> check)
    {
        var rules = SourceRules.All;
        var numbered = rules.Where(rule => rule.TrapNumber > 0).ToList();
        check(rules.Select(rule => rule.Id).Distinct().Count() == rules.Length
                && numbered.Select(rule => rule.TrapNumber).Distinct().Count() == numbered.Count
                && rules.All(rule => rule.Folders.Length > 0 && rule.Folders.All(SourceFolders.All.Contains) && rule.BadSnippet.Length > 0 && rule.GoodSnippet.Length > 0)
                && numbered.All(rule => "SADME".Contains(rule.Stage)) && rules.Where(rule => rule.TrapNumber == 0).All(rule => rule.Stage == '-')
                && rules.All(rule => rule.AllowedHits >= 0),
            "source rule table: every rule has its own id, folders and snippets, every trap number once, an evidence stage S, A, D, M or E");
    }

    // The canonical project has all four folders; an export has some, and each one it lacks is named as skipped, never a failure.
    static void FoldersAreFound(Action<bool, string> check, SourceTree tree)
    {
        var counts = SourceFolders.All.Select(folder => (Folder: folder, Count: tree.Files(folder).Count)).ToList();
        var read = counts.Where(entry => entry.Count > 0).Select(entry => entry.Folder + " " + entry.Count);
        var skipped = counts.Where(entry => entry.Count == 0 && SourceFolders.Directories.Contains(entry.Folder)).Select(entry => entry.Folder).ToList();
        var found = counts.Count(entry => entry.Count > 0);
        check(tree.Root != null && found > 0 && (!tree.Canonical || skipped.Count == 0),
            "source rules read " + string.Join(", ", read) + (skipped.Count > 0 ? " (übersprungen: " + string.Join(", ", skipped) + ")" : ""));
    }

    static void RuleHolds(Action<bool, string> check, SourceTree tree, SourceRule rule, Func<SourceRule, bool> extra, IReadOnlyDictionary<string, string> names)
    {
        if (tree.Count(rule.Folders) == 0) return;
        var hits = SourceRuleReader.Violations(rule, tree.Files(rule.Folders), tree.Context);
        var name = names?.GetValueOrDefault(rule.Id) ?? rule.CheckName;
        check(Within(rule, hits.Count) && (extra?.Invoke(rule) ?? true), name + " (" + HitsText(rule, hits) + ")");
    }

    // A rule holds with as many hits as it allows; a rule without allowed hits holds with none.
    internal static bool Within(SourceRule rule, int hits) => hits <= rule.AllowedHits;

    // The hits as a run prints them: every one by place; for a rule with allowed hits the count against the allowed number, and the
    // places only when there are too many.
    internal static string HitsText(SourceRule rule, List<SourceHit> hits)
    {
        var places = string.Join(", ", hits.Select(hit => hit.Label));
        if (rule.AllowedHits == 0) return places;
        return hits.Count + " of " + rule.AllowedHits + " allowed" + (Within(rule, hits.Count) ? "" : ": " + places);
    }

    // The bad snippet is found, the good one is not; a rule that finds nothing in its bad snippet guards nothing.
    static bool Probe(SourceRule rule)
    {
        var bad = new SourceFile(rule.Folders[0], "Probe.cs", rule.BadSnippet);
        var good = new SourceFile(rule.Folders[0], "Probe.cs", rule.GoodSnippet);
        return SourceRuleReader.Find(rule, bad, SourceRuleReader.ProbeContext).Any()
            && !SourceRuleReader.Find(rule, good, SourceRuleReader.ProbeContext).Any();
    }

    static bool ReaderIgnoresCommentsAndStrings()
    {
        var source = "var a = \"x.GetEntityObject(1)\"; // y.GetEntityObject(2)\n"
            + "/* z.GetEntityObject(3) */\n"
            + "var b = $\"{w.GetEntityObject(4)}\";\n"
            + "var c = @\"say \"\"q.GetEntityObject(5)\"\"\";\n"
            + "v\n  .GetEntityObject(6);\n";
        return SourceRuleReader.HitLines("entity-object", source).SequenceEqual(new[] { 3, 6 });
    }

    static bool MarkerNeedsReason()
    {
        // The marker texts are put together at run time, so that no line of this file is itself a marker with a forbidden word.
        return MarkedHits(" entity object: the caller checks the activation first") == 0
            && MarkedHits(" the caller checks the activation first") == 0
            && MarkedHits("") == 1
            && MarkedHits(" entity object:") == 1
            && MarkedHits(" entity object: fine") == 1
            && MarkedHits(" IsHostile: the caller checks the activation first") == 1
            && MarkedHits(" entity object: see 01-" + "combat.md for the reason") == 1
            && MarkedHits(" entity object: read from C" + ":/notes about it") == 1;
    }

    // The hits of an entity object call whose line ends in a trap marker with the given text.
    static int MarkedHits(string markerText) => SourceRuleReader.HitCount("entity-object", "a.GetEntityObject(1); // trap" + "-ok:" + markerText);

    // Also a marker on a line no rule hits has to be well formed, else it would silently excuse the next change to that line.
    static void MarkersAreWellFormed(Action<bool, string> check, SourceTree tree)
    {
        var problems = new List<string>();
        foreach (var file in tree.Files(SourceFolders.All))
            for (var i = 0; i < file.RawLines.Length; i++)
                if (TrapOk.TextOf(file.RawLines[i]) is { } text && TrapOk.Problem(text) is { } problem)
                    problems.Add(file.RelativePath + ":" + (i + 1) + " " + problem);
        check(problems.Count == 0, "every trap-ok marker gives a reason of its own words, naming no reference document, no unpublished work and no private path (" + string.Join(", ", problems) + ")");
    }

    // Each row of the trap list in FALLEN-CORE.md has a rule with its number, stage and test name, and each rule with a number has its row.
    static void TrapListMatchesTable(Action<bool, string> check, SourceTree tree)
    {
        const string name = "every trap of FALLEN-CORE.md has its source rule with the same number, stage and test name, and every numbered rule its row";
        var path = tree.Root == null ? null : Path.Combine(tree.Root, "FALLEN-CORE.md");
        if (path == null || !File.Exists(path))
        {
            check(true, name + " (übersprungen: FALLEN-CORE.md fehlt)");
            return;
        }
        var rows = Regex.Matches(File.ReadAllText(path),
            @"(?m)^\|\s*(?<nr>\d+)\s*\|\s*(?<stage>[A-Z])\s*\|.*\|\s*`(?<check>[^`]+)`\s*\|\s*$");
        var problems = new List<string>();
        if (rows.Count == 0) problems.Add("no trap rows found");
        foreach (Match row in rows)
        {
            var number = int.Parse(row.Groups["nr"].Value);
            var rule = SourceRules.All.FirstOrDefault(candidate => candidate.TrapNumber == number);
            if (rule == null) problems.Add("trap " + number + " has no rule");
            else if (rule.Stage.ToString() != row.Groups["stage"].Value) problems.Add("trap " + number + " has another stage");
            else if (!rule.CheckName.StartsWith(row.Groups["check"].Value, StringComparison.Ordinal)) problems.Add("trap " + number + " has another test name");
        }
        foreach (var rule in SourceRules.All.Where(rule => rule.TrapNumber > 0))
            if (!rows.Any(row => int.Parse(row.Groups["nr"].Value) == rule.TrapNumber)) problems.Add("rule " + rule.Id + " has no row");
        check(problems.Count == 0, name + " (" + string.Join(", ", problems) + ")");
    }
}
