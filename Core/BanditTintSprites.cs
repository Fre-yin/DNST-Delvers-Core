using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// BanditTint, the sprite copies: the tracked renderers, the frame step that swaps in the orange copy of the sprite each one
// shows, the copy itself, and giving the game's sprite back.
internal static partial class BanditTint
{
    private sealed class Tracked
    {
        internal SpriteRenderer Renderer;
        internal Sprite Original;
        internal string Owner;
        internal string Fx;
        internal TintKind Kind;
    }

    // Which turn a renderer's copies take: a building's green (Pixel), a summon's water hues (SummonPixel), a skill effect's
    // colours (SkillPixel).
    private enum TintKind { Building, Summon, Skill }

    private const float ScanSeconds = 0.25f;
    private static readonly Dictionary<IntPtr, Tracked> tracked = new();
    private static readonly Dictionary<IntPtr, Sprite> copies = new();
    // A summon's and a skill effect's copies apart: the same game sprite may be a building part's too, and the turns differ.
    private static readonly Dictionary<IntPtr, Sprite> summonCopies = new();
    private static readonly Dictionary<IntPtr, Sprite> skillCopies = new();
    private static readonly HashSet<IntPtr> made = new();
    private static readonly HashSet<IntPtr> failed = new();
    private static float nextScan;
    private static bool failWarned;

