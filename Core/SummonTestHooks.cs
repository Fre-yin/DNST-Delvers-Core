namespace DungeonSettlersDelvers.Core;

// DelversCoreRuntime, the summon probe's test hooks for the Toolkit's block -Only beschwoerung-gegner: does a bandit's
// bubbleling fight for the bandits (SummonSide)? Internal, called through CoreLink.Internal, so Core's public surface stays as it is.
// Only in the loaded test campaign. A bandit casts with the existing UseEnemySkillForTest(bandit, "SKILL_SummonBubbling", "x,y").
public static partial class DelversCoreRuntime
{
    // Gives the unit a skill the game's way, e.g. SKILL_SummonBubbling to a bandit: a party figure through AddSkillRequested, an
    // enemy as the spawner gives its skills (SkillComponent.AddSkill; AddSkillRequested registers the skill with the party's
    // data and throws KeyNotFound for an enemy, run 20261031-175908). Returns queued, notest or nounit.
    internal static string SummonGiveSkillForTest(string unitGuid, string skillKey)
    {
        if (!LoadedTestCampaign()) return "notest";
        var unit = Guid.TryParse(unitGuid, out var guid) ? CoreGame.FindUnit(guid) : null;
        if (unit == null || string.IsNullOrEmpty(skillKey)) return "nounit";
        if (unit.Faction == FactionType.Player) CoreGame.Enqueue(new AddSkillRequested(CoreGame.CurrentMap, unit.Guid, skillKey).Cast<IEventData>());
        else
        {
            var skills = CoreGame.Component<SkillComponent>(unit.Cast<IEntity>());
            if (skills == null) return "nounit";
            skills.AddSkill(skillKey);
        }
        DelversHost.Info("CORE_TEST_SUMMON_SKILL unit=" + unitGuid + " skill=" + skillKey);
        return "queued";
    }

    // The comfort option "bubblelings attack the water mage's target" for the party (SummonFollowTarget). Returns on, off or notest.
    internal static string SummonFollowForTest(bool on)
    {
        if (!LoadedTestCampaign()) return "notest";
        CoreSettings.SetSummonsFollowTarget(on);
        return on ? "on" : "off";
    }

    // The owner's target set the way Core sets a bandit's (CoreTargets, its summons follow at once): "set", or why not.
    internal static string SummonOwnerTargetForTest(string ownerGuid, string targetGuid)
    {
        if (!LoadedTestCampaign()) return "notest";
        var owner = Guid.TryParse(ownerGuid, out var o) ? CoreGame.FindUnit(o) : null;
        var target = Guid.TryParse(targetGuid, out var t) ? CoreGame.FindUnit(t) : null;
        if (owner == null || target == null) return "unknown";
        CoreTargets.SetForced(owner, CoreGame.Component<CombatInfoComponent>(owner.Cast<IEntity>()), target.Cast<IEntity>());
        return "set";
    }

    // How many effects of the caster Core marked as the bandits' so far (BanditTintSkill); null outside a test campaign.
    internal static string SkillTintCountForTest(string caster)
        => LoadedTestCampaign() ? BanditTintSkill.CountOf(caster).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;

    // The map's summon cap per side (SummonLimit) on or off, so a probe sees the game's own count next to it: "on" or "off".
    internal static string SummonLimitForTest(bool on)
    {
        if (!LoadedTestCampaign()) return "notest";
        SummonLimit.On = on;
        DelversHost.Info("CORE_SUMMON_LIMIT test on=" + (on ? "1" : "0"));
        return on ? "on" : "off";
    }

    // The owner's target and each of its summons' combat target: "owner=<guid|-> <summon>=<guid|->,…". Null outside a test campaign.
    internal static string SummonTargetsForTest(string ownerGuid)
        => LoadedTestCampaign() && !string.IsNullOrEmpty(ownerGuid) ? SummonProbe.Targets(ownerGuid) : null;

    // The owner's summons as the game sees them (SummonProbe.Status); starts counting their hits. Null outside a test campaign.
    internal static string SummonStatusForTest(string ownerGuid)
        => LoadedTestCampaign() && !string.IsNullOrEmpty(ownerGuid) ? SummonProbe.Status(ownerGuid) : null;
}
