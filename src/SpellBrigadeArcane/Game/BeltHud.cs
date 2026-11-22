namespace SpellBrigadeArcane.Game;

using System;
using Il2CppInterop.Runtime;
using SpellBrigadeArcane.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Potion Belt on screen, built from the game's own health bar (layout facts: the Ctrl+7 dump of 22 Nov):
/// a round slot left of the bar (a stone disc with an empty bottle-shaped recess; the potion bottle sits in it while one
/// is stored), a line where the potion will be drunk, and the health above that line in orange while a potion is stored.
/// The orange is the game's own fill strip and end piece, recoloured, and anchored exactly like the game's red fill
/// (same offsets, read from the game's objects), so it follows the bar at every resolution. Nothing of the game's is
/// changed; everything here is extra children of the health slider.
/// </summary>
internal static class BeltHud
{
    private const float PopSeconds = 0.35f;
    private const float HueShift = 0.065f; // the game's red-orange fill -> amber, keeping its shading
    private static readonly Color Amber = new(1f, 0.85f, 0.35f, 1f);

    private static PlayerHealthDisplay? _display;
    private static Slider? _bar;
    private static GameObject? _slot;
    private static Image? _bottle;
    private static RectTransform? _marker;
    private static RectTransform? _fill;
    private static RectTransform? _safe;
    private static GameObject? _safeEnd;
    private static float _nextFind;
    private static float _popUntil;
    private static bool _failed;
    private static FMODUnity.EventReference _storeSound;
    private static FMODUnity.EventReference _drinkSound;
    private static bool _haveSounds;

    public static void Update(float now)
    {
        if (_failed) return;
        var active = MissionBonuses.Identity != null && MissionBonuses.PotionBeltActive;
        if (!active)
        {
            if (_slot != null) _slot.SetActive(false);
            if (_marker != null) _marker.gameObject.SetActive(false);
            if (_safe != null) _safe.gameObject.SetActive(false);
            return;
        }

        if (_slot == null || _bar == null || _marker == null)
        {
            if (now < _nextFind) return;
            _nextFind = now + 1f;
            try
            {
                if (!Build()) return;
            }
            catch (Exception ex)
            {
                _failed = true;
                ModLog.Error($"BeltHud: could not add the potion slot; Potion Belt still works without it: {ex}");
                return;
            }
        }

        var stored = PotionBeltRunner.HasPotion;
        _slot!.SetActive(true);
        _bottle!.gameObject.SetActive(stored);
        var pop = now < _popUntil ? 1f + 0.5f * (_popUntil - now) / PopSeconds : 1f;
        _slot.transform.localScale = new Vector3(pop, pop, 1f);

        var range = _bar!.maxValue - _bar.minValue;
        var t = range > 0f
            ? Mathf.Clamp01((PotionBelt.DefaultThreshold * _bar.maxValue - _bar.minValue) / range)
            : PotionBelt.DefaultThreshold;
        var value = _bar.normalizedValue;
        var leftToRight = _bar.direction == Slider.Direction.LeftToRight;

        _marker!.gameObject.SetActive(stored && leftToRight);
        var showSafe = stored && leftToRight && _safe != null && _fill != null && value > t;
        if (_safe != null) _safe.gameObject.SetActive(showSafe);
        if (!stored || !leftToRight || _fill == null) return;

        // The game's fill spans anchors 0..value with fixed offsets; "threshold" = where its right edge would be at t.
        var edge = _fill.offsetMax.x;
        _marker.anchorMin = new Vector2(t, _fill.anchorMin.y);
        _marker.anchorMax = new Vector2(t, _fill.anchorMax.y);
        _marker.offsetMin = new Vector2(edge - 2f, _fill.offsetMin.y);
        _marker.offsetMax = new Vector2(edge + 2f, _fill.offsetMax.y);
        if (!showSafe) return;
        _safe!.anchorMin = new Vector2(t, _fill.anchorMin.y);
        _safe.anchorMax = new Vector2(value, _fill.anchorMax.y);
        _safe.offsetMin = new Vector2(edge, _fill.offsetMin.y);
        _safe.offsetMax = new Vector2(edge, _fill.offsetMax.y);
        if (_safeEnd != null && _display != null && _display.rightCorner != null)
            _safeEnd.SetActive(_display.rightCorner.activeSelf);
    }

