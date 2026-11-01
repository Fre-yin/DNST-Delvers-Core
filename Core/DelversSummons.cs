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

    // The owner as a unit of the shown map or a party member, living or dead; null when there is none.
    internal static UnitEntity OwnerUnitOf(IEntity summon)
        => Guid.TryParse(OwnerOf(summon), out var owner) ? CoreGame.FindUnit(owner) : null;

    // Whether the owner is one of Core's units, a bandit.
    internal static bool IsCoreOwned(IEntity summon)
    {
        var owner = OwnerUnitOf(summon);
        return owner != null && CoreUnitRules.IsCoreUnit(owner.Key);
    }

    // The owner's living summons on the shown map, units and buildings, in the order they appeared (SummonOwnerRules). Every
    // side's units are searched: a player's bubbleling is an Ally and stands in no list of the player's units.
    internal static List<IEntity> SummonsOf(string owner)
    {
        var result = new List<IEntity>();
        if (string.IsNullOrEmpty(owner)) return result;
        var found = new Dictionary<string, IEntity>(StringComparer.Ordinal);
        var alive = new HashSet<string>(StringComparer.Ordinal);
        foreach (var unit in CoreGame.MapUnits())
        {
            if (unit.IsDead) continue;
            var entity = unit.Cast<IEntity>();
            Note(entity, owner, found, alive);
        }
        var buildings = CoreGame.InMap?.GetCombatableBuildingsOnMap();
        for (var i = 0; buildings != null && i < buildings.Count; i++)
        {
            var building = buildings[i];
            if (building == null || building.IsDead) continue;
            Note(building.Cast<IEntity>(), owner, found, alive);
        }
        order.Keep(alive);
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
