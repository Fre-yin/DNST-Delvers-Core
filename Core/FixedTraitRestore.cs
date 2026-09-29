using HarmonyLib;
using Il2CppInterop.Runtime;

namespace DungeonSettlersDelvers.Core;

// Restores registered fixed traits before the native component deserializer
// consumes a saved unit. See FixedTraitRules for when a save loses them.
internal static class FixedTraitRestore
{
    internal static void BeforeComponentsDeserialize(ComponentSaveList saved, IEntity entity)
    {
        string profileKey = null;
        try
        {
            var restored = RestoreSavedUnit(saved, DelversCoreRuntime.FixedTraits, out profileKey, out var packId);
            if (restored != null)
                DelversHost.Warning("CORE_FIXED_TRAITS_RESTORED pack=" + packId
                    + " profile=" + profileKey
                    + " entity=" + DescribeEntity(entity)
                    + " restored=" + string.Join(",", restored)
                    + " cause=saveWrittenWithoutPack");
        }
        catch (Exception ex)
        {
            // RestoreSavedUnit publishes only after a complete rebuild, so the
            // saved data is untouched here. Rethrowing would make Harmony skip
            // the native ComponentList.Deserialize and leave the unit without
            // any components; block saving instead and let the native load run.
            if (DelversCoreRuntime.FixedTraits.ShouldReportFailure(profileKey))
                DelversHost.Error("CORE_FIXED_TRAITS_RESTORE_FAILED profile=" + profileKey
                    + " entity=" + DescribeEntity(entity) + " saveWillBeBlocked=true error=" + ex);
            LoadIntegrityGuard.ReportFailure("fixed-traits:" + (profileKey ?? "unknown"), ex);
        }
    }

    // Returns the restored keys, or null when the unit needs no change.
    internal static List<string> RestoreSavedUnit(ComponentSaveList saved, FixedTraitRegistry registry,
        out string profileKey, out string packId)
    {
        profileKey = null;
        packId = null;
        if (saved == null) return null;
        UnitProfileComponentSaveData profile = null;
        AffecterComponentSaveData affecters = null;
        for (var i = 0; i < saved.Count; i++)
        {
            var component = saved[i];
            if (component?.Data == null) continue;
            if (component.Type == ComponentType.UnitProfile)
                profile = component.Data.TryCast<UnitProfileComponentSaveData>();
            else if (component.Type == ComponentType.Affecter)
                affecters = component.Data.TryCast<AffecterComponentSaveData>();
        }
        profileKey = profile?.ProfileKey;
        if (affecters?.AffecterHolders == null
            || !registry.TryGet(profileKey, out packId, out var fixedTraits)) return null;

        var holders = affecters.AffecterHolders;
        var keys = new List<string>(holders.Count);
        for (var i = 0; i < holders.Count; i++) keys.Add(holders[i]?.Key);
        var restoredOrder = FixedTraitRules.WithMissing(keys, fixedTraits);
        if (restoredOrder == null) return null;

        // The rule only inserts absent keys, so existing holders are matched in
        // order and reused unchanged. Build first and publish once.
        var migrated = new HolderList();
        var restored = new List<string>();
        var next = 0;
        foreach (var key in restoredOrder)
        {
            if (next < holders.Count && holders[next]?.Key == key)
            {
                DelversNativeList.AddValue(migrated, holders[next++]);
                continue;
            }
            DelversNativeList.AddValue(migrated, new AffecterHolder { Key = key, Stack = 1 });
            restored.Add(key);
        }
        if (next != holders.Count)
            throw new InvalidOperationException("Saved affecters could not be matched unambiguously.");
        affecters.AffecterHolders = migrated;
        return restored;
    }

    internal static void RunAudit()
    {
        const string ProbeProfile = "UNITVISUAL_DelversFixedTraitAuditProbe";
        var registry = new FixedTraitRegistry();
        using (registry.Register("core-audit", ProbeProfile,
            new[] { "AFFECTER_Elf", "AFFECTER_AuditBackground", "AFFECTER_AuditIndividual" }))
        {
            var wiped = CreateProbe(ProbeProfile, "AFFECTER_BlessOfWorldTree", "AFFECTER_FireplaceWarmth");
            var restored = RestoreSavedUnit(wiped, registry, out _, out var packId);
            Require(packId == "core-audit" && restored?.Count == 3 && KeysOf(wiped).SequenceEqual(new[] {
                    "AFFECTER_Elf", "AFFECTER_AuditBackground", "AFFECTER_AuditIndividual",
                    "AFFECTER_BlessOfWorldTree", "AFFECTER_FireplaceWarmth" }),
                "wiped unit regains all fixed traits in registered order");
            Require(RestoreSavedUnit(wiped, registry, out _, out _) == null, "restore is idempotent");

            var partial = CreateProbe(ProbeProfile, "AFFECTER_Elf", "AFFECTER_AuditIndividual");
            Require(RestoreSavedUnit(partial, registry, out _, out _)?.Count == 1
                && KeysOf(partial).SequenceEqual(new[] {
                    "AFFECTER_Elf", "AFFECTER_AuditBackground", "AFFECTER_AuditIndividual" }),
                "a missing middle trait is inserted after its predecessor");

            var intact = CreateProbe(ProbeProfile,
                "AFFECTER_Elf", "AFFECTER_AuditBackground", "AFFECTER_AuditIndividual");
            var intactHolders = FindAffecters(intact).AffecterHolders;
            Require(RestoreSavedUnit(intact, registry, out _, out _) == null
                && FindAffecters(intact).AffecterHolders.Pointer == intactHolders.Pointer,
                "intact save untouched");

            var unregistered = CreateProbe("UNITVISUAL_ElfSlimUnisex_10", "AFFECTER_BlessOfWorldTree");
            Require(RestoreSavedUnit(unregistered, registry, out _, out _) == null
                && KeysOf(unregistered).Count == 1, "unregistered profile untouched");
        }
        Require(registry.Count == 0, "audit registration released");

        var target = AccessTools.Method(typeof(ComponentList), nameof(ComponentList.Deserialize));
        var patches = target == null ? null : Harmony.GetPatchInfo(target);
        Require(patches?.Prefixes.Any(patch => patch.PatchMethod?.DeclaringType == typeof(FixedTraitRestorePatch)
                && patch.priority == Priority.First) == true,
            "restore runs as first prefix of ComponentList.Deserialize");

        DelversHost.Info("CORE_FIXED_TRAITS_AUDIT_PASS registered=" + DelversCoreRuntime.FixedTraits.Count
            + " wipedRestore=PASS order=PASS idempotence=PASS partialRestore=PASS intactControl=PASS"
            + " unregisteredControl=PASS patchOrder=PASS");
    }

    private static ComponentSaveList CreateProbe(string profileKey, params string[] keys)
    {
        var holders = new HolderList();
        foreach (var key in keys) DelversNativeList.AddValue(holders, new AffecterHolder { Key = key, Stack = 1 });
        var result = new ComponentSaveList();
        result.Add(new ComponentSaveData { Type = ComponentType.Affecter,
            Data = new AffecterComponentSaveData { AffecterHolders = holders, InactiveMoodCauseHolders = new() } });
        result.Add(new ComponentSaveData { Type = ComponentType.UnitProfile,
            Data = new UnitProfileComponentSaveData { ProfileKey = profileKey } });
        return result;
    }

    private static AffecterComponentSaveData FindAffecters(ComponentSaveList data)
    {
        for (var i = 0; i < data.Count; i++)
            if (data[i]?.Type == ComponentType.Affecter)
                return data[i].Data?.TryCast<AffecterComponentSaveData>();
        return null;
    }

    private static List<string> KeysOf(ComponentSaveList data)
    {
        var holders = FindAffecters(data)?.AffecterHolders;
        var keys = new List<string>();
        if (holders == null) return keys;
        for (var i = 0; i < holders.Count; i++) keys.Add(holders[i]?.Key);
        return keys;
    }

    private static string DescribeEntity(IEntity entity)
    {
        if (entity == null) return "unknown";
        try { return entity.Guid.ToString(); }
        catch { return "unavailable"; }
    }

    private static void Require(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("Core fixed-trait audit failed: " + name);
    }
}

// First prefix: pack migrations running later may rely on the fixed traits.
[HarmonyPatch(typeof(ComponentList), nameof(ComponentList.Deserialize))]
internal static class FixedTraitRestorePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(ComponentSaveList __0, IEntity __1)
    {
        FixedTraitRestore.BeforeComponentsDeserialize(__0, __1);
        DelversCoreRuntime.NotifyBeforeComponentsDeserialize(__0, __1);
    }

    private static Exception Finalizer(Exception __exception)
        => LoadIntegrityGuard.ObserveFinalizer(__exception, "ComponentList.Deserialize");
}

[HarmonyPatch(typeof(UnitProfileComponent), nameof(UnitProfileComponent.Deserialize),
    new[] { typeof(ComponentSaveBaseData), typeof(IEntity) })]
internal static class DelversUnitProfileLoadPatch
{
    private static void Prefix(ComponentSaveBaseData __0, IEntity __1)
        => DelversCoreRuntime.NotifyBeforeUnitProfileDeserialize(__0, __1);

    private static void Postfix(UnitProfileComponent __instance)
        => DelversCoreRuntime.NotifyAfterUnitProfileDeserialize(__instance);

    private static Exception Finalizer(Exception __exception)
        => LoadIntegrityGuard.ObserveFinalizer(__exception, "UnitProfileComponent.Deserialize");
}
