namespace DungeonSettlersDelvers.Core;

// "The next new campaign is a test campaign" (Core API 1.8.0, the visible
// self-test that creates a campaign with founders). On the campaign screen there is no campaign
// GUID yet, so a test arms this before it presses "New game"; Core binds the GUID once the new
// campaign exists (ApocalypseDifficulty.FinishNewCampaign) and, for this one creation, lets every
// matching founder offer through (NativeFoundersEstablish). A load or the time limit disarms it, so
// a test that stops halfway never marks a real campaign later. Pure, so the offline tests cover it.
internal sealed class NextCampaignTest
{
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly Func<DateTime> clock;
    private DateTime? armedAt;

    internal NextCampaignTest(Func<DateTime> clock) => this.clock = clock;

    internal static NextCampaignTest Shared { get; } = new(() => DateTime.UtcNow);

    internal void Arm() => armedAt = clock();

    internal void Disarm() => armedAt = null;

    internal bool Pending
    {
        get
        {
            if (armedAt is not { } at) return false;
            if (clock() - at <= Lifetime) return true;
            armedAt = null;
            return false;
        }
    }

    // True once, for the campaign that is being created now.
    internal bool Consume()
    {
        var pending = Pending;
        armedAt = null;
        return pending;
    }
}
