#if BEPINEX
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using DecidePlayerCombatBehaviour = global::Refactor.Task.DecidePlayerCombatBehaviour;
using TargetType = global::Refactor.Combat.TargetType;
using AffectTarget = global::Refactor.Combat.AffectTarget;
#else
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using DecidePlayerCombatBehaviour = Il2CppRefactor.Task.DecidePlayerCombatBehaviour;
using TargetType = Il2CppRefactor.Combat.TargetType;
using AffectTarget = Il2CppRefactor.Combat.AffectTarget;
#endif

namespace DungeonSettlersDelvers.Core;

// The player's priority casts (GroupAiPriorityRules), read from the tactical menu's places (TacticalFormation.AllPriorityCasts).
// Called from the game's skill choice (GroupAiDecideSkillPatch): without the full group AI first, so the places lead and the game
// decides the rest ("einfacher Modus", on every difficulty); with it after the dodge, the moves, the bandage and the global
// cooldown, right before Core's own choice. A place is ready when the unit has learned the skill, it takes part in the game's
// auto choice (TacticalFormation.CastTakesPart) and the game's own check of its skill choice lets it through
// (GroupAiSituation.NativeStateOf: global cooldown, cooldown, use check); the game then picks the target as for any of its skills.
// A unit without auto attack takes no part at all, without a line. A member held in formation skips a place whose skill
// reaches no foe from where it stands ("noreach"), so the choice does not come back every tick to a move the hold cancels (143
// casts logged, none landed). CORE_AI_PRIORITY once per unit, place and change of result.
internal static class GroupAiPriority
{
    private static readonly Dictionary<string, string> lastLogged = new(StringComparer.Ordinal);
    private static readonly HashSet<string> noreachLogged = new(StringComparer.Ordinal);

    internal static void Reset()
    {
        lastLogged.Clear();
        noreachLogged.Clear();
    }

    // Whether the priority places act at all: they belong to the tactical formation's window, so the comfort option tactical
    // formation (QolText) switches them off with it; the places stay saved. The one place to change if they should act alone.
    internal static bool Applies => CoreSettings.TacticalEnabled;

    // The first ready place's skill, else null. emergency: the healer in the author's emergency (full group AI only).
    internal static ActiveSkill_Base Pick(DecidePlayerCombatBehaviour decide, UnitEntity owner, bool emergency = false)
    {
        if (decide == null || owner == null || !Applies) return null;
        if (CoreGame.Component<EntityInfoComponent>(owner.Cast<IEntity>())?.IsBehaviourOption(BehaviourOption.AutoAttack) == false) return null;
        var guid = CoreGame.GuidOf(owner);
        if (!TacticalFormation.AllPriorityCasts().TryGetValue(guid, out var places)) return null;
        var unit = guid.ToString("D");
        List<string> learned = null;
        foreach (var place in GroupAiPriorityRules.Order(places, emergency))
        {
            var key = places[place];
            learned ??= TacticalFormation.ActiveSkills(owner);
            var result = !learned.Contains(key) ? "notlearned"
                : !TacticalFormation.CastTakesPart(owner, key) ? "autooff"
                : GroupAiFormation.Holds(owner) && !InReach(owner, key) ? "noreach"
                : GroupAiSituation.NativeStateOf(decide, owner, key);
            var ready = result == "free";
            Note(unit, place, key, ready ? "cast" : result, emergency);
            if (!ready) continue;
            var skill = CombatDataManager.Instance?.GetActiveSkillData(key);
            if (skill != null) return skill;
        }
        return null;
    }

    // For a held member of the formation a free skill aimed at foes
    // that reaches none from where it stands is "noreach" for the rules (GroupAiPolicy.Decide), which take only free slots; its
    // slots= in CORE_AI_RULE shows it, CORE_AI_FORMATION_NOREACH each time a skill of the unit falls out of reach (once per session
    // hid the line of a later fight). Skills on allies or the unit itself stay as they are; nothing
    // changes without a formation that holds the unit.
    internal static void MarkOutOfReach(GroupAiSituation s)
    {
        if (s?.Owner == null || !GroupAiFormation.Holds(s.Owner)) return;
        foreach (var slot in s.Slots)
        {
            if (!slot.Free || slot.Data?.AffectTarget is not (AffectTarget.SingleHostile or AffectTarget.MultipleHostile)) continue;
            var key = s.Guid + "|" + slot.Key;
            if (InReach(s.Owner, slot.Key))
            {
                noreachLogged.Remove(key);
                continue;
            }
            slot.State = "noreach";
            if (noreachLogged.Add(key)) GroupAiPolicy.Log($"CORE_AI_FORMATION_NOREACH unit={s.Guid} skill={slot.Key}");
        }
    }

    // A skill on the unit itself (TargetType.SelfTarget, a buff like Iron Clad) is always in reach; the others need a foe within their
    // range from where the unit stands (Iron Clad, range 0, stayed noreach for a whole fight).
    private static bool InReach(UnitEntity owner, string key)
    {
        var data = DataSheetManager.Instance?.GetSkillData(key);
        return data?.TargetType == TargetType.SelfTarget || GroupAiFormation.FoeInReach(owner, data?.Range ?? 1);
    }

    private static void Note(string unit, int place, string skill, string result, bool emergency)
    {
        var key = unit + "|" + place.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (lastLogged.TryGetValue(key, out var last) && last == result) return;
        lastLogged[key] = result;
        GroupAiPolicy.Log(GroupAiPriorityRules.Line(unit, place, skill, result, emergency));
    }
}
