namespace DungeonSettlersDelvers.Core;

// The author's skill book for Lifebloom (WEITERGABE_Skillbuch_16x16): using it teaches the unit Lifebloom
// (Lifebloom.Learn), and the book is used up. Blue like the revive scroll (Rarity.Rare), its own 16×16 icon. It follows
// the bandits' option ("bandits are optional on other campaigns, so the book too"): always on Apocalypse, elsewhere only
// with the bandits on; without them, or for a unit that knows Lifebloom already, the use is refused before the book leaves
// the bag. In the game's part of a save it is cloth (StandIn), so a save stays loadable without Core. For now only the
// toolkit hands it out; the bandit camp's chest and the guild merchant follow later. Rules without the game.
internal static class LifebloomBookRules
{
    internal const string ItemKey = CoreSaveItems.Prefix + "LifebloomBook";
    internal const string SkillKey = "SKILL_DelversCoreLifebloomBook";
    internal const string IconFile = "Lifebloom_Skillbuch_16x16.png";
    internal const string StandIn = "ITEM_Textile";
    internal const int Price = 5000;
    // Rarity.Rare (2) shows blue in the game's tooltip, as the revive scroll does.
    internal const int RarityValue = 2;

    // Why the use is refused, or null when it may go on.
    internal static string RefuseReason(bool partyMember, bool banditsOn, bool skillRow, bool knows)
    {
        if (!partyMember) return "notparty";
        if (!banditsOn || !skillRow) return "inactive";
        return knows ? "known" : null;
    }

    internal static string RefusedLine(string unit, string reason) => "CORE_LIFEBLOOM_BOOK_REFUSED unit=" + unit + " reason=" + reason;

    internal static string UsedLine(string unit, bool learned) => "CORE_LIFEBLOOM_BOOK_USED unit=" + unit + " result=" + (learned ? "learned" : "failed");

    // refresh: whether the quick slots were told (Lifebloom.Learn's view updates).
    internal static string UsedLine(string unit, bool learned, bool refreshed) => UsedLine(unit, learned) + " refresh=" + (refreshed ? "sent" : "none");

    // The short notice after learning, {0} the unit's name; German and English, the other
    // languages drafts in the style of the soul stones' notice.
    internal const string LearnedNoticeKey = "TEXTKEY_HelpNotice_DelversLifebloomLearned";

    internal static readonly UniqueCandidateTranslations LearnedNotice = new(
        "{0} learned Lifebloom.", "{0}이(가) 생명의 개화를 배웠습니다.", "{0} a appris Floraison de vie.", "{0} hat Lebensblüte gelernt.",
        "{0} изучает Цветение жизни.", "{0}学会了生命绽放。", "{0}學會了生命綻放。", "{0}は命の開花を習得した。",
        "{0} aprendió Floración vital.", "{0} aprendeu Floração vital.");

    internal static string NameKey => "TEXTKEY_" + ItemKey + "_NAME";
    internal static string DescKey => "TEXTKEY_" + ItemKey + "_DESC";
    internal static string FlavorKey => "TEXTKEY_" + ItemKey + "_FLAVOR";

    // German; English and the other languages are drafts for the author to approve.
    internal static readonly UniqueCandidateTranslations Name = new(
        "Skill Book: Lifebloom", "스킬북: 생명의 개화", "Livre de compétence : Floraison de vie", "Skillbuch: Lebensblüte",
        "Книга навыка: Цветение жизни", "技能书：生命绽放", "技能書：生命綻放", "スキルブック：命の開花",
        "Libro de habilidad: Floración vital", "Livro de habilidade: Floração vital");

    internal static readonly UniqueCandidateTranslations Description = new(
        "Teaches you the skill Lifebloom. The spell has its own cooldown and three charges that recharge on their own. At the end Lifebloom blooms once more and heals with a strong burst.",
        "생명의 개화 스킬을 배웁니다. 이 주문은 고유한 재사용 대기시간과 스스로 다시 충전되는 세 번의 충전을 가집니다. 마지막에 생명의 개화가 한 번 더 피어나 강력하게 회복합니다.",
        "T'apprend la compétence Floraison de vie. Le sort a son propre temps de recharge et trois charges qui se rechargent d'elles mêmes. À la fin, Floraison de vie s'épanouit une dernière fois et soigne d'un puissant élan.",
        "Bringt dir den Skill Lebensblüte bei. Der Zauber hat eine eigene Abklingzeit und drei Ladungen, die sich von selbst wieder aufladen. Zum Ende blüht die Lebensblüte noch einmal auf und heilt mit einem kräftigen Schub.",
        "Обучает навыку «Цветение жизни». У заклинания свой откат и три заряда, которые восстанавливаются сами. В конце Цветение жизни распускается ещё раз и мощно исцеляет.",
        "教会你技能“生命绽放”。该法术拥有独立的冷却时间和三次会自行恢复的充能。结束时生命绽放会再次绽放，带来一次强力治疗。",
        "教會你技能「生命綻放」。該法術擁有獨立的冷卻時間和三次會自行恢復的充能。結束時生命綻放會再次綻放，帶來一次強力治療。",
        "スキル「命の開花」を習得する。この呪文には独自のクールタイムと、自然に回復する3回のチャージがある。最後に命の開花がもう一度咲き、力強く回復する。",
        "Te enseña la habilidad Floración vital. El hechizo tiene su propio tiempo de recarga y tres cargas que se recargan solas. Al final, Floración vital florece una vez más y cura con un fuerte impulso.",
        "Ensina a habilidade Floração vital. O feitiço tem seu próprio tempo de recarga e três cargas que se recarregam sozinhas. No fim, Floração vital floresce mais uma vez e cura com um forte impulso.");

    internal static readonly UniqueCandidateTranslations Flavor = new(
        "The pages smell of moss and a little of campfire.",
        "책장에서 이끼 냄새와 약간의 모닥불 냄새가 납니다.",
        "Les pages sentent la mousse et un peu le feu de camp.",
        "Die Seiten riechen nach Moos und ein wenig nach Lagerfeuer.",
        "Страницы пахнут мхом и немного костром.",
        "书页散发着苔藓的气味，还有一点篝火的味道。",
        "書頁散發著苔蘚的氣味，還有一點篝火的味道。",
        "ページは苔と、少しだけ焚き火の匂いがする。",
        "Las páginas huelen a musgo y un poco a hoguera.",
        "As páginas cheiram a musgo e um pouco a fogueira.");

    internal static IEnumerable<(string Name, UniqueCandidateTranslations Text)> All => new[]
    {
        (NameKey, Name), (DescKey, Description), (FlavorKey, Flavor), (LearnedNoticeKey, LearnedNotice)
    };
}
