namespace SpellBrigadeArcane.Game;

using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Debug aid ([Debug] DumpArt): writes the game's enchantment art and how the buttons draw it to
/// BepInEx/SpellBrigadeArcane-art/ — every enchantment's icon and glow texture as PNG, plus a report of one Mastered game
/// button, one not-Mastered game button and our four (images, colours, sizes, animator values). Read-only: it copies
/// textures through a temporary RenderTexture and changes nothing on screen. Runs each time the Enchantments screen has
/// been open for 1.5 s (textures only the first time), at most 3 times per session.
/// </summary>
internal static class ArtDump
{
    private const float OpenDelay = 1.5f;
    private const int MaxReports = 3;

    private static readonly HashSet<string> DumpedTextures = new();
    private static float _nextFind;
    private static UpgradeBuyerUI? _buyer;
    private static float _openSince = -1f;
    private static bool _reportedThisOpen;
    private static int _reports;

    public static string Folder => Path.Combine(Paths.BepInExRootPath, "SpellBrigadeArcane-art");

    public static void Tick(float now)
    {
        if (_reports >= MaxReports) return;
        if (_buyer == null)
        {
            if (now < _nextFind) return;
            _nextFind = now + 2f;
            foreach (var obj in Object.FindObjectsOfType(Il2CppType.Of<UpgradeBuyerUI>(), true))
            {
                _buyer = obj?.TryCast<UpgradeBuyerUI>();
                if (_buyer != null) break;
            }

            if (_buyer == null) return;
        }

        if (!_buyer.IsOpen)
        {
            _openSince = -1f;
            _reportedThisOpen = false;
            return;
        }

        if (_openSince < 0f) _openSince = now;
        if (_reportedThisOpen || now - _openSince < OpenDelay || !GameUpgrades.IsReady) return;
        _reportedThisOpen = true;
        _reports++;
        ModLog.Step($"ArtDump: report {_reports}");
        Report(_buyer, _reports);
    }

    private static void Report(UpgradeBuyerUI buyer, int number)
    {
        Directory.CreateDirectory(Folder);
        var lines = new List<string> { $"Art dump {number}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}" };

        if (number == 1)
        {
            lines.Add("");
            lines.Add("== Enchantment sprites (rects in texture pixels, origin bottom-left)");
            foreach (UpgradeIdentifier id in Enum.GetValues(typeof(UpgradeIdentifier)))
            {
                var config = Safe(() => GameUpgrades.Configuration(id));
                if (config == null)
                {
                    lines.Add($"{id}: no configuration");
                    continue;
                }

                lines.Add($"{id}: icon {Describe(config.Icon)}");
                lines.Add($"{id}: glow {Describe(config.Glow)}");
                DumpTexture(config.Icon, lines);
                DumpTexture(config.Glow, lines);
            }
        }

        UpgradeButton? mastered = null, normal = null;
        foreach (var tier in buyer.tiers)
        {
            if (tier == null) continue;
            foreach (var button in tier.upgradeButtons)
            {
                if (button == null) continue;
                if (button.maxLevelAchieved) mastered ??= button;
                else normal ??= button;
            }
        }

        if (mastered != null) DescribeButton("game button, Mastered", mastered, lines, number == 1);
        if (normal != null) DescribeButton("game button, not Mastered", normal, lines, number == 1);
        var column = buyer.tiers[buyer.tiers.Count - 1]?.transform.parent?.Find(ArcaneColumn.ColumnName);
        if (column != null)
        {
            foreach (var ours in column.GetComponentsInChildren<UpgradeButton>(true))
                DescribeButton("our button " + ours.gameObject.name, ours, lines, false);
        }
        else
        {
            lines.Add("our column: not found");
        }

        var file = Path.Combine(Folder, $"report{number}.txt");
        File.WriteAllLines(file, lines);
        ModLog.Info($"ArtDump: wrote {file} ({DumpedTextures.Count} texture(s) so far)");
    }

