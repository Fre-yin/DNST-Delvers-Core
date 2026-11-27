using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace DungeonSettlersDelvers.Core;

// The summoner of a summon. The game keeps the summoner's GUID on the summon itself,
// UnitEntity.Summoner and BuildingEntity.Summoner, as a Nullable<Guid>. Through the interop wrapper it arrives boxed the
// way IL2CPP boxes a nullable, so HasValue and Value read the wrong bytes (no summon
// matched its summoner; Frieren's seed burst never found her seed). The backing field is read in
// place instead (SummonerRules). Public for the packs, e.g. Zoltraak's own and built buildings.
public static class DelversSummons
{
    private const string Field = "<Summoner>k__BackingField";
    private static int unitOffset = -1;
    private static int buildingOffset = -1;

    // The summoner's GUID in the format of unit.Guid.ToString(), "" when the entity was not summoned or the field is not
    // readable.
    public static string SummonerGuid(IEntity entity)
    {
        if (entity == null) return "";
        try
        {
            var unit = entity.TryCast<UnitEntity>();
            if (unit != null) return Read(unit, ref unitOffset, Il2CppClassPointerStore<UnitEntity>.NativeClassPtr, "unit");
            var building = entity.TryCast<BuildingEntity>();
            if (building != null) return Read(building, ref buildingOffset, Il2CppClassPointerStore<BuildingEntity>.NativeClassPtr, "building");
        }
        catch (Exception) { /* No summoner readable: not a summon. */ }
        return "";
    }

    // Summon -> owner and owner -> summons, one way for the players and the bandits: the formation window, the
    // bandits' AI and BanditTint all ask here. Internal until a pack needs it.
    private static readonly SummonOwnerRules order = new();

    // The owner's GUID, "" when the entity was not summoned (SummonerGuid).
    internal static string OwnerOf(IEntity summon) => SummonerGuid(summon);

    // The owner as a unit on any loaded map (the shown map first): a living unit, or a party member living or dead; null when there is
    // none. A bandit summons on the floor the party fights on, also while the player looks at the base. A dead bandit stands on no
    // map any more (IsCoreOwned asks its corpse).
    internal static UnitEntity OwnerUnitOf(IEntity summon)
        => Guid.TryParse(OwnerOf(summon), out var owner) ? CoreGame.FindUnitOnAnyMap(owner, out _) : null;

    // Whether the owner is one of Core's units, a bandit.
    internal static bool IsCoreOwned(IEntity summon)
    {
        var owner = OwnerUnitOf(summon);
        if (owner != null) return CoreUnitRules.IsCoreUnit(owner.Key);
        // A dead bandit's summons stay bandits (CorpseKeep, also once its corpse is gone): a dead herbalist's sun bloom keeps aiming
        // at the party, not at the other bandits.
        var id = OwnerOf(summon);
        return id.Length > 0 && CorpseKeep.IsDeadBandit(id);
    }

    // The owner's living summons on the shown map, units and buildings, in the order they appeared (SummonOwnerRules). Every
    // side's units are searched: a player's bubbleling is an Ally and stands in no list of the player's units.
    internal static List<IEntity> SummonsOf(string owner) => SummonsIn(owner, CoreGame.InMap, true);

    // The same on a loaded map (the map an owner died on); the running numbers of the shown map's summons stay as they are.
    internal static List<IEntity> SummonsOf(string owner, MapType map) => SummonsIn(owner, CoreGame.InMapOf(map), false);

    private static List<IEntity> SummonsIn(string owner, IInMapDataSupplier inMap, bool shown)
    {
        var result = new List<IEntity>();
        if (string.IsNullOrEmpty(owner) || inMap == null) return result;
        var found = new Dictionary<string, IEntity>(StringComparer.Ordinal);
        var alive = new HashSet<string>(StringComparer.Ordinal);
        var units = inMap.GetAliveUnitsOnMap();
        for (var i = 0; units != null && i < units.Count; i++)
        {
            var unit = units[i];
            if (unit == null || unit.IsDead) continue;
            Note(unit.Cast<IEntity>(), owner, found, alive);
        }
        var buildings = inMap.GetCombatableBuildingsOnMap();
        for (var i = 0; buildings != null && i < buildings.Count; i++)
        {
            var building = buildings[i];
            if (building == null || building.IsDead) continue;
            Note(building.Cast<IEntity>(), owner, found, alive);
        }
        if (shown) order.Keep(alive);
        foreach (var id in order.Sort(found.Keys)) result.Add(found[id]);
        return result;
    }

    // Each summon on the map gets its running number when first seen, whoever owns it, so the order holds for every owner.
    private static void Note(IEntity entity, string owner, Dictionary<string, IEntity> found, HashSet<string> alive)
    {
        var summoner = OwnerOf(entity);
        if (summoner.Length == 0) return;
        var id = entity.Guid.ToString();
        alive.Add(id);
        order.Note(id);
        if (SummonOwnerRules.IsOwnedBy(summoner, owner)) found[id] = entity;
    }

    // The order goes with the campaign.
    internal static void Forget() => order.Clear();

    private static string Read(Il2CppObjectBase entity, ref int offset, IntPtr type, string kind)
    {
        if (offset < 0)
        {
            var field = type == IntPtr.Zero ? IntPtr.Zero : IL2CPP.GetIl2CppField(type, Field);
            offset = field == IntPtr.Zero ? 0 : (int)IL2CPP.il2cpp_field_get_offset(field);
            DelversHost.Info(SummonerRules.FieldLine(kind, offset));
        }
        if (!SummonerRules.FieldUsable(offset) || entity.Pointer == IntPtr.Zero) return "";
        var address = entity.Pointer + offset;
        var bytes = new byte[16];
        Marshal.Copy(address + SummonerRules.ValueOffset, bytes, 0, 16);
        return SummonerRules.GuidText(Marshal.ReadByte(address) != 0, bytes);
    }
}
