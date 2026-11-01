using DungeonSettlersDelvers.Core;
using static TestKit;

// The skill book for Lifebloom.
internal static class LifebloomBookSection
{
    internal static void Run()
    {
        // The author's skill book for Lifebloom: blue, 5000, own icon, cloth in the save, follows the bandits' option.
        Check(LifebloomBookRules.ItemKey == "ITEM_DelversCoreLifebloomBook" && LifebloomBookRules.SkillKey == "SKILL_DelversCoreLifebloomBook"
                && LifebloomBookRules.IconFile == "Lifebloom_Skillbuch_16x16.png" && LifebloomBookRules.StandIn == "ITEM_Textile"
                && LifebloomBookRules.Price == 5000 && LifebloomBookRules.RarityValue == 2
                && LifebloomBookRules.RefuseReason(true, true, true, false) == null
                && LifebloomBookRules.RefuseReason(true, false, true, false) == "inactive" && LifebloomBookRules.RefuseReason(true, true, false, false) == "inactive"
                && LifebloomBookRules.RefuseReason(true, true, true, true) == "known" && LifebloomBookRules.RefuseReason(false, true, true, false) == "notparty"
                && LifebloomBookRules.RefusedLine("u", "known") == "CORE_LIFEBLOOM_BOOK_REFUSED unit=u reason=known"
                && LifebloomBookRules.UsedLine("u", true) == "CORE_LIFEBLOOM_BOOK_USED unit=u result=learned"
                && LifebloomBookRules.UsedLine("u", true, true) == "CORE_LIFEBLOOM_BOOK_USED unit=u result=learned refresh=sent"
                && LifebloomBookRules.UsedLine("u", false, false) == "CORE_LIFEBLOOM_BOOK_USED unit=u result=failed refresh=none"
                && LifebloomBookRules.LearnedNoticeKey.StartsWith("TEXTKEY_HelpNotice_", StringComparison.Ordinal)
                && LifebloomBookRules.LearnedNotice.German == "{0} hat Lebensblüte gelernt." && LifebloomBookRules.LearnedNotice.English == "{0} learned Lifebloom."
                && LifebloomBookRules.LearnedNotice.AllLanguages.All(text => text.Contains("{0}"))
                && LifebloomBookRules.NameKey == "TEXTKEY_ITEM_DelversCoreLifebloomBook_NAME"
                && LifebloomBookRules.Name.German == "Skillbuch: Lebensblüte"
                && LifebloomBookRules.Flavor.German == "Die Seiten riechen nach Moos und ein wenig nach Lagerfeuer."
                && LifebloomBookRules.Description.German.StartsWith("Bringt dir den Skill Lebensblüte bei.")
                && LifebloomBookRules.All.All(entry => entry.Text.AllLanguages.Count == 10
                    && entry.Text.AllLanguages.All(text => !string.IsNullOrWhiteSpace(text) && text.IndexOf('–') < 0 && text.IndexOf('—') < 0 && text.IndexOf('-') < 0)),
            "skill book: keys, blue, 5000, cloth in the save, refused without the bandits, when known or for a stranger; texts in 10 languages");
    }
}
