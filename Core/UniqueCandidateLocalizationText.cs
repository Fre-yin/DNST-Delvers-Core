namespace DungeonSettlersDelvers.Core;

internal sealed class UniqueCandidateTranslations
{
    internal UniqueCandidateTranslations(string english, string korean, string french, string german,
        string russian, string chineseSimplified, string chineseTraditional, string japanese,
        string spanish, string portugueseBrazil)
    {
        English = english;
        Korean = korean;
        French = french;
        German = german;
        Russian = russian;
        ChineseSimplified = chineseSimplified;
        ChineseTraditional = chineseTraditional;
        Japanese = japanese;
        Spanish = spanish;
        PortugueseBrazil = portugueseBrazil;
    }

    internal string English { get; }
    internal string Korean { get; }
    internal string French { get; }
    internal string German { get; }
    internal string Russian { get; }
    internal string ChineseSimplified { get; }
    internal string ChineseTraditional { get; }
    internal string Japanese { get; }
    internal string Spanish { get; }
    internal string PortugueseBrazil { get; }

    internal IReadOnlyList<string> AllLanguages => new[]
    {
        English, Korean, French, German, Russian, ChineseSimplified,
        ChineseTraditional, Japanese, Spanish, PortugueseBrazil
    };
}

internal static class UniqueCandidateLocalizationText
{
    internal const string NativeLockConflictKey = "TEXTKEY_CreateCampaign_LockConflict";

    internal static readonly UniqueCandidateTranslations LockConflictFallback = new(
        "The selected options conflict with the current locks.",
        "선택한 옵션이 현재 잠금 설정과 충돌합니다.",
        "Les options choisies sont incompatibles avec les verrous actifs.",
        "Die gewählten Optionen stehen mit den aktuellen Sperren in Konflikt.",
        "Выбранные параметры конфликтуют с текущими блокировками.",
        "所选选项与当前锁定设置冲突。",
        "所選選項與目前鎖定設定衝突。",
        "選択した項目が現在のロック設定と競合しています。",
        "Las opciones seleccionadas entran en conflicto con los bloqueos actuales.",
        "As opções selecionadas entram em conflito com os bloqueios atuais.");
}
