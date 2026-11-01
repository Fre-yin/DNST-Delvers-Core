using DungeonSettlersDelvers.Core;
using static TestKit;

// The camp's wake (BanditCampWakeRules): a party member inside the camp's box, then at once in combat Apocalypse or after half
// the game's surprise window.
internal static class BanditCampWakeSection
{
    internal static void Run()
    {
        var box = (MinX: 510, MinY: 94, MaxX: 553, MaxY: 113);
        Check(BanditCampWakeRules.Inside(box, 510, 94) && BanditCampWakeRules.Inside(box, 553, 113) && BanditCampWakeRules.Inside(box, 530, 100)
                && !BanditCampWakeRules.Inside(box, 509, 100) && !BanditCampWakeRules.Inside(box, 554, 100) && !BanditCampWakeRules.Inside(box, 530, 114),
            "camp wake: a party member counts inside the camp's box, edges included, the corridor outside");
        Check(BanditCampWakeRules.LeadSeconds(true, 4f) == 0f && BanditCampWakeRules.LeadSeconds(false, 4f) == 2f
                && BanditCampWakeRules.LeadSeconds(false, 0f) == 2f && BanditCampWakeRules.LeadSeconds(false, 6f) == 3f,
            "camp wake: at once in combat Apocalypse, else half the surprise window (4 s when the game gives none)");
        Check(!BanditCampWakeRules.Due(-1f, 10f, 0f) && BanditCampWakeRules.Due(5f, 5f, 0f) && !BanditCampWakeRules.Due(5f, 6.9f, 2f)
                && BanditCampWakeRules.Due(5f, 7f, 2f),
            "camp wake: due only after the party entered and the lead has run");
    }
}
