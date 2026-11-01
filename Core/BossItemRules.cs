using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The boss bandits carry gear of their own that may drop. Each part is a blood wood part of the game
// (same base values) under an own key, in Apocalypse violet. No own pictures: Core turns the game's blood wood pictures
// violet at runtime (BossItemLook). In the game's part of a save each part is written as its blood wood part (CoreSaveItems,
// the stand-in way of the bandages), so a save loads without Core and then holds the plain blood wood parts. The effects
// follow part by part. Rules without the game.
internal static class BossItemRules
{
    internal const string GreatSwordKey = CoreSaveItems.Prefix + "BossGreatSword";
    internal const string ShieldKey = CoreSaveItems.Prefix + "BossShield";
    internal const string BowKey = CoreSaveItems.Prefix + "BossBow";
    internal const string StaffKey = CoreSaveItems.Prefix + "BossStaff";

    // Own part, the game's blood wood part it copies and is saved as.
    internal static readonly (string Key, string Native)[] Parts =
    {
        (GreatSwordKey, "ITEM_BloodWoodGreatSword"),
        (ShieldKey, "ITEM_BloodWoodShield"),
        (BowKey, "ITEM_BloodWoodBow"),
        (StaffKey, "ITEM_NatureBloodWoodStaff")
    };

    // The game knows no rarity "special" (Refactor.Rarity: Common 0 … Legendary 3). Legendary,
    // the parts are Apocalypse items; their names show it in Apocalypse violet (BossItemText).
    internal const int RarityValue = 3;

    internal static bool IsBossItem(string key) => StandIn(key) != null;

    // ---- the parts' own values ----

    // Where a part differs from its blood wood part. Statuses are entries "Stat_value" of
    // Equipment_AdditionalStatuses, as the game's tooltip reads them (Tooltip_Base.ParseStats); the
    // logged the shield's as AttackSpeedPerModifier_-30, PhysicalResistance_8, GuardChance_10.
    // - The warden: block chance 5 more (the effect's +5 % moved into the row), the attack speed malus -20 instead of -30,
    //   physical resistance 15 (a flat value, damage times 100 / (100 + resistance)).
    // - The greatsword: hit chance -5 instead of -10, physical attack per modifier 15 instead of 10.
    // - The staff: skill energy cost +25 % (SkillEnergyCostPerModifier, read by ActiveSkill_Base.GetEnergyCost).
    // - The bow: sight +3 cells (VisionRange).
    // Add: the value is added to the blood wood part's, else it replaces it. Missing: a missing entry is added with the value.
    internal readonly struct StatChange
    {
        internal readonly string Key;
        internal readonly string Stat;
        internal readonly float Value;
        internal readonly bool Add;
        internal readonly bool Missing;

        internal StatChange(string key, string stat, float value, bool add, bool missing)
        {
            Key = key;
            Stat = stat;
            Value = value;
            Add = add;
            Missing = missing;
        }
    }

    internal static readonly StatChange[] StatChanges =
    {
        new(ShieldKey, "GuardChance", 5f, true, true),
        new(ShieldKey, "AttackSpeedPerModifier", -20f, false, false),
        new(ShieldKey, "PhysicalResistance", 15f, false, true),
        new(GreatSwordKey, "HitChance", -5f, false, false),
        new(GreatSwordKey, "PhysicalAttackPowerPerModifier", 15f, false, false),
        new(StaffKey, "SkillEnergyCostPerModifier", 25f, true, true),
        new(BowKey, "VisionRange", 3f, true, true)
    };

    // The base attack of a part (Equipment_PhysicalAttackPower, Equipment_MagicAttackPower), or null to keep the blood wood
    // part's: the greatsword 62 (56), the bow 38 (34), the staff's magic 46 (42).
    internal static readonly (string Key, float? Physical, float? Magic)[] BaseAttacks =
    {
        (GreatSwordKey, 62f, null),
        (BowKey, 38f, null),
        (StaffKey, null, 46f)
    };

    internal static (float Physical, float Magic) BaseAttack(string key, float physical, float magic)
    {
        foreach (var (part, ownPhysical, ownMagic) in BaseAttacks)
            if (part == key) return (ownPhysical ?? physical, ownMagic ?? magic);
        return (physical, magic);
    }

    // The game's empty status column is the one entry "NULL"; StatUtility.Init_EquipStats skips the whole column when its
    // first entry is "NULL" (bow and staff got NULL,VisionRange_3 and lost both). So "NULL" entries
    // go before any change is applied, and a column left empty is "NULL" again. The entries are separate strings; "|" is
    // only the table's separator (ItemTable.tsv), the log joins them with ",".
    internal const string EmptyColumn = "NULL";

