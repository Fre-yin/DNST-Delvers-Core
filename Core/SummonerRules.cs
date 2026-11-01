namespace DungeonSettlersDelvers.Core;

// The summoner of a summon read from its backing field (DelversSummons). The field is a
// Nullable<Guid>: hasValue at +0, the Guid at +4. No game types here.
internal static class SummonerRules
{
    internal const int ValueOffset = 4;

    // A field offset of 0 or less means the field was not found; the summoner then reads as "".
    internal static bool FieldUsable(int offset) => offset > 0;

    // The summoner's GUID as the game writes a unit's GUID, "" when there is none.
    internal static string GuidText(bool hasValue, byte[] bytes)
    {
        if (!hasValue || bytes == null || bytes.Length != 16) return "";
        var guid = new Guid(bytes);
        return guid == Guid.Empty ? "" : guid.ToString();
    }

    internal static string FieldLine(string kind, int offset)
        => $"CORE_SUMMON_FIELD type={kind} offset={offset} ok={(FieldUsable(offset) ? 1 : 0)}";
}
