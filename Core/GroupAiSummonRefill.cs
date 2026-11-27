namespace DungeonSettlersDelvers.Core;

// Out of a fight a water mage of the damage or support role summons up to its most again (GroupAiWaterRules):
// the damage dealer a toad and two bubblelings, the support two bubblelings, so it goes full into the next fight. From each look of
// GroupAiFormation, only for a mage that stands (still), while the energy keeps its reserve and the skill is ready
// (GroupAiSummonCast.CastIdle). CORE_AI_ROLE_WATER action=refill-… per summon.
// The summons' walk to their places in the formation and the support's bubblelings beside the most hurt are GroupAiSummonWalk's, on
// SummonControl's hold (a summon takes no plain move order of Core's: its tree puts its IdleTask back after one tick).
internal static class GroupAiSummonRefill
{
    internal static void Look(List<UnitEntity> party, bool fight, Func<UnitEntity, bool> still)
    {
        if (fight || !GroupAiSettings.Enabled) return;
        var viewer = CoreGame.Flow?._campaignData?.PlayerUnitDataContainer?.QuickSlotViewer;
        foreach (var mage in party)
        {
            if (mage == null || mage.IsDead || !still(mage)) continue;
            var guid = mage.Guid.ToString();
            var role = GroupAiRoles.PlayerOf(guid);
            if (role is not (TacticalRole.Damage or TacticalRole.Support)) continue;
            var slots = viewer?.GetAll(mage.Guid);
            var keys = new List<string>();
            for (var i = 0; slots != null && i < slots.Length; i++)
                if (!string.IsNullOrEmpty(slots[i])) keys.Add(slots[i]);
            if (!GroupAiWaterRules.IsWaterMage(keys)) continue;
            var (bubblings, toads) = GroupAiPolicy.OwnWaterSummons(guid);
            var damage = role == TacticalRole.Damage && keys.Contains(WeakBanditKitKeys.SummonBubbleToad);
            var step = GroupAiWaterRules.Refill(damage, bubblings, toads, true, keys.Contains(WeakBanditKitKeys.SummonBubbling));
            var cast = step == GroupAiWaterRules.Step.Toad && GroupAiSummonCast.CastIdle(mage, WeakBanditKitKeys.SummonBubbleToad);
            if (!cast && bubblings < GroupAiWaterRules.Bubblings && keys.Contains(WeakBanditKitKeys.SummonBubbling))
            {
                step = GroupAiWaterRules.Step.Bubbling;
                cast = GroupAiSummonCast.CastIdle(mage, WeakBanditKitKeys.SummonBubbling);
            }
            if (cast)
                GroupAiPolicy.Log(GroupAiWaterRules.Line(guid, TacticalRoles.Word(role), "refill-" + step.ToString().ToLowerInvariant(), null, 0f, bubblings, toads,
                    CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
        }
    }
}
