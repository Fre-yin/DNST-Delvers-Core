using System.Text;
using System.Text.RegularExpressions;
using static TestKit;

// The public surface of Core as a list (Tests/Core/PublicApi.txt): every public type and every public member of a public
// type, with parameter types, return type and the modifiers a pack can see. A pack binds to these by name or by signature,
// so a member that appears, disappears or changes shape shows up here and the list changes in the same commit. The list
// records what is public now; it does not say what stays.
internal static class ApiSurfaceSection
{
    const string ListFile = "PublicApi.txt";
    // A member the toolkit binds by name; the reader must find it in Core's real sources.
    const string KnownMember = "DelversCoreRuntime.CoreOption(string) : bool | static";

    internal static void Run()
    {
        ReaderFindsWhatItShould();
        ReaderReportsWhatItCannotRead();
        SurfaceMatchesTheList();
    }

    // A probe source with every shape the reader handles, and text that only looks public (comments, strings, internal
    // types and members): the reader gives exactly the expected lines.
    static void ReaderFindsWhatItShould()
    {
        var problems = new List<string>();
        var found = PublicApiReader.Read(ProbeSource, "probe", problems);
        var missing = Expected.Except(found).ToList();
        var extra = found.Except(Expected).ToList();
        Check(problems.Count == 0 && missing.Count == 0 && extra.Count == 0 && found.OrderBy(line => line, StringComparer.Ordinal).SequenceEqual(Expected),
            "public API: the reader finds every public line of a probe source and nothing else");
        Detail("the reader gave a line that is not expected", extra);
        Detail("the reader missed an expected line", missing);
        Detail("the reader reported a problem", problems);
    }

    // A public form the reader does not know is reported, never skipped.
    static void ReaderReportsWhatItCannotRead()
    {
        var problems = new List<string>();
        PublicApiReader.Read("public static class P { public int this[int i] { get { return i; } } }", "probe", problems);
        PublicApiReader.Read("public static class P { public static void F() { ", "probe", problems);
        Check(problems.Count == 2, "public API: an indexer and unbalanced braces are reported as problems");
        Detail("reported", problems);
    }

    static void SurfaceMatchesTheList()
    {
        var core = FindCoreSourceDirectory();
        var listPath = core == null ? null : Path.Combine(Path.GetDirectoryName(core)!, "Tests", "Core", ListFile);
        var listed = listPath != null && File.Exists(listPath) ? ReadList(listPath) : null;
        Check(listed != null, "public API: Tests/Core/PublicApi.txt stands next to Core's sources");
        if (listed == null) return;

        var problems = new List<string>();
        var actual = SurfaceOf(core, problems);
        Check(problems.Count == 0, "public API: every public declaration of Core's sources can be read");
        Detail("cannot be read", problems);
        Check(actual.Contains(KnownMember), "public API: the reader finds " + KnownMember + " in Core's sources");

        var added = actual.Except(listed).ToList();
        var gone = listed.Except(actual).ToList();
        Check(added.Count == 0 && gone.Count == 0, "public API: Core's public members are exactly the lines of Tests/Core/PublicApi.txt");
        Detail("public in Core, not in the list", added);
        Detail("in the list, not public in Core", gone);
        Check(listed.SequenceEqual(listed.OrderBy(line => line, StringComparer.Ordinal).Distinct()),
            "public API: the list is sorted and holds no line twice");
        HeaderFollowsTheList(listPath, core, listed);
    }

    // The header line names the API version and the checksum of the lines. A list that changes without its header, or an
    // ApiVersion that changes without it, fails here: the version goes up with every change of the public surface.
    static void HeaderFollowsTheList(string listPath, string core, List<string> listed)
    {
        var header = File.ReadAllLines(listPath).Select(line => Regex.Match(line, HeaderPattern)).FirstOrDefault(match => match.Success);
        var source = Regex.Match(File.ReadAllText(Path.Combine(core, "DelversCoreRuntime.cs")), ApiVersionPattern);
        var sum = Checksum(listed);
        Check(header != null && source.Success && header.Groups[1].Value == source.Groups[1].Value && header.Groups[2].Value == sum,
            "public API: the header line holds ApiVersion and the checksum of the list");
        if (header == null || !source.Success) Detail("missing", new[] { header == null ? "the header line \"# Core API <version>, checksum <sum>\"" : "ApiVersion in DelversCoreRuntime.cs" });
        else if (header.Groups[2].Value != sum || header.Groups[1].Value != source.Groups[1].Value)
            Detail("API changed, raise ApiVersion and update the header line",
                new[] { "header " + header.Groups[1].Value + " " + header.Groups[2].Value + ", ApiVersion " + source.Groups[1].Value + ", list " + sum });
    }

