namespace DungeonSettlersDelvers.Core;

// The decisions of the bandages and of the first aid kit on a standing unit, as pure rules. Bandages and AidKitStanding read
// the game's values and call these; the offline tests cover them. The shares, cuts and stage rules stay in WoundRules.
internal static class BandageRules
{
    // Kit: the game's aid kit put on another unit (UseOnOther); it closes every wound and takes all the game's bleeding.
    internal enum Kind { Light, Heavy, Herbal, Salve, Kit }

    internal const string AidKitItemKey = "ITEM_AidKit";
    internal const string OintmentItemKey = "ITEM_Ointment";
    internal const string WildHerbItemKey = "ITEM_WildHerb";

    // The bandage an item key names; null for every other item.
    internal static Kind? KindOf(string itemKey)
    {
        if (itemKey == BandageText.LightItemKey) return Kind.Light;
        if (itemKey == BandageText.HeavyItemKey) return Kind.Heavy;
        if (itemKey == BandageText.HerbalItemKey) return Kind.Herbal;
        if (itemKey == BandageText.SalveItemKey) return Kind.Salve;
        return null;
    }

    // What an item does put on another unit (UseOnOther), standing or fallen alike, each at its own strength
    // (with the bandages a fallen one's bleeding can be lessened or stopped, by the fitting bandage) so the
    // cheapest one never does all: the bandages as on oneself, the game's raw herbs like a light bandage, the aid kit
    // closes all. Null for every other item, the game's ointment too: it heals the other as in the game, a heal with a source
    // like any (WoundRules.Heal). Eaten or used on oneself the game's items stay the game's.
    internal static Kind? KindOnOther(string itemKey) => KindOf(itemKey) ?? itemKey switch
    {
        WildHerbItemKey => Kind.Light,
        AidKitItemKey => Kind.Kit,
        _ => null
    };

    // The two bandages of first aid and the aid kit also take a share of the game's bleeding.
    internal static bool CutsBleeding(Kind kind) => kind == Kind.Herbal || kind == Kind.Salve || kind == Kind.Kit;

    // The share of the game's bleeding stacks a bandage of first aid takes, all for the aid kit; 0 for the others.
    internal static double BleedingCutShare(Kind kind)
        => kind == Kind.Kit ? 1.0 : kind == Kind.Salve ? WoundRules.SalveBleedingCut : kind == Kind.Herbal ? WoundRules.HerbalBleedingCut : 0;

    // The salve leaves the ointment's routine running, so its own heal follows; the others end it.
    internal static bool LeavesRoutineRunning(Kind kind) => kind == Kind.Salve;

    // The share of the unit's whole health a bandage takes from the wound (WoundRules.ClosesWound for the two of first aid).
    internal static double ShareOf(Kind kind)
        => kind == Kind.Light ? WoundRules.LightBandageShare : kind == Kind.Heavy ? WoundRules.HeavyBandageShare : WoundRules.ClosesWound;
    // Checked before the unit takes the bandage in hand: only with an open wound, or for the two of first aid with the game's
    // bleeding alone.
    internal static bool AllowsUse(bool woundOpen, Kind kind, int bleedingStacks)
        => woundOpen || (CutsBleeding(kind) && bleedingStacks > 0);

    // A unit an item may be put on (BandageOnOther.TargetAt): the same rule as on oneself for the items Core dresses with (kind,
    // KindOnOther), so the light and heavy bandage and the raw herbs need an open wound and are never used up on the game's bleeding
    // alone; the game's ointment (kind null) on any bleeding unit, as before.
    internal static bool TakesOnOther(Kind? kind, bool woundOpen, int bleedingStacks)
        => kind is Kind known ? AllowsUse(woundOpen, known, bleedingStacks) : woundOpen || bleedingStacks > 0;

    // The buffer after the bandage is on. A lethal wound is not closed by the herbal or salve bandage: they take 30 or 40 %
    // of the whole health, unless the setting lets them close it; the light and heavy bandage take their share as on any stage.
    internal static double BufferAfter(WoundRules.Stage stageBefore, Kind kind, double buffer, double maxLife, bool fatalCloses)
        => TakesFatalShare(stageBefore, kind)
            ? WoundRules.FatalBandage(buffer, kind == Kind.Salve, maxLife, fatalCloses)
            : WoundRules.Bandage(buffer, ShareOf(kind), maxLife);

    // Whether the lethal-wound rule applies: only the herbal and the salve bandage on a lethal wound; fatalCloses of
    // BufferAfter matters only then.
    internal static bool TakesFatalShare(WoundRules.Stage stageBefore, Kind kind)
        => stageBefore == WoundRules.Stage.Fatal && (kind == Kind.Herbal || kind == Kind.Salve);

    // ---- the first aid kit on a standing unit (Q, AidKitStanding) ----

    internal const string RefusalNoItem = "native:noitem";
    internal const string RefusalNoRule = "native:norule";
    internal const string RefusalNotParty = "native:notparty";
    internal const string RefusalNoAffecters = "native:noaffecters";
    internal const string RefusalDown = "native:down";
    internal const string RefusalNoWound = "native:nowound";

    // What Core read about the unit and its kit when Q was pressed.
    internal readonly struct AidKitFacts
    {
        internal AidKitFacts(bool hasItem, bool woundsRule, bool woundOpen, bool livingPartyMember, bool hasAffecters, bool down, int bleedingStacks)
        {
            HasItem = hasItem;
            WoundsRule = woundsRule;
            WoundOpen = woundOpen;
            LivingPartyMember = livingPartyMember;
            HasAffecters = hasAffecters;
            Down = down;
            BleedingStacks = bleedingStacks;
        }

        internal bool HasItem { get; }
        internal bool WoundsRule { get; }
        internal bool WoundOpen { get; }
        internal bool LivingPartyMember { get; }
        internal bool HasAffecters { get; }
        internal bool Down { get; }
        internal int BleedingStacks { get; }
    }

    // The kit also stops a bleeding completely before the unit is down. Null: Core takes the Q over and orders the kit on the
    // unit, whatever the stage (a lethal wound included); else the reason the game's own handler runs. A fallen unit is the
    // game's own case ("down").
    internal static string AidKitRefusal(AidKitFacts facts)
    {
        if (!facts.HasItem) return RefusalNoItem;
        if (!facts.WoundsRule && !facts.WoundOpen) return RefusalNoRule;
        if (!facts.LivingPartyMember) return RefusalNotParty;
        if (!facts.HasAffecters) return RefusalNoAffecters;
        if (facts.Down) return RefusalDown;
        if (!facts.WoundOpen && facts.BleedingStacks <= 0) return RefusalNoWound;
        return null;
    }
}
