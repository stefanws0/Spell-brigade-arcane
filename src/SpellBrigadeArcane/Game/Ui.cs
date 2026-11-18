namespace SpellBrigadeArcane.Game;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;

/// <summary>Read-only helpers to describe game objects in the scan report.</summary>
internal static class Ui
{
    /// <summary>The native IL2CPP class name of any game object (e.g. "UpgradeTierUI").</summary>
    public static string ClassName(Il2CppObjectBase obj)
    {
        var klass = IL2CPP.il2cpp_object_get_class(obj.Pointer);
        return klass == IntPtr.Zero ? "?" : Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(klass)) ?? "?";
    }

    /// <summary>"Canvas/Panel/Row" style path, at most 7 levels.</summary>
    public static string Path(Transform t)
    {
        var parts = new List<string>();
        for (var cur = t; cur != null && parts.Count < 7; cur = cur.parent) parts.Add(cur.gameObject.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    /// <summary>Where a UI element sits on screen (world space of its canvas), its local size and anchored position.</summary>
    public static string Span(Transform t)
    {
        var rt = t.TryCast<RectTransform>();
        if (rt == null) return "(no RectTransform)";
        var r = rt.rect;
        var low = rt.TransformPoint(new Vector3(r.x, r.y, 0f));
        var high = rt.TransformPoint(new Vector3(r.x + r.width, r.y + r.height, 0f));
        return $"[x {low.x:0.##}..{high.x:0.##}, y {low.y:0.##}..{high.y:0.##}, local {r.width:0}x{r.height:0}, anchored ({rt.anchoredPosition.x:0},{rt.anchoredPosition.y:0})]";
    }

    public static string Components(Transform t)
    {
        var parts = new List<string>();
        foreach (var c in t.GetComponents<Component>())
        {
            if (c != null) parts.Add(ClassName(c));
        }

        return string.Join(", ", parts);
    }

    /// <summary>Runs a getter for the report: its value as text, "null", or "!" plus the error (never throws).</summary>
    public static string Try(Func<object?> getter)
    {
        try
        {
            return getter()?.ToString() ?? "null";
        }
        catch (Exception ex)
        {
            return $"!{ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>All TMP texts (with their path and visibility) and localized-string references under a transform.</summary>
    public static void Texts(Transform root, List<string> lines, string indent)
    {
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            lines.Add($"{indent}text {Path(text.transform)}: '{text.text}' active={text.gameObject.activeInHierarchy} " +
                      $"colorA={Try(() => text.color.a)} groupAlpha={Try(() => text.GetComponentInParent<CanvasGroup>()?.alpha)}");
        foreach (var loc in root.GetComponentsInChildren<LocalizeStringEvent>(true))
            lines.Add($"{indent}localized {Path(loc.transform)}: {Try(() => loc.StringReference?.ToString())}");
    }

    /// <summary>Who is subscribed to a game event: "Target.Method (DeclaringType)" per listener.</summary>
    public static string Listeners(Il2CppSystem.Delegate? action)
    {
        if (action == null) return "none";
        var names = new List<string>();
        foreach (var d in action.GetInvocationList())
        {
            var target = d.Target == null ? "static" : ClassName(d.Target);
            names.Add($"{target}.{Try(() => d.Method?.Name)} ({Try(() => d.Method?.DeclaringType?.FullName)})");
        }

        return string.Join(", ", names);
    }
}