    const string HeaderPattern = @"^# Core API (\d+\.\d+\.\d+), checksum ([0-9A-F]{8})\b";
    const string ApiVersionPattern = @"public static readonly string ApiVersion = ""(\d+\.\d+\.\d+)"";";

    // SHA256 over the lines joined by line feeds, the first 8 hex digits in capitals; line ends of the file do not count.
    internal static string Checksum(IEnumerable<string> lines)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines))), 0, 4);

    // What a failed check names, one line each under the FAIL line (nothing is printed when there is nothing to name).
    static void Detail(string what, IEnumerable<string> lines)
    {
        foreach (var line in lines) Console.WriteLine("  " + what + ": " + line);
    }

    // The lines of the list; a line starting with # is a comment.
    static List<string> ReadList(string path) => File.ReadAllLines(path)
        .Where(line => line.Trim().Length > 0 && !line.StartsWith("#", StringComparison.Ordinal)).ToList();

    // Every public line of every source file in Core, sorted. A type split over several files gives its type line once.
    static List<string> SurfaceOf(string core, List<string> problems)
    {
        var lines = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(core, "*.cs"))
        {
            var source = File.ReadAllText(file);
            if (!source.Contains("public", StringComparison.Ordinal)) continue;
            foreach (var line in PublicApiReader.Read(source, Path.GetFileName(file), problems)) lines.Add(line);
        }
        return lines.OrderBy(line => line, StringComparer.Ordinal).ToList();
    }

    const string ProbeSource = @"
using System;
namespace Probe;
// public static void InComment();
public static partial class Probe
{
    /* public static void InBlock(); */
    public const string Text = ""public static void InString();"";
    public static readonly string Version = ""1.0"";
    [Obsolete(""x"")] public static bool Flag { get { lock (Gate) return flag; } private set { flag = value; } }
    public static int Count => items.Count;
    public static IReadOnlyDictionary<string, object> Make(string label, Func<string> text, bool muted = false, params string[] more)
    {
        var inner = $""{(muted ? ""a"" : ""b"")} public static void InInterpolation();"";
        return null;
    }
    public static unsafe void Add<T>(List<T> list, T item) where T : class { }
    internal static void Hidden(int a) { }
    static void Plain() { }
    public interface IInner { string Name { get; } void Run(int count); }
    public enum Mode { Off, On = 1 }
    internal sealed class Skipped { public void Run() { } }
}
public delegate void Handler(Probe sender, int count);
internal static class NotExposed { public static void Run() { } }
";

    static readonly string[] Expected =
    {
        "Handler(Probe, int) : void | delegate",
        "Probe | static class",
        "Probe.Add<T>(List<T>, T) : void | static",
        "Probe.Count : int | static, get",
        "Probe.Flag : bool | static, get",
        "Probe.IInner | interface",
        "Probe.IInner.Name : string | get",
        "Probe.IInner.Run(int) : void",
        "Probe.Make(string, Func<string>, bool = false, params string[]) : IReadOnlyDictionary<string, object> | static",
        "Probe.Mode | enum",
        "Probe.Mode.Off | enum value",
        "Probe.Mode.On | enum value",
        "Probe.Text : string | const",
        "Probe.Version : string | static, readonly",
    };
}

// Reads the public declarations out of the text of a C# source with patterns, not with a compiler: comments, strings and
// directive lines are cut out, the braces give the nesting, and each declaration is read from its header. What it does not
// know (indexers, operators, records, unbalanced braces) is reported as a problem. A later check of the built assembly's
// metadata replaces it.
sealed class PublicApiReader
{
    // The modifiers a pack can see, in the order they are written in a line.
    static readonly string[] ShownModifiers = { "static", "const", "readonly", "volatile", "abstract", "sealed", "virtual", "override", "event" };
    static readonly string[] AccessModifiers = { "public", "private", "protected", "internal" };
    static readonly string[] OtherModifiers = { "partial", "unsafe", "async", "extern", "new" };
    static readonly string[] TypeKinds = { "class", "struct", "interface", "enum", "record" };
    static readonly string[] AccessorNames = { "get", "set", "init" };

