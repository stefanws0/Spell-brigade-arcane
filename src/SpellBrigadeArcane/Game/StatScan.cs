namespace SpellBrigadeArcane.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Spec build step 1: once per mission (per local <c>GameplayPlayerIdentity</c> and scene), 3 s after its body exists,
/// writes the wizard's stats and each equipped spell's values for the stats the sixth row will raise. Read-only.
/// </summary>
internal static class StatScan
{
    private const float Interval = 1f;
    private const float SettleSeconds = 3f;
    private const int MaxSections = 5;

    private static readonly string[] Interesting =
    {
        "Speed", "Range", "Projectiles", "ElementStrengthMultiplier", "ElementWeaknessMultiplier",
        "HealthPotionAmountMultiplier", "DropChance", "Damage", "MaxHealth", "Size", "FireRate",
    };

    private static readonly HashSet<string> Scanned = new();
    private static string? _pending;
    private static float _pendingSince;
    private static float _next;
    private static int _sections;

    public static void Tick(float now)
    {
        if (now < _next || _sections >= MaxSections) return;
        _next = now + Interval;
        ModLog.StepOnce("stat-find", "StatScan: first FindObjectsOfType");
        var identity = FindLocalIdentity();
        // Wait until the wizard's body exists, not just its identity.
        if (identity == null || identity.currentGameplayPlayer == null)
        {
            _pending = null;
            return;
        }

        var scene = SceneManager.GetActiveScene().name;
        var key = $"{identity.GetInstanceID()}@{scene}";
        if (Scanned.Contains(key)) return;
        if (_pending != key)
        {
            _pending = key;
            _pendingSince = now;
            return;
        }

        if (now - _pendingSince < SettleSeconds) return;
        Scanned.Add(key);
        _sections++;
        ModLog.Step($"StatScan: local wizard found in '{scene}', scanning stats");
        var lines = new List<string>();
        var stats = identity.stats;
        ScanReport.Guard(lines, "StatScan: header", () =>
            lines.Add($"Wizard '{Ui.Try(() => identity.GetDisplayName())}' identity 0x{identity.Pointer.ToInt64():X} scene '{scene}'"));
        ScanReport.Guard(lines, "StatScan: stats and modifiers", () => DescribeStats(stats, lines));
        ScanReport.Guard(lines, "StatScan: character values", () => DescribeCharacter(stats, lines));
        ScanReport.Guard(lines, "StatScan: spells", () => DescribeSpells(stats, lines));
        ScanReport.Guard(lines, "StatScan: events", () =>
            lines.Add($"OnHealthPotionCollected listeners: {Ui.Try(() => Ui.Listeners(LocalEvents.OnHealthPotionCollected?.OnAction))}"));
        ScanReport.Section($"Wizard stats at mission start [{scene}]", lines);
    }

    private static GameplayPlayerIdentity? FindLocalIdentity()
    {
        foreach (var obj in Object.FindObjectsOfType(Il2CppType.Of<GameplayPlayerIdentity>()))
        {
            var identity = obj?.TryCast<GameplayPlayerIdentity>();
            if (identity != null && identity.IsOwner) return identity;
        }

        return null;
    }

    private static void DescribeStats(PlayerStats stats, List<string> lines)
    {
        var list = stats.GetPlayerStats();
        for (var i = 0; i < list.Count; i++)
        {
            var s = list[i];
            lines.Add($"stat {s.StatType}: base={s.BaseValue} calc={s.StatCalculationType} modifiers={s.Modifiers?.Count} value={Ui.Try(() => stats.GetCharacterValue(s.StatType))}");
            var mods = s.Modifiers;
            if (mods == null || mods.Count == 0) continue;
            lines.Add($"  sumOfModifiers={Ui.Try(() => s.GetSumOfModifiers())} sumOfNonStarting={Ui.Try(() => s.GetSumOfNonStartingModifiers())} display={Ui.Try(() => s.GetDisplayValue())}");
            for (var m = 0; m < mods.Count; m++)
            {
                var mod = mods[m];
                lines.Add($"  modifier {m}: {Ui.ClassName(mod)} value={Ui.Try(() => mod.GetValue())} '{Ui.Try(() => mod.ToString())}'");
            }
        }
    }

    private static void DescribeCharacter(PlayerStats stats, List<string> lines)
    {
        foreach (var name in Interesting)
        {
            var stat = (StatType)Enum.Parse(typeof(StatType), name);
            lines.Add($"character {name} = {Ui.Try(() => stats.GetCharacterValue(stat))} improved={Ui.Try(() => stats.HasAnyCharacterImprovementFor(stat))}");
        }
    }

    private static void DescribeSpells(PlayerStats stats, List<string> lines)
    {
        var spells = stats.GetEquippedSpells();
        for (var i = 0; i < spells.Count; i++)
        {
            var spell = spells[i];
            var values = Interesting.Select(name =>
            {
                var stat = (StatType)Enum.Parse(typeof(StatType), name);
                return $"{name}={Ui.Try(() => stats.GetSpellValue(spell, stat))}";
            });
            lines.Add($"spell {spell} element={Ui.Try(() => stats.GetElement(spell))} level={Ui.Try(() => stats.GetLevel(spell))}: {string.Join(" ", values)}");
        }
    }
}
