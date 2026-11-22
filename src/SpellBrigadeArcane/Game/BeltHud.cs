namespace SpellBrigadeArcane.Game;

using System;
using Il2CppInterop.Runtime;
using SpellBrigadeArcane.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Potion Belt on screen: a round slot left of the game's health bar (a dark disc with an empty bottle-shaped
/// recess; the potion bottle sits in it while one is stored) and a line on the bar at the drink threshold while a potion is stored. Storing and drinking pop the slot and
/// play the potion's own spawn / pickup sounds. Children of the game's health slider; nothing of the game's is changed.
/// </summary>
internal static class BeltHud
{
    private const float PopSeconds = 0.35f;
    private static readonly Color Amber = new(1f, 0.72f, 0.2f, 1f);
    private static readonly Color Safe = new(1f, 0.62f, 0.1f, 0.6f);

    private static PlayerHealthDisplay? _display;
    private static Slider? _bar;
    private static GameObject? _slot;
    private static Image? _bottle;
    private static RectTransform? _marker;
    private static RectTransform? _safe;
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

        _marker!.gameObject.SetActive(stored);
        if (_safe != null) _safe.gameObject.SetActive(stored && _bar!.direction == Slider.Direction.LeftToRight);
        if (!stored) return;
        var range = _bar!.maxValue - _bar.minValue;
        var t = range > 0f
            ? Mathf.Clamp01((PotionBelt.DefaultThreshold * _bar.maxValue - _bar.minValue) / range)
            : PotionBelt.DefaultThreshold;
        if (_bar.direction == Slider.Direction.RightToLeft) t = 1f - t;
        _marker.anchorMin = new Vector2(t, 0f);
        _marker.anchorMax = new Vector2(t, 1f);
        if (_safe == null) return;
        // The health above the threshold is the part the stored potion protects: tint it, leave the rest red.
        var value = Mathf.Max(t, _bar.normalizedValue);
        _safe.anchorMin = new Vector2(t, 0f);
        _safe.anchorMax = new Vector2(value, 1f);
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

        ModLog.Step("BeltHud: building the potion slot and marker");
        var height = Mathf.Max(barRect.rect.height, 12f);
        var size = Mathf.Max(height * 2.2f, 40f);

        _slot = NewChild("ARC_PotionSlot", barRect);
        var slotRect = _slot.GetComponent<RectTransform>();
        slotRect.anchorMin = new Vector2(0f, 0.5f);
        slotRect.anchorMax = new Vector2(0f, 0.5f);
        slotRect.pivot = new Vector2(1f, 0.5f);
        slotRect.anchoredPosition = new Vector2(-14f, 0f);
        slotRect.sizeDelta = new Vector2(size, size);
        var back = _slot.AddComponent<Image>();
        back.sprite = IconSprites.Named("belt_slot.png") ?? throw new InvalidOperationException("belt_slot.png is not built in");
        back.raycastTarget = false;

        var bottleObject = NewChild("Bottle", slotRect);
        Stretch(bottleObject.GetComponent<RectTransform>(), 0f);
        _bottle = bottleObject.AddComponent<Image>();
        _bottle.sprite = IconSprites.Named("belt_bottle.png") ?? throw new InvalidOperationException("belt_bottle.png is not built in");
        _bottle.raycastTarget = false;

        var fill = _bar!.fillRect;
        var fillArea = fill == null ? null : fill.parent.TryCast<RectTransform>();
        if (fillArea != null)
        {
            var safeObject = NewChild("ARC_PotionSafe", fillArea);
            _safe = safeObject.GetComponent<RectTransform>();
            _safe.offsetMin = Vector2.zero;
            _safe.offsetMax = Vector2.zero;
            var tint = safeObject.AddComponent<Image>();
            tint.color = Safe;
            tint.raycastTarget = false;
        }

        var markerObject = NewChild("ARC_PotionMarker", barRect);
        _marker = markerObject.GetComponent<RectTransform>();
        _marker.pivot = new Vector2(0.5f, 0.5f);
        _marker.anchoredPosition = Vector2.zero;
        _marker.sizeDelta = new Vector2(4f, height * 0.6f);
        var line = markerObject.AddComponent<Image>();
        line.color = Amber;
        line.raycastTarget = false;

        ModLog.Info(FormattableString.Invariant(
            $"BeltHud: slot and marker added to {Ui.Path(barRect)} (bar {barRect.rect.width:0}x{barRect.rect.height:0}, direction {_bar!.direction}, range {_bar.minValue:0.##}..{_bar.maxValue:0.##}, slot {size:0})."));
        return true;
    }

    private static GameObject NewChild(string name, RectTransform parent)
    {
        var child = new GameObject(name);
        var rect = child.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        return child;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
