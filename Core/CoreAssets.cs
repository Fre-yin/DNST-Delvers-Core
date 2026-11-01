using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Core's own images: PNG files in the package's Core folder, read once on first
// use and kept for the whole session. A missing or unexpected file is logged and left null, so the
// caller keeps the game's own icon. Today: the Lifebloom skill icon (32x32, also for 2, 1 and 0 charges; the slot shows the
// charges left, the game has no such display) and its effect icon (16x16), the bandage
// icons, the Serrated Blade and chain pull icons, the Lifebloom skill book icon, the drawn body blood and the formation button.
internal static class CoreAssets
{
    internal const string LifebloomSkillFile = "Lifebloom_Zaubersymbol_32x32.png";
    internal const string LifebloomEffectFile = "Lifebloom_Effektsymbol_16x16.png";
    // Index = charges left (0 to 2); a full charge count shows LifebloomSkillFile.
    internal static readonly string[] LifebloomChargeFiles =
        { "Lifebloom_Zaubersymbol_32x32_0.png", "Lifebloom_Zaubersymbol_32x32_1.png", "Lifebloom_Zaubersymbol_32x32_2.png" };

    private static bool loaded;
    private static Sprite lifebloomSkill;
    private static Sprite lifebloomEffect;
    private static readonly Sprite[] lifebloomCharges = new Sprite[3];

    // The loader's package folder; without a host the game's Mods folder.
    internal static string Folder => Path.Combine(
        DelversHost.PackageDirectory ?? Path.Combine(Path.GetDirectoryName(Application.dataPath), "Mods", "DungeonSettlersDelvers"),
        "Core");

    internal static Sprite LifebloomSkill
    {
        get
        {
            Load();
            return lifebloomSkill;
        }
    }

    // The skill icon for the charges left (LifebloomRules.IconCharges); the full icon for a full count or a missing file.
    internal static Sprite LifebloomSkillFor(int charges)
    {
        Load();
        var index = LifebloomRules.IconCharges(charges);
        var sprite = index < lifebloomCharges.Length ? lifebloomCharges[index] : null;
        return sprite ? sprite : lifebloomSkill;
    }

    // The bandage's own 16×16 icon, null when its file is missing (the caller keeps the placeholder). Read once,
    // with one CORE_BANDAGE_ICONS line.
    private static bool bandagesLoaded;
    private static readonly Dictionary<string, Sprite> bandageIcons = new(StringComparer.Ordinal);

    internal static Sprite BandageIcon(string itemKey)
    {
        var file = BandageIconRules.OwnIconFile(itemKey);
        if (file == null) return null;
        if (!bandagesLoaded)
        {
            bandagesLoaded = true;
            var missing = new List<string>();
            foreach (var name in BandageIconRules.OwnIconFiles)
            {
                var sprite = Read(name, 16, 16, missing);
                if (sprite) bandageIcons[name] = sprite;
            }
            DelversHost.Info(BandageIconRules.IconsLine(bandageIcons.Count, missing));
        }
        return bandageIcons.TryGetValue(file, out var icon) ? icon : null;
    }

    // The bandit room's waterfall (8 frames cut from one 384x80 sheet) and the two parts of the wooden bridge (64x32), loaded
    // once; null without the files (BanditRoomFeatureRules).
    private static bool roomFeaturesLoaded;
    private static Sprite[] waterfallFrames;
    private static Sprite bridgeUnder;
    private static Sprite bridgeOver;

    internal static Sprite[] WaterfallFrames()
    {
        LoadRoomFeatures();
        return waterfallFrames;
    }

    internal static Sprite BridgeUnder()
    {
        LoadRoomFeatures();
        return bridgeUnder;
    }

    internal static Sprite BridgeOver()
    {
        LoadRoomFeatures();
        return bridgeOver;
    }

    // The tool's list icon of a room part: the waterfall's first frame, the bridge's deck with its rail over it (made once);
    // null for another kind or without the files.
    private static Sprite bridgeIcon;

    internal static Sprite RoomFeatureIcon(string kind)
    {
        LoadRoomFeatures();
        if (kind == BanditRoomFeatureRules.WaterfallKind) return waterfallFrames?[0];
        if (kind != BanditRoomFeatureRules.BridgeKind || !bridgeUnder || !bridgeOver) return null;
        if (!bridgeIcon) bridgeIcon = Combine(bridgeUnder, bridgeOver, "Bruecke_Symbol");
        return bridgeIcon;
    }

    private static Sprite Combine(Sprite below, Sprite top, string name)
    {
        var width = (int)below.rect.width;
        var height = (int)below.rect.height;
        var lower = below.texture.GetPixels32();
        var upper = top.texture.GetPixels32();
        var mixed = new Color32[lower.Length];
        for (var i = 0; i < lower.Length; i++)
        {
            var (r, g, b, a) = BanditRoomFeatureRules.Over((upper[i].r, upper[i].g, upper[i].b, upper[i].a), (lower[i].r, lower[i].g, lower[i].b, lower[i].a));
            mixed[i] = new Color32(r, g, b, a);
        }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(mixed);
        texture.Apply();
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 16f, 0u, SpriteMeshType.FullRect);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    private static void LoadRoomFeatures()
    {
        if (roomFeaturesLoaded) return;
        roomFeaturesLoaded = true;
        var missing = new List<string>();
        var sheet = Read(BanditRoomFeatureRules.WaterfallSheet, BanditRoomFeatureRules.WaterfallFrames * BanditRoomFeatureRules.WaterfallFrameWidth,
            BanditRoomFeatureRules.WaterfallFrameHeight, missing);
        bridgeUnder = Read(BanditRoomFeatureRules.BridgeUnderFile, BanditRoomFeatureRules.BridgeWidth, BanditRoomFeatureRules.BridgeHeight, missing);
        bridgeOver = Read(BanditRoomFeatureRules.BridgeOverFile, BanditRoomFeatureRules.BridgeWidth, BanditRoomFeatureRules.BridgeHeight, missing);
        if (sheet)
        {
            waterfallFrames = new Sprite[BanditRoomFeatureRules.WaterfallFrames];
            for (var i = 0; i < waterfallFrames.Length; i++)
            {
                var rect = new Rect(BanditRoomFeatureRules.WaterfallFrameX(i), 0f, BanditRoomFeatureRules.WaterfallFrameWidth, BanditRoomFeatureRules.WaterfallFrameHeight);
                waterfallFrames[i] = Sprite.Create(sheet.texture, rect, new Vector2(0.5f, 0.5f), 16f, 0u, SpriteMeshType.FullRect);
                waterfallFrames[i].name = "Wasserfall_" + i;
                waterfallFrames[i].hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
        }
        DelversHost.Info("CORE_BANDIT_ROOM_ASSETS loaded=" + ((sheet ? 1 : 0) + (bridgeUnder ? 1 : 0) + (bridgeOver ? 1 : 0)) + " missing=" + (missing.Count == 0 ? "-" : string.Join(",", missing)));
    }

    // The author's icon of the blade's Serrated Blade (BanditSerratedRules.IconFile), 32x32, loaded once; null without the file.
    private static bool serratedLoaded;
    private static Sprite serrated;

    internal static Sprite SerratedBladeIcon()
    {
        if (!serratedLoaded)
        {
            serratedLoaded = true;
            var missing = new List<string>();
            serrated = Read(BanditSerratedRules.IconFile, 32, 32, missing);
            DelversHost.Info("CORE_BANDIT_SERRATED_ICON loaded=" + (serrated ? 1 : 0) + " missing=" + (missing.Count == 0 ? "-" : string.Join(",", missing)));
        }
        return serrated;
    }

    // The author's icon of the bandits' chain pull (BanditChainRules.IconFile), 32x32, loaded once; null without the file.
    private static bool banditChainLoaded;
    private static Sprite banditChain;

    internal static Sprite BanditChainIcon()
    {
        if (!banditChainLoaded)
        {
            banditChainLoaded = true;
            var missing = new List<string>();
            banditChain = Read(BanditChainRules.IconFile, 32, 32, missing);
            DelversHost.Info("CORE_BANDIT_CHAIN_ICON loaded=" + (banditChain ? 1 : 0) + " missing=" + (missing.Count == 0 ? "-" : string.Join(",", missing)));
        }
        return banditChain;
    }

    // The author's skill book for Lifebloom (LifebloomBookRules.IconFile), 16x16, loaded once; null without the file.
    private static bool lifebloomBookLoaded;
    private static Sprite lifebloomBook;

    internal static Sprite LifebloomBookIcon()
    {
        if (!lifebloomBookLoaded)
        {
            lifebloomBookLoaded = true;
            var missing = new List<string>();
            lifebloomBook = Read(LifebloomBookRules.IconFile, 16, 16, missing);
            DelversHost.Info("CORE_LIFEBLOOM_BOOK_ICON loaded=" + (lifebloomBook ? 1 : 0) + " missing=" + (missing.Count == 0 ? "-" : string.Join(",", missing)));
        }
        return lifebloomBook;
    }

    // The formation button in the option row (TacticalFormationButton), 20x23 like the game's option buttons, switched on and
    // off like them. The crown on the leader in the formation
    // window, 7x5. Loaded once; null without the files (the copy keeps the game's icon, the window shows no crown).
    internal const string FormationButtonOffFile = "Formation_Knopf_Aus_20x23.png";
    internal const string FormationButtonOnFile = "Formation_Knopf_An_20x23.png";
    internal const string FormationCrownFile = "Formation_Krone_7x5.png";
    private static bool formationLoaded;
    private static Sprite formationButtonOff;
    private static Sprite formationButtonOn;
    private static Sprite formationCrown;

    internal static Sprite FormationButtonOff
    {
        get
        {
            LoadFormation();
            return formationButtonOff;
        }
    }

    internal static Sprite FormationButtonOn
    {
        get
        {
            LoadFormation();
            return formationButtonOn;
        }
    }

    internal static Sprite FormationCrown
    {
        get
        {
            LoadFormation();
            return formationCrown;
        }
    }

    // The cross of the cast dropdown's clearing row (a plain, clearly visible cross), drawn once in the colour
    // of the switched on button instead of a file: 14x14 pixels, two diagonals two pixels wide.
    private static Sprite formationCross;

    internal static Sprite FormationCross
    {
        get
        {
            if (formationCross) return formationCross;
            const int size = 14;
            var line = new Color32(182, 131, 90, 255);
            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var onLine = Math.Abs(x - y) <= 1 || Math.Abs(x + y - (size - 1)) <= 1;
                    var inside = x >= 2 && x < size - 2 && y >= 2 && y < size - 2;
                    pixels[y * size + x] = onLine && inside ? line : clear;
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply();
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            formationCross = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 16f, 0u, SpriteMeshType.FullRect);
            formationCross.name = "Formation_Kreuz";
            formationCross.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return formationCross;
        }
    }

    private static void LoadFormation()
    {
        if (formationLoaded) return;
        formationLoaded = true;
        var missing = new List<string>();
        formationButtonOff = Read(FormationButtonOffFile, 20, 23, missing);
        formationButtonOn = Read(FormationButtonOnFile, 20, 23, missing);
        formationCrown = Read(FormationCrownFile, 7, 5, missing);
        DelversHost.Info($"CORE_ASSETS formation loaded={3 - missing.Count} missing={(missing.Count == 0 ? "-" : string.Join(",", missing))}");
    }

    // The author's drawn body blood (WoundBodyArtRules), 64x40 each, loaded once; null for a missing frame.
    private static bool bodyBloodLoaded;
    private static readonly Sprite[,] bodyBlood = new Sprite[3, WoundBodyArtRules.Frames];

    internal static Sprite BodyBlood(int stage, int frame)
    {
        if (stage < 0 || stage >= 3 || frame < 0 || frame >= WoundBodyArtRules.Frames) return null;
        if (!bodyBloodLoaded)
        {
            bodyBloodLoaded = true;
            var missing = new List<string>();
            for (var s = 0; s < 3; s++)
                for (var f = 0; f < WoundBodyArtRules.Frames; f++)
                    bodyBlood[s, f] = Read(WoundBodyArtRules.File(s, f), WoundBodyArtRules.Width, WoundBodyArtRules.Height, missing);
            DelversHost.Info($"CORE_ASSETS bodyblood loaded={3 * WoundBodyArtRules.Frames - missing.Count} missing={(missing.Count == 0 ? "-" : string.Join(",", missing))}");
        }
        return bodyBlood[stage, frame];
    }

    // Picture 19: the overlay takes the body sprite's own pivot and pixels per unit,
    // read at runtime, instead of the centre. One sprite per stage, frame, pivot and PPU, made from the loaded texture.
    private static readonly Dictionary<string, Sprite> bodyBloodAt = new(StringComparer.Ordinal);

    internal static Sprite BodyBlood(int stage, int frame, Vector2 pivot, float pixelsPerUnit)
    {
        var source = BodyBlood(stage, frame);
        if (!source) return null;
        var key = WoundBodyArtRules.SpriteKey(stage, frame, pivot.x, pivot.y, pixelsPerUnit);
        if (bodyBloodAt.TryGetValue(key, out var made) && made) return made;
        made = Sprite.Create(source.texture, source.rect, pivot, pixelsPerUnit, 0u, SpriteMeshType.FullRect);
        if (!made) return source;
        made.name = source.name + "_" + key;
        made.hideFlags = HideFlags.DontUnloadUnusedAsset;
        if (bodyBloodAt.Count >= 256) bodyBloodAt.Clear();
        bodyBloodAt[key] = made;
        return made;
    }

    internal static Sprite LifebloomEffect
    {
        get
        {
            Load();
            return lifebloomEffect;
        }
    }

    // The boss effects' own pictures (BossItemRules.EffectIconFile), each read once; null while the file is not there.
    private static readonly Dictionary<string, Sprite> effectIcons = new(StringComparer.Ordinal);

    internal static Sprite BossEffectIcon(string file)
    {
        if (file == null) return null;
        if (effectIcons.TryGetValue(file, out var sprite)) return sprite;
        var missing = new List<string>();
        sprite = Read(file, BossItemRules.EffectIconSize, BossItemRules.EffectIconSize, missing);
        effectIcons[file] = sprite;
        if (sprite) DelversHost.Info("CORE_ASSETS boss effect icon=" + file);
        return sprite;
    }

    internal static void ResetLifecycle()
    {
        effectIcons.Clear();
        loaded = false;
        lifebloomSkill = null;
        lifebloomEffect = null;
        Array.Clear(lifebloomCharges, 0, lifebloomCharges.Length);
        bandagesLoaded = false;
        bandageIcons.Clear();
        bodyBloodLoaded = false;
        formationLoaded = false;
        formationButtonOff = formationButtonOn = formationCrown = formationCross = null;
        bodyBloodAt.Clear();
        Array.Clear(bodyBlood, 0, bodyBlood.Length);
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        var missing = new List<string>();
        lifebloomSkill = Read(LifebloomSkillFile, 32, 32, missing);
        lifebloomEffect = Read(LifebloomEffectFile, 16, 16, missing);
        for (var i = 0; i < LifebloomChargeFiles.Length; i++) lifebloomCharges[i] = Read(LifebloomChargeFiles[i], 32, 32, missing);
        DelversHost.Info($"CORE_ASSETS loaded={2 + LifebloomChargeFiles.Length - missing.Count} missing={(missing.Count == 0 ? "-" : string.Join(",", missing))}");
    }

    // A guide topic's own picture (GuideTopicText.ImageFile), read once; null, also remembered, when the file is missing or wrong.
    private static readonly Dictionary<int, Sprite> guideImages = new();

    internal static Sprite GuideImage(int id)
    {
        if (guideImages.TryGetValue(id, out var known)) return known;
        var file = GuideTopicText.ImageFile(id);
        Sprite sprite = null;
        if (File.Exists(Path.Combine(Folder, file)))
        {
            var missing = new List<string>();
            sprite = Read(file, GuideTopicText.ImageWidth, GuideTopicText.ImageHeight, missing, FilterMode.Bilinear, 100f);
            DelversHost.Info("CORE_GUIDE_IMAGE topic=" + id + " file=" + file + " loaded=" + (sprite != null ? 1 : 0));
        }
        guideImages[id] = sprite;
        return sprite;
    }

    // Pixel art: point filtering, 16 pixels per unit, never unloaded by the game's asset cleanup.
    private static Sprite Read(string file, int width, int height, List<string> missing) => Read(file, width, height, missing, FilterMode.Point, 16f);

    private static Sprite Read(string file, int width, int height, List<string> missing, FilterMode filter, float pixelsPerUnit)
    {
        var path = Path.Combine(Folder, file);
        if (!File.Exists(path))
        {
            missing.Add(file);
            return null;
        }
        Texture2D texture = null;
        try
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false) || texture.width != width || texture.height != height)
                throw new InvalidDataException("unexpected image " + file);
            texture.name = Path.GetFileNameWithoutExtension(file) + "_Texture";
            texture.filterMode = filter;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0u, SpriteMeshType.FullRect);
            if (!sprite) throw new InvalidOperationException("no sprite for " + file);
            sprite.name = Path.GetFileNameWithoutExtension(file);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
        catch (Exception ex)
        {
            if (texture) UnityEngine.Object.Destroy(texture);
            missing.Add(file);
            DelversHost.Warning("CORE_ASSETS_FAILED file=" + file + ": " + ex.GetType().Name);
            return null;
        }
    }
}
