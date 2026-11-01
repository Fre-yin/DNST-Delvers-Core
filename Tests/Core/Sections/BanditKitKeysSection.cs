using System.Reflection;
using DungeonSettlersDelvers.Core;
using static TestKit;

// Bandits: the skill keys that the decisions look up (BanditKitKeys) against the kits of the roles (CoreUnitRules).
internal static class BanditKitKeysSection
{
    internal static void Run()
    {
        KeysAreInTheKits();
        EveryConstantIsListed();
    }

    static List<string> KeysOfConstants()
    {
        var keys = new List<string>();
        foreach (var field in typeof(BanditKitKeys).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            if (field.IsLiteral && field.FieldType == typeof(string)) keys.Add((string)field.GetRawConstantValue());
        return keys;
    }

    static void KeysAreInTheKits()
    {
        // Each key a role's decisions look up is a skill of that role's kit (the herbalist's Lifebloom is added by SkillsFor).
        var missing = new List<string>();
        var checkedKeys = 0;
        foreach (var unit in CoreUnitRules.Units)
        {
            var kit = CoreUnitRules.SkillsFor(unit, true);
            foreach (var key in BanditKitKeys.ForRole(unit.Role))
            {
                checkedKeys++;
                if (Array.IndexOf(kit, key) < 0) missing.Add(unit.Role + ":" + key);
            }
        }
        Check(missing.Count == 0 && checkedKeys == 12, "kit keys: every key of a role's decisions is in that role's kit (missing: " + string.Join(",", missing) + ")");
        Check(BanditKitKeys.ForRole("blade").Length == 2 && BanditKitKeys.ForRole("guard").Length == 4 && BanditKitKeys.ForRole("archer").Length == 4
                && BanditKitKeys.ForRole("healer").Length == 2 && BanditKitKeys.ForRole("none").Length == 0 && BanditKitKeys.ForRole(null).Length == 0,
            "kit keys: four roles with their keys, an unknown role has none");
    }

    static void EveryConstantIsListed()
    {
        // A new constant belongs to a role's list, or the check above never sees it; no key twice, all of the game's SKILL_ form.
        var listed = new HashSet<string>();
        foreach (var role in new[] { "blade", "guard", "archer", "healer" })
            foreach (var key in BanditKitKeys.ForRole(role)) listed.Add(key);
        var constants = KeysOfConstants();
        Check(constants.Count == 12 && constants.Count == new HashSet<string>(constants).Count && constants.All(listed.Contains) && listed.Count == constants.Count,
            "kit keys: every constant is in exactly the lists of its role, none twice");
        Check(constants.All(key => key.StartsWith("SKILL_", StringComparison.Ordinal)), "kit keys: every key is a SKILL_ key");
        // The party's own copy of the vines key and the herbalist's Lifebloom stay the same text.
        Check(BanditKitKeys.Vines == GroupAiRules.VinesSkill && BanditKitKeys.Lifebloom == LifebloomRules.SkillKey && GroupAiRules.HealSkills.Contains(BanditKitKeys.Lifebloom),
            "kit keys: vines and Lifebloom are the keys the party's rules and Lifebloom's rules use");
    }
}
