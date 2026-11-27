namespace DungeonSettlersDelvers.Core;

// Core's chance, one stream per function (for multiplayer; the plan of that day). A draw that changes the game
// comes from the multiplayer bridge when it shares its draws (CoreRandom), else from SplitMix64 over the campaign, the game tick, the
// unit, the stream and the count of draws of that stream in that tick: the same campaign and the same course give the same rolls,
// also after loading. Every caller compares with its own chance as before, so the odds stay as they were. A stream number is never
// used for another function; a new function takes a new one. Rules without the game.
internal static class CoreRandomRules
{
    // The bridge's mod id for Core's streams (DsmpBridge.Multiplayer.RandValue(modId, stream)).
    internal const string ModId = "delvers";

    internal const uint Wounds = 1;
    internal const uint EnemyAbilities = 2;
    internal const uint KitDrops = 3;
    internal const uint Founders = 4;
    internal const uint BanditWake = 5;
    // A bandit's fixed traits (BanditTraitRules): drawn without the tick, so the same on every load and player.
    internal const uint BanditTraits = 6;
    // Whether a bandit plays a combo at an opening (BanditComboRules), and the sentence of its call.
    internal const uint BanditCombos = 7;
    internal const uint BanditCalls = 8;
    // Whether a patrol of the game's comes as weak bandits (WeakBandits.PatrolFor), and which.
    internal const uint BanditPatrol = 9;

    // Every stream with its function, frozen: a number keeps its function for good.
    internal static readonly (uint Stream, string Function)[] Streams =
    {
        (Wounds, "wounds"), (EnemyAbilities, "enemy-abilities"), (KitDrops, "kit-drops"), (Founders, "founders"), (BanditWake, "bandit-wake"), (BanditTraits, "bandit-traits"),
        (BanditCombos, "bandit-combos"), (BanditCalls, "bandit-calls"), (BanditPatrol, "bandit-patrol")
    };

    // SplitMix64's finaliser.
    internal static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    // A GUID folded into 64 bits; Guid.Empty gives 0.
    internal static ulong Key(Guid guid)
    {
        var bytes = guid.ToByteArray();
        return BitConverter.ToUInt64(bytes, 0) ^ Mix(BitConverter.ToUInt64(bytes, 8));
    }

    // The value in [0, 1) for one draw: 53 bits of the mixed inputs.
    internal static double Value(Guid campaign, long tick, Guid unit, uint stream, uint count)
    {
        var mixed = Mix(Key(campaign) ^ Mix((ulong)tick ^ Mix(Key(unit) ^ Mix(((ulong)stream << 32) | count))));
        return (mixed >> 11) * (1.0 / (1UL << 53));
    }

    internal static string Line(string source, string reason)
        => "CORE_RANDOM source=" + source + " reason=" + reason;
}
