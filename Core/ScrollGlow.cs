using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// The revive scroll's own look on the return scroll (players must tell them apart; paper
// gold-yellow, ribbon silver, a faint ring, ScrollGlowRules). Made at runtime from the game's own sprite, the way the game's
// TextureHelper copies textures (RenderTexture, then ReadPixels), so no image of the game is shipped.
internal static class ScrollGlow
{
    private static readonly Color32 Ring = new(ScrollGlowRules.Ring.R, ScrollGlowRules.Ring.G, ScrollGlowRules.Ring.B, ScrollGlowRules.Ring.A);
    private const byte Solid = 128;

    internal static Sprite Create(Sprite source)
    {
        var rect = source.textureRect;
        int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
        if (width <= 0 || height <= 0) return null;
        var copy = Copy(source.texture, rect, width, height);
        var pixels = copy.GetPixels32();
        var result = new Il2CppStructArray<Color32>(pixels.Length);
        for (var i = 0; i < pixels.Length; i++) result[i] = pixels[i];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var pixel = pixels[index];
                if (pixel.a >= Solid)
                {
                    var (r, g, b) = ScrollGlowRules.Turn(pixel.r, pixel.g, pixel.b);
                    result[index] = new Color32(r, g, b, pixel.a);
                    continue;
                }
                if (Distance(pixels, width, height, x, y) == 1) result[index] = Ring;
            }
        copy.SetPixels32(result);
        copy.filterMode = FilterMode.Point;
        copy.wrapMode = TextureWrapMode.Clamp;
        copy.Apply();
        copy.hideFlags = HideFlags.DontUnloadUnusedAsset;
        var pivot = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height);
        var sprite = Sprite.Create(copy, new Rect(0, 0, width, height), pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        DelversHost.Info($"CORE_SCROLL_GLOW_READY size={width}x{height}");
        return sprite;
    }

    // The texture's part that holds the sprite, readable whether the original is readable or not.
    private static Texture2D Copy(Texture texture, Rect rect, int width, int height)
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

    // 1 when a solid pixel touches (x, y), 2 when one is two steps away, otherwise 0.
    private static int Distance(Il2CppStructArray<Color32> pixels, int width, int height, int x, int y)
    {
        var best = 0;
        for (var dy = -2; dy <= 2; dy++)
            for (var dx = -2; dx <= 2; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                if (pixels[ny * width + nx].a < Solid) continue;
                var step = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (step == 1) return 1;
                best = 2;
            }
        return best;
    }
}
