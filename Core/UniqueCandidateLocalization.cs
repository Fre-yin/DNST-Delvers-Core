using HarmonyLib;
#if BEPINEX
using global::Refactor.Setting;
#else
using Il2CppRefactor.Setting;
#endif
#if BEPINEX
using global::Refactor.Util;
#else
using Il2CppRefactor.Util;
#endif
#if BEPINEX
using global::Util.Sheet;
#else
using Il2CppUtil.Sheet;
#endif

namespace DungeonSettlersDelvers.Core;

internal static class UniqueCandidateLocalization
{
    private static readonly TextKeyTableData[] Rows =
    {
        Row(UniqueCandidateLocalizationText.NativeLockConflictKey,
            UniqueCandidateLocalizationText.LockConflictFallback),
        Row(LoadIntegrityNoticeText.SaveBlockedKey, LoadIntegrityNoticeText.SaveBlocked),
        Row(LoadIntegrityNoticeText.SaveBlockedShortKey, LoadIntegrityNoticeText.SaveBlockedShort)
    };

    private static IntPtr registeredSheet;
    private static LanguageType registeredLanguage;

    internal static void ResetLifecycle()
    {
        registeredSheet = IntPtr.Zero;
        registeredLanguage = default;
    }

    internal static void EnsureCurrent()
    {
        var sheet = DataSheetManager.Instance?._text;
        if (sheet?._tableData == null || sheet._textTable == null) return;
        var language = LanguageSetting.GetCurrentLanguage();
        if (registeredSheet == sheet.Pointer && registeredLanguage == language
            && Rows.All(row => sheet._textTable.ContainsKey(row.Key))) return;
        Register(sheet, language);
    }

    internal static void Register(TextSheet sheet, LanguageType language)
    {
        if (sheet?._tableData == null || sheet._textTable == null) return;
        foreach (var row in Rows)
        {
            var isNativeFallback = row.Key == UniqueCandidateLocalizationText.NativeLockConflictKey;
            if (isNativeFallback && sheet._tableData.TryGetValue(row.Key, out var nativeRow)
                && nativeRow.Pointer != row.Pointer)
            {
                var nativeText = TextKeyLanguageTool.GetPreferredText(
                    TextKeyLanguageTool.GetTextByLanguage(nativeRow, language), nativeRow.English);
                sheet._textTable[row.Key] = string.IsNullOrWhiteSpace(nativeText)
                    ? TextKeyLanguageTool.GetPreferredText(
                        TextKeyLanguageTool.GetTextByLanguage(row, language), row.English)
                    : nativeText;
                continue;
            }

            var activeRow = row;
            if (sheet._tableData.TryGetValue(row.Key, out var existing))
            {
                if (existing.Pointer != row.Pointer && !SameTranslations(existing, row))
                    throw new InvalidOperationException("Core-TextKey ist bereits belegt: " + row.Key);
                activeRow = existing;
            }
            else sheet._tableData.Add(row.Key, row);

            var localized = TextKeyLanguageTool.GetTextByLanguage(activeRow, language);
            sheet._textTable[row.Key] = TextKeyLanguageTool.GetPreferredText(localized, activeRow.English);
        }

        registeredSheet = sheet.Pointer;
        registeredLanguage = language;
    }

    internal static string GetLockConflictText()
    {
        EnsureCurrent();
        var sheet = DataSheetManager.Instance?._text;
        return sheet?._textTable != null
            && sheet._textTable.TryGetValue(UniqueCandidateLocalizationText.NativeLockConflictKey, out var value)
            && !string.IsNullOrWhiteSpace(value)
                ? value
                : UniqueCandidateLocalizationText.LockConflictFallback.English;
    }

    internal static void ValidateAllLanguages()
    {
        foreach (var translation in UniqueCandidateLocalizationText.LockConflictFallback.AllLanguages)
        {
            if (string.IsNullOrWhiteSpace(translation))
                throw new InvalidOperationException("Leere Core-Fallback-Übersetzung: "
                    + UniqueCandidateLocalizationText.NativeLockConflictKey);
            RejectLongDash(UniqueCandidateLocalizationText.NativeLockConflictKey, translation);
        }
        foreach (var (key, text) in LoadIntegrityNoticeText.All)
            foreach (var translation in text.AllLanguages)
            {
                if (string.IsNullOrWhiteSpace(translation))
                    throw new InvalidOperationException("Leere Core-Übersetzung: " + key);
                RejectLongDash(key, translation);
            }
    }

    private static void RejectLongDash(string key, string text)
    {
        if (text.IndexOf('\u2013') >= 0 || text.IndexOf('\u2014') >= 0)
            throw new InvalidOperationException("Core-Übersetzung enthält einen Langstrich: " + key);
    }

    private static bool SameTranslations(TextKeyTableData left, TextKeyTableData right)
    {
        foreach (var language in Enum.GetValues<LanguageType>())
            if (TextKeyLanguageTool.GetTextByLanguage(left, language)
                != TextKeyLanguageTool.GetTextByLanguage(right, language)) return false;
        return true;
    }

    private static TextKeyTableData Row(string key, UniqueCandidateTranslations text) => new()
    {
        Key = key,
        English = text.English,
        Korean = text.Korean,
        French = text.French,
        German = text.German,
        Russian = text.Russian,
        ChineseSimplified = text.ChineseSimplified,
        ChineseTraditional = text.ChineseTraditional,
        Japanese = text.Japanese,
        Spanish = text.Spanish,
        PortugueseBrazil = text.PortugueseBrazil
    };
}

[HarmonyPatch(typeof(TextSheet), nameof(TextSheet.ParseMatchingLanguage))]
internal static class DelversLanguageRegistrationPatch
{
    private static void Postfix(TextSheet __instance, LanguageType __0)
    {
        try { UniqueCandidateLocalization.Register(__instance, __0); }
        catch (Exception ex)
        {
            try { DelversHost.Warning("CORE_LOCALIZATION_REGISTRATION_FAILED: " + ex.Message); }
            catch { /* The native language parse must keep running. */ }
        }
    }
}