    /// <summary>A potion went into the belt: pop the slot and play the potion's spawn chime.</summary>
    public static void OnStored(Vector3 position)
    {
        _popUntil = Time.unscaledTime + PopSeconds;
        try
        {
            if (!_haveSounds)
            {
                ModLog.StepOnce("belt-sounds", "BeltHud: first read of the potion's sounds");
                var player = Object.FindObjectOfType(Il2CppType.Of<HealthPickupSoundPlayer>())?.TryCast<HealthPickupSoundPlayer>();
                if (player != null)
                {
                    _storeSound = player.spawnSound;
                    _drinkSound = player.pickupSound;
                    _haveSounds = true;
                }
            }

            if (_haveSounds) FMODUnity.RuntimeManager.PlayOneShot(_storeSound, position);
        }
        catch (Exception ex)
        {
            ModLog.WarnOnce("belt-store-sound", "BeltHud: no sound for storing a potion: " + ex.Message);
        }
    }

    /// <summary>The stored potion was drunk: pop the slot and play the potion's pickup sound.</summary>
    public static void OnDrunk(Vector3 position)
    {
        _popUntil = Time.unscaledTime + PopSeconds;
        try
        {
            if (_haveSounds) FMODUnity.RuntimeManager.PlayOneShot(_drinkSound, position);
        }
        catch (Exception ex)
        {
            ModLog.WarnOnce("belt-drink-sound", "BeltHud: no sound for drinking the potion: " + ex.Message);
        }
    }

    private static bool Build()
    {
        ModLog.StepOnce("belt-hud-find", "BeltHud: first FindObjectOfType PlayerHealthDisplay");
        _display = Object.FindObjectOfType(Il2CppType.Of<PlayerHealthDisplay>())?.TryCast<PlayerHealthDisplay>();
        if (_display == null) return false;
        _bar = _display.bar;
        var barRect = _bar == null ? null : _bar.transform.TryCast<RectTransform>();
        if (barRect == null) return false;

        ModLog.Step("BeltHud: building the potion slot, marker and safe zone");
        var size = Mathf.Max(barRect.rect.height * 1.25f, 32f);
        _slot = NewChild("ARC_PotionSlot", barRect);
        var slotRect = _slot.GetComponent<RectTransform>();
        slotRect.anchorMin = new Vector2(0f, 0.5f);
        slotRect.anchorMax = new Vector2(0f, 0.5f);
        slotRect.pivot = new Vector2(1f, 0.5f);
        slotRect.anchoredPosition = new Vector2(-size * 0.14f, 0f);
        slotRect.sizeDelta = new Vector2(size, size);
        var back = _slot.AddComponent<Image>();
        back.sprite = IconSprites.Named("belt_slot.png") ?? throw new InvalidOperationException("belt_slot.png is not built in");
        back.raycastTarget = false;

        var bottleObject = NewChild("Bottle", slotRect);
        Stretch(bottleObject.GetComponent<RectTransform>());
        _bottle = bottleObject.AddComponent<Image>();
        _bottle.sprite = IconSprites.Named("belt_bottle.png") ?? throw new InvalidOperationException("belt_bottle.png is not built in");
        _bottle.raycastTarget = false;

        _fill = _bar!.fillRect;
        var fillArea = _fill == null ? null : _fill.parent.TryCast<RectTransform>();
        var fillImage = _fill == null ? null : _fill.GetComponent<Image>();
        if (_fill != null && fillArea != null && fillImage != null)
        {
            // The orange body: the game's fill strip, recoloured, as the next child after the game's fill.
            var safeObject = NewChild("ARC_PotionSafe", fillArea);
            _safe = safeObject.GetComponent<RectTransform>();
            _safe.SetSiblingIndex(_fill.GetSiblingIndex() + 1);
            Copy(fillImage, safeObject.AddComponent<Image>());

            // Its pointed end: a copy of the game's right corner, placed exactly like the game's.
            var corner = _display.rightCorner == null ? null : _display.rightCorner.transform.TryCast<RectTransform>();
            var cornerImage = corner == null ? null : corner.GetComponent<Image>();
            if (corner != null && cornerImage != null)
            {
                _safeEnd = NewChild("End", _safe);
                var end = _safeEnd.GetComponent<RectTransform>();
                end.anchorMin = corner.anchorMin;
                end.anchorMax = corner.anchorMax;
                end.pivot = corner.pivot;
                end.offsetMin = corner.offsetMin;
                end.offsetMax = corner.offsetMax;
                Copy(cornerImage, _safeEnd.AddComponent<Image>());
            }
        }
        else
        {
            ModLog.Warn("BeltHud: the health bar has no fill to copy; no orange zone.");
        }

        var markerObject = NewChild("ARC_PotionMarker", fillArea ?? barRect);
        _marker = markerObject.GetComponent<RectTransform>();
        var line = markerObject.AddComponent<Image>();
        line.color = Amber;
        line.raycastTarget = false;

        ModLog.Info(FormattableString.Invariant(
            $"BeltHud: built on {Ui.Path(barRect)} (bar {barRect.rect.width:0}x{barRect.rect.height:0}, slot {size:0}, orange zone {(_safe != null ? "on" : "off")}, end piece {(_safeEnd != null ? "on" : "off")})."));
        return true;
    }