    // One declaration of a scope: its header and how it ended. End is ';', '{' (a body follows, from Start to Finish),
    // '=' (an initializer) or '>' (an expression body, "=>").
    readonly struct Declaration
    {
        internal Declaration(string header, char end, int start, int finish)
        {
            Header = header;
            End = end;
            Start = start;
            Finish = finish;
        }

        internal string Header { get; }
        internal char End { get; }
        internal int Start { get; }
        internal int Finish { get; }
    }

    readonly string text;
    readonly string origin;
    readonly List<string> entries = new();
    readonly List<string> problems;

    PublicApiReader(string source, string origin, List<string> problems)
    {
        this.origin = origin;
        this.problems = problems;
        text = Plain(source);
    }

    // The public lines of one source, in the order they stand. Problems are added to the list with the origin's name.
    internal static List<string> Read(string source, string origin, List<string> problems)
    {
        var reader = new PublicApiReader(source, origin, problems);
        reader.ReadScope(0, reader.text.Length, null, false);
        return reader.entries;
    }

    void Problem(string message) => problems.Add(origin + ": " + message);

    // ---- scopes and declarations ----

    void ReadScope(int from, int to, string owner, bool inInterface)
    {
        foreach (var declaration in Split(from, to))
        {
            var rest = Collapse(declaration.Header);
            if (rest.Length == 0) continue;
            if (rest.StartsWith("namespace ", StringComparison.Ordinal))
            {
                if (declaration.End == '{') ReadScope(declaration.Start, declaration.Finish, owner, false);
                continue;
            }
            var modifiers = TakeModifiers(ref rest);
            var exposed = modifiers.Contains("public") || (inInterface && !modifiers.Any(modifier => AccessModifiers.Contains(modifier)));
            if (!exposed) continue;
            var kind = FirstWord(rest);
            if (kind == "delegate") ReadDelegate(rest.Substring(kind.Length).TrimStart(), modifiers, owner);
            else if (TypeKinds.Contains(kind)) ReadType(kind, rest, modifiers, declaration, owner);
            else if (owner == null) Problem("a public member outside a type: " + rest);
            else ReadMember(rest, modifiers, declaration, owner);
        }
    }

    void ReadType(string kind, string rest, List<string> modifiers, Declaration declaration, string owner)
    {
        if (kind == "record")
        {
            Problem("records are not read: " + rest);
            return;
        }
        var match = Regex.Match(rest, @"^\w+\s+(\w+(?:<[^>]*>)?)");
        if (!match.Success)
        {
            Problem("a type without a name: " + rest);
            return;
        }
        var full = Qualified(owner, Normalize(match.Groups[1].Value));
        entries.Add(full + " | " + string.Join(" ", Shown(modifiers).Append(kind)));
        if (declaration.End != '{') return;
        if (kind == "enum") ReadEnumValues(declaration, full);
        else ReadScope(declaration.Start, declaration.Finish, full, kind == "interface");
    }

    void ReadEnumValues(Declaration declaration, string owner)
    {
        foreach (var item in SplitTop(text.Substring(declaration.Start, declaration.Finish - declaration.Start)))
        {
            var name = Collapse(StripAttributes(item));
            var equals = name.IndexOf('=');
            if (equals >= 0) name = name.Substring(0, equals).Trim();
            if (name.Length > 0) entries.Add(Qualified(owner, name) + " | enum value");
        }
    }

    void ReadDelegate(string rest, List<string> modifiers, string owner)
        => ReadMethod(rest, Shown(modifiers).Append("delegate"), owner);

    void ReadMember(string rest, List<string> modifiers, Declaration declaration, string owner)
    {
        if (rest.Contains("operator ", StringComparison.Ordinal) || rest.Contains("this[", StringComparison.Ordinal))
        {
            Problem("an indexer or operator is not read: " + rest);
            return;
        }
        var where = rest.IndexOf(" where ", StringComparison.Ordinal);
        if (where >= 0) rest = rest.Substring(0, where);
        if (rest.EndsWith(")", StringComparison.Ordinal)) ReadMethod(rest, Shown(modifiers), owner);
        else ReadValue(rest, Shown(modifiers).ToList(), declaration, owner);
    }

