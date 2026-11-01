using HarmonyLib;

namespace DungeonSettlersDelvers.Core;

// The difficulty tooltip on the campaign screen. While the screen shows
// Apocalypse in place of Downfall, the tooltip names Apocalypse in its colour, takes Core's
// description and adds the Apocalypse rules to the game's own sections. The game's value stays
// Downfall, so the native rows are Downfall's and stay true.
internal static class ApocalypseTooltip
{
    private const string ResourceSectionKey = "TEXTKEY_CreateCampaign_DifficultyTooltip_ResourceSection";
    private const string MonsterSectionKey = "TEXTKEY_CreateCampaign_DifficultyTooltip_MonsterDifficulty";
    private const string PlayerSectionKey = "TEXTKEY_CreateCampaign_DifficultyTooltip_PlayerDifficulty";

    internal static void Apply(Tooltip_Difficulty tooltip, bool combat, int difficulty)
    {
        if (tooltip == null || !ApocalypseDifficulty.Shows(combat, difficulty) || !CampaignScreenOpen()) return;
        UniqueCandidateLocalization.EnsureCurrent();
        if (tooltip._titleText != null) tooltip._titleText.text = MenuText.Pick(ApocalypseText.TitleColoured);
        if (tooltip._desText != null)
            tooltip._desText.text = MenuText.Pick(combat ? ApocalypseText.CombatDescription : ApocalypseText.ManagementDescription);
        if (!combat)
        {
            // The game's resource rows (the same four ShowManagementDifficulty builds inline) plus the
            // merchants' smaller stock (ApocalypseMerchants).
            if (tooltip._resourceSection == null) return;
            var resources = Tooltip_Difficulty.GetManagementResourceRows((ManagementDifficulty)difficulty);
            if (resources == null) return;
            AddRow(resources, ApocalypseText.TooltipMerchantGoods, ApocalypseText.TooltipFewer);
            EnsureRows(tooltip._resourceSection, resources.Count, "resource");
            tooltip._resourceSection.SetUI(ResourceSectionKey, resources);
            return;
        }
        // The Apocalypse rules join the game's own rows, in the same sections and row parts: what
        // enemies gain under the monster modifiers, the pause key under the allies' modifiers
        // (a section of its own came out smaller and cramped). The rows come
        // from the game's helpers exactly as ShowCombatDifficulty builds them inline: monster values
        // inverted, ally values not.
        var level = (CombatDifficulty)difficulty;
        if (tooltip._monsterSection != null)
        {
            var monster = CombatRows(Tooltip_Difficulty.GetMonsterDifficultyAffecterKey(level), true);
            AddRow(monster, ApocalypseText.TooltipEnemiesAttack, ApocalypseText.TooltipAtOnce);
            AddRow(monster, ApocalypseText.TooltipAmbush, ApocalypseText.TooltipRemoved);
            AddRow(monster, WoundText.TooltipWounds, WoundText.TooltipBleedOn);
            EnsureRows(tooltip._monsterSection, monster.Count, "monster");
            tooltip._monsterSection.SetUI(MonsterSectionKey, monster);
        }
        if (tooltip._playerSection != null && CoreSettings.ApocalypseNoPause)
        {
            var player = CombatRows(Tooltip_Difficulty.GetPlayerDifficultyAffecterKey(level), false);
            AddRow(player, ApocalypseText.TooltipPauseKey, ApocalypseText.TooltipSlowMotion);
            EnsureRows(tooltip._playerSection, player.Count, "player");
            tooltip._playerSection.SetUI(PlayerSectionKey, player);
        }
    }

    // A section owns a fixed set of row objects, and SetUI never adds one: more data rows than
    // objects end up joined with line breaks in the last object, which then shrinks its text and
    // overlaps its neighbour (DifficultyTooltipSection.SetUI, guide lesson 20). Clones
    // of the section's own last row fill the gap, right behind it under the same parent; the game's
    // layout rebuild after Show sizes them (Tooltip_Base.SetTooltipAlignment). The clones stay, and
    // SetUI switches off every row it does not need.
    private static void EnsureRows(DifficultyTooltipSection section, int needed, string name)
    {
        var rows = section?._rows;
        if (rows == null || rows.Count == 0 || rows.Count >= needed) return;
        var before = rows.Count;
        while (rows.Count < needed)
        {
            var last = rows[rows.Count - 1];
            var clone = UnityEngine.Object.Instantiate(last.gameObject, last.transform.parent);
            clone.name = last.gameObject.name + "_DelversCore" + rows.Count;
            clone.transform.SetSiblingIndex(last.transform.GetSiblingIndex() + 1);
            var row = clone.GetComponent<TooltipValueRow>();
            if (row == null)
            {
                UnityEngine.Object.Destroy(clone);
                break;
            }
            rows.Add(row);
        }
        DelversHost.Info("CORE_APOCALYPSE_TOOLTIP_ROWS section=" + name + " before=" + before + " after=" + rows.Count);
    }

    private static Il2CppSystem.Collections.Generic.List<DifficultyTooltipRow> CombatRows(string affecterKey, bool invert)
        => (string.IsNullOrEmpty(affecterKey) ? null : Tooltip_Difficulty.GetCombatAffecterRows(affecterKey, invert))
            ?? new Il2CppSystem.Collections.Generic.List<DifficultyTooltipRow>();

    // The tooltip class also serves other screens; a selection left from an earlier visit of the
    // campaign screen must not rename a Downfall campaign there.
    private static bool CampaignScreenOpen()
    {
        var panel = UnityEngine.Object.FindObjectOfType<SubUI_CampaignSetting>();
        return panel != null && panel.isActiveAndEnabled;
    }

    private static void AddRow(Il2CppSystem.Collections.Generic.List<DifficultyTooltipRow> rows,
        UniqueCandidateTranslations label, UniqueCandidateTranslations value)
        => DelversNativeList.AddValue(rows, new DifficultyTooltipRow
        {
            Label = MenuText.Pick(label),
            Value = MenuText.Pick(value),
            ValueColor = TextColor.Negative
        });
}

[HarmonyPatch(typeof(Tooltip_Difficulty), nameof(Tooltip_Difficulty.ShowCombatDifficulty))]
internal static class ApocalypseCombatTooltipPatch
{
    // After the body: Show has set the title before and the body ends with the description.
    private static void Postfix(Tooltip_Difficulty __instance, CombatDifficulty difficulty)
        => ApocalypseHooks.Run("combat-tooltip", () => ApocalypseTooltip.Apply(__instance, true, (int)difficulty));
}

[HarmonyPatch(typeof(Tooltip_Difficulty), nameof(Tooltip_Difficulty.ShowManagementDifficulty))]
internal static class ApocalypseManagementTooltipPatch
{
    private static void Postfix(Tooltip_Difficulty __instance, ManagementDifficulty difficulty)
        => ApocalypseHooks.Run("management-tooltip", () => ApocalypseTooltip.Apply(__instance, false, (int)difficulty));
}