    // The statuses of an own part: StatChanges applied to the blood wood part's.
    internal static string[] Statuses(string key, string[] native)
    {
        var entries = new List<string>();
        foreach (var entry in native ?? Array.Empty<string>())
            if (!string.IsNullOrEmpty(entry) && entry != EmptyColumn) entries.Add(entry);
        foreach (var change in StatChanges)
        {
            if (change.Key != key) continue;
            var found = false;
            for (var i = 0; i < entries.Count; i++)
            {
                if (!TryStat(entries[i], out var stat, out var value) || stat != change.Stat) continue;
                entries[i] = Stat(stat, change.Add ? value + change.Value : change.Value);
                found = true;
            }
            if (!found && change.Missing) entries.Add(Stat(change.Stat, change.Value));
        }
        return entries.Count == 0 ? new[] { EmptyColumn } : entries.ToArray();
    }

    // "Stat_value": the name before the last underscore, the value after it.
    internal static bool TryStat(string entry, out string stat, out float value)
    {
        stat = null;
        value = 0f;
        var at = entry?.LastIndexOf('_') ?? -1;
        if (at <= 0) return false;
        stat = entry.Substring(0, at);
        return float.TryParse(entry.Substring(at + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    internal static string Stat(string stat, float value) => stat + "_" + value.ToString("0.###", CultureInfo.InvariantCulture);

    // The tooltip's subtitle names the rarity, then the slot ("TEXTKEY_RarityType_{0}", Tooltip_Item). For a boss part the
    // game's Legendary word becomes the Special word (BossItemText.Special); the rarity itself stays Legendary.
    internal const string LegendaryTextKey = "TEXTKEY_RarityType_Legendary";

    internal static string SpecialSubtitle(string subtitle, string legendary, string special)
    {
        if (string.IsNullOrEmpty(subtitle) || string.IsNullOrEmpty(legendary) || string.IsNullOrEmpty(special)) return subtitle;
        var at = subtitle.IndexOf(legendary, StringComparison.Ordinal);
        return at < 0 ? subtitle : subtitle.Substring(0, at) + special + subtitle.Substring(at + legendary.Length);
    }

    // The game repairs an item only with a recipe of its name: RepairTask.GenerateRepairInfos looks up the key with "ITEM"
    // replaced by "RECIPE" (DataSheetManager.GetRecipeData) and skips the item without one (the
    // task was made, nothing repaired). Each own part gets that recipe as a copy of its blood wood part's, made at no bench,
    // so it serves the repair only.
    internal static string RepairRecipeKey(string itemKey) => itemKey?.Replace("ITEM", "RECIPE");

    // CORE_BOSS_ITEM_EFFECTS_RESTORED: the equipment effects put back after a load.
    internal static string RestoredLine(int units, int effects)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BOSS_ITEM_EFFECTS_RESTORED units={0} effects={1}", units, effects);

    // CORE_BOSS_ITEM_RECIPE_OUTPUT: a part mapped to its repair recipe in the game's output index.
    internal static string OutputLine(string key, string recipe, bool mapped)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BOSS_ITEM_RECIPE_OUTPUT key={0} recipe={1} result={2}", key, recipe, mapped ? "mapped" : "kept");

    // CORE_BOSS_ITEM_RECIPE: a repair recipe added for an own part, or why not.
    internal static string RecipeLine(string key, string template, bool found, float workload)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BOSS_ITEM_RECIPE key={0} from={1} result={2} workload={3:0.#}",
            key, template, found ? "added" : "template-missing", workload);

    // Each part's effect is a native equipment effect, as on Devrick's pendant: the part names it in
    // Equipment_OnEquipAffecters and the tooltip shows its own block with icon, name and text (Tooltip_Item.SetEquipAffecter,
    // TooltipSAffecterGrant: TEXTKEY_<effect>_NAME and _DESC). The effect is a row of the effect table made from the
    // pendant's; no values of its own, a marker for the effects' logic. Its key has no further underscore, so [KEY] in a text
    // links to it and CoreSaveEffects keeps it out of the game's save.
    internal const string EffectTemplate = "AFFECTER_DevrickPendant";

    internal static string EffectKey(string itemKey) => itemKey?.Replace("ITEM_", "AFFECTER_");

    // The author's icons: all four effects their own pictures (EffectIconFile; also the shield the Blood Warden gives).
    // The icon hooks ask for the own picture first (ReviveEffectsIcon.OwnSprite), so the game effect named here, and for the
    // warden its slight red cast (WardenRed), only shows when that file is missing.
    // The game effect whose icon an effect shows, or null for its own picture or the game's placeholder.
    internal static string EffectIcon(string effectKey)
    {
        if (effectKey == EffectKey(GreatSwordKey)) return "AFFECTER_Bleeding";
        if (effectKey == EffectKey(ShieldKey)) return "AFFECTER_Barrier";
        if (effectKey == BossEffectRules.BloodGuardKey) return BossEffectRules.BloodGuardTemplate;
        return null;
    }

    // The own picture an effect shows when the file is in Core's package folder, or null. All four as
    // EffectIconSize squares in the style of the game's skill icons, frame and dark ground in the picture. The tooltip's
    // effect slot (AffecterIconUI) stretches any sprite to its fixed area, so 32 on about 35 pixels keeps the pixels even,
    // where a 16 was stretched 2.19 times; the game puts its framed 32 skill icons in the same slot.
    internal const int EffectIconSize = 32;

    internal static string EffectIconFile(string effectKey)
    {
        if (effectKey == EffectKey(GreatSwordKey)) return "Aderlass_Effektsymbol_32x32.png";
        if (effectKey == EffectKey(ShieldKey) || effectKey == BossEffectRules.BloodGuardKey) return "Blutwaechter_Effektsymbol_32x32.png";
        if (effectKey == EffectKey(BowKey)) return "Falkenaugen_Effektsymbol_32x32.png";
        if (effectKey == EffectKey(StaffKey)) return "Wildwuchs_Effektsymbol_32x32.png";
        return null;
    }

    // Whether the effect's game icon is shown with the red cast: the warden's equipment effect and the shield it gives (the
    // one the effect bar shows on the member; grey there).
    internal static bool RedIcon(string effectKey) => effectKey == EffectKey(ShieldKey) || effectKey == BossEffectRules.BloodGuardKey;

    // The red cast: red moves WardenRed of the way up, green and blue as much down; the grey stays recognisable. The author
    // picked the preview's middle strength.
    internal const double WardenRed = 0.22;

    internal static (byte R, byte G, byte B, byte A) RedCast(byte r, byte g, byte b, byte a)
    {
        if (a == 0) return (r, g, b, a);
        return ((byte)Math.Min(255, Math.Round(r + (255 - r) * WardenRed)), (byte)Math.Round(g * (1 - WardenRed)),
            (byte)Math.Round(b * (1 - WardenRed)), a);
    }

    // The equipment effects a unit's equipped items call for, each once, in the order of the items.
    internal static List<string> EffectsFor(IEnumerable<string> itemKeys)
    {
        var effects = new List<string>();
        if (itemKeys == null) return effects;
        foreach (var key in itemKeys)
        {
            if (!IsBossItem(key)) continue;
            var effect = EffectKey(key);
            if (!effects.Contains(effect)) effects.Add(effect);
        }
        return effects;
    }

    internal static bool IsEffect(string key)
    {
        foreach (var (part, _) in Parts)
            if (EffectKey(part) == key) return true;
        return false;
    }

    // CORE_BOSS_ITEM_EFFECT: an equipment effect row added, or why not.
    internal static string EffectLine(string key, string template, bool ok, bool exposed)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BOSS_ITEM_EFFECT key={0} from={1} result={2} exposed={3}",
            key, template, ok ? "ok" : "template-missing", exposed ? 1 : 0);

    // The tooltip colours a status row by its sign only (Tooltip_Item.AddStatRow: plus Positive, minus Negative). For skill
    // energy cost a plus is a drawback: on a boss part its row takes the other colour. No game item
    // carries this status (ItemTable.tsv), so only the boss parts.
    internal const string EnergyCostStat = "SkillEnergyCostPerModifier";

    internal static bool InvertsColour(string stat, bool bossPart) => bossPart && stat == EnergyCostStat;

    // CORE_BOSS_ITEM_STATS: an own part's base attack and statuses, the blood wood part's and its own.
    // skills: the part's Equipment_ItemDefaultSkills, a check against the offline table.
    internal static string StatsLine(string key, (float Physical, float Magic) nativeBase, (float Physical, float Magic) ownBase, string[] native, string[] own,
        string[] skills)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BOSS_ITEM_STATS key={0} physical={1:0.###}->{2:0.###} magic={3:0.###}->{4:0.###} native={5} own={6} skills={7}",
            key, nativeBase.Physical, ownBase.Physical, nativeBase.Magic, ownBase.Magic, string.Join(",", native ?? Array.Empty<string>()), string.Join(",", own),
            string.Join(",", skills ?? Array.Empty<string>()));

    // The blood wood part an own part copies, or null for any other key.
    internal static string StandIn(string key)
    {
        foreach (var (own, native) in Parts)
            if (own == key) return native;
        return null;
    }

    // ---- violet ----

    // Measured pixel by pixel on the preview image the author approved: a strong
    // red (hue RedHueFrom..360 or 0..RedHueTo, saturation at least StrongSaturation) takes the violet's hue at full
    // saturation, a dull red or brown (saturation from BanditTintRules.MinSaturation up to StrongSaturation) turns grey;
    // brightness and alpha stay. Every other colour stays: the staff's green crystal, the bow's pale string, the dark outline.
    // #B581CF, Core's Apocalypse colour: hue 280 degrees.
    internal const double VioletHue = 280.0;
    internal const double RedHueFrom = 330.0;
    internal const double RedHueTo = 30.0;
    internal const double StrongSaturation = 0.5;

    internal static bool IsRed(double hue) => hue >= RedHueFrom || hue <= RedHueTo;

    // One pixel, 0..255 per channel.
    internal static (byte R, byte G, byte B, byte A) Violet(byte r, byte g, byte b, byte a)
    {
        if (a == 0) return (r, g, b, a);
        BanditTintRules.ToHsv(r / 255.0, g / 255.0, b / 255.0, out var h, out var s, out var v);
        if (s < BanditTintRules.MinSaturation || !IsRed(h)) return (r, g, b, a);
        if (s >= StrongSaturation) (h, s) = (VioletHue, 1.0);
        else s = 0.0;
        BanditTintRules.FromHsv(h, s, v, out var nr, out var ng, out var nb);
        return (BanditTintRules.ToByte(nr), BanditTintRules.ToByte(ng), BanditTintRules.ToByte(nb), a);
    }

    // The tooltip's Legendary decoration in gold, turned violet for a boss part: a coloured pixel of gold or orange hue
    // (GoldHueFrom..GoldHueTo, saturation at least BanditTintRules.MinSaturation) takes the violet's hue and keeps its
    // saturation, brightness and alpha; everything else stays.
    internal const double GoldHueFrom = 15.0;
    internal const double GoldHueTo = 70.0;

    internal static (byte R, byte G, byte B, byte A) Gilded(byte r, byte g, byte b, byte a)
    {
        if (a == 0) return (r, g, b, a);
        BanditTintRules.ToHsv(r / 255.0, g / 255.0, b / 255.0, out var h, out var s, out var v);
        if (s < BanditTintRules.MinSaturation || h < GoldHueFrom || h > GoldHueTo) return (r, g, b, a);
        BanditTintRules.FromHsv(VioletHue, s, v, out var nr, out var ng, out var nb);
        return (BanditTintRules.ToByte(nr), BanditTintRules.ToByte(ng), BanditTintRules.ToByte(nb), a);
    }

    // CORE_BOSS_ITEM_DECO: once a session, what the tooltip's rarity decoration was when Core turned it violet.
    internal static string DecoPart(string image, string sprite, bool active, byte r, byte g, byte b)
        => string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}:{3:X2}{4:X2}{5:X2}", image, sprite, active ? 1 : 0, r, g, b);

    internal static string DecoLine(string key, int decos, IEnumerable<string> parts)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BOSS_ITEM_DECO key={0} decos={1} parts={2}", key, decos, string.Join(",", parts));

    // One boss of the party drops its part, the floor's seed picks which ("1 von 4"); the other three
    // corpses lie without theirs. The pick comes from the floor's seed, region and floor (RawDungeonDataContainer, saved with
    // the game; catalog chapter map and dungeon, RawDungeonDataContainer), so a load picks the same boss and nothing is stored. index: the boss's place in the party.
    internal static int ChosenIndex(int seed, int region, int floor, int count)
    {
        if (count <= 0) return -1;
        unchecked
        {
            var h = (uint)seed * 0x9E3779B1u ^ (uint)region * 0x85EBCA77u ^ (uint)floor * 0xC2B2AE3Du;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (int)(h % (uint)count);
        }
    }

    // CORE_BOSS_ITEM_CHOSEN: the boss whose part stays, once per floor.
    internal static string ChosenLine(int seed, int region, int floor, int index, string role)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BOSS_ITEM_CHOSEN seed={0} region={1} floor={2} index={3} role={4}",
            seed, region, floor, index, role);

    // CORE_BOSS_ITEM_REGISTERED: an own part added to the game's item table.
    internal static string RegisteredLine(string key, string native, int rarity, bool found)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BOSS_ITEM_REGISTERED key={0} from={1} rarity={2} result={3}",
            key, native, rarity, found ? "added" : "template-missing");

    // CORE_BOSS_ITEM_LOOK: a violet copy made for an icon or a weapon picture.
    internal static string LookLine(string where, string key, int width, int height)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BOSS_ITEM_LOOK at={0} key={1} size={2}x{3}", where, key, width, height);
}
