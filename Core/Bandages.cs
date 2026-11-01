using HarmonyLib;
#if BEPINEX
using SkillExecutionContext = global::Refactor.Combat.ExecutionContext;
using ExecutionCheckResult = global::Refactor.Combat.ExecutionCheckResult;
using ActiveSkill_Base = global::Refactor.Combat.ActiveSkill_Base;
using OintmentRoutine = global::Refactor.Combat.SkillRelated.SKILL_Ointment._SkillRoutine_d__0;
#else
using SkillExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
using ExecutionCheckResult = Il2CppRefactor.Combat.ExecutionCheckResult;
using ActiveSkill_Base = Il2CppRefactor.Combat.ActiveSkill_Base;
using OintmentRoutine = Il2CppRefactor.Combat.SkillRelated.SKILL_Ointment._SkillRoutine_d__0;
#endif

namespace DungeonSettlersDelvers.Core;

// Bandages for the bleeding wound (build 3c). A light bandage takes 10 % of the
// unit's whole health from the wound, a heavy one 20 % (set C, WoundRules.Bandage); the herbal and salve bandages of first aid (FirstAid) close every
// wound and also take a share of the game's bleeding (WoundRules
// .HerbalBleedingCut.SalveBleedingCut). The unit puts it on itself: the skill is the player's
// ointment (SKILL_Ointment, self target), whose routine Core takes over for these items, so no
// ointment heal runs; only the salve lets the routine run on with its own heal (BandageContent.SalveHeal) against
// the rest of the bleeding and the lost health. Only with an open wound, or for the two of first aid
// with the game's bleeding, checked before the unit takes the bandage in hand
// (ActiveSkill_Base.CheckForUseItem; leitfaden lesson 21). In the game's part of a save they are
// the game's cloth, thick cloth or ointment (CoreSaveItems, StandIn), so a save stays loadable
// without Core. Merchants offer light and heavy ones only where wounds exist (ApocalypseRules
// .Wounds); Apocalypse management cuts their stock like any other good. No lambdas (see
// ReviveScroll).
// The item and skill rows, the merchant offers and the placeholder icons are BandageContent.
internal static class Bandages
{
    internal const string LightKey = BandageText.LightItemKey;
    internal const string HeavyKey = BandageText.HeavyItemKey;
    internal const string HerbalKey = BandageText.HerbalItemKey;
    internal const string SalveKey = BandageText.SalveItemKey;
    internal const string LightSkillKey = "SKILL_DelversCoreLightBandage";
    internal const string HeavySkillKey = "SKILL_DelversCoreHeavyBandage";
    internal const string HerbalSkillKey = "SKILL_DelversCoreHerbalBandage";
    internal const string SalveSkillKey = "SKILL_DelversCoreSalveBandage";
    // The same bandages put on another unit (BandageOnOther, Shift with the item's quick slot): an entity target in reach 1, as the
    // game's aid kit has.
    internal const string LightOtherSkillKey = "SKILL_DelversCoreLightBandageOther";
    internal const string HeavyOtherSkillKey = "SKILL_DelversCoreHeavyBandageOther";
    internal const string HerbalOtherSkillKey = "SKILL_DelversCoreHerbalBandageOther";
    internal const string SalveOtherSkillKey = "SKILL_DelversCoreSalveBandageOther";
    // The game's aid kit, ointment and raw herbs put on another unit (UseOnOther), the same way.
    internal const string AidKitOtherSkillKey = "SKILL_DelversCoreAidKitOther";
    internal const string OintmentOtherSkillKey = "SKILL_DelversCoreOintmentOther";
    internal const string WildHerbOtherSkillKey = "SKILL_DelversCoreWildHerbOther";

    internal static void Initialize()
    {
        CoreSaveItems.Register(LightKey, StandIn(LightKey));
        CoreSaveItems.Register(HeavyKey, StandIn(HeavyKey));
        CoreSaveItems.Register(HerbalKey, StandIn(HerbalKey));
        CoreSaveItems.Register(SalveKey, StandIn(SalveKey));
        // Saves wrote the herbal bandage as thick cloth.
        CoreSaveItems.RegisterLegacy(HerbalKey, "ITEM_ThickTextile");
    }

    internal static bool IsOurs(string itemKey)
        => itemKey == LightKey || itemKey == HeavyKey || itemKey == HerbalKey || itemKey == SalveKey;