    // Every frame after the game's animations.
    internal static void LateFrame()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + ScanSeconds;
            Scan();
        }
        // The buildings' health bar (BanditBar) also while the tint is off.
        BanditBar.LateFrame(view);
        if (hue == null || tracked.Count == 0) return;
        var newCopies = 0;
        List<IntPtr> dead = null;
        foreach (var pair in tracked)
        {
            var t = pair.Value;
            if (t.Renderer == null)
            {
                (dead ??= new List<IntPtr>()).Add(pair.Key);
                continue;
            }
            var shown = t.Renderer.sprite;
            if (shown == null || made.Contains(shown.Pointer)) continue;
            t.Original = shown;
            var cache = t.Kind == TintKind.Summon ? summonCopies : t.Kind == TintKind.Skill ? skillCopies : copies;
            if (!cache.TryGetValue(shown.Pointer, out var orange))
            {
                if (failed.Contains(shown.Pointer) || newCopies >= BanditTintRules.CopiesPerFrame) continue;
                newCopies++;
                orange = Copy(shown, t.Kind);
                if (orange == null)
                {
                    failed.Add(shown.Pointer);
                    continue;
                }
                cache[shown.Pointer] = orange;
                made.Add(orange.Pointer);
            }
            t.Renderer.sprite = orange;
        }
        if (dead != null) foreach (var key in dead) tracked.Remove(key);
    }

    // A summon: every renderer under the unit's pivot (its body; the HUD hangs elsewhere), with the summon's turn. A skill effect:
    // every renderer under the effect's object, with the skill's turn.
    private static int Track(Transform top, string owner, string fx, TintKind kind = TintKind.Building)
    {
        var summon = kind != TintKind.Building;
        var count = 0;
        var renderers = top.GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; renderers != null && i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null || tracked.ContainsKey(renderer.Pointer)) continue;
            // A building keeps only its own sprite and its area (Sprite, Sprite/Area/...): not its HUD (corners, markers, signs)
            // and not the effects the game hangs under it (they are pooled and come through OnOwner with their own id).
            if (fx == null && !summon && !BanditTintRules.BuildingPartKept(PathOf(renderer.transform, top))) continue;
            tracked[renderer.Pointer] = new Tracked { Renderer = renderer, Original = renderer.sprite, Owner = owner, Fx = fx, Kind = kind };
            count++;
        }
        return count;
    }

    // The names from below top down to t, "A/B/C"; null when t is not under top.
    private static string PathOf(Transform t, Transform top)
    {
        var parts = new List<string>();
        for (var at = t; at != null; at = at.parent)
        {
            if (at == top) { parts.Reverse(); return string.Join("/", parts); }
            parts.Add(at.name);
        }
        return null;
    }

    private static void Restore(Tracked t)
    {
        if (t.Renderer == null || t.Original == null) return;
        var shown = t.Renderer.sprite;
        if (shown != null && made.Contains(shown.Pointer)) t.Renderer.sprite = t.Original;
    }

    private static void RestoreAll(string why)
    {
        if (tracked.Count == 0) return;
        foreach (var t in tracked.Values) Restore(t);
        var count = tracked.Count;
        tracked.Clear();
        if (lines++ < BanditTintRules.LogCap) DelversHost.Info(BanditTintRules.Line("restore", why, count, 0));
    }

    // One pixel's new colour, 0..255 per channel.
    internal delegate (byte R, byte G, byte B, byte A) PixelTurn(byte r, byte g, byte b, byte a);

    // The orange copy of a game sprite.
    private static Sprite Copy(Sprite source, TintKind kind)
    {
        try
        {
            var sprite = kind switch
            {
                TintKind.Summon => Recolored(source, SummonOrange, "_DelversBanditSummon"),
                TintKind.Skill => Recolored(source, SkillOrange, "_DelversBanditSkill"),
                _ => Recolored(source, Orange, "_DelversBandit")
            };
            // A skill effect's copies count on the skill lines, so they do not use up the buildings' (run 20261101-001155).
            if (sprite != null && (kind == TintKind.Skill ? skillLines++ < BanditTintRules.SkillLogCap : lines++ < BanditTintRules.LogCap))
                DelversHost.Info(BanditTintRules.CopyLine(source.name, (int)sprite.rect.width, (int)sprite.rect.height, copies.Count + summonCopies.Count + skillCopies.Count + 1));
            return sprite;
        }
        catch (Exception ex)
        {
            if (!failWarned)
            {
                failWarned = true;
                DelversHost.Warning("CORE_SEED_TINT_FAILED sprite=" + (source?.name ?? "-") + ": " + ex.Message);
            }
            return null;
        }
    }

    private static (byte R, byte G, byte B, byte A) Orange(byte r, byte g, byte b, byte a)
        => BanditTintRules.Pixel(r, g, b, a, hue ?? BanditTintRules.DefaultHue, plant);

    private static (byte R, byte G, byte B, byte A) SummonOrange(byte r, byte g, byte b, byte a)
        => BanditTintRules.SummonPixel(r, g, b, a, hue ?? BanditTintRules.DefaultHue);

    private static (byte R, byte G, byte B, byte A) SkillOrange(byte r, byte g, byte b, byte a)
        => BanditTintRules.SkillPixel(r, g, b, a, hue ?? BanditTintRules.DefaultHue);

    // A recoloured copy of a game sprite, pixel by pixel through turn: same place, size, pivot and pixels per unit; null
    // for an empty sprite. Also the boss items' violet (BossItemLook). Throws when the picture cannot be read.
    internal static Sprite Recolored(Sprite source, PixelTurn turn, string suffix)
    {
        var rect = source.textureRect;
        int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
        if (width <= 0 || height <= 0 || source.texture == null) return null;
        var copy = Readable(source.texture, rect, width, height);
        var pixels = copy.GetPixels32();
        var result = new Il2CppStructArray<Color32>(pixels.Length);
        for (var i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            var (r, g, b, a) = turn(p.r, p.g, p.b, p.a);
            result[i] = new Color32(r, g, b, a);
        }
        copy.SetPixels32(result);
        copy.filterMode = source.texture.filterMode;
        copy.wrapMode = TextureWrapMode.Clamp;
        copy.Apply();
        copy.hideFlags = HideFlags.DontUnloadUnusedAsset;
        // The pivot in pixels of the sprite's rect, moved to the copied part of the texture.
        var offset = source.textureRectOffset;
        var pivot = new Vector2((source.pivot.x - offset.x) / rect.width, (source.pivot.y - offset.y) / rect.height);
        var sprite = Sprite.Create(copy, new Rect(0, 0, width, height), pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = source.name + suffix;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    private static Texture2D Readable(Texture texture, Rect rect, int width, int height)
    {
        var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(texture, target);
            RenderTexture.active = target;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0);
            copy.Apply();
            return copy;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
