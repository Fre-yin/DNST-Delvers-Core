using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The bandages' icons: the four bandages (light, heavy, herbal, salve) show their own 16×16 images from the package's Core
// folder, original work, not copies of a game icon (confirmed). Without such a file the bandage keeps
// the placeholder icon of the game's goods it stands in for (Bandages.IconFor). ITEM_Bandage is not shown anywhere; only
// the probe reads its size and atlas at run time. Rules without the game, tested offline.
internal static class BandageIconRules
{
    internal const string BaseIcon = "ITEM_Bandage";

    // The bandages' own 16×16 icons in the package's Core folder; ITEM_Bandage stays unused.
    internal static readonly string[] OwnIconFiles =
        { "Verband_leicht_16x16.png", "Verband_schwer_16x16.png", "Verband_Kraeuter_16x16.png", "Verband_Salbe_16x16.png" };

    // The own icon's file for a bandage, null for every other item.
    internal static string OwnIconFile(string itemKey) => itemKey switch
    {
        BandageText.LightItemKey => OwnIconFiles[0],
        BandageText.HeavyItemKey => OwnIconFiles[1],
        BandageText.HerbalItemKey => OwnIconFiles[2],
        BandageText.SalveItemKey => OwnIconFiles[3],
        _ => null
    };

    internal static string IconsLine(int loaded, IReadOnlyCollection<string> missing)
        => $"CORE_BANDAGE_ICONS loaded={loaded} missing={(missing == null || missing.Count == 0 ? "-" : string.Join(",", missing))} fallback={(missing == null || missing.Count == 0 ? "none" : "placeholder")}";

    // atlas: the sprite is a cut-out of a larger texture.
    internal static string ProbeLine(string key, bool found, int width, int height, int textureWidth, int textureHeight,
        float pixelsPerUnit, bool readable, bool packed, string filter)
        => found
            ? string.Format(CultureInfo.InvariantCulture,
                "CORE_BANDAGE_ICON_PROBE key={0} rect={1}x{2} texture={3}x{4} atlas={5} ppu={6:0.##} readable={7} packed={8} filter={9}",
                key, width, height, textureWidth, textureHeight, textureWidth != width || textureHeight != height ? 1 : 0,
                pixelsPerUnit, readable ? 1 : 0, packed ? 1 : 0, filter)
            : "CORE_BANDAGE_ICON_PROBE key=" + key + " found=0";
}