    internal static bool IsOurSkill(string skillKey)
        => skillKey == LightSkillKey || skillKey == HeavySkillKey || skillKey == HerbalSkillKey || skillKey == SalveSkillKey || IsOtherSkill(skillKey);

    internal static bool IsOtherSkill(string skillKey)
        => skillKey == LightOtherSkillKey || skillKey == HeavyOtherSkillKey || skillKey == HerbalOtherSkillKey || skillKey == SalveOtherSkillKey
            || skillKey == AidKitOtherSkillKey || skillKey == OintmentOtherSkillKey || skillKey == WildHerbOtherSkillKey;

    // The skill that puts the item on another unit, null for every other item.
    internal static string OtherSkillOf(string itemKey) => itemKey switch
    {
        LightKey => LightOtherSkillKey,
        HeavyKey => HeavyOtherSkillKey,
        HerbalKey => HerbalOtherSkillKey,
        SalveKey => SalveOtherSkillKey,
        BandageRules.AidKitItemKey => AidKitOtherSkillKey,
        BandageRules.OintmentItemKey => OintmentOtherSkillKey,
        BandageRules.WildHerbItemKey => WildHerbOtherSkillKey,
        _ => null
    };

    // The item an other skill puts on (OtherSkillOf the other way round), null for every other skill.
    internal static string ItemOfOtherSkill(string skillKey) => skillKey switch
    {
        LightOtherSkillKey => LightKey,
        HeavyOtherSkillKey => HeavyKey,
        HerbalOtherSkillKey => HerbalKey,
        SalveOtherSkillKey => SalveKey,
        AidKitOtherSkillKey => BandageRules.AidKitItemKey,
        OintmentOtherSkillKey => BandageRules.OintmentItemKey,
        WildHerbOtherSkillKey => BandageRules.WildHerbItemKey,
        _ => null
    };

    // The game's item a bandage is written as in the game's part of a save. The herbal bandage is
    // made from the light one now and is written as cloth like it.
    internal static string StandIn(string itemKey)
        => itemKey == SalveKey ? "ITEM_Ointment" : "ITEM_Textile";

    private static string Describe(string itemKey) => itemKey switch
    {
        SalveKey => "salve",
        HerbalKey => "herbal",
        HeavyKey => "heavy",
        BandageRules.AidKitItemKey => "aidkit",
        BandageRules.OintmentItemKey => "ointment",
        BandageRules.WildHerbItemKey => "wildherb",
        _ => "light"
    };

    // ---- use ----

    // Checked when the player uses the item, before the unit takes it out of the bag: only with a
    // wound, or for the bandages of first aid also with the game's bleeding alone.
    // The game's own item condition (ItemUseCondition, type ItemUse) is skipped there and only checked
    // during the cast, when the bandage is already in hand; a refusal at that point left it in hand and
    // gone from the bag.
    internal static bool AllowUse(SkillExecutionContext context)
    {
        var item = context?.UseItem;
        if (item == null || !IsOurs(item.Key)) return true;
        var user = context.User?.TryCast<UnitEntity>();
        var guid = CoreGame.GuidOf(user);
        if (user != null && BandageRules.AllowsUse(Wounds.IsOpen(guid), BandageRules.KindOf(item.Key).Value, BleedingStacks(user))) return true;
        DelversHost.Info($"CORE_BANDAGE_REFUSED unit={guid:D} item={Describe(item.Key)}");
        return false;
    }

