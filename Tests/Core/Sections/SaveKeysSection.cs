using System.Text.RegularExpressions;
using DungeonSettlersDelvers.Core;
using static TestKit;

// For Core's block of the save (the settings follow after the release): the names Core writes into a campaign save
// are frozen here. A save written by one Core version is read by every later one, so a name may never change or vanish without a
// way back, and a new one is a decision of its own: add it to Frozen together with the code that writes it.
internal static class SaveKeysSection
{
    // Every JSON name of CoreSaveData, top-level and nested, pv, pr and px since the presence of unique units;
    // camp, seed, wagon, flip, box and a room part's s since the bandit camp; formations since the tactical formation (its places
    // reuse unit, x and y); casts since its priority casts (per unit three skill keys, "" for an empty place);
    // sets, leader and places since the formation book (eight sets and the active one, which reuses active);
    // formationOff since the formation's switch (the members switched off); wake since the camp's wake on entering
    // became a property of the camp file (true only for a camp that wakes); formationSleep since Prioritize Sleep
    // switches the formation (the members sleep took out).
    static readonly string[] Frozen =
    {
        "active", "apocalypse", "at", "autoUse", "body", "box", "camp", "campaign", "casts", "combat", "cooldown", "count", "duration", "extra", "flip",
        "formationOff", "formationSleep", "formations", "index",
        "itemSlots", "items", "k", "key", "leader", "lifebloom", "list", "m", "management", "next", "on", "ownSkills", "ownUnits", "places", "queue",
        "pr", "pv", "px", "remaining", "research", "revives", "roomFeatures", "s", "seed", "sets", "sickness", "slot", "snapshot", "storageItems", "tasks", "text",
        "total", "undressed", "unit", "units", "version", "wagon", "wake", "wound", "woundfatal", "x", "y"
    };

    internal static void Run()
    {
        var core = FindCoreSourceDirectory();
        var path = core == null ? null : Path.Combine(core, "CoreSaveData.cs");
        Check(path != null && File.Exists(path), "save keys: CoreSaveData.cs stands in Core's sources");
        if (path == null || !File.Exists(path)) return;
        var found = KeysOf(File.ReadAllText(path));
        var added = found.Where(key => !Frozen.Contains(key)).OrderBy(key => key, StringComparer.Ordinal).ToList();
        var gone = Frozen.Where(key => !found.Contains(key)).ToList();
        Check(added.Count == 0, "save keys: every name Core writes into the save is frozen (new: " + string.Join(", ", added) + ")");
        Check(gone.Count == 0, "save keys: no frozen name is gone from the code (gone: " + string.Join(", ", gone) + ")");
        Check(Frozen.Distinct(StringComparer.Ordinal).Count() == Frozen.Length, "save keys: the frozen list holds each name once");
        Check(CoreSaveData.Version == 1, "save keys: the block's version is still 1");
    }

    // The JSON names in the code without comments: indexers ["name"], the helpers Text, Int and Flag, and string constants.
    static HashSet<string> KeysOf(string source)
    {
        var code = Regex.Replace(source, @"//[^\n]*", "");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(code, @"\[""([A-Za-z]+)""\]")) keys.Add(match.Groups[1].Value);
        foreach (Match match in Regex.Matches(code, @"\b(?:Text|Int|Flag)\(\w+, ""([A-Za-z]+)""")) keys.Add(match.Groups[1].Value);
        foreach (Match match in Regex.Matches(code, @"const string \w+ = ""([A-Za-z]+)""")) keys.Add(match.Groups[1].Value);
        return keys;
    }
}
