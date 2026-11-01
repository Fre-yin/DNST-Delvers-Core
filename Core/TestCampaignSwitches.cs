namespace DungeonSettlersDelvers.Core;

// Switches that loosen uniqueness for tests (review, H7 and H10). They only ever act on a
// campaign that Core knows as a test campaign by its GUID, and every question checks that again,
// so a real campaign stays strict even with a switch left on. Any doubt means strict.
// Pure logic, tested offline; the test mark itself lives in CampaignPresenceStore.
internal sealed class TestCampaignSwitches : ICoreRegistry
{
    private readonly object gate = new();
    private readonly HashSet<Guid> recruitmentFree = new();
    private readonly HashSet<Guid> founderOffersForced = new();
    private readonly Func<Guid, bool> isTestCampaign;

    internal TestCampaignSwitches(Func<Guid, bool> isTestCampaign)
        => this.isTestCampaign = isTestCampaign ?? throw new ArgumentNullException(nameof(isTestCampaign));

    // Returns false when the switch may not be set: switching on needs a test campaign.
    internal bool SetRecruitmentFree(Guid campaign, bool free) => Set(recruitmentFree, campaign, free);

    internal bool UniquenessEnforced(Guid campaign) => !IsOn(recruitmentFree, campaign);

    internal bool SetFounderOffersForced(Guid campaign, bool forced) => Set(founderOffersForced, campaign, forced);

    internal bool FounderOffersForced(Guid campaign) => IsOn(founderOffersForced, campaign);

    internal void Forget(Guid campaign)
    {
        lock (gate)
        {
            recruitmentFree.Remove(campaign);
            founderOffersForced.Remove(campaign);
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            recruitmentFree.Clear();
            founderOffersForced.Clear();
        }
    }

    private bool Set(HashSet<Guid> switches, Guid campaign, bool on)
    {
        if (campaign == Guid.Empty) return false;
        if (on && !IsTest(campaign)) return false;
        lock (gate)
        {
            if (on) switches.Add(campaign);
            else switches.Remove(campaign);
        }
        return true;
    }

    private bool IsOn(HashSet<Guid> switches, Guid campaign)
    {
        if (campaign == Guid.Empty) return false;
        lock (gate)
            if (!switches.Contains(campaign)) return false;
        return IsTest(campaign);
    }

    private bool IsTest(Guid campaign)
    {
        try { return isTestCampaign(campaign); }
        catch { return false; }
    }
}
