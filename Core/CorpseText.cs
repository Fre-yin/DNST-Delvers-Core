namespace DungeonSettlersDelvers.Core;

// Words for the equipment on the body (steps 2 and 3): short,
// like the game's own action and reason texts, in the game's 10 languages.
internal static class CorpseText
{
    // Core's text keys in the game's sheet (UniqueCandidateLocalization.Rows).
    internal const string UndressKey = "TEXTKEY_InteractionOption_DelversUndress";
    internal const string UndressFirstKey = "TEXTKEY_InteractionInvalid_DelversUndressFirst";
    internal const string LeaveWarningKey = "TEXTKEY_HelpNotice_DelversCorpseLeave";

    // The action at a dressed corpse (step 2). German and English in his words, the other languages
    // drafts to match.
    internal static readonly UniqueCandidateTranslations Undress = new(
        "Strip Equipment",
        "장비 벗기기",
        "Retirer l'équipement",
        "Ausrüstung ausziehen",
        "Снять снаряжение",
        "卸下装备",
        "卸下裝備",
        "装備を剥ぐ",
        "Quitar el equipo",
        "Remover equipamento");

    // The red reason at the grave and the base portal while the carried corpse is dressed (step 3).
    internal static readonly UniqueCandidateTranslations UndressFirst = new(
        "Undress first",
        "먼저 벗기세요",
        "Déshabillez d'abord",
        "Erst ausziehen",
        "Сначала разденьте",
        "请先脱下装备",
        "請先脫下裝備",
        "先に脱がせてください",
        "Primero desvístelo",
        "Primeiro dispa");

    // The notice when the party leaves a map with dressed fallen behind (step 3).
    internal static readonly UniqueCandidateTranslations LeaveWarning = new(
        "Fallen still in their gear stay behind.",
        "장비를 입은 쓰러진 동료가 남겨집니다.",
        "Des tombés encore équipés restent sur place.",
        "Angezogene Gefallene bleiben zurück.",
        "Павшие в снаряжении останутся здесь.",
        "仍穿着装备的阵亡者会被留下。",
        "仍穿著裝備的陣亡者會被留下。",
        "装備を着けたままの仲間が置き去りになります。",
        "Los caídos con su equipo se quedan atrás.",
        "Os caídos ainda equipados ficam para trás.");

    internal static IEnumerable<UniqueCandidateTranslations> All()
    {
        yield return Undress;
        yield return UndressFirst;
        yield return LeaveWarning;
    }
}
