using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// C: when a party unit puts a bandage on itself.
// The AI uses the bandages laid on the item quick slots (Q and the Extended Hotbar's two extra slots);
// from the bag only with the Group AI option on (FromBagKey, off by default). The kind follows the situation, the other kind
// stands in when the wanted one is not ready ("yes, both ways"), and no salve is kept back.
// The light and heavy bandage join the list as the last stand-in, heavy before light, and only on an
// open wound, the only thing they act on (BandageRules.AllowsUse).
internal static class GroupAiBandageRules
{
    internal const float PoolLimit = 0.5f;
    internal const float HealerPoolLimit = 0.6f;
    internal const float EmergencyPool = 0.25f;
    internal const float CooldownSeconds = 10f;
    internal const string FromBagKey = "groupai.bandagesfrombag";
    internal const string Salve = "salve";
    internal const string Herbal = "herbal";
    internal const string Heavy = "heavy";
    internal const string Light = "light";
    // The game's first aid kit (ITEM_AidKit, SKILL_AidKit): at a lethal wound the
    // kit first, else the salve; below lethal the AI never takes it.
    internal const string AidKit = "aidkit";
    internal const string AidKitItemKey = "ITEM_AidKit";
    internal const string AidKitSkillKey = "SKILL_AidKit";

    // The slot names in the log, in the order GroupAiBandageSlots reads them.
    internal static readonly string[] SlotNames = { "q", "extra1", "extra2" };

    // The severe stage's name ("heavy", the third stage; it lowers every heal), whatever the pool; "cd": no
    // other heal free, bleeding or an open wound, and the weaker pool below the limit (the healer's higher); null: no need.
    internal static string Trigger(string severe, bool bleedingOrWound, bool healAvailable, float pool, bool healer)
    {
        if (severe != null) return severe;
        if (!bleedingOrWound || healAvailable) return null;
        return pool < (healer ? HealerPoolLimit : PoolLimit) ? "cd" : null;
    }

    // The situation's kind: the salve at a severe wound (heavy) or below PoolLimit, else the herbal one (light, deep).
    internal static string Want(bool severe, float pool) => severe || pool < PoolLimit ? Salve : Herbal;

    internal static string Want(bool severe, float pool, bool fatal) => fatal ? AidKit : Want(severe, pool);

    // A bandage kind is ready when the bag holds it and its key lies on a quick slot, or the bag option is on.
    internal static bool Ready(string kind, string[] slotKinds, int inBag, bool fromBag)
        => inBag > 0 && (fromBag || Array.IndexOf(slotKinds ?? Array.Empty<string>(), kind) >= 0);

    // The order in which the kinds stand in for the wanted one: the aid kit only when wanted (a lethal wound), then the salve,
    // then the herbal one; the other bandage of first aid both ways.
    private static readonly string[] AidKitOrder = { AidKit, Salve, Herbal };
    private static readonly string[] SalveOrder = { Salve, Herbal };
    private static readonly string[] HerbalOrder = { Herbal, Salve };
    // The last stand-ins, on an open wound only.
    private static readonly string[] WoundOrder = { Heavy, Light };

    // The first ready kind in the wanted kind's order, else heavy or light on an open wound, else null.
    internal static string Pick(string want, Func<string, bool> ready, bool woundOpen)
    {
        foreach (var kind in want == AidKit ? AidKitOrder : want == Salve ? SalveOrder : HerbalOrder)
            if (ready(kind)) return kind;
        if (!woundOpen) return null;
        foreach (var kind in WoundOrder)
            if (ready(kind)) return kind;
        return null;
    }

    // Where the picked bandage comes from: the first slot holding its key, else the bag.
    internal static string From(string kind, string[] slotKinds)
    {
        for (var i = 0; slotKinds != null && i < slotKinds.Length && i < SlotNames.Length; i++)
            if (slotKinds[i] == kind) return SlotNames[i];
        return "bag";
    }

    // Why nothing was picked: "none:noslot" when the bag holds a bandage that lies on no slot and the option is off.
    internal static string NoneReason(int inBag, bool fromBag) => !fromBag && inBag > 0 ? "none:noslot" : "none";

    // The kind behind an item key: one of Core's bandages or the game's aid kit, else null.
    internal static string KindOf(string key)
    {
        if (key == AidKitItemKey) return AidKit;
        return BandageRules.KindOf(key) switch
        {
            BandageRules.Kind.Salve => Salve,
            BandageRules.Kind.Herbal => Herbal,
            BandageRules.Kind.Heavy => Heavy,
            BandageRules.Kind.Light => Light,
            _ => null
        };
    }

    // What the bag holds of each kind, for the log.
    internal readonly record struct BagCounts(int Salves, int Herbals, int Heavies, int Lights, int Kits)
    {
        internal int Bandages => Salves + Herbals + Heavies + Lights;
    }

    internal static string Slots(string[] slotKinds)
    {
        var parts = new string[SlotNames.Length];
        for (var i = 0; i < SlotNames.Length; i++)
            parts[i] = (i == 0 ? "q" : "e" + i) + ":" + (slotKinds != null && i < slotKinds.Length && slotKinds[i] != null ? slotKinds[i] : "-");
        return string.Join(",", parts);
    }

    // "use", or why it waits: a foe within 1 that is neither taunted nor held, unless the pool is below EmergencyPool.
    internal static string Safety(int nearest, bool nearestNeutral, float pool)
    {
        if (nearest > 1 || nearestNeutral || pool < EmergencyPool) return "use";
        return "wait:nearest";
    }

    // bagopt: the option "AI may take bandages from the bag" (0|1); salves, herbals, heavies, lights: what the bag holds of each;
    // at a lethal wound the aid kits at the end (kits=n).
    internal static string Line(string unit, string item, string trigger, float pool, string stage, bool bleeding, int nearest, bool taunted,
        string result, string from, string slots, bool fromBag, BagCounts bag, long tick, bool fatal)
        => string.Format(CultureInfo.InvariantCulture,
            "CORE_AI_BANDAGE unit={0} item={1} trigger={2} pool={3:0.00} stage={4} bleeding={5} nearest={6} taunted={7} result={8} from={9} slots={10} bagopt={11} salves={12} herbals={13} heavies={14} lights={15} tick={16}",
            unit, item ?? "-", trigger ?? "-", pool, stage ?? "-", bleeding ? 1 : 0, nearest == int.MaxValue ? -1 : nearest, taunted ? 1 : 0, result,
            from ?? "-", slots ?? "-", fromBag ? 1 : 0, bag.Salves, bag.Herbals, bag.Heavies, bag.Lights, tick)
            + (fatal ? " kits=" + bag.Kits.ToString(CultureInfo.InvariantCulture) : "");

    // (Korgas, result=use at pool 0.00, no CORE_BANDAGE_USED, down 130 ticks later): an order of Core's
    // that put no bandage on within LostSeconds (putting one on takes 2 s) is logged as lost, with what the unit was then.
    internal const float LostSeconds = 4f;

    internal static string LostReason(bool dead, bool down, bool stunned) => dead ? "dead" : down ? "down" : stunned ? "stun" : "unknown";

    internal static string LostLine(string unit, string item, float waited, string reason, string behaviour, long tick)
        => string.Format(CultureInfo.InvariantCulture, "CORE_AI_BANDAGE_LOST unit={0} item={1} waited={2:0.0} reason={3} behaviour={4} tick={5}",
            unit, item ?? "-", waited, reason, behaviour ?? "-", tick);
}
