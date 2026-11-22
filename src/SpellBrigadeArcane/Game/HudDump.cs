namespace SpellBrigadeArcane.Game;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Debug aid (Ctrl+7 with Debug.TestHotkeys): writes how the game's health bar is built right now to
/// BepInEx/SpellBrigadeArcane-art/hud-N.txt — every object under the health display with its rectangle, anchors,
/// screen corners and image details, the canvas scaling, and the bar's textures as PNG. Read-only.
/// </summary>
internal static class HudDump
{
    private static int _count;

    public static void Write()
    {
        ModLog.Step("HudDump: writing the health bar's layout");
        var display = Object.FindObjectOfType(Il2CppType.Of<PlayerHealthDisplay>())?.TryCast<PlayerHealthDisplay>();
        if (display == null)
        {
            ModLog.Info("HudDump: no health display on screen (Ctrl+7 works in a mission).");
            return;
        }

        Directory.CreateDirectory(ArtDump.Folder);
        var lines = new List<string>
        {
            $"Health bar dump {++_count}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}, screen {Screen.width}x{Screen.height}",
        };

        var bar = display.bar;
        if (bar != null)
        {
            lines.Add(F($"slider: value {bar.value} normalized {bar.normalizedValue} range {bar.minValue}..{bar.maxValue} direction {bar.direction} wholeNumbers {bar.wholeNumbers}"));
            lines.Add($"slider.fillRect: {(bar.fillRect == null ? "null" : Ui.Path(bar.fillRect))}");
            lines.Add($"slider.handleRect: {(bar.handleRect == null ? "null" : Ui.Path(bar.handleRect))}");
        }

        lines.Add($"leftCorner: {(display.leftCorner == null ? "null" : Ui.Path(display.leftCorner.transform) + " active=" + display.leftCorner.activeInHierarchy)}");
        lines.Add($"rightCorner: {(display.rightCorner == null ? "null" : Ui.Path(display.rightCorner.transform) + " active=" + display.rightCorner.activeInHierarchy)}");

        var canvas = display.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            var root = canvas.rootCanvas;
            lines.Add(F($"canvas '{root.name}': renderMode {root.renderMode} scaleFactor {root.scaleFactor} referencePixelsPerUnit {root.referencePixelsPerUnit} size {root.pixelRect.width}x{root.pixelRect.height}"));
            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null)
                lines.Add(F($"canvasScaler: mode {scaler.uiScaleMode} reference {scaler.referenceResolution.x}x{scaler.referenceResolution.y} match {scaler.matchWidthOrHeight} screenMatch {scaler.screenMatchMode}"));
        }

        lines.Add("");
        lines.Add("Objects (depth-first from the health display; rect = local size; corners = screen px, bottom-left and top-right):");
        Describe(display.transform, 0, lines);
        var file = Path.Combine(ArtDump.Folder, $"hud-{_count}.txt");
        File.WriteAllLines(file, lines);
        ModLog.Info($"HudDump: wrote {file}");
    }

    private static void Describe(Transform t, int depth, List<string> lines)
    {
        var pad = new string(' ', depth * 2);
        var rect = t.TryCast<RectTransform>();
        var line = $"{pad}'{t.gameObject.name}' active={t.gameObject.activeInHierarchy} [{Ui.Components(t)}]";
        if (rect != null)
        {
            var r = rect.rect;
            var low = rect.TransformPoint(new Vector3(r.xMin, r.yMin, 0f));
            var high = rect.TransformPoint(new Vector3(r.xMax, r.yMax, 0f));
            line += F($" rect {r.width:0.##}x{r.height:0.##} anchors ({rect.anchorMin.x:0.###},{rect.anchorMin.y:0.###})-({rect.anchorMax.x:0.###},{rect.anchorMax.y:0.###}) offsets ({rect.offsetMin.x:0.##},{rect.offsetMin.y:0.##})/({rect.offsetMax.x:0.##},{rect.offsetMax.y:0.##}) pivot ({rect.pivot.x:0.##},{rect.pivot.y:0.##}) scale {rect.localScale.x:0.###} rot {rect.localEulerAngles.z:0.#} corners ({low.x:0.#},{low.y:0.#})-({high.x:0.#},{high.y:0.#})");
        }

        lines.Add(line);
        var image = t.GetComponent<Image>();
        if (image != null)
        {
            var sprite = image.sprite;
            var info = F($"{pad}  image: enabled {image.enabled} type {image.type} fillMethod {image.fillMethod} fillAmount {image.fillAmount:0.###} fillOrigin {image.fillOrigin} preserveAspect {image.preserveAspect} colour ({image.color.r:0.###},{image.color.g:0.###},{image.color.b:0.###},{image.color.a:0.###}) ppuMultiplier {image.pixelsPerUnitMultiplier:0.##} material '{(image.material == null ? "none" : image.material.name)}'");
            lines.Add(info);
            if (sprite != null)
            {
                var padding = UnityEngine.Sprites.DataUtility.GetPadding(sprite);
                lines.Add(F($"{pad}  sprite '{sprite.name}' tex '{(sprite.texture == null ? "none" : sprite.texture.name)}' rect ({sprite.rect.x:0.#},{sprite.rect.y:0.#} {sprite.rect.width:0.#}x{sprite.rect.height:0.#}) border ({sprite.border.x:0},{sprite.border.y:0},{sprite.border.z:0},{sprite.border.w:0}) padding ({padding.x:0.#},{padding.y:0.#},{padding.z:0.#},{padding.w:0.#}) ppu {sprite.pixelsPerUnit:0.#} pivot ({sprite.pivot.x:0.#},{sprite.pivot.y:0.#})"));
                ArtDump.DumpTexture(sprite, lines);
            }
        }

        var mask = t.GetComponent<Mask>();
        if (mask != null) lines.Add($"{pad}  mask: enabled {mask.enabled} showMaskGraphic {mask.showMaskGraphic}");
        var rectMask = t.GetComponent<RectMask2D>();
        if (rectMask != null) lines.Add(F($"{pad}  rectMask2D: padding ({rectMask.padding.x:0.#},{rectMask.padding.y:0.#},{rectMask.padding.z:0.#},{rectMask.padding.w:0.#})"));
        var group = t.GetComponent<CanvasGroup>();
        if (group != null) lines.Add(F($"{pad}  canvasGroup: alpha {group.alpha:0.###}"));
        var animator = t.GetComponent<Animator>();
        if (animator != null) lines.Add($"{pad}  animator: enabled {animator.enabled} controller '{(animator.runtimeAnimatorController == null ? "none" : animator.runtimeAnimatorController.name)}'");

        for (var i = 0; i < t.childCount; i++) Describe(t.GetChild(i), depth + 1, lines);
    }

    private static string F(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
