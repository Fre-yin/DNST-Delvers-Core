using HarmonyLib;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// The boss parts' violet look without own pictures: the game's blood wood picture, recoloured once per part and place by
// BossItemRules.Violet through the bandit tint's copy (BanditTint.Recolored), and kept. The icon comes through the item icon
// hook the bandages use (BandageIconPatch), the picture on the unit through UnitVisualDataManager.GetWeaponSprite, which
// RefreshAllEquipment asks before PaintAll draws the unit (CoreUnits). The item tooltip's rarity decoration (Tooltip_Item.
// _rarityDecos, gold for Legendary) turns violet for a boss part and back for any other item: its
// pictures through BossItemRules.Gilded, its colour tint likewise; CORE_BOSS_ITEM_DECO tells once what the tooltip had. Its
// subtitle says Special in place of Legendary (BossItemRules.SpecialSubtitle).
internal static class BossItemLook
{
    private static readonly Dictionary<string, Sprite> made = new(StringComparer.Ordinal);
    private static readonly Dictionary<IntPtr, (Sprite Sprite, Color Colour)> decoOriginals = new();
    private static bool failWarned;
    private static bool decoLogged;

    // The violet copy for an own part (key) of the game's picture of its blood wood part, or the picture as it is when it
    // cannot be recoloured.
    internal static Sprite Violet(string where, string key, Sprite source)
        => Copy(where, key, source, BossItemRules.Violet);

    private static Sprite Copy(string where, string key, Sprite source, BanditTint.PixelTurn turn)
    {
        if (source == null) return null;
        var id = where + "|" + key;
        if (made.TryGetValue(id, out var kept) && kept != null) return kept;
        try
        {
            var sprite = BanditTint.Recolored(source, turn, "_DelversBoss");
            if (sprite == null) return source;
            made[id] = sprite;
            DelversHost.Info(BossItemRules.LookLine(where, key, (int)sprite.rect.width, (int)sprite.rect.height));
            return sprite;
        }
        catch (Exception ex)
        {
            if (!failWarned)
            {
                failWarned = true;
                DelversHost.Warning("CORE_BOSS_ITEM_LOOK_FAILED key=" + key + ": " + ex.Message);
            }
            return source;
        }
    }

    // After the item tooltip filled itself for key: its rarity decoration violet for a boss part, as the game made it otherwise.
    internal static void TooltipShown(Tooltip_Item tooltip, string key)
    {
        var decos = tooltip?._rarityDecos;
        if (decos == null) return;
        var boss = BossItemRules.IsBossItem(key);
        var seen = new List<string>();
        for (var i = 0; i < decos.Count; i++)
        {
            var image = decos[i];
            if (image == null) continue;
            if (!decoOriginals.TryGetValue(image.Pointer, out var original))
            {
                if (!boss) continue;
                original = (image.sprite, image.color);
                decoOriginals[image.Pointer] = original;
            }
            if (boss)
            {
                if (original.Sprite != null) image.sprite = Copy("deco", original.Sprite.name, original.Sprite, BossItemRules.Gilded);
                var (r, g, b, a) = BossItemRules.Gilded(ToByte(original.Colour.r), ToByte(original.Colour.g), ToByte(original.Colour.b), ToByte(original.Colour.a));
                image.color = new Color32(r, g, b, a);
                seen.Add(BossItemRules.DecoPart(image.name, original.Sprite != null ? original.Sprite.name : "-", image.gameObject.activeInHierarchy,
                    ToByte(original.Colour.r), ToByte(original.Colour.g), ToByte(original.Colour.b)));
            }
            else
            {
                image.sprite = original.Sprite;
                image.color = original.Colour;
            }
        }
        if (boss && tooltip._subtitleText != null)
            tooltip._subtitleText.text = BossItemRules.SpecialSubtitle(tooltip._subtitleText.text,
                DataSheetManager.Instance?.GetText(BossItemRules.LegendaryTextKey), MenuText.Pick(BossItemText.Special));
        if (boss && !decoLogged)
        {
            decoLogged = true;
            DelversHost.Info(BossItemRules.DecoLine(key, decos.Count, seen));
        }
    }

