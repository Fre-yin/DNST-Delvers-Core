using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace DungeonSettlersDelvers.Core;

// Core's own rows in the game's effect table (AffecterSheet._affecterTable): a row made from a game
// effect as template, with Core's key, comment, stacks, duration, categories and sign. Used by the revive effects, the
// wounds, Lifebloom and the boss parts' equipment effects.
internal static class CoreAffecterRows
{
    // (true, null) when the row is there already, (false, null) without the template, (true, row) for the row added.
    // exposed: whether the player sees it, null for shown (the revive effects); the boss effects take the template's.
    internal static (bool Ok, AffecterTableData Added) Add(AffecterSheet sheet, string key, string templateKey, string comment, float? mood,
        bool templateValues, int maxStack = 1, bool hasDuration = true, string[] categories = null, bool positive = false, bool? exposed = null)
    {
        if (sheet.GetData(key) != null) return (true, null);
        var template = sheet.GetData(templateKey);
        if (template == null) return (false, null);
        var row = new AffecterTableData
        {
            Key = key,
            DevComment1 = key,
            DevComment2 = "Delvers Core: " + comment,
            Type = template.Type,
            MaxStack = maxStack,
            IsPositive = positive ? PositiveType.Positive : PositiveType.Negative,
            HasDuration = hasDuration,
            IsAbsolute = template.IsAbsolute,
            IsExposedToPlayer = exposed ?? true,
            BuildingAttachable = false,
            Categories = categories != null ? new Il2CppStringArray(categories) : template.Categories,
            AdjustStatsData = new Il2CppStringArray(mood.HasValue
                ? new[] { "Mood_" + mood.Value.ToString("0", System.Globalization.CultureInfo.InvariantCulture) }
                : Array.Empty<string>()),
            AdjustStatsPerStackData = new Il2CppStringArray(0),
            FloatValues = new Il2CppStructArray<float>(0),
            BindingValue = string.Empty,
            AdjustStats = new Il2CppSystem.Collections.Generic.Dictionary<StatType, float>(),
            AdjustStatsPerStack = new Il2CppSystem.Collections.Generic.Dictionary<StatType, float>()
        };
        if (mood.HasValue) row.AdjustStats.Add(StatType.Mood, mood.Value);
        if (templateValues)
        {
            // The text form for the game's LinkValues, which reads it after Parse, and the parsed
            // values for a row added later (like the toolkit's rows, which set both).
            row.AdjustStatsData = template.AdjustStatsData;
            var values = template.AdjustStats;
            if (values != null)
                foreach (var stat in values.Keys) row.AdjustStats[stat] = values[stat];
        }
        sheet._affecterTable.Add(key, row);
        return (true, row);
    }
}
