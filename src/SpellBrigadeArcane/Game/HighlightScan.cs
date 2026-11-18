namespace SpellBrigadeArcane.Game;

using System.Collections.Generic;

/// <summary>
/// While the Enchantments screen is open, notes each newly highlighted enchantment (max 8 per session) together with
/// what the details panel shows for it, to learn the rule behind the Mastery reveal. Read-only.
/// </summary>
internal static class HighlightScan
{
    private const float Interval = 0.25f;
    private const int MaxHighlights = 8;

    private static readonly HashSet<string> Seen = new();
    private static string? _pending;
    private static int _pendingTicks;
    private static float _next;

    /// <summary>All highlight sections of this session are written.</summary>
    public static bool IsFinished => Seen.Count >= MaxHighlights;

    public static void Tick(float now, UpgradeBuyerUI? buyer)
    {
        if (now < _next) return;
        _next = now + Interval;
        if (buyer == null || !buyer.IsOpen || Seen.Count >= MaxHighlights)
        {
            _pending = null;
            return;
        }

        var cfg = buyer.currentHighlightedUpgradeButton?.configuration;
        // Fallback: a change of the details panel's name text also counts as a new highlight.
        var shownName = Ui.Try(() => buyer.upgradeDetailsUI?.nameText?.text);
        if (cfg == null && (shownName == "null" || shownName.StartsWith("!") || shownName.Length == 0))
        {
            _pending = null;
            return;
        }

        var key = $"{cfg?.name}|{shownName}";
        if (Seen.Contains(key)) return;
        if (_pending != key)
        {
            _pending = key; // wait one more poll so the details panel has refreshed
            _pendingTicks = 1;
            return;
        }

        if (++_pendingTicks < 2) return;
        Seen.Add(key);
        _pending = null;
        var lines = new List<string>();
        ScanReport.Guard(lines, $"HighlightScan: {key}", () => Describe(buyer, cfg, shownName, lines));
        ScanReport.Section($"Highlighted {Ui.Try(() => cfg?.Name ?? shownName)}", lines);
    }

    private static void Describe(UpgradeBuyerUI buyer, UpgradeConfiguration? cfg, string shownName, List<string> lines)
    {
        if (cfg == null)
        {
            lines.Add($"details name text='{shownName}' (no highlighted button configuration)");
        }
        else
        {
            var manager = SingletonPersistent<LocalPlayer>.Instance.GetUpgradeManager();
            var id = cfg.Identifier;
            lines.Add($"details name text='{shownName}'; '{cfg.name}' id={id} level {Ui.Try(() => manager.GetLevel(id))}/{Ui.Try(() => manager.GetMaxLevel(id))} " +
                      $"maxed={Ui.Try(() => manager.IsMaxLevel(id))} levels={cfg.UpgradeLevels?.Count}");
        }

        var details = buyer.upgradeDetailsUI;
        if (details == null)
        {
            lines.Add("No details panel.");
            return;
        }

        lines.Add($"LevelText='{Ui.Try(() => details.LevelText)}' Parameter0='{Ui.Try(() => details.Parameter0)}' Parameter1='{Ui.Try(() => details.Parameter1)}'");
        Ui.Texts(details.transform, lines, "  ");
    }
}
