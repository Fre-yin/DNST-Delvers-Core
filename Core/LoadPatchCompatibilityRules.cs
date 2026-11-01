namespace DungeonSettlersDelvers.Core;

// Known integrations are exact, reviewed patch identities. Recognition only
// changes audit presentation; every patch remains covered by Core's finalizers. The owner rule below tells Core's ids from foreign ones.
internal static class LoadPatchCompatibilityRules
{
    private const string ExtendedHotbarMelonAssembly = "DungeonSettlers10Slots";
    private const string ExtendedHotbarBepInExAssembly = "DungeonSettlersHotbar.BepInEx";
    // 1.0.2 changed only the author name, license texts and an audit-only Harmony id;
    // both load patches are identical to 1.0.1.
    private static readonly string[] ReviewedExtendedHotbarVersions = { "1.0.1.0", "1.0.2.0" };

    internal static bool IsKnownExtendedHotbar(string target, string stage,
        string assemblyName, string assemblyVersion, string declaringType, string methodName)
    {
        if (!IsReviewedExtendedHotbarAssembly(assemblyName)
            || Array.IndexOf(ReviewedExtendedHotbarVersions, assemblyVersion) < 0)
            return false;

        return target == "UnitQuickSlotContainer.Deserialize"
                && stage == "postfix"
                && declaringType == "DungeonSettlers10Slots.ContainerSlotsLoaded"
                && methodName == "Postfix"
            || target == "QuickSlotData.Deserialize"
                && stage == "prefix"
                && declaringType == "DungeonSettlers10Slots.LoadSlots"
                && methodName == "Prefix";
    }

    // Whether a Harmony id is Core's own: the id itself, or an id below it ("<core id>.<name>", like the probe classes' and the
    // enemy skill test's). A similar id without the dot stays foreign.
    internal static bool IsCoreOwner(string owner, string coreOwner)
        => !string.IsNullOrEmpty(coreOwner) && owner != null
            && (string.Equals(owner, coreOwner, StringComparison.Ordinal)
                || owner.StartsWith(coreOwner + ".", StringComparison.Ordinal));

    private static bool IsReviewedExtendedHotbarAssembly(string assemblyName)
        => string.Equals(assemblyName, ExtendedHotbarMelonAssembly, StringComparison.Ordinal)
            || string.Equals(assemblyName, ExtendedHotbarBepInExAssembly, StringComparison.Ordinal);
}