    // The ointment's routine, at its start, for our items: the bandage is used up, Core dresses the
    // wound instead of the ointment's heal. True when the routine was taken over; the salve leaves it
    // running, so its heal (BandageContent.SalveHeal) follows.
    internal static bool TakeOver(OintmentRoutine routine)
    {
        if (routine == null || routine.__1__state != 0) return false;
        var context = routine.context;
        var item = context?.UseItem;
        if (item == null) return false;
        // Put on another unit (BandageOnOther, UseOnOther) through one of the other skills: the game's aid kit, ointment and raw
        // herbs too. Used on oneself, the game's items keep their own routine.
        var onOther = Bandages.IsOtherSkill(context.Publisher?.TryCast<ActiveSkill_Base>()?.Key);
        var known = onOther ? BandageRules.KindOnOther(item.Key) : IsOurs(item.Key) ? BandageRules.KindOf(item.Key) : null;
        if (known == null) return false;
        var kind = known.Value;
        var leavesRunning = BandageRules.LeavesRoutineRunning(kind);
        if (!leavesRunning) routine.__1__state = -1;
        // Put on another unit the wound dressed is the target's.
        var helper = context.User?.TryCast<UnitEntity>();
        var user = context.TargetEntity?.TryCast<UnitEntity>() ?? helper;
        var guid = CoreGame.GuidOf(user);
        var before = Wounds.Buffer(guid);
        var stageBefore = Wounds.StageAt(guid, before);
        var fallen = onOther && IsFallen(user);
        // The wound can close during the two seconds of putting it on; the bandage is then used up for nothing.
        if (before > 0)
        {
            Wounds.SeeMaxLife(user, guid);
            // A lethal wound: herbal and salve take their lethal share (WoundRules.FatalBandage), the others as always, on a
            // fallen unit too.
            var after = BandageRules.BufferAfter(stageBefore, kind, before, Wounds.MaxLife(guid),
                BandageRules.TakesFatalShare(stageBefore, kind) && CoreSettings.FatalBandageCloses);
            Wounds.Set(guid, after, "bandage");
            Wounds.LeaveFatal(user, guid);
            Wounds.SyncNow(user);
        }
        var line = $"CORE_BANDAGE_USED unit={guid:D} item={Describe(item.Key)} before={WoundRules.Stacks(before)} after={WoundRules.Stacks(Wounds.Buffer(guid))} stage={WoundRules.Describe(stageBefore)}->{WoundRules.Describe(Wounds.Stage(guid))}";
        if (BandageRules.CutsBleeding(kind))
            line += " bleeding=" + CutBleeding(user, BandageRules.BleedingCutShare(kind));
        if (helper != null && user != null && helper.Guid != user.Guid) line += $" helper={CoreGame.GuidOf(helper):D}";
        if (fallen) line += " fallen=1";
        DelversHost.Info(line);
        GroupAiBandageUse.OnUsed(guid.ToString("D"));
        return !leavesRunning;
    }

    // Near death and not dead: the game's severe damage or Deadly (the aid kit's own fallen state, AidKitStanding).
    internal static bool IsFallen(UnitEntity unit)
    {
        if (unit == null || unit.IsDead) return false;
        var affecters = CoreGame.Affecters(unit);
        return affecters != null && (affecters.Has(AidKitStanding.SevereDamageKey) || affecters.Has(ReviveSnapshots.DeadlyKey));
    }

    private static int BleedingStacks(UnitEntity unit)
    {
        var affecters = CoreGame.Affecters(unit);
        if (affecters == null || !affecters.Has(Wounds.BleedingKey)) return 0;
        return affecters.GetStack(Wounds.BleedingKey);
    }

    // Takes the share of the game's bleeding stacks; returns "before->after" for the log.
    private static string CutBleeding(UnitEntity unit, double share)
    {
        var stacks = BleedingStacks(unit);
        var cut = WoundRules.BleedingCut(stacks, share);
        if (cut > 0 && unit != null)
            CoreGame.Apply(new AffecterApplyData(unit.Cast<IEntity>(), Wounds.BleedingKey, -cut, 0f, false, null).Cast<IApplyData>());
        return stacks + "->" + (stacks - cut);
    }
}

// Once per use from the bag or the quick slot (only caller: PlayerControlEventHandler.TryUseItem). A failed result
// keeps the item in the bag and shows the game's own refusal.
[HarmonyPatch(typeof(ActiveSkill_Base), nameof(ActiveSkill_Base.CheckForUseItem))]
internal static class BandageUseGatePatch
{
    private static void Postfix(SkillExecutionContext context, ref ExecutionCheckResult __result)
    {
        if ((int)__result < 0) return;
        try
        {
            if (!Bandages.AllowUse(context)) __result = ExecutionCheckResult.Failed_UnvalidTarget;
        }
        catch (Exception ex) { ApocalypseHooks.Report("wound-bandage-gate", ex); }
    }
}

// Runs only when an ointment or a bandage actually fires.
[HarmonyPatch(typeof(OintmentRoutine), "MoveNext")]
internal static class BandageRoutinePatch
{
    private static bool Prefix(OintmentRoutine __instance, ref bool __result)
    {
        try
        {
            if (!Bandages.TakeOver(__instance)) return true;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            ApocalypseHooks.Report("wound-bandage", ex);
            return true;
        }
    }
}