    // "Name(parameters)" with its return type in front (none: a constructor).
    void ReadMethod(string rest, IEnumerable<string> words, string owner)
    {
        var open = MatchingOpen(rest);
        if (open < 0)
        {
            Problem("a method header without its parameter list: " + rest);
            return;
        }
        var (before, name) = SplitName(rest.Substring(0, open).TrimEnd());
        var parameters = Parameters(rest.Substring(open + 1, rest.Length - open - 2));
        if (before.Length > 0)
        {
            entries.Add(Qualified(owner, name) + "(" + parameters + ") : " + Normalize(before) + Suffix(words));
            return;
        }
        if (owner != null && Regex.Replace(owner.Split('.')[^1], "<.*>", "") == name) entries.Add(Qualified(owner, "ctor") + "(" + parameters + ")" + Suffix(words));
        else Problem("a method without a return type that is no constructor: " + rest);
    }

    // A property, a field, a constant or an event: "Type Name" with the accessors of a property.
    void ReadValue(string rest, List<string> words, Declaration declaration, string owner)
    {
        var (before, name) = SplitName(rest);
        if (before.Length == 0 || before.EndsWith(",", StringComparison.Ordinal))
        {
            Problem("a member this reader cannot split: " + rest);
            return;
        }
        if (declaration.End == '{') words.AddRange(Accessors(declaration));
        else if (declaration.End == '>') words.Add("get");
        entries.Add(Qualified(owner, name) + " : " + Normalize(before) + Suffix(words));
    }

