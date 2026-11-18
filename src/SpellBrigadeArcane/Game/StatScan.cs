namespace SpellBrigadeArcane.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Object = UnityEngine.Object;

/// <summary>
/// Spec build step 1: once per mission (per local <c>GameplayPlayerIdentity</c>), 3 s after it appears, writes the
/// wizard's stats and each equipped spell's values for the stats the sixth row will raise. Read-only.
/// </summary>
internal static class StatScan
{
    private const float Interval = 1f;
    private const float SettleSeconds = 3f;

    private static readonly string[] Interesting =
    {
        "Speed", "Range", "Projectiles", "ElementStrengthMultiplier", "ElementWeaknessMultiplier",
        "HealthPotionAmountMultiplier", "DropChance", "Damage", "MaxHealth",
    };

    private static readonly HashSet<IntPtr> Scanned = new();
    private static IntPtr _pending;
    private static float _pendingSince;
    private static float _next;

    public static void Tick(float now)
    {
        if (now < _next) return;
        _next = now + Interval;
        var identity = FindLocalIdentity();
        if (identity == null || Scanned.Contains(identity.Pointer)) return;
        if (_pending != identity.Pointer)
        {
            _pending = identity.Pointer;
            _pendingSince = now;
            return;
        }

        if (now - _pendingSince < SettleSeconds) return;
        Scanned.Add(identity.Pointer);
        ModLog.Step("StatScan: local wizard found, scanning stats");
        var lines = new List<string>();
        try
        {
            Describe(identity, lines);
        }
        catch (Exception ex)
        {
            lines.Add($"!! failed: {ex.GetType().Name}: {ex.Message}");
            ModLog.Warn($"StatScan: {ex}");
        }

        ScanReport.Section("Wizard stats at mission start", lines);
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

    private static void Describe(GameplayPlayerIdentity identity, List<string> lines)
    {
        var stats = identity.stats;
        lines.Add($"Wizard '{Ui.Try(() => identity.GetDisplayName())}' identity 0x{identity.Pointer.ToInt64():X}");

        var list = stats.GetPlayerStats();
        for (var i = 0; i < list.Count; i++)
        {
            var s = list[i];
            lines.Add($"stat {s.StatType}: base={s.BaseValue} calc={s.StatCalculationType} modifiers={s.Modifiers?.Count} value={Ui.Try(() => stats.GetCharacterValue(s.StatType))}");
        }

        foreach (var name in Interesting)
        {
            var stat = (StatType)Enum.Parse(typeof(StatType), name);
            lines.Add($"character {name} = {Ui.Try(() => stats.GetCharacterValue(stat))} improved={Ui.Try(() => stats.HasAnyCharacterImprovementFor(stat))}");
        }

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
