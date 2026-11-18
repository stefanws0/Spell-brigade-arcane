namespace SpellBrigadeArcane.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Spec build step 1: writes what the sixth row needs to know about the game's Enchantments screen to the scan report,
/// each time the screen is open and settled (max 3 scans per session). Also drives <see cref="HighlightScan"/>. Read-only.
/// </summary>
internal static class EnchantmentScan
{
    private const float Interval = 1f;
    private const int MaxScans = 3;
    private static float _next;
    private static int _scans;
    private static int _readyTicks;
    private static bool _scannedThisOpen;
    private static UpgradeBuyerUI? _buyer;
    private static string? _lastState;
    private static bool _done;

    public static void Tick(float now)
    {
        // Everything this scan will ever write is written: no more FindObjectsOfType for the rest of the session.
        if (_done) return;
        if (now >= _next)
        {
            _next = now + Interval;
            Poll();
        }

        try
        {
            HighlightScan.Tick(now, _buyer);
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("highlight-scan", ex, now);
        }

        if (_scans >= MaxScans && HighlightScan.IsFinished)
        {
            _done = true;
            _buyer = null;
            ModLog.Info("EnchantmentScan: done, polling stopped");
        }
    }

    private static void Poll()
    {
        var buyer = FindBuyer();
        _buyer = buyer;
        if (buyer != null) LogState(buyer);
        if (buyer == null || !buyer.IsOpen)
        {
            // Screen closed: the next time it opens, scan again (up to MaxScans per session).
            _scannedThisOpen = false;
            _readyTicks = 0;
            return;
        }

        if (_scannedThisOpen || _scans >= MaxScans) return;
        var group = buyer.GetComponent<CanvasGroup>();
        if (group != null && group.alpha < 0.99f)
        {
            _readyTicks = 0;
            return;
        }

        if (++_readyTicks < 2) return; // settled: ready on two consecutive ticks
        _scannedThisOpen = true;
        _scans++;
        ModLog.Step($"EnchantmentScan: Enchantments screen open and settled, scan {_scans}");
        var lines = new List<string>();
        ScanReport.Guard(lines, "EnchantmentScan: screen", () => DescribeScreen(buyer, lines));
        ScanReport.Guard(lines, "EnchantmentScan: rows", () => DescribeRows(buyer, lines));
        ScanReport.Guard(lines, "EnchantmentScan: levels and prices", () => DescribeManager(buyer, lines));
        ScanReport.Guard(lines, "EnchantmentScan: details panel", () => DescribeDetails(buyer, lines));
        ScanReport.Guard(lines, "EnchantmentScan: local player save flags", () => DescribeSaveFlags(lines));
        ScanReport.Guard(lines, "EnchantmentScan: events", () => DescribeEvents(lines));
        ScanReport.Section($"Enchantments screen (scan {_scans})", lines);
    }

    /// <summary>Logs the buyer's (IsOpen, active, alpha ok) state the first time it is found and whenever it changes.</summary>
    private static void LogState(UpgradeBuyerUI buyer)
    {
        var group = buyer.GetComponent<CanvasGroup>();
        var alpha = group != null ? group.alpha : 1f;
        var state = $"IsOpen={buyer.IsOpen} active={buyer.isActiveAndEnabled} alphaOk={alpha >= 0.99f}";
        if (state == _lastState) return; // alpha itself is not part of the key: it changes every tick while fading
        ModLog.Info(_lastState == null ? $"EnchantmentScan: configured buyer found, {state} (alpha={alpha})" : $"EnchantmentScan: buyer state {state} (alpha={alpha})");
        _lastState = state;
    }

    /// <summary>The first active, fully set-up buyer (it may still be closed or animating).</summary>
    private static UpgradeBuyerUI? FindBuyer()
    {
        ModLog.StepOnce("enchant-find", "EnchantmentScan: first FindObjectsOfType");
        foreach (var obj in Object.FindObjectsOfType(Il2CppType.Of<UpgradeBuyerUI>(), true))
        {
            var buyer = obj?.TryCast<UpgradeBuyerUI>();
            if (buyer == null || !buyer.isActiveAndEnabled) continue;
            var tiers = buyer.tiers;
            if (tiers == null || tiers.Count == 0) continue;
            var buttons = tiers[0].upgradeButtons;
            if (buttons == null || buttons.Count == 0 || buttons[0].configuration == null) continue;
            return buyer;
        }

        return null;
    }

