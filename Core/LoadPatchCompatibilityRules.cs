namespace DungeonSettlersDelvers.Core;

// Known integrations are exact, reviewed patch identities. Recognition only
// changes audit presentation; every patch remains covered by Core's finalizers.
internal static class LoadPatchCompatibilityRules
{
    private const string ExtendedHotbarMelonAssembly = "DungeonSettlers10Slots";
    private const string ExtendedHotbarBepInExAssembly = "DungeonSettlersHotbar.BepInEx";
    private const string ExtendedHotbarVersion = "1.0.1.0";

    internal static bool IsKnownExtendedHotbar(string target, string stage,
        string assemblyName, string assemblyVersion, string declaringType, string methodName)
    {
        if (!IsReviewedExtendedHotbarAssembly(assemblyName)
            || !string.Equals(assemblyVersion, ExtendedHotbarVersion, StringComparison.Ordinal))
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

    private static bool IsReviewedExtendedHotbarAssembly(string assemblyName)
        => string.Equals(assemblyName, ExtendedHotbarMelonAssembly, StringComparison.Ordinal)
            || string.Equals(assemblyName, ExtendedHotbarBepInExAssembly, StringComparison.Ordinal);
}
