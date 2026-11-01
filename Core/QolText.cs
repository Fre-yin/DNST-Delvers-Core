namespace DungeonSettlersDelvers.Core;

// The comfort options of the Delvers menu (the shield band and the tactical formation are quality of life
// options, switchable, on by default). Player settings of the game copy (CoreSettings), not of a campaign: off hides or stills a
// feature at once and keeps every saved formation and priority place for when it is switched on again. Like the other switches
// of the menu they have no explaining line; the game's guide says what they do. Pure, so the offline tests cover the texts.
// The tactical formation ships as experimental and says so in its name.
internal static class QolText
{
    internal const string ShieldBandKey = "qol.shield-band";
    internal const string TacticalKey = "qol.tactical";
    internal const string SummonFollowKey = "qol.summons-follow-target";

    internal static readonly UniqueCandidateTranslations Section = new(
        "Quality of life", "편의 기능", "Confort", "Komfort", "Удобство", "便利功能", "便利功能", "快適機能", "Comodidad", "Conforto");

    internal static readonly UniqueCandidateTranslations ShieldBand = new(
        "Shield band", "보호막 띠", "Bande de bouclier", "Schildband", "Полоса щита", "护盾条", "護盾條", "シールドバー", "Barra de escudo",
        "Barra de escudo");

    internal static readonly UniqueCandidateTranslations Tactical = new(
        "Tactical formation (experimental)", "전술 대형 (실험적)", "Formation tactique (expérimentale)", "Taktische Formation (experimentell)",
        "Тактический строй (экспериментально)", "战术队形（实验性）", "戰術隊形（實驗性）", "戦術隊形（試験的）", "Formación táctica (experimental)",
        "Formação tática (experimental)");

    // A water mage's bubblelings attack the mage's target (SummonFollowTarget); the names are the game's own (TEXTKEY_UNIT_Bubbleling,
    // the water magic tree).
    internal static readonly UniqueCandidateTranslations SummonFollow = new(
        "Bubblelings attack the water mage's target", "버블링이 물 마법사의 대상을 공격", "Les Bubblelings attaquent la cible du mage de l'eau",
        "Blubberlinge greifen das Ziel des Wassermagiers an", "Бабблелинги атакуют цель водного мага", "泡泡灵攻击水魔法师的目标",
        "泡泡靈攻擊水系魔法師的目標", "バブリングが水魔法使いの標的を攻撃", "Los Burbujines atacan al objetivo del mago de agua",
        "Os Bubblelings atacam o alvo do mago de água");

    // Every text with a name, for the check of the languages (CoreTextRows.Sources).
    internal static IEnumerable<(string Key, UniqueCandidateTranslations Text)> All => new[]
    {
        ("qol.section", Section), ("qol.shield-band.name", ShieldBand), ("qol.tactical.name", Tactical), ("qol.summons-follow-target.name", SummonFollow)
    };
}
