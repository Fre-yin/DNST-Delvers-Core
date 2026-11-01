#if BEPINEX
using AffectTarget = global::Refactor.Combat.AffectTarget;
using AttackType = global::Refactor.Combat.AttackType;
using DamageType = global::Refactor.Combat.DamageType;
#else
using AffectTarget = Il2CppRefactor.Combat.AffectTarget;
using AttackType = Il2CppRefactor.Combat.AttackType;
using DamageType = Il2CppRefactor.Combat.DamageType;
#endif

namespace DungeonSettlersDelvers.Core;


// The role of each party member, read from its basic attack and quick slots. Cached per unit until
// that loadout changes; each new role is logged once.
internal static class GroupAiRoles
{
    private static readonly Dictionary<string, (string Slots, GroupAiRole Role)> cache = new(StringComparer.Ordinal);

    internal static void Reset() => cache.Clear();

    internal static GroupAiRole? Known(string guid)
        => cache.TryGetValue(guid, out var entry) ? entry.Role : null;

    internal static GroupAiRole Of(string guid, IReadOnlyList<string> slots, string basicAttack)
    {
        var loadout = basicAttack + "|" + string.Join("+", slots);
        if (cache.TryGetValue(guid, out var entry) && entry.Slots == loadout) return entry.Role;
        var basic = Sheet(basicAttack);
        var role = GroupAiRules.DetectRole(slots, basicAttack, IsAllySkill,
            basic != null && basic.HitType == AttackType.Ranged,
            basic != null && basic.DamageType == DamageType.Magic);
        cache[guid] = (loadout, role);
        DelversHost.Info(GroupAiRules.RoleLine(guid, role, basicAttack, slots));
        return role;
    }

    private static bool IsAllySkill(string key)
    {
        var data = Sheet(key);
        return data != null && (data.AffectTarget == AffectTarget.SingleAlly || data.AffectTarget == AffectTarget.MultipleAlly);
    }

    internal static SkillTableData Sheet(string key)
    {
        try
        {
            return string.IsNullOrEmpty(key) ? null : DataSheetManager.Instance?.GetSkillData(key);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
