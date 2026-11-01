using HarmonyLib;
#if BEPINEX
using AffecterApplyData = global::Refactor.AffecterApplyData;
using CreateVFXRequest = global::Refactor.FX.CreateVFXRequest;
using ExecutionContext = global::Refactor.Combat.ExecutionContext;
using FXRequestData = global::Refactor.FX.FXRequestData;
using IFXRequest = global::Refactor.FX.IFXRequest;
#else
using AffecterApplyData = Il2CppRefactor.AffecterApplyData;
using CreateVFXRequest = Il2CppRefactor.FX.CreateVFXRequest;
using ExecutionContext = Il2CppRefactor.Combat.ExecutionContext;
using FXRequestData = Il2CppRefactor.FX.FXRequestData;
using IFXRequest = Il2CppRefactor.FX.IFXRequest;
#endif

namespace DungeonSettlersDelvers.Core;

// The bandits' skill effects in orange, one way for every skill. Many effects hang under VFXPoolRoot without an owner (Arrow
// Rain 84/85, Cling Vines Area 131: SetVFXOwner never
// comes), so neither owner nor hierarchy says whose they are. Every effect of a skill or an affecter is made from an
// FXRequestData of its FX class with the execution's context (FXRequestData.ToRequest(inMapData, ExecutionContext)) or the
// affecter's apply data (ToRequest(inMapData, IApplyData)); a CreateVFX request carries the effect's Guid. So the caster is
// read there, once for all skills: a Core unit, a summon of one (BanditTint.IsBanditSummon) or a bandit building that casts
// itself, as the blooms do (BanditTint.IsBanditBuildingSeen), marks the Guid, and BanditTint
// recolours the effect when the game creates it (VFXView.CreateVFX, BanditTint.OnVfx), the way BanditTintSource does for the
// seeds. A player's effects are never marked, nor an affecter's look on a unit of the players' side (a bandit's bleeding on a
// party member keeps the game's colours; run 20261031-231855 had it orange). No list of the map is read here.
internal static class BanditTintSkill
{
    private const int Cap = 512;
    private static readonly Dictionary<string, string> effects = new(StringComparer.Ordinal);
    // One line per caster and effect id (run 20261031-235430: a line per effect used the cap up before the later skills).
    private static readonly HashSet<string> logged = new(StringComparer.Ordinal);
    // How many effects each caster had marked, for the toolkit (SkillTintCountForTest).
    private static readonly Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);

    internal static void Reset()
    {
        effects.Clear();
        logged.Clear();
        counts.Clear();
    }

    internal static int CountOf(string caster) => caster != null && counts.TryGetValue(caster, out var n) ? n : 0;

    // A request the game made from an FX step; marked when it creates an effect and the caster is a bandit's.
    internal static void OnRequest(IFXRequest request, IEntity caster)
    {
        if (request == null || caster == null || !IsBandit(caster)) return;
        var create = request.TryCast<CreateVFXRequest>();
        if (create == null) return;
        if (effects.Count >= Cap) effects.Clear();
        var fx = create.Guid.ToString();
        var by = caster.Guid.ToString();
        effects[fx] = by;
        if (counts.ContainsKey(by) || counts.Count < 256) counts[by] = CountOf(by) + 1;
        if (logged.Count < BanditTintRules.SkillLogCap && logged.Add(caster.Guid.ToString() + "|" + (int)create.VFX))
            DelversHost.Info(BanditTintRules.SkillLine((int)create.VFX, fx, caster.Guid.ToString(), SafeKey(caster)));
    }

    // The bandit whose skill made this effect, taken once (BanditTint.OnVfx), or null.
    internal static string Take(string fx)
    {
        if (fx == null || effects.Count == 0 || !effects.TryGetValue(fx, out var caster)) return null;
        effects.Remove(fx);
        return caster;
    }

    private static bool IsBandit(IEntity caster)
    {
        var unit = caster.TryCast<UnitEntity>();
        if (unit != null && CoreUnitRules.IsCoreUnit(unit.Key)) return true;
        var id = caster.Guid.ToString();
        return BanditTint.IsBanditSummon(id) || BanditTint.IsBanditBuildingSeen(id);
    }

    private static string SafeKey(IEntity entity)
    {
        try { return entity.TryCast<UnitEntity>()?.Key ?? entity.Key; }
        catch (Exception) { return "?"; }
    }
}

// The requests of a skill's FX steps, with the execution's context. Arguments bound by name.
[HarmonyPatch(typeof(FXRequestData), nameof(FXRequestData.ToRequest), new[] { typeof(IInMapDataSupplier), typeof(ExecutionContext) })]
internal static class BanditTintSkillContextPatch
{
    private static void Postfix(ExecutionContext context, IFXRequest __result)
    {
        try { BanditTintSkill.OnRequest(__result, context?.User); }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_TINT_FAILED skill: " + ex.Message); }
    }
}

// The requests of an affecter's FX, with its apply data (an AffecterApplyData names who put it on).
[HarmonyPatch(typeof(FXRequestData), nameof(FXRequestData.ToRequest), new[] { typeof(IInMapDataSupplier), typeof(IApplyData) })]
internal static class BanditTintSkillApplyPatch
{
    private static void Postfix(IApplyData applyData, IFXRequest __result)
    {
        try
        {
            var affecter = applyData?.TryCast<AffecterApplyData>();
            if (affecter?.Target?.TryCast<UnitEntity>() is { Faction: FactionType.Player }) return;
            BanditTintSkill.OnRequest(__result, affecter?.User);
        }
        catch (Exception ex) { DelversHost.Warning("CORE_SEED_TINT_FAILED affecter: " + ex.Message); }
    }
}
