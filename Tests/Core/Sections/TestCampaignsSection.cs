using DungeonSettlersDelvers.Core;
using static TestKit;

// Test campaigns by GUID, recruitment free, forgetting, the next new campaign as test.
internal static class TestCampaignsSection
{
    internal static void Run()
    {
        TestCampaignMarks();
        NextCampaignTest();
    }

    static void TestCampaignMarks()
    {
        // Test campaigns by GUID, "recruitment free" and forgetting (review, H7 to H10).
        var testRoot = Path.Combine(Path.GetTempPath(), "delvers-testcampaign-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var testStore = new CampaignPresenceStore(testRoot);
            var testCampaign = Guid.NewGuid();
            var realCampaign = Guid.NewGuid();
            Check(!testStore.IsTestCampaign(testCampaign) && !testStore.IsTestCampaign(Guid.Empty),
                "an unmarked campaign is no test campaign");
            testStore.MarkTestCampaign(testCampaign, "test");
            testStore.Mark(testCampaign, "frieren", "marker");
            testStore.Mark(realCampaign, "frieren", "marker");
            var legacyDirectory = Path.Combine(testRoot, "FrierenPortrait", "recruited");
            Directory.CreateDirectory(legacyDirectory);
            File.WriteAllText(Path.Combine(legacyDirectory, testCampaign.ToString("D") + ".marker"), "legacy");
            Check(testStore.IsTestCampaign(testCampaign) && !testStore.IsTestCampaign(realCampaign)
                    && testStore.HasSeen(testCampaign, "frieren"),
                "a marked test campaign is recognized by its GUID alone");
            var forgotten = testStore.Forget(testCampaign);
            Check(forgotten == 3 && !testStore.HasSeen(testCampaign, "frieren") && !testStore.IsTestCampaign(testCampaign)
                    && testStore.HasSeen(realCampaign, "frieren"),
                "forgetting removes the campaign's markers in both layouts and its test mark, nothing else");

            var marked = new HashSet<Guid> { testCampaign };
            var switches = new TestCampaignSwitches(marked.Contains);
            Check(switches.UniquenessEnforced(testCampaign) && switches.UniquenessEnforced(realCampaign),
                "uniqueness is strict by default, also in a test campaign");
            Check(!switches.SetRecruitmentFree(realCampaign, true) && switches.UniquenessEnforced(realCampaign)
                    && !switches.SetFounderOffersForced(realCampaign, true) && !switches.FounderOffersForced(realCampaign),
                "a real campaign cannot be loosened");
            Check(switches.SetRecruitmentFree(testCampaign, true) && !switches.UniquenessEnforced(testCampaign)
                    && switches.SetFounderOffersForced(testCampaign, true) && switches.FounderOffersForced(testCampaign),
                "a test campaign can switch recruitment free and force founder offers");
            marked.Remove(testCampaign);
            Check(switches.UniquenessEnforced(testCampaign) && !switches.FounderOffersForced(testCampaign),
                "a switch left on stops acting once the campaign is no test campaign any more");
            var throwing = new TestCampaignSwitches(_ => throw new IOException("unreadable"));
            Check(!throwing.SetRecruitmentFree(testCampaign, true) && throwing.UniquenessEnforced(testCampaign),
                "when the test mark cannot be read, everything stays strict");
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }
    }

    static void NextCampaignTest()
    {
        var now = new DateTime(2026, 10, 10, 18, 0, 0, DateTimeKind.Utc);
        var next = new NextCampaignTest(() => now);
        var idle = next.Pending;
        next.Arm();
        var armed = next.Pending;
        var consumed = next.Consume();
        var again = next.Consume();
        next.Arm();
        now = now.AddMinutes(16);
        Check(!idle && armed && consumed && !again && !next.Pending,
            "next new campaign as test: armed once, used once, and gone after 15 minutes");
    }
}
