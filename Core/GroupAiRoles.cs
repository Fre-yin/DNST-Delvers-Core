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


// The role of each party member, read from its basic attack and quick slots; a role the player set in the formation window replaces
// it (GroupAiRoleRules.Effective). Cached per unit until that loadout or the player's role changes; each new
// role is logged once.
internal static class GroupAiRoles
{
    private static readonly Dictionary<string, (string Slots, GroupAiRole Role)> cache = new(StringComparer.Ordinal);

    internal static void Reset() => cache.Clear();

    internal static GroupAiRole? Known(string guid)
        => cache.TryGetValue(guid, out var entry) ? entry.Role : null;

    internal static GroupAiRole Of(string guid, IReadOnlyList<string> slots, string basicAttack)
    {
        var player = PlayerOf(guid);
        var loadout = basicAttack + "|" + string.Join("+", slots) + "|" + player;
        if (cache.TryGetValue(guid, out var entry) && entry.Slots == loadout) return entry.Role;
        var basic = Sheet(basicAttack);
        var ranged = basic != null && basic.HitType == AttackType.Ranged;
        var magic = basic != null && basic.DamageType == DamageType.Magic;
        var kit = GroupAiRules.DetectRole(slots, basicAttack, IsAllySkill, ranged, magic);
        var role = GroupAiRoleRules.Effective(kit, player, ranged, magic);
        cache[guid] = (loadout, role);
        DelversHost.Info(GroupAiRules.RoleLine(guid, role, basicAttack, slots));
        if (player != TacticalRole.Neutral) DelversHost.Info(GroupAiRoleRules.RoleLine(guid, player, kit, role));
        return role;
    }

    // The player's role of a member: only with the full group AI and while a formation applies on the map (GroupAiFormation.RoleOf).
    internal static TacticalRole PlayerOf(string guid)
        => GroupAiSettings.Enabled && Guid.TryParse(guid, out var unit) ? GroupAiFormation.RoleOf(unit) : TacticalRole.Neutral;

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