    // The accessors a pack can call: get, set and init without a narrower access word in front.
    IEnumerable<string> Accessors(Declaration property)
    {
        var found = new List<string>();
        foreach (var accessor in Split(property.Start, property.Finish))
        {
            var words = Collapse(accessor.Header).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || !AccessorNames.Contains(words[^1]))
            {
                Problem("an accessor this reader does not know: " + accessor.Header.Trim());
                continue;
            }
            if (words.Length == 1 || words[0] == "public") found.Add(words[^1]);
        }
        return AccessorNames.Where(found.Contains);
    }

    // ---- splitting a scope into declarations ----

    // The declarations of a scope: headers up to ';', a block, '=' or '=>'. Attributes in front are left out; blocks and
    // initializers are skipped, not read.
    List<Declaration> Split(int from, int to)
    {
        var found = new List<Declaration>();
        var header = new StringBuilder();
        var i = from;
        while (i < to)
        {
            var c = text[i];
            if (c == '[' && header.ToString().Trim().Length == 0)
            {
                var attributeEnd = Closing(i, '[', ']', to);
                if (attributeEnd < 0) break;
                i = attributeEnd + 1;
            }
            else if (c == '(' || c == '[')
            {
                var close = Closing(i, c, c == '(' ? ')' : ']', to);
                if (close < 0) break;
                header.Append(text, i, close - i + 1);
                i = close + 1;
            }
            else if (c == ';')
            {
                found.Add(new Declaration(header.ToString(), ';', i, i));
                header.Clear();
                i++;
            }
            else if (c == '{')
            {
                var close = Closing(i, '{', '}', to);
                if (close < 0) break;
                found.Add(new Declaration(header.ToString(), '{', i + 1, close));
                header.Clear();
                i = close + 1;
                // A property with an initializer: "{ get; } = value;".
                var after = i;
                while (after < to && char.IsWhiteSpace(text[after])) after++;
                if (after < to && text[after] == '=') i = EndOfStatement(after, to) + 1;
            }
            else if (c == '=')
            {
                var end = i + 1 < to && text[i + 1] == '>' ? '>' : '=';
                var close = EndOfStatement(i, to);
                found.Add(new Declaration(header.ToString(), end, i, close));
                header.Clear();
                i = close + 1;
            }
            else if (c == '}')
            {
                Problem("a closing brace without its opening one");
                return found;
            }
            else
            {
                header.Append(c);
                i++;
            }
        }
        if (i < to || header.ToString().Trim().Length > 0) Problem("unbalanced braces or a declaration without its end: " + Collapse(header.ToString()));
        return found;
    }

    // The index of the bracket that closes the one at open; -1 when there is none before to.
    int Closing(int open, char opening, char closing, int to)
    {
        var depth = 0;
        for (var i = open; i < to; i++)
        {
            if (text[i] == opening) depth++;
            else if (text[i] == closing && --depth == 0) return i;
        }
        return -1;
    }

    // The ';' that ends the statement starting at from, outside every bracket; to when there is none.
    int EndOfStatement(int from, int to)
    {
        var depth = 0;
        for (var i = from; i < to; i++)
        {
            if (text[i] is '(' or '[' or '{') depth++;
            else if (text[i] is ')' or ']' or '}') depth--;
            else if (text[i] == ';' && depth == 0) return i;
        }
        return to;
    }

    // ---- header words, names and parameters ----

    static List<string> TakeModifiers(ref string rest)
    {
        var modifiers = new List<string>();
        while (true)
        {
            var word = FirstWord(rest);
            if (!ShownModifiers.Contains(word) && !AccessModifiers.Contains(word) && !OtherModifiers.Contains(word)) return modifiers;
            modifiers.Add(word);
            rest = rest.Substring(word.Length).TrimStart();
        }
    }

    static string FirstWord(string rest) => Regex.Match(rest, @"^\w+").Value;

    // The modifiers of the list a pack can see, in a fixed order.
    static IEnumerable<string> Shown(List<string> modifiers) => ShownModifiers.Where(modifiers.Contains);

    static string Suffix(IEnumerable<string> words)
    {
        var list = words.ToList();
        return list.Count == 0 ? "" : " | " + string.Join(", ", list);
    }

    static string Qualified(string owner, string name) => owner == null ? name : owner + "." + name;

    // "type name" to the type in front and the name (with its generic parameters, if it has any).
    static (string Before, string Name) SplitName(string declaration)
    {
        var end = declaration.Length;
        if (declaration.EndsWith(">", StringComparison.Ordinal))
        {
            var depth = 0;
            for (var i = declaration.Length - 1; i >= 0; i--)
            {
                if (declaration[i] == '>') depth++;
                else if (declaration[i] == '<' && --depth == 0)
                {
                    end = i;
                    break;
                }
            }
        }
        var start = end;
        while (start > 0 && (char.IsLetterOrDigit(declaration[start - 1]) || declaration[start - 1] == '_')) start--;
        return (declaration.Substring(0, start).TrimEnd(), declaration.Substring(start, end - start) + Normalize(declaration.Substring(end)));
    }

    // The index of the parenthesis that opens the list closed by the last character; -1 when there is none.
    static int MatchingOpen(string rest)
    {
        var depth = 0;
        for (var i = rest.Length - 1; i >= 0; i--)
        {
            if (rest[i] == ')') depth++;
            else if (rest[i] == '(' && --depth == 0) return i;
        }
        return -1;
    }

    // "Type name = default, Type name" to "Type = default, Type": the types and what an optional parameter defaults to.
    static string Parameters(string list)
    {
        var parts = new List<string>();
        foreach (var raw in SplitTop(list))
        {
            var parameter = StripAttributes(raw.Trim());
            var suffix = "";
            var equals = IndexOfTop(parameter, '=');
            if (equals >= 0)
            {
                suffix = " = " + Normalize(parameter.Substring(equals + 1));
                parameter = parameter.Substring(0, equals);
            }
            parts.Add(Normalize(SplitName(Collapse(parameter)).Before) + suffix);
        }
        return string.Join(", ", parts);
    }

    // The parts of a list between the commas that stand outside every bracket (including generic arguments).
    static List<string> SplitTop(string list)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < list.Length; i++)
        {
            if (list[i] is '(' or '[' or '{' or '<') depth++;
            else if (list[i] is ')' or ']' or '}' or '>') depth = Math.Max(0, depth - 1);
            else if (list[i] == ',' && depth == 0)
            {
                parts.Add(list.Substring(start, i - start));
                start = i + 1;
            }
        }
        if (list.Substring(start).Trim().Length > 0) parts.Add(list.Substring(start));
        return parts;
    }

    static int IndexOfTop(string list, char wanted)
    {
        var depth = 0;
        for (var i = 0; i < list.Length; i++)
        {
            if (list[i] is '(' or '[' or '{' or '<') depth++;
            else if (list[i] is ')' or ']' or '}' or '>') depth = Math.Max(0, depth - 1);
            else if (list[i] == wanted && depth == 0) return i;
        }
        return -1;
    }

    static string StripAttributes(string text)
    {
        text = text.TrimStart();
        while (text.StartsWith("[", StringComparison.Ordinal))
        {
            var depth = 0;
            var close = -1;
            for (var i = 0; i < text.Length && close < 0; i++)
            {
                if (text[i] == '[') depth++;
                else if (text[i] == ']' && --depth == 0) close = i;
            }
            if (close < 0) break;
            text = text.Substring(close + 1).TrimStart();
        }
        return text;
    }

    static string Collapse(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    // A type or a default value written the same way whatever the spacing of the source.
    static string Normalize(string text)
    {
        var collapsed = Collapse(text);
        collapsed = Regex.Replace(collapsed, @"\s*([<>\[\]().])\s*", "$1");
        return Regex.Replace(collapsed, @"\s*,\s*", ", ");
    }

    // ---- the source without comments, strings and directives ----

    // Comments, directive lines and the content of strings and characters are cut out; a string stays as "" and a character
    // as '', so no brace, semicolon or word of public inside them is read as code.
    static string Plain(string source)
    {
        var plain = new StringBuilder(source.Length);
        var lineStart = true;
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (c == '/' && next == '/') i = LineEnd(source, i);
            else if (c == '/' && next == '*')
            {
                var close = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? source.Length : close + 2;
                plain.Append(' ');
            }
            else if (c == '#' && lineStart) i = LineEnd(source, i);
            else if (StringStart(source, i, out var quote, out var verbatim, out var interpolated))
            {
                i = quote;
                SkipString(source, ref i, verbatim, interpolated);
                plain.Append("\"\"");
                lineStart = false;
            }
            else if (c == '\'')
            {
                i++;
                while (i < source.Length && source[i] != '\'') i += source[i] == '\\' ? 2 : 1;
                i++;
                plain.Append("''");
                lineStart = false;
            }
            else
            {
                plain.Append(c);
                if (c == '\n') lineStart = true;
                else if (!char.IsWhiteSpace(c)) lineStart = false;
                i++;
            }
        }
        return plain.ToString();
    }

    static int LineEnd(string source, int from)
    {
        var end = source.IndexOf('\n', from);
        return end < 0 ? source.Length : end;
    }

    // Whether a string literal starts at i (with its $ and @ prefixes); quote is the index of its opening quotation mark.
    static bool StringStart(string source, int i, out int quote, out bool verbatim, out bool interpolated)
    {
        verbatim = false;
        interpolated = false;
        quote = i;
        while (quote < source.Length && (source[quote] == '$' || source[quote] == '@'))
        {
            if (source[quote] == '@') verbatim = true;
            else interpolated = true;
            quote++;
        }
        return quote < source.Length && source[quote] == '"';
    }

    // i stands at the opening quotation mark and ends after the closing one.
    static void SkipString(string source, ref int i, bool verbatim, bool interpolated)
    {
        i++;
        while (i < source.Length)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (c == '"' && verbatim && next == '"') i += 2;
            else if (c == '"')
            {
                i++;
                return;
            }
            else if (c == '\\' && !verbatim) i += 2;
            else if (c == '{' && interpolated && next == '{') i += 2;
            else if (c == '{' && interpolated) SkipHole(source, ref i);
            else i++;
        }
    }

    // The code in an interpolated string's braces; i stands at the opening brace and ends after the closing one.
    static void SkipHole(string source, ref int i)
    {
        var depth = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if ((c == '"' || c == '$' || c == '@') && StringStart(source, i, out var quote, out var verbatim, out var interpolated))
            {
                i = quote;
                SkipString(source, ref i, verbatim, interpolated);
            }
            else if (c == '\'')
            {
                i++;
                while (i < source.Length && source[i] != '\'') i += source[i] == '\\' ? 2 : 1;
                i++;
            }
            else
            {
                if (c == '{') depth++;
                else if (c == '}') depth--;
                i++;
                if (depth == 0) return;
            }
        }
    }
}