    /// <summary>Gives <paramref name="target"/> the same sprite and draw settings as the game's image, recoloured orange.</summary>
    private static void Copy(Image source, Image target)
    {
        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
        target.material = source.material;
        target.raycastTarget = false;
        var recoloured = source.sprite == null ? null : Recolour(source.sprite);
        if (recoloured != null)
        {
            target.sprite = recoloured;
            target.color = source.color;
        }
        else
        {
            target.sprite = source.sprite; // fallback: the game's own art with a yellow tint
            target.color = new Color(1f, 0.9f, 0.3f, source.color.a);
        }
    }

    /// <summary>A copy of a game sprite with its hue turned towards amber; brightness and alpha (the shading) stay.</summary>
    private static Sprite? Recolour(Sprite sprite)
    {
        RenderTexture? rt = null;
        var previous = RenderTexture.active;
        try
        {
            ModLog.StepOnce("belt-recolour", "BeltHud: first sprite recolour");
            var atlas = sprite.texture;
            var r = sprite.rect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            rt = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(atlas, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var c = tex.GetPixel(x, y);
                    Color.RGBToHSV(c, out var hue, out var sat, out var val);
                    var shifted = Color.HSVToRGB(Mathf.Repeat(hue + HueShift, 1f), sat, val);
                    shifted.a = c.a;
                    tex.SetPixel(x, y, shifted);
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = atlas.filterMode;
            tex.hideFlags = HideFlags.HideAndDontSave;
            var copy = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, sprite.border);
            copy.name = "ARC_" + sprite.name;
            copy.hideFlags = HideFlags.HideAndDontSave;
            return copy;
        }
        catch (Exception ex)
        {
            ModLog.WarnOnce("belt-recolour-failed", $"BeltHud: could not recolour '{sprite.name}': {ex.Message}");
            return null;
        }
        finally
        {
            RenderTexture.active = previous;
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
        }
    }

    private static GameObject NewChild(string name, RectTransform parent)
    {
        var child = new GameObject(name);
        var rect = child.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        return child;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