    private static byte ToByte(float channel) => (byte)Mathf.Clamp(Mathf.RoundToInt(channel * 255f), 0, 255);

    // A boss effect's icon: its own picture when drawn, the game's icon with the red cast for the warden, else null (the
    // game's icon as it is, or the placeholder).
    internal static Sprite EffectIcon(string key, Func<string, Sprite> gameIcon)
    {
        var own = CoreAssets.BossEffectIcon(BossItemRules.EffectIconFile(key));
        if (own) return own;
        var icon = BossItemRules.EffectIcon(key);
        if (icon == null || !BossItemRules.RedIcon(key)) return null;
        return Copy("effect", key, gameIcon(icon), BossItemRules.RedCast);
    }

    // The item icon of an own part: the game's icon of its blood wood part, violet.
    internal static Sprite Icon(ResourceLoader loader, string key)
    {
        var native = BossItemRules.StandIn(key);
        return native == null || loader == null ? null : Violet("icon", key, loader.LoadItemIcon(native));
    }
}

// The energy cost row of a boss part in the other colour (BossItemRules.InvertsColour): Show tells the item, the typed
// AddStatRow tells the status, the text AddStatRow gets the colour the game picked by sign.
[HarmonyPatch(typeof(Tooltip_Item), nameof(Tooltip_Item.Show))]
internal static class BossItemTooltipKeyPatch
{
    internal static string Shown;

    private static void Prefix(string key) => Shown = key;
}

[HarmonyPatch(typeof(Tooltip_Item), nameof(Tooltip_Item.AddStatRow), new[] { typeof(StatType), typeof(float) })]
internal static class BossItemStatRowPatch
{
    internal static bool Invert;

    private static void Prefix(StatType statType)
        => Invert = BossItemRules.InvertsColour(statType.ToString(), BossItemRules.IsBossItem(BossItemTooltipKeyPatch.Shown));

    private static Exception Finalizer(Exception __exception)
    {
        Invert = false;
        return __exception;
    }
}

[HarmonyPatch(typeof(Tooltip_Item), nameof(Tooltip_Item.AddStatRow), new[] { typeof(string), typeof(string), typeof(TextColor) })]
internal static class BossItemStatColourPatch
{
    private static void Prefix(ref TextColor valueColor)
    {
        if (!BossItemStatRowPatch.Invert) return;
        if (valueColor == TextColor.Positive) valueColor = TextColor.Negative;
        else if (valueColor == TextColor.Negative) valueColor = TextColor.Positive;
    }
}

[HarmonyPatch(typeof(Tooltip_Item), nameof(Tooltip_Item.Show))]
internal static class BossItemTooltipPatch
{
    private static void Postfix(Tooltip_Item __instance, string key)
    {
        try { BossItemLook.TooltipShown(__instance, key); }
        catch (Exception ex) { ApocalypseHooks.Report("boss-item-tooltip", ex); }
    }
}

// The picture on the unit: the game is asked for the blood wood part's picture, Core hands back its violet copy.
[HarmonyPatch(typeof(UnitVisualDataManager), nameof(UnitVisualDataManager.GetWeaponSprite))]
internal static class BossItemWeaponSpritePatch
{
    private static void Prefix(ref string item, out string __state)
    {
        __state = null;
        var native = BossItemRules.StandIn(item);
        if (native == null) return;
        __state = item;
        item = native;
    }

    private static void Postfix(string __state, ref Sprite __result)
    {
        if (__state == null) return;
        try { __result = BossItemLook.Violet("weapon", __state, __result); }
        catch (Exception ex) { ApocalypseHooks.Report("boss-item-weapon", ex); }
    }
}
