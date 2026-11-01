namespace DungeonSettlersDelvers.Core;

// The presence of unique units (CampaignPresence) on Core's block of the bound campaign (CoreSaveStore.Current). A mark goes
// into the block at once, so the unit cannot appear twice in this session; it reaches the disk only with the next save of the
// game, never before. A crash before that save leaves nothing behind. A mark for a campaign that is not bound yet (a spawn
// before the block is set up) waits in memory and joins the block when that campaign binds. Pure, so the offline tests cover it.
internal static class CampaignPresenceBook
{
    private static readonly Dictionary<Guid, List<string>> pending = new();

    internal static void ResetLifecycle() => pending.Clear();

    internal static bool Seen(CoreSaveData current, Guid campaign, string id)
    {
        if (current != null && current.Campaign == campaign) return current.PresenceSeen(id);
        return pending.TryGetValue(campaign, out var waiting) && waiting.Contains(id);
    }

    // True when the id is newly marked.
    internal static bool Mark(CoreSaveData current, Guid campaign, string id)
    {
        if (campaign == Guid.Empty || string.IsNullOrEmpty(id)) return false;
        if (current != null && current.Campaign == campaign) return current.SetPresenceSeen(id);
        if (!pending.TryGetValue(campaign, out var waiting)) pending[campaign] = waiting = new List<string>();
        if (waiting.Contains(id)) return false;
        waiting.Add(id);
        return true;
    }

    // The campaign's block is bound: what waited for it joins it.
    internal static void Bound(CoreSaveData current)
    {
        if (current == null || !pending.TryGetValue(current.Campaign, out var waiting)) return;
        pending.Remove(current.Campaign);
        foreach (var id in waiting) current.SetPresenceSeen(id);
    }

    // Once, for a block from a Core before the field: an old marker file counts only by CampaignPresenceRules.Adopt. Without the
    // save's time nothing is taken over (the unit may appear again rather than stay locked). When there were old files, the block
    // carries the field from now on and they are not read again, so a marker skipped here is not taken over by a later, newer
    // save. Without old files the block stays as it was (a save of a campaign without markers keeps its bytes).
    internal static (int Adopted, int Skipped) Adopt(CoreSaveData current, DateTime? saveWrittenUtc,
        IEnumerable<(string Id, DateTime WrittenUtc)> legacy)
    {
        if (current == null || current.HasPresence || legacy == null) return (0, 0);
        int adopted = 0, skipped = 0;
        foreach (var (id, written) in legacy)
        {
            if (saveWrittenUtc is DateTime saved && CampaignPresenceRules.Adopt(saved, written))
            {
                if (current.SetPresenceSeen(id)) adopted++;
            }
            else skipped++;
        }
        if (adopted + skipped > 0) current.StartPresence();
        return (adopted, skipped);
    }
}
