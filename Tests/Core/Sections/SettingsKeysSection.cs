using System.Text.RegularExpressions;
using DungeonSettlersDelvers.Core;
using static TestKit;

// For Core's switches in settings.json (the save's names stand in SaveKeysSection): every switch CoreSettings reads
// is frozen here with its default. A player's file outlives every Core version, so a name may never change and a default never
// flip without a decision of its own: change Frozen together with the code. The keys built from a part (a founder, an option, a
// hint, a probe group) are frozen by their form. The number keys read elsewhere keep their own default and range in their rules.
internal static class SettingsKeysSection
{
    static readonly (string Key, bool Default)[] Frozen =
    {
        ("apocalypse.no-pause", true), ("apocalypse.offered", false), ("apocalypse.slow.bar", true),
        ("bandits.areaignoresdowned", true), ("bandits.basic-only", false), ("bandits.camp.probe", false),
        ("bandits.finish-downed-always", false), ("bandits.groupWake", true), ("bandits.sparedownedincone", false),
        ("corpse.burial.warning", true), ("groupai.adds-first", true), ("groupai.bandagesfrombag", false),
        ("groupai.enabled", false), ("groupai.focus.native-healers", true), ("hints.enabled", true),
        ("qol.shield-band", true), ("qol.summons-follow-target", true), ("qol.tactical", true),
        ("revive.scroll.offered", true), ("wounds.fatal.bandagecloses", false), ("wounds.fatal.fromheavyonly", true),
    };

    // The keys built from a part, as CoreSettings writes them.
    static readonly string[] FrozenForms = { "\"founders.\" + ", "\"rules.\" + ", "\"hints.shown.\" + " };

    internal static void Run()
    {
        var core = FindCoreSourceDirectory();
        var path = core == null ? null : Path.Combine(core, "CoreSettings.cs");
        Check(path != null && File.Exists(path), "settings keys: CoreSettings.cs stands in Core's sources");
        if (path == null || !File.Exists(path)) return;
        var source = File.ReadAllText(path);
        var found = SwitchesOf(source, ConstantsOf(core), out var unresolved);
        Check(unresolved.Count == 0, "settings keys: every switch names its key as a text or a constant (open: " + string.Join(", ", unresolved) + ")");
        var frozen = Frozen.ToDictionary(entry => entry.Key, entry => entry.Default, StringComparer.Ordinal);
        var added = found.Keys.Where(key => !frozen.ContainsKey(key)).OrderBy(key => key, StringComparer.Ordinal).ToList();
        var gone = frozen.Keys.Where(key => !found.ContainsKey(key)).ToList();
        var flipped = found.Where(pair => frozen.TryGetValue(pair.Key, out var value) && value != pair.Value).Select(pair => pair.Key).ToList();
        Check(added.Count == 0, "settings keys: every switch Core reads is frozen (new: " + string.Join(", ", added) + ")");
        Check(gone.Count == 0, "settings keys: no frozen switch is gone from the code (gone: " + string.Join(", ", gone) + ")");
        Check(flipped.Count == 0, "settings keys: no switch changed its default (changed: " + string.Join(", ", flipped) + ")");
        Check(frozen.Count == Frozen.Length, "settings keys: the frozen list holds each key once");
        Check(FrozenForms.All(form => source.Contains(form, StringComparison.Ordinal)), "settings keys: the founder, option and hint keys keep their form");
        Check(source.Contains("\".enabled\"", StringComparison.Ordinal), "settings keys: a founder's key still ends in .enabled");
        Check(CoreProbeRules.KeyPrefix == "probes.", "settings keys: the probe groups keep their prefix");
    }

    // Get(key, true|false) in CoreSettings: the key as a text, a constant of the file or Class.Constant of Core.
    static Dictionary<string, bool> SwitchesOf(string source, Dictionary<string, string> constants, out List<string> unresolved)
    {
        var code = Regex.Replace(source, @"//[^\n]*", "");
        var switches = new Dictionary<string, bool>(StringComparer.Ordinal);
        unresolved = new List<string>();
        foreach (Match match in Regex.Matches(code, @"\bGet\((?<key>""[^""]*""|[A-Za-z_][A-Za-z0-9_.]*), (?<value>true|false)\)"))
        {
            var reference = match.Groups["key"].Value;
            string key;
            if (reference.StartsWith("\"", StringComparison.Ordinal)) key = reference.Trim('"');
            else if (!constants.TryGetValue(reference.Contains('.') ? reference : "CoreSettings." + reference, out key))
            {
                unresolved.Add(reference);
                continue;
            }
            switches[key] = match.Groups["value"].Value == "true";
        }
        return switches;
    }

    // Class.Name -> value for every string constant in Core's sources, the class being the last one declared before it.
    static Dictionary<string, string> ConstantsOf(string core)
    {
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(core, "*.cs"))
        {
            var code = File.ReadAllText(file);
            var classes = Regex.Matches(code, @"\bclass (\w+)").Cast<Match>().ToList();
            foreach (Match match in Regex.Matches(code, @"const string (\w+) = ""([^""]*)"""))
            {
                var owner = classes.LastOrDefault(found => found.Index < match.Index);
                if (owner != null) constants[owner.Groups[1].Value + "." + match.Groups[1].Value] = match.Groups[2].Value;
            }
        }
        return constants;
    }
}
