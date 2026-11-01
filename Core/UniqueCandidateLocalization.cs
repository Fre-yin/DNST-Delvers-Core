using HarmonyLib;

namespace DungeonSettlersDelvers.Core;

internal static class UniqueCandidateLocalization
{
    // The rows come from the plain list CoreTextRows.Registered, in its order.
    private static readonly TextKeyTableData[] Rows = BuildRows();

    // A loop, not a lambda over the tuples: it would need NullableAttribute, which the IL2CPP reference set lacks (CS0656).
    private static TextKeyTableData[] BuildRows()
    {
        var rows = new List<TextKeyTableData>();
        foreach (var (key, text) in CoreTextRows.Registered) rows.Add(Row(key, text));
        return rows.ToArray();
    }

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
        var problem = CoreTextRules.Problems(CoreTextRows.Checked).FirstOrDefault();
        if (problem != null) throw new InvalidOperationException(problem);
    }

    private static bool SameTranslations(TextKeyTableData left, TextKeyTableData right)
    {
        foreach (var language in Enum.GetValues<LanguageType>())
            if (TextKeyLanguageTool.GetTextByLanguage(left, language)
                != TextKeyLanguageTool.GetTextByLanguage(right, language)) return false;
        return true;
    }

    // Each language laid out by CoreTextLayout (a number stays with its percent sign).
    private static TextKeyTableData Row(string key, UniqueCandidateTranslations text) => new()
    {
        Key = key,
        English = CoreTextLayout.KeepUnits(text.English),
        Korean = CoreTextLayout.KeepUnits(text.Korean),
        French = CoreTextLayout.KeepUnits(text.French),
        German = CoreTextLayout.KeepUnits(text.German),
        Russian = CoreTextLayout.KeepUnits(text.Russian),
        ChineseSimplified = CoreTextLayout.KeepUnits(text.ChineseSimplified),
        ChineseTraditional = CoreTextLayout.KeepUnits(text.ChineseTraditional),
        Japanese = CoreTextLayout.KeepUnits(text.Japanese),
        Spanish = CoreTextLayout.KeepUnits(text.Spanish),
        PortugueseBrazil = CoreTextLayout.KeepUnits(text.PortugueseBrazil)
    };
}

[HarmonyPatch(typeof(TextSheet), nameof(TextSheet.ParseMatchingLanguage))]
internal static class DelversLanguageRegistrationPatch
{
    private static void Postfix(TextSheet __instance, LanguageType type)
    {
        try { UniqueCandidateLocalization.Register(__instance, type); }
        catch (Exception ex)
        {
            try { DelversHost.Warning("CORE_LOCALIZATION_REGISTRATION_FAILED: " + ex.Message); }
            catch { /* The native language parse must keep running. */ }
        }
    }
}
