using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// Where the weak bandits come (30. and), pure so the offline tests cover it:
// - In the forest (region 0) on the levels 1 to 3 (the game's phases 1 to 3, floor index 0 to 2), in a combat room whose monsters
//   the game rolls (RANDOMSPAWN, room type Combat), a weak group stands in for the normal monster pack with Percent (10 %) chance.
//   Level 4 holds the camp.
// - The floor's seed, region, floor and the room's id decide it, the same for the same floor; nothing is saved. A room the game
//   refills later (its respawn) gets normal monsters, as in the game.
// - The group: two classes on level 1, three on levels 2 and 3, picked at random from the classes of the level by a second hash,
//   no class twice.
internal static class WeakBanditSpawnRules
{
    internal const string PercentKey = "bandits.weak.percent";
    internal const double DefaultPercent = 10.0;
    internal const int Region = 0;
    // The game's RoomType.Combat (DungeonRoomTable RoomType 2, the Combat_Middle rooms).
    internal const int CombatRoomType = 2;

    // Only a plain combat room takes a weak group; HardCombat (3), Boss (4) and the others keep the game's roll. The game hands the
    // room's type to GetRandomSpawnGroupData from its own room data, so the decision waits for that call.
    internal static bool Replaceable(int roomType) => roomType == CombatRoomType;
    internal const string GroupKey = "SPAWN_DelversCore_WeakBandits";
    private const uint RoomSalt = 0x5745414Bu;
    private const uint GroupSalt = 0x47524F55u;

    internal static double Percent(double? setting)
        => setting is double v && !double.IsNaN(v) ? Math.Clamp(v, 0.0, 100.0) : DefaultPercent;

    // The weak level of a floor (1 to 3), or 0 where weak bandits never come.
    internal static int LevelOf(int region, int floor) => region == Region && floor >= 0 && floor < WeakBanditRules.Levels ? floor + 1 : 0;

    internal static int SizeOf(int level) => level <= 1 ? 2 : 3;

    // Whether this room of this floor gets a weak group.
    internal static bool Hit(int seed, int region, int floor, string roomId, double percent)
        => percent > 0.0 && Hash(seed, region, floor, roomId, RoomSalt) % 10000u < (uint)Math.Round(percent * 100.0);

    // The classes of the group (their key ends), distinct, in the order the hash drew them.
    internal static List<string> GroupOf(int seed, int region, int floor, string roomId, int level, IReadOnlyList<string> classes)
    {
        var pool = new List<string>(classes);
        var picked = new List<string>();
        var h = Hash(seed, region, floor, roomId, GroupSalt);
        for (var i = 0; i < SizeOf(level) && pool.Count > 0; i++)
        {
            var index = (int)(h % (uint)pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
            h = Mix(h + 0x9E3779B9u);
        }
        return picked;
    }

    // The unit keys of a group on a level.
    internal static string[] KeysOf(int level, IEnumerable<string> classes)
    {
        var keys = new List<string>();
        foreach (var suffix in classes)
        {
            var d = WeakBanditRules.Find(level, suffix);
            if (d != null) keys.Add(d.Key);
        }
        return keys.ToArray();
    }

    internal static IReadOnlyList<string> ClassesOf() => Array.ConvertAll(WeakBanditRules.Classes, c => c.Suffix);

    // FNV-1a over the room id, mixed with seed, region, floor and a salt.
    private static uint Hash(int seed, int region, int floor, string roomId, uint salt)
    {
        unchecked
        {
            var h = 2166136261u ^ salt;
            foreach (var ch in roomId ?? "")
            {
                h ^= ch;
                h *= 16777619u;
            }
            h ^= (uint)seed * 0x9E3779B1u ^ (uint)region * 0x85EBCA77u ^ (uint)floor * 0xC2B2AE3Du;
            return Mix(h);
        }
    }

    private static uint Mix(uint h)
    {
        unchecked
        {
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }

    internal static string RoomLine(int seed, int region, int floor, string roomId, string[] keys)
        => string.Format(CultureInfo.InvariantCulture, "CORE_WEAKBANDIT_ROOM seed={0} region={1} floor={2} room={3} units={4}",
            seed, region, floor, roomId ?? "-", string.Join(",", keys));

    internal static string FloorLine(int seed, int region, int floor, int rooms, int replaced, double percent)
        => string.Format(CultureInfo.InvariantCulture, "CORE_WEAKBANDIT_FLOOR seed={0} region={1} floor={2} rooms={3} replaced={4} percent={5:0.#}",
            seed, region, floor, rooms, replaced, percent);
}
