namespace DungeonSettlersDelvers.Core;

// The diagnostics groups, rules without the game. Core's probes only watch and write log lines; they are off in a release and
// a tester switches a group on in Core's settings file, one key per group (value 1 or true, as for every Core switch). A group
// that is off writes no probe line and its probe classes are not patched at all (CoreProbes); an unknown group is off.
// A probe class names its group with [ProbeGroup(...)]; which classes the patch plan skips and which ones CoreProbes patches or
// takes back is decided here, from the attribute alone.
internal static class CoreProbeRules
{
    // The eleven groups and their settings keys. Only these exist.
    internal const string Commands = "probes.commands";
    internal const string Hud = "probes.hud";
    internal const string Wound = "probes.wound";
    internal const string Tint = "probes.tint";
    internal const string BossBar = "probes.bossbar";
    internal const string Areas = "probes.areas";
    internal const string Seeds = "probes.seeds";
    internal const string Corpse = "probes.corpse";
    internal const string Text = "probes.text";
    internal const string OneShot = "probes.oneshot";
    internal const string Items = "probes.items";
    // The effect log of the Toolkit's blocks (BanditEffectsForTest, SummonStatusForTest's hit count): a postfix on the game's
    // execution hook, patched only while this group is on.
    internal const string Effects = "probes.effects";

    internal const string KeyPrefix = "probes.";

    // Every group is off until its key says otherwise.
    internal const bool DefaultOn = false;

    internal static readonly string[] Groups =
    {
        Commands, Hud, Wound, Tint, BossBar, Areas, Seeds, Corpse, Text, OneShot, Items, Effects
    };

    internal static bool IsGroup(string group) => group != null && Array.IndexOf(Groups, group) >= 0;

    // Whether a group is on. read is the settings' reader (key, value when the key is missing), CoreSettings.Get in the game.
    // An unknown group is off without asking.
    internal static bool GroupOn(string group, Func<string, bool, bool> read)
        => IsGroup(group) && read(group, DefaultOn);

    // The groups that are on, in the order of Groups.
    internal static List<string> OnGroups(Func<string, bool, bool> read)
    {
        var on = new List<string>();
        foreach (var group in Groups)
            if (GroupOn(group, read)) on.Add(group);
        return on;
    }

    // The group a class is marked with, or null for an ordinary class (also for null).
    internal static string GroupOf(Type type)
    {
        if (type == null) return null;
        var marks = type.GetCustomAttributes(typeof(ProbeGroupAttribute), false);
        return marks.Length == 0 ? null : ((ProbeGroupAttribute)marks[0]).Group;
    }

    internal static bool IsProbeClass(Type type) => GroupOf(type) != null;

    // The types the patch plan of Core's own classes works on: everything but the probe classes, in the given order.
    internal static List<Type> WithoutProbeClasses(IEnumerable<Type> types)
    {
        var kept = new List<Type>();
        foreach (var type in types)
            if (!IsProbeClass(type)) kept.Add(type);
        return kept;
    }

    // The probe classes among the types, in the given order.
    internal static List<Type> ProbeClasses(IEnumerable<Type> types)
    {
        var found = new List<Type>();
        foreach (var type in types)
            if (IsProbeClass(type)) found.Add(type);
        return found;
    }

    // What a switch of the settings changes: the classes to patch now, the patched ones to take back, and the failed ones that
    // may be tried again because their group went off.
    internal sealed class Changes
    {
        internal List<Type> Patch { get; } = new();
        internal List<Type> Unpatch { get; } = new();
        internal List<Type> Forgive { get; } = new();
    }

    // Compares the groups that are on with the state: a class of a group that is on is patched unless it is patched or failed
    // already; a patched class of a group that is off is taken back; a failed class of a group that is off is forgiven.
    // A class with an unknown group counts as off.
    internal static Changes Plan(IEnumerable<Type> probeClasses, ICollection<string> onGroups, ICollection<Type> patched, ICollection<Type> failed)
    {
        var changes = new Changes();
        foreach (var type in probeClasses)
        {
            var on = onGroups.Contains(GroupOf(type) ?? "");
            if (on && !patched.Contains(type) && !failed.Contains(type)) changes.Patch.Add(type);
            else if (!on && patched.Contains(type)) changes.Unpatch.Add(type);
            else if (!on && failed.Contains(type)) changes.Forgive.Add(type);
        }
        return changes;
    }
}

// Marks a probe class with its diagnostics group (one of the keys of CoreProbeRules). Core's patch plan skips the class;
// CoreProbes patches it while the group is on.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
internal sealed class ProbeGroupAttribute : Attribute
{
    public ProbeGroupAttribute(string group) => Group = group;

    internal string Group { get; }
}
