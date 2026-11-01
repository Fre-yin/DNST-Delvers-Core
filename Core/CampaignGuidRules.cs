namespace DungeonSettlersDelvers.Core;

// (Antrag 4 of the field test): an addon reads the GUID of the campaign Core is bound to, so it
// can ask IsTestCampaign before it turns on test-only parts. The source is Core's own binding (CoreSaveStore, set when a
// campaign loads or a new one begins), never a log line or a scene search; outside a campaign there is none. Text in the
// "D" format, as every other campaign GUID of the API. Rules without the game.
internal static class CampaignGuidRules
{
    internal const string ApiVersion = "1.9.0";

    internal static string Text(Guid campaign) => campaign == Guid.Empty ? null : campaign.ToString("D");
}