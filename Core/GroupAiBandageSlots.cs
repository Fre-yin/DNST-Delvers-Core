using System.Reflection;

namespace DungeonSettlersDelvers.Core;

// The bandage kinds on a unit's item quick slots, in GroupAiBandageRules.SlotNames order: Q from the game's quick slot viewer
// (QuickSlotData has one item key), then the Extended Hotbar's two extra slots through GroupAiBandageSlotReader. The reader
// is looked for once per campaign; CORE_AI_BANDAGE_SLOTS says once whether it was found.
internal static class GroupAiBandageSlots
{
    private static bool searched;
    private static MethodInfo reader;

    internal static void Reset()
    {
        searched = false;
        reader = null;
    }

    internal static string[] Kinds(GroupAiSituation s)
    {
        var unit = s?.Owner;
        if (unit == null) return new string[GroupAiBandageRules.SlotNames.Length];
        string quick = null;
        try { quick = s.Decide?._quickSlotViewer?.GetItemQuickSlotKey(unit.Guid); }
        catch (Exception ex) { DelversHost.Warning("CORE_AI_BANDAGE_SLOTS q failed: " + ex.Message); }
        return Kinds(quick, s.Guid);
    }

    // Outside the combat decision (GroupAiAftercare): Q from the campaign's quick slots, as AidKitStanding reads it.
    internal static string[] Kinds(UnitEntity unit, string guid)
    {
        string quick = null;
        try { quick = CoreGame.Flow?._campaignData?.PlayerUnitDataContainer?._unitQuickSlot?.GetItemQuickSlotKey(unit.Guid); }
        catch (Exception ex) { DelversHost.Warning("CORE_AI_BANDAGE_SLOTS q failed: " + ex.Message); }
        return Kinds(quick, guid);
    }

    private static string[] Kinds(string quick, string guid)
    {
        var kinds = new string[GroupAiBandageRules.SlotNames.Length];
        kinds[0] = Kind(quick);
        var extra = GroupAiBandageSlotReader.Read(Reader(), guid);
        for (var i = 0; i < extra.Length && i + 1 < kinds.Length; i++) kinds[i + 1] = Kind(extra[i]);
        return kinds;
    }

    private static string Kind(string key) => GroupAiBandageRules.KindOf(key);

    private static MethodInfo Reader()
    {
        if (searched) return reader;
        searched = true;
        try { reader = GroupAiBandageSlotReader.Find(AppDomain.CurrentDomain.GetAssemblies()); }
        catch (Exception ex) { DelversHost.Warning("CORE_AI_BANDAGE_SLOTS search failed: " + ex.Message); }
        GroupAiPolicy.Log(GroupAiRules.BandageSlotsLine(reader != null ? reader.DeclaringType?.FullName + "." + reader.Name : "none"));
        return reader;
    }
}