    private static void DescribeScreen(UpgradeBuyerUI buyer, List<string> lines)
    {
        ScanReport.Guard(lines, "screen: header", () =>
            lines.Add($"Screen {Screen.width}x{Screen.height}; buyer at {Ui.Path(buyer.transform)} {Ui.Span(buyer.transform)} " +
                      $"IsOpen={buyer.IsOpen} lossyScale={buyer.transform.lossyScale}"));
        Canvas? canvas = null;
        ScanReport.Guard(lines, "screen: canvas and group", () =>
        {
            canvas = buyer.GetComponentInParent<Canvas>();
            if (canvas != null) lines.Add($"Canvas '{canvas.name}' renderMode={canvas.renderMode} scaleFactor={canvas.scaleFactor}");
            var group = buyer.GetComponent<CanvasGroup>();
            if (group != null) lines.Add($"Buyer CanvasGroup alpha={group.alpha} interactable={group.interactable}");
        });

        var tiers = buyer.tiers;
        var parent = tiers[0].transform.parent;
        ScanReport.Guard(lines, "screen: rows' parent and children", () =>
        {
            lines.Add($"Rows' parent: {Ui.Path(parent)} [{Ui.Components(parent)}] {Ui.Span(parent)}");
            var vlg = parent.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
                lines.Add($"  VerticalLayoutGroup spacing={vlg.spacing} padding(l,r,t,b)=({vlg.padding.left},{vlg.padding.right},{vlg.padding.top},{vlg.padding.bottom}) controlHeight={vlg.childControlHeight}");
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                lines.Add($"  child {i}: '{child.name}' active={child.gameObject.activeSelf} [{Ui.Components(child)}] {Ui.Span(child)}");
            }
        });

        // Ancestors of the rows' parent, up to and including the canvas.
        ScanReport.Guard(lines, "screen: ancestors", () =>
        {
            var level = 0;
            for (var anc = parent.parent; anc != null && level < 12; anc = anc.parent, level++)
            {
                lines.Add($"Ancestor +{level + 1}: '{anc.name}' [{Ui.Components(anc)}] {Ui.Span(anc)}");
                if (canvas != null && anc == canvas.transform) break;
            }
        });

        // The rows' parent's siblings (what else lives next to the rows).
        ScanReport.Guard(lines, "screen: siblings", () =>
        {
            var grand = parent.parent;
            if (grand == null) return;
            for (var i = 0; i < grand.childCount; i++)
            {
                var sib = grand.GetChild(i);
                if (sib == parent) continue;
                lines.Add($"Sibling of rows' parent: '{sib.name}' active={sib.gameObject.activeSelf} [{Ui.Components(sib)}] {Ui.Span(sib)}");
            }
        });

        // Row V (the last tier) is the template the sixth row will clone: dump its subtree.
        ScanReport.Guard(lines, "screen: Row V subtree", () =>
        {
            var last = tiers[tiers.Count - 1];
            lines.Add($"Row V subtree ('{last.name}'):");
            Subtree(last.transform, lines, 0);
        });

