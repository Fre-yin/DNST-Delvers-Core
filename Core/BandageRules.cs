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
    // alone; the game's ointment (kind null) on any bleeding unit, as before. The aid kit on a fallen unit is the game's own case, as
    // with Q (AidKitRefusal): Core's skill for it runs the ointment's class and would use the kit up without the game's rescue, so
    // it takes no fallen unit and the kit stays in the bag; the game's own click on the fallen unit rescues it.
    internal static bool TakesOnOther(Kind? kind, bool woundOpen, int bleedingStacks, bool fallen = false)
        => kind == Kind.Kit && fallen ? false
            : kind is Kind known ? AllowsUse(woundOpen, known, bleedingStacks) : woundOpen || bleedingStacks > 0;

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

    // ---- the aid kit put on another unit, watched until Core dressed with it ----

    // A kit put on a comrade (BandageOtherWatch): only the game using up this very kit for Core's skill opens an entry, one per use;
    // Core's dressing of that use closes it (Dressed, the helper's newest entry, as the dressing follows its own use at once). An
    // entry still open RefundAfterSeconds of game time later is due: the kit comes back. A kit that left the bag any other way
    // (dropped, given, traded, sold) opens nothing and closes nothing, so no kit is doubled and none is lost. A load or a new
    // campaign clears the ledger, as its times belong to the old clock.
    internal const double RefundAfterSeconds = 5.0;

    // Only Core's dressing with the kit put on a comrade closes a kit's entry; a bandage or salve of the same helper, on itself or on
    // another, leaves it open.
    internal static bool ClosesKitEntry(bool onOther, Kind kind) => onOther && kind == Kind.Kit;

    // A kit Core's skill took into the helper's hand that was never used up (the use broke off, say the target walked away): the
    // game leaves it in the hand. Once the helper no longer uses an item, HandBackAfterSeconds after the aim, the kit goes back
    // (BandageOtherWatch, CORE_BANDAGE_BACK). A kit used up belongs to the ledger alone, so no kit comes back twice; a use still
    // running after HandWatchSeconds is let go.
    internal const double HandBackAfterSeconds = 2.0;
    internal const double HandWatchSeconds = 60.0;

    internal enum HandStep { Wait, Back, Done }

    // The helper still uses the kit: the click in the target mode gives it an ActCombatBehaviourTask with Core's kit skill (the game's
    // UseSkillMode.UseSkill), which walks to the target and puts the kit on; from the bag the game's own UseItem
    // tasks do the same. Any other task, or none, is no use of the kit.
    internal static bool StillUsing(string taskSkill, string taskName)
        => taskSkill == AidKitOtherSkillKey || (taskName != null && taskName.Contains("UseItem", StringComparison.Ordinal));
    internal const string AidKitOtherSkillKey = "SKILL_DelversCoreAidKitOther";

    internal static HandStep HandBack(bool usedUp, bool stillUsing, bool carriesKit, double sinceAim)
    {
        if (usedUp) return HandStep.Done;
        if (sinceAim < HandBackAfterSeconds) return HandStep.Wait;
        if (stillUsing) return sinceAim >= HandWatchSeconds ? HandStep.Done : HandStep.Wait;
        return carriesKit ? HandStep.Back : HandStep.Done;
    }

    internal sealed class KitLedger<T>
    {
        internal readonly struct Entry
        {
            internal Entry(Guid helper, T where, double goneAt)
            {
                Helper = helper;
                Where = where;
                GoneAt = goneAt;
            }

            internal Guid Helper { get; }
            internal T Where { get; }
            internal double GoneAt { get; }
        }

        private readonly List<Entry> open = new();

        internal int Count => open.Count;

        internal void UsedUp(Guid helper, T where, double at) => open.Add(new Entry(helper, where, at));

        // Core dressed with the kit this helper used up last; every other entry, of this helper or another, stays.
        internal bool Dressed(Guid helper)
        {
            for (var i = open.Count - 1; i >= 0; i--)
            {
                if (open[i].Helper != helper) continue;
                open.RemoveAt(i);
                return true;
            }
            return false;
        }

        // The entries due at now, taken out of the ledger: each comes back exactly once.
        internal List<Entry> Due(double now)
        {
            var due = open.FindAll(entry => now - entry.GoneAt >= RefundAfterSeconds);
            open.RemoveAll(entry => now - entry.GoneAt >= RefundAfterSeconds);
            return due;
        }

        internal void Clear() => open.Clear();
    }
}
