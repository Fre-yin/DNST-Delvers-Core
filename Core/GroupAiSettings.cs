namespace DungeonSettlersDelvers.Core;

// The group AI's switches: Core's settings (GroupAiRules.EnabledKey, AddsFirstKey), the first one
// overridable by the Toolkit for a test. Every change starts the AI's campaign state anew.
// Rather an accessibility feature, locked from Very Hard up (GroupAiLockRules): on such a campaign
// the AI does not run even with the switch on in the file. The Toolkit's test switch is not locked. CORE_AI_GROUP_LOCK
// once per change of the lock while the switch is on.
internal static class GroupAiSettings
{
    private static bool? testOverride;
    private static bool? lastLocked;

    internal static bool Enabled
    {
        get
        {
            if (testOverride.HasValue) return testOverride.Value;
            if (!CoreSettings.GroupAiEnabled) return false;
            return !LockedNow();
        }
    }

    internal static bool AddsFirst => CoreSettings.GroupAiAddsFirst;

    // The running campaign's combat difficulty locks the AI (Very Hard, Downfall, Apocalypse).
    internal static bool Locked => GroupAiLockRules.Locked((int)CoreGame.CombatLevel());

    internal static bool SetForTest(bool on)
    {
        testOverride = on;
        GroupAiPolicy.ResetCampaign();
        DelversHost.Info("CORE_AI_TEST enabled=" + (on ? "true" : "false"));
        return true;
    }

    internal static void ResetLifecycle()
    {
        testOverride = null;
        lastLocked = null;
        GroupAiPolicy.ResetCampaign();
    }

    private static bool LockedNow()
    {
        var level = (int)CoreGame.CombatLevel();
        var locked = GroupAiLockRules.Locked(level);
        if (lastLocked != locked)
        {
            lastLocked = locked;
            DelversHost.Info(GroupAiLockRules.Line(locked, level));
            if (locked) GroupAiPolicy.ResetCampaign();
        }
        return locked;
    }
}