        ScanReport.Guard(lines, "screen: refund button", () =>
        {
            var refund = buyer.refundButton;
            lines.Add($"Refund button: {(refund != null ? Ui.Path(refund.transform) + " " + Ui.Span(refund.transform) : "none")}");
        });
    }

    private static void Subtree(Transform t, List<string> lines, int depth)
    {
        lines.Add($"{new string(' ', 2 + depth * 2)}'{t.name}' active={t.gameObject.activeSelf} [{Ui.Components(t)}] {Ui.Span(t)}");
        if (depth >= 4) return;
        for (var i = 0; i < t.childCount; i++) Subtree(t.GetChild(i), lines, depth + 1);
    }

    private static void DescribeRows(UpgradeBuyerUI buyer, List<string> lines)
    {
        var tiers = buyer.tiers;
        for (var i = 0; i < tiers.Count; i++)
        {
            var index = i;
            ScanReport.Guard(lines, $"rows: row {index + 1}", () => DescribeRow(tiers[index], index, lines));
        }
    }

    private static void DescribeRow(UpgradeTierUI tier, int i, List<string> lines)
    {
        lines.Add($"Row {i + 1}: '{tier.name}' at {Ui.Path(tier.transform)} {Ui.Span(tier.transform)} [{Ui.Components(tier.transform)}]");
        lines.Add($"  locked={Ui.Try(() => tier.IsLocked)} lockReasons=[{Ui.Try(() => Join(tier.cachedLockReasons))}]");
        var cfg = tier.upgradeTierConfiguration;
        if (cfg != null)
        {
            lines.Add($"  config '{cfg.name}': challengeToUnlock={cfg.challengeToUnlock} lockedByProgression={Ui.Try(() => cfg.IsLockedByProgression())} " +
                      $"comingSoon={Ui.Try(() => cfg.GetIsComingSoon())} inDemo={Ui.Try(() => cfg.GetIsIncludedInDemo())} upgrades={cfg.UpgradesIncludedInTier?.Count}");
        }

        ScanReport.Guard(lines, $"rows: row {i + 1} texts", () => Ui.Texts(tier.transform, lines, "  "));
        var buttons = tier.upgradeButtons;
        for (var b = 0; b < buttons.Count; b++)
        {
            var button = b;
            ScanReport.Guard(lines, $"rows: row {i + 1} button {button + 1}", () => DescribeButton(buttons[button], lines));
        }
    }

    private static void DescribeButton(UpgradeButton button, List<string> lines)
    {
        lines.Add($"  Button '{button.name}' {Ui.Span(button.transform)} levelPips={button.upgradeLevelObjects?.Length} " +
                  $"lockReasons=[{Ui.Try(() => Join(button.lockReasons))}] canAfford={button.canAfford} maxed={button.maxLevelAchieved}");
        var cfg = button.configuration;
        if (cfg == null)
        {
            lines.Add("    (no configuration)");
            return;
        }

        lines.Add($"    '{cfg.name}' id={cfg.Identifier} Name='{cfg.Name}' Description='{cfg.Description}' InitialCost={cfg.InitialCost} " +
                  $"icon={cfg.Icon?.name} glow={cfg.Glow?.name}");
        var levels = cfg.UpgradeLevels;
        for (var l = 0; l < levels.Count; l++)
        {
            var level = levels[l];
            var stat = level.TryCast<StatUpgrade>();
            var final = level.TryCast<FinalUpgrade>();
            if (stat != null) lines.Add($"    level {l + 1}: StatUpgrade {stat.Stat} {stat.Amount} ('{stat.Name}')");
            else if (final != null) lines.Add($"    level {l + 1}: Mastery {final.FinalUpgradeType} params=[{FinalParams(final)}] Description='{final.Description}'");
            else lines.Add($"    level {l + 1}: {Ui.ClassName(level)}");
        }
    }

    private static string FinalParams(FinalUpgrade final)
    {
        var count = final.FinalUpgradeParameters?.Count ?? 0;
        var values = new List<string>();
        for (var p = 0; p < count; p++)
        {
            var index = p;
            values.Add(Ui.Try(() => final.GetParameterValue(index)));
        }

        return string.Join(", ", values);
    }

    private static void DescribeManager(UpgradeBuyerUI buyer, List<string> lines)
    {
        var player = SingletonPersistent<LocalPlayer>.Instance;
        var manager = player.GetUpgradeManager();
        var total = manager.GetNumberOfUpgrades();
        lines.Add($"Gold={Ui.Try(() => player.GetGold())} upgradesBought={total} refundValue={Ui.Try(() => manager.GetRefundValue())} " +
                  $"BaseIncreaseCost={Ui.Try(() => UpgradeManager.BaseIncreaseCost)} UpgradeCostGrowthRate={Ui.Try(() => UpgradeManager.UpgradeCostGrowthRate)}");

        var configs = manager.configurationForUpgrades;
        var masteries = 0;
        foreach (UpgradeIdentifier id in Enum.GetValues(typeof(UpgradeIdentifier)))
        {
            var levelText = Ui.Try(() => manager.GetLevel(id));
            var maxed = Ui.Try(() => manager.IsMaxLevel(id));
            if (maxed == "True") masteries++;
            var line = $"{id}: level {levelText}/{Ui.Try(() => manager.GetMaxLevel(id))} maxed={maxed}";
            UpgradeConfiguration? cfg = null;
            try
            {
                if (configs != null && configs.ContainsKey(id)) cfg = configs[id];
            }
            catch (Exception ex)
            {
                line += $" config=!{ex.GetType().Name}: {ex.Message}";
            }

            if (cfg != null)
            {
                var initial = cfg.InitialCost;
                var level = int.TryParse(levelText, out var parsed) ? parsed : -1;
                var cost = Ui.Try(() => manager.CalculateCost(cfg));
                var formula = Ui.Try(() => manager.CalculateCost(initial, level, total));
                line += $" InitialCost={initial} cost={cost} CalculateCost({initial},{levelText},{total})={formula} {(cost == formula ? "MATCH" : "DIFFERS")}";
                line += $" lastBought={Ui.Try(() => manager.CalculateLastBoughtCost(cfg))}";
                line += $" value '{Ui.Try(() => manager.GetCurrentValueString(id))}' -> '{Ui.Try(() => manager.GetNextValueString(id))}'";
            }

            lines.Add(line);
        }

        lines.Add($"Masteries (game enchantments at max level): {masteries}");

        var initialCosts = new SortedSet<int>();
        var tiers = buyer.tiers;
        for (var i = 0; i < tiers.Count; i++)
        {
            var buttons = tiers[i].upgradeButtons;
            for (var b = 0; b < buttons.Count; b++)
            {
                var cfg = buttons[b].configuration;
                if (cfg != null) initialCosts.Add(cfg.InitialCost);
            }
        }

        foreach (var initial in initialCosts)
        {
            var byBought = Enumerable.Range(0, 7).Select(bought => Ui.Try(() => manager.CalculateCost(initial, bought, total)));
            var byTotal = new[] { 0, 10, 20, 40, 80 }.Select(t => $"{t}:{Ui.Try(() => manager.CalculateCost(initial, 0, t))}");
            lines.Add($"Price curve initial={initial}: bought 0..6 at total {total} = [{string.Join(", ", byBought)}]; bought 0 by total [{string.Join(", ", byTotal)}]; " +
                      $"bought 3 at total 0 = {Ui.Try(() => manager.CalculateCost(initial, 3, 0))}, at total 40 = {Ui.Try(() => manager.CalculateCost(initial, 3, 40))}");
        }
    }

    private static void DescribeDetails(UpgradeBuyerUI buyer, List<string> lines)
    {
        var details = buyer.upgradeDetailsUI;
        if (details == null)
        {
            lines.Add("No details panel.");
            return;
        }

        lines.Add($"Details panel at {Ui.Path(details.transform)} {Ui.Span(details.transform)} statRows={details.statUpgradeUIElements?.Count} " +
                  $"UpgradeFinalLockedTerm='{Ui.Try(() => UpgradeDetailsUI.UpgradeFinalLockedTerm)}'");
        Ui.Texts(details.transform, lines, "  ");
    }

    private static void DescribeSaveFlags(List<string> lines)
    {
        var player = SingletonPersistent<LocalPlayer>.Instance;
        lines.Add($"LocalPlayer save flags: lastClearedSaveTimeStamp={Ui.Try(() => player.lastClearedSaveTimeStamp)} " +
                  $"hasClearedSaveData={Ui.Try(() => player.hasClearedSaveData)} startPlayingTimestamp={Ui.Try(() => player.startPlayingTimestamp)}");
    }

    private static void DescribeEvents(List<string> lines)
    {
        lines.Add($"OnUpgradePerformed listeners: {Ui.Try(() => Ui.Listeners(LocalEvents.OnUpgradePerformed?.OnAction))}");
        lines.Add($"OnUpgradeRefundsPerformed listeners: {Ui.Try(() => Ui.Listeners(LocalEvents.OnUpgradeRefundsPerformed?.OnAction))}");
        lines.Add($"OnHealthPotionCollected listeners: {Ui.Try(() => Ui.Listeners(LocalEvents.OnHealthPotionCollected?.OnAction))}");
        lines.Add($"UpgradeManager.OnUpgradesRefunded listeners: {Ui.Try(() => Ui.Listeners(SingletonPersistent<LocalPlayer>.Instance.GetUpgradeManager().OnUpgradesRefunded))}");
        lines.Add($"LocalPlayer.OnGoldChanged listeners: {Ui.Try(() => Ui.Listeners(SingletonPersistent<LocalPlayer>.Instance.OnGoldChanged))}");
    }

    private static string Join(Il2CppSystem.Collections.Generic.List<LockReason>? reasons)
    {
        if (reasons == null) return "null";
        var parts = new List<string>();
        for (var i = 0; i < reasons.Count; i++) parts.Add(reasons[i].ToString());
        return string.Join(", ", parts);
    }
}
