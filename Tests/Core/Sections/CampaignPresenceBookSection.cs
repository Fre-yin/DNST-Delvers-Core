using DungeonSettlersDelvers.Core;
using static TestKit;

// The presence of unique units in Core's block of the save (CampaignPresenceBook): written only with the game's save, the old
// marker files taken over once by their time, a crash before the save leaves nothing behind.
internal static class CampaignPresenceBookSection
{
    static readonly string Root = Path.Combine(Path.GetTempPath(), "delvers-presence-book-" + Guid.NewGuid().ToString("N"));
    static readonly string SavePath = Path.Combine(Root, "Saves", "Clan_AutoSave_1.json");
    const string Plain = "{\n  \"EntityContainerSaveData\": {},\n  \"CampaignSaveHeader\": {\"SaveName\": \"Clan_AutoSave_1\"}\n}";
    static readonly DateTime SaveTime = new(2026, 10, 30, 10, 0, 0, DateTimeKind.Utc);

    internal static void Run()
    {
        var host = new TestDelversHost("presence-book");
        DelversHost.Bind(host);
        try
        {
            Rules();
            LegacyFiles();
            CrashBeforeSaveFreesTheUnit();
            SeenBySaveStaysLocked();
            LaterSaveDoesNotLockAgain();
            NoMarkersKeepTheBlock();
            NewCampaignTakesNothing();
            MarkBeforeBindJoinsTheBlock();
            SavesAreStableAndTheGameKeepsItsPart();
        }
        finally
        {
            CoreSaveStore.ResetLifecycle();
            DelversHost.Unbind(host);
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    static void Rules()
    {
        Check(CampaignPresenceRules.BitOf("frieren") == 0 && CampaignPresenceRules.BitOf("kragas") == 3 && CampaignPresenceRules.BitOf("other") == -1
                && CampaignPresenceRules.MaskText(0x0f) == "0f" && CampaignPresenceRules.ParseMask("0f") == 15 && CampaignPresenceRules.ParseMask("zz") == 0,
            "presence: a fixed table gives each id its bit, kept as two hex digits");
        Check(CampaignPresenceRules.Adopt(SaveTime, SaveTime.AddMinutes(-5)) && CampaignPresenceRules.Adopt(SaveTime, SaveTime)
                && !CampaignPresenceRules.Adopt(SaveTime, SaveTime.AddMinutes(5)),
            "presence: an old marker counts only when the save was written after it");
        var data = CoreSaveData.Create(Guid.NewGuid());
        Check(!data.HasPresence && data.SetPresenceSeen("liana") && !data.SetPresenceSeen("liana") && data.SetPresenceSeen("example-hero")
                && data.PresenceSeen("liana") && data.PresenceSeen("example-hero") && !data.PresenceSeen("frieren")
                && data.Serialize().Contains("\"pv\":1") && data.Serialize().Contains("\"pr\":\"04\"") && data.Serialize().Contains("\"px\":[\"example-hero\"]"),
            "presence: the block holds the version, the table's bits in hex and the other ids in a short list");
    }

    // An older Core's marker files of one campaign, with their times.
    static Guid WriteLegacy(DateTime frierenTime)
    {
        var campaign = Guid.NewGuid();
        var folder = Path.Combine(Root, "DungeonSettlersDelvers", "Core", "recruited", campaign.ToString("D"));
        Directory.CreateDirectory(folder);
        var marker = Path.Combine(folder, "frieren.marker");
        File.WriteAllText(marker, "x");
        File.SetLastWriteTimeUtc(marker, frierenTime);
        return campaign;
    }

    static void WriteSave()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
        File.WriteAllText(SavePath, Plain);
        File.SetLastWriteTimeUtc(SavePath, SaveTime);
    }

    static void LegacyFiles()
    {
        var campaign = WriteLegacy(SaveTime.AddMinutes(-1));
        var legacyRoot = Path.Combine(Root, "FrierenPortrait", "recruited");
        Directory.CreateDirectory(legacyRoot);
        var old = Path.Combine(legacyRoot, campaign.ToString("D") + "-Lowell.marker");
        File.WriteAllText(old, "x");
        File.SetLastWriteTimeUtc(old, SaveTime.AddMinutes(-2));
        File.WriteAllText(Path.Combine(Root, "DungeonSettlersDelvers", "Core", "recruited", campaign.ToString("D"), "example-hero.marker"), "x");
        var found = new CampaignPresenceStore(Root).LegacyMarkers(campaign);
        Check(found.Count == 3 && found.Any(entry => entry.Id == "frieren") && found.Any(entry => entry.Id == "lowell" && entry.WrittenUtc == SaveTime.AddMinutes(-2))
                && found.Any(entry => entry.Id == "example-hero"),
            "presence: the old files of a campaign are found in all their places, read only");
    }

    static void Bind(Guid campaign, Func<Guid, IEnumerable<(string Id, DateTime WrittenUtc)>> legacy)
    {
        CoreSaveStore.ResetLifecycle();
        CoreSaveStore.LegacyPresence = legacy;
        CoreSaveStore.Remember(Plain, SavePath);
        CoreSaveStore.BindLoaded(campaign, SavePath);
    }

    static void CrashBeforeSaveFreesTheUnit()
    {
        WriteSave();
        var campaign = WriteLegacy(SaveTime.AddMinutes(3));
        Bind(campaign, new CampaignPresenceStore(Root).LegacyMarkers);
        Check(!CampaignPresenceBook.Seen(CoreSaveStore.Current, campaign, "frieren") && CoreSaveStore.Current.HasPresence,
            "presence: a marker newer than the save (a crash before saving) does not lock Frieren");
    }

    static void LaterSaveDoesNotLockAgain()
    {
        WriteSave();
        var campaign = WriteLegacy(SaveTime.AddMinutes(3));
        Bind(campaign, new CampaignPresenceStore(Root).LegacyMarkers);
        CoreSaveStore.SaveStarted();
        var written = CoreSaveStore.Decorate(SavePath + ".tmp", Plain);
        CoreSaveStore.SaveEnded();
        File.WriteAllText(SavePath, written);
        File.SetLastWriteTimeUtc(SavePath, SaveTime.AddMinutes(10));
        Bind(campaign, new CampaignPresenceStore(Root).LegacyMarkers);
        CoreSaveStore.ResetLifecycle();
        CoreSaveStore.LegacyPresence = new CampaignPresenceStore(Root).LegacyMarkers;
        CoreSaveStore.Remember(written, SavePath);
        CoreSaveStore.BindLoaded(campaign, SavePath);
        Check(!CampaignPresenceBook.Seen(CoreSaveStore.Current, campaign, "frieren"),
            "presence: after the crash case is saved, a newer save does not take the skipped marker over");
    }

    static void NoMarkersKeepTheBlock()
    {
        WriteSave();
        CoreSaveStore.ResetLifecycle();
        CoreSaveStore.LegacyPresence = new CampaignPresenceStore(Root).LegacyMarkers;
        var campaign = Guid.NewGuid();
        var block = CoreSaveData.Create(campaign).Serialize();
        CoreSaveStore.Remember(CoreSaveBlock.Append(Plain, block), SavePath);
        CoreSaveStore.BindLoaded(campaign, SavePath);
        Check(CoreSaveStore.Current.Serialize() == block, "presence: a campaign without old markers keeps its block as it was");
    }

    static void SeenBySaveStaysLocked()
    {
        WriteSave();
        var campaign = WriteLegacy(SaveTime.AddMinutes(-3));
        Bind(campaign, new CampaignPresenceStore(Root).LegacyMarkers);
        Check(CampaignPresenceBook.Seen(CoreSaveStore.Current, campaign, "frieren"), "presence: a marker the save has seen keeps Frieren locked");
        CoreSaveStore.SaveStarted();
        var written = CoreSaveStore.Decorate(SavePath + ".tmp", Plain);
        CoreSaveStore.SaveEnded();
        CoreSaveStore.ResetLifecycle();
        CoreSaveStore.Remember(written, SavePath);
        CoreSaveStore.BindLoaded(campaign, SavePath);
        Check(CampaignPresenceBook.Seen(CoreSaveStore.Current, campaign, "frieren"),
            "presence: after the next save the lock lives in the block and needs no file any more");
    }

    static void NewCampaignTakesNothing()
    {
        var campaign = WriteLegacy(SaveTime.AddMinutes(-3));
        CoreSaveStore.ResetLifecycle();
        var asked = false;
        IEnumerable<(string Id, DateTime WrittenUtc)> Legacy(Guid guid)
        {
            asked = true;
            return new CampaignPresenceStore(Root).LegacyMarkers(guid);
        }
        CoreSaveStore.LegacyPresence = Legacy;
        CoreSaveStore.BeginNewCampaign(campaign);
        Check(!asked && !CampaignPresenceBook.Seen(CoreSaveStore.Current, campaign, "frieren"),
            "presence: a new campaign takes over no old marker");
    }

    static void MarkBeforeBindJoinsTheBlock()
    {
        CoreSaveStore.ResetLifecycle();
        var campaign = Guid.NewGuid();
        Check(CampaignPresenceBook.Mark(CoreSaveStore.Current, campaign, "lowell") && CampaignPresenceBook.Seen(CoreSaveStore.Current, campaign, "lowell"),
            "presence: a mark before the campaign is bound waits in memory");
        CoreSaveStore.BeginNewCampaign(campaign);
        Check(CoreSaveStore.Current.PresenceSeen("lowell"), "presence: the waiting mark joins the block when the campaign binds");
        CoreSaveStore.ResetLifecycle();
        CoreSaveStore.BeginNewCampaign(campaign);
        Check(!CoreSaveStore.Current.PresenceSeen("lowell"), "presence: a mark never saved is gone after a restart (the crash case)");
    }

    static void SavesAreStableAndTheGameKeepsItsPart()
    {
        CoreSaveStore.ResetLifecycle();
        var campaign = Guid.NewGuid();
        CoreSaveStore.BeginNewCampaign(campaign);
        CampaignPresenceBook.Mark(CoreSaveStore.Current, campaign, "frieren");
        CampaignPresenceBook.Mark(CoreSaveStore.Current, campaign, "frieren");
        CoreSaveStore.SaveStarted();
        var first = CoreSaveStore.Decorate(SavePath + ".tmp", Plain);
        var second = CoreSaveStore.Decorate(SavePath + ".tmp", Plain);
        CoreSaveStore.SaveEnded();
        Check(first == second && CoreSaveBlock.Extract(first).Contains("\"pr\":\"01\""),
            "presence: two saves without a change give the same bytes");
        Check(CoreSaveBlock.Remove(first) == Plain, "presence: the game's part of the save stays as it was, so a save loads without Core");
    }
}
