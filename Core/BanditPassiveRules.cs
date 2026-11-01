using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The passives of a Core bandit in the game's passive table (BanditPassives), pure. The game's spawn of a non-playable unit
// (UnitSpawner.SetNonPlayableSkill) adds every skill of the row and asks to register it (RegisterPassiveActionRequested), but
// PassiveActionContainer.Register leaves a unit it does not hold yet (it looks the unit up in its own table first), and a
// bandit's skill keys arrive before the unit is held: -I3 saw 2 entries per bandit, its two affecters, none of its
// skills. Once the unit is held, every listed passive that is missing is registered with the game's own Register, as Fix C did
// for the Life Burst alone (GroupAiHealerSeed.CheckLifeBurst).
internal static class BanditPassiveRules
{
    internal const int LogCap = 200;

    // Passives whose code reads the unit's equipment, which a bandit does not have (Core dresses it in its look only): the game's
    // skills that touch the equipment at all. Passive_HealingTouch asks EquipmentReader for the weapon's sub type (-I2,
    // NullReferenceException in its AddSkillRoutine), Passive_ShieldHandling threw in its OnInit (-I3,
    // in Register itself); TwoHandGrip, FireBattleMage and NatureSpirit have equip routines as well. Never registered for a bandit.
    internal static readonly string[] NeedsEquipment =
        { "SKILL_HealingTouch", "SKILL_ShieldHandling", "SKILL_TwoHandGrip", "SKILL_FireBattleMage", "SKILL_NatureSpirit" };

    internal static bool Registrable(string key) => !string.IsNullOrEmpty(key) && Array.IndexOf(NeedsEquipment, key) < 0;

    // The listed passives the table does not hold, in their order, each once; never one that needs the equipment.
    internal static List<string> ToRegister(IReadOnlyList<string> listed, Func<string, bool> held)
    {
        var result = new List<string>();
        if (listed == null) return result;
        foreach (var key in listed)
            if (Registrable(key) && !result.Contains(key) && (held == null || !held(key))) result.Add(key);
        return result;
    }

    // CORE_BANDIT_PASSIVES: what was missing and what the game's Register left in the table.
    internal static string Line(string unit, string key, int listed, IReadOnlyList<string> missing, IReadOnlyList<string> registered)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_PASSIVES unit={0} key={1} listed={2} missing={3} registered={4}",
            unit ?? "-", key ?? "-", listed, Join(missing), Join(registered));

    private static string Join(IReadOnlyList<string> keys)
    {
        if (keys == null || keys.Count == 0) return "-";
        var shortKeys = new List<string>();
        foreach (var key in keys) shortKeys.Add(GroupAiRules.Short(key));
        return string.Join("+", shortKeys);
    }
}
