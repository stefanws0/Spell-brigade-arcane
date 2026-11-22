namespace SpellBrigadeArcane.Game;

using System;
using System.Collections.Generic;
using System.IO;
using SpellBrigadeArcane.Core;
using UnityEngine;

/// <summary>
/// The sixth tier's own icon and glow sprites, loaded from the PNGs built into the DLL (art/icons). Same loader as the
/// Dash mod's card icons. Null if a PNG is missing or can't be decoded (the caller then keeps the game's icon).
/// </summary>
internal static class IconSprites
{
    private const string ResourcePrefix = "SpellBrigadeArcane.Icons.";
    private static readonly Dictionary<string, Sprite?> Cache = new();

    public static Sprite? Icon(ArcaneId id) => Get(id.ToString().ToLowerInvariant() + ".png");

    public static Sprite? Glow(ArcaneId id) => Get(id.ToString().ToLowerInvariant() + "_glow.png");

    /// <summary>Any built-in PNG from art/icons by file name, e.g. "belt_slot.png".</summary>
    public static Sprite? Named(string file) => Get(file);

    private static Sprite? Get(string file)
    {
        if (Cache.TryGetValue(file, out var cached) && cached != null) return cached;
        Sprite? sprite = null;
        try
        {
            ModLog.StepOnce("icon-load", "IconSprites: first icon load");
            using var stream = typeof(IconSprites).Assembly.GetManifestResourceStream(ResourcePrefix + file);
            if (stream == null)
            {
                ModLog.WarnOnce("icon-missing-" + file, $"IconSprites: {file} is not built in");
            }
            else
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                sprite = MakeSprite(file, ms.ToArray());
            }
        }
        catch (Exception ex)
        {
            ModLog.WarnOnce("icon-failed-" + file, $"IconSprites: {file} failed: {ex.Message}");
        }

        Cache[file] = sprite;
        return sprite;
    }

    private static Sprite? MakeSprite(string name, byte[] png)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(tex, png)) return null;
        tex.name = "ARC_" + name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave; // survive the game's Resources.UnloadUnusedAssets on scene changes
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "ARC_" + name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
