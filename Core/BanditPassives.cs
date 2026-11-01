using HarmonyLib;
#if BEPINEX
using PassiveActionContainer = global::Refactor.Tick.PassiveActionContainer;
#else
using PassiveActionContainer = Il2CppRefactor.Tick.PassiveActionContainer;
#endif

namespace DungeonSettlersDelvers.Core;

// A Core bandit's passives in the game's passive table (BanditPassiveRules). Right after the game takes the unit into the map's
// PassiveActionContainer (RegisterEntity), and again at a bandit's decision: every passive its SkillComponent lists (_passiveSkills:
// the row's, or one a test gave it) that the table lacks is registered with the game's own Register, never RegisterEntity (which
// builds the unit's table anew). At a decision a unit is looked at again only when its count of listed passives changes. The game
// drops the table with the unit; after a load the check runs again. CORE_BANDIT_PASSIVES per registration, capped.
internal static class BanditPassives
{
    private static readonly Dictionary<string, int> seen = new(StringComparer.Ordinal);
    private static readonly HashSet<string> failed = new(StringComparer.Ordinal);
    private static int lines;

    internal static void Reset()
    {
        seen.Clear();
        failed.Clear();
        lines = 0;
    }

    internal static void Check(UnitEntity unit, MapType map)
    {
        if (unit == null || unit.IsDead) return;
        var container = CoreGame.TickHandler(map)?._passiveActions;
        // Not held yet: tried again at the next decision.
        if (container == null || !container.IsEntityRegistered(unit.Cast<IEntity>())) return;
        Register(container, unit);
    }

    // The game took the unit into its passive table (PassiveActionContainer.RegisterEntity): a bandit's passives right away, so
    // one that never fights has them too (-I2: guards and swordsmen without a decision had none).
    internal static void OnHeld(PassiveActionContainer container, IEntity target)
    {
        var unit = target?.TryCast<UnitEntity>();
        if (container == null || unit == null || unit.IsDead || !CoreUnitRules.IsCoreUnit(unit.Key)) return;
        // The game builds the unit's table anew here, so it is looked at again whatever was seen before.
        Register(container, unit, true);
    }

    private static void Register(PassiveActionContainer container, UnitEntity unit, bool anew = false)
    {
        var entity = unit.Cast<IEntity>();
        var listed = CoreGame.Component<SkillComponent>(entity)?._passiveSkills;
        var count = listed?.Count ?? 0;
        var guid = unit.Guid.ToString();
        if (count == 0 || (!anew && seen.TryGetValue(guid, out var before) && before == count)) return;
        var keys = new List<string>();
        for (var i = 0; i < count; i++) keys.Add(listed[i]);
        var missing = BanditPassiveRules.ToRegister(keys, key => failed.Contains(key) || Holds(container, entity, key));
        var registered = new List<string>();
        foreach (var key in missing)
        {
            // A passive the game cannot set up for a bandit throws in Register (its OnInit); it is left out for the session, and
            // the decision that asked goes on.
            try { container.Register(entity, key, true); }
            catch (Exception ex)
            {
                if (failed.Add(key)) DelversHost.Warning("CORE_BANDIT_PASSIVES_FAILED key=" + key + " unit=" + unit.Key + ": " + ex.GetType().Name);
                continue;
            }
            if (Holds(container, entity, key)) registered.Add(key);
        }
        if (seen.Count >= 256) seen.Clear();
        seen[guid] = count;
        if (missing.Count > 0 && lines++ < BanditPassiveRules.LogCap)
            DelversHost.Info(BanditPassiveRules.Line(guid, unit.Key, count, missing, registered));
    }

    // Whether the unit's own table (PassiveActionContainer._passiveActions[guid]) holds the key.
    internal static bool Holds(PassiveActionContainer container, IEntity entity, string key)
    {
        var table = container?._passiveActions;
        if (table == null || !table.ContainsKey(entity.Guid)) return false;
        var own = table[entity.Guid];
        return own != null && own.ContainsKey(key);
    }
}

// A bandit taken into the game's passive table (BanditPassives.OnHeld). Arguments by name.
[HarmonyPatch(typeof(PassiveActionContainer), nameof(PassiveActionContainer.RegisterEntity))]
internal static class BanditPassivesHeldPatch
{
    private static void Postfix(PassiveActionContainer __instance, IEntity target)
    {
        try { BanditPassives.OnHeld(__instance, target); }
        catch (Exception ex) { ApocalypseHooks.Report("bandit-passives-held", ex); }
    }
}