    private static void DescribeButton(string label, UpgradeButton button, List<string> lines, bool dumpTextures)
    {
        lines.Add("");
        lines.Add($"== {label}: {Ui.Path(button.transform)}");
        lines.Add($"  canAfford={button.canAfford} maxLevelAchieved={button.maxLevelAchieved} config={Safe(() => button.configuration?.name) ?? "null"}");
        lines.Add($"  iconImage={Safe(() => Ui.Path(button.iconImage.transform)) ?? "null"} iconGlow={Safe(() => Ui.Path(button.iconGlow.transform)) ?? "null"}");

        var animator = button.Animator;
        if (animator != null)
        {
            lines.Add($"  animator enabled={animator.isActiveAndEnabled} controller={Safe(() => animator.runtimeAnimatorController?.name) ?? "null"} " +
                      $"state={Safe(() => (object)animator.GetCurrentAnimatorStateInfo(0).fullPathHash)} t={Safe(() => (object)animator.GetCurrentAnimatorStateInfo(0).normalizedTime)}");
            foreach (var p in animator.parameters)
            {
                var value = p.type switch
                {
                    AnimatorControllerParameterType.Bool => animator.GetBool(p.nameHash).ToString(),
                    AnimatorControllerParameterType.Int => animator.GetInteger(p.nameHash).ToString(),
                    AnimatorControllerParameterType.Float => animator.GetFloat(p.nameHash).ToString("0.###"),
                    _ => "(trigger)",
                };
                lines.Add($"  param {p.name} ({p.type}) = {value}");
            }
        }

        foreach (var group in button.GetComponentsInChildren<CanvasGroup>(true))
            lines.Add($"  canvasGroup {Rel(button, group.transform)} alpha={group.alpha:0.###}");

        foreach (var image in button.GetComponentsInChildren<Image>(true))
        {
            var rt = image.rectTransform;
            var role = image.Pointer == button.iconImage?.Pointer ? " [iconImage]" : image.Pointer == button.iconGlow?.Pointer ? " [iconGlow]" : "";
            lines.Add($"  image {Rel(button, image.transform)}{role}: active={image.gameObject.activeInHierarchy} enabled={image.enabled} " +
                      $"color={Color(image.color)} canvasAlpha={Safe(() => (object)image.canvasRenderer.GetAlpha())} size={rt.rect.width:0.#}x{rt.rect.height:0.#} " +
                      $"pos=({rt.anchoredPosition.x:0.#},{rt.anchoredPosition.y:0.#}) scale={rt.localScale.x:0.###} type={image.type} " +
                      $"preserveAspect={image.preserveAspect} material={Safe(() => image.material?.name) ?? "null"} sprite {Describe(image.sprite)}");
            if (dumpTextures) DumpTexture(image.sprite, lines);
        }
    }

    private static string Rel(UpgradeButton button, Transform t)
    {
        var parts = new List<string>();
        for (var cur = t; cur != null && cur.Pointer != button.transform.Pointer; cur = cur.parent) parts.Add(cur.gameObject.name);
        parts.Reverse();
        return parts.Count == 0 ? "." : string.Join("/", parts);
    }

    private static string Describe(Sprite? sprite)
    {
        if (sprite == null) return "null";
        var tex = sprite.texture;
        var textureRect = Safe(() => (object)RectText(sprite.textureRect)) ?? "n/a";
        return $"'{sprite.name}' tex='{tex?.name}' {tex?.width}x{tex?.height} rect={RectText(sprite.rect)} textureRect={textureRect} " +
               $"pivot=({sprite.pivot.x:0.#},{sprite.pivot.y:0.#}) ppu={sprite.pixelsPerUnit:0.#} packed={sprite.packed} " +
               $"border=({sprite.border.x:0},{sprite.border.y:0},{sprite.border.z:0},{sprite.border.w:0})";
    }

    private static string RectText(Rect r) => $"({r.x:0.#},{r.y:0.#} {r.width:0.#}x{r.height:0.#})";

    private static string Color(Color c) => $"({c.r:0.###},{c.g:0.###},{c.b:0.###},{c.a:0.###})";

    /// <summary>Saves a sprite's whole texture (an atlas, possibly) once, as &lt;texture name&gt;.png.</summary>
    internal static void DumpTexture(Sprite? sprite, List<string> lines)
    {
        var tex = sprite?.texture;
        if (tex == null) return;
        var name = string.IsNullOrEmpty(tex.name) ? "texture_" + tex.GetInstanceID() : tex.name;
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        if (!DumpedTextures.Add(name)) return;

        RenderTexture? rt = null;
        Texture2D? copy = null;
        var previous = RenderTexture.active;
        try
        {
            rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            copy.Apply();
            var png = ImageConversion.EncodeToPNG(copy);
            var bytes = new byte[png.Length];
            for (var i = 0; i < bytes.Length; i++) bytes[i] = png[i];
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), bytes);
        }
        catch (Exception ex)
        {
            lines.Add($"  ! texture {name} not saved: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            RenderTexture.active = previous;
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
            if (copy != null) Object.Destroy(copy);
        }
    }

    private static T? Safe<T>(Func<T?> getter) where T : class
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }
}
