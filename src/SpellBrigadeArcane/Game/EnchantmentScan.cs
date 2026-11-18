namespace SpellBrigadeArcane.Game;

using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Spec build step 1: writes what the sixth row needs to know about the game's Enchantments screen to the scan report,
/// once per session, the first time the screen is open and set up. Read-only.
/// </summary>
internal static class EnchantmentScan
{
    private const float Interval = 1f;
    private static float _next;
    private static bool _done;

    public static void Tick(float now)
    {
        if (_done || now < _next) return;
        _next = now + Interval;
        var buyer = FindReadyBuyer();
        if (buyer == null) return;
        _done = true;
        ModLog.Step("EnchantmentScan: Enchantments screen found, scanning");
        var lines = new List<string>();
        Guard(lines, "screen", () => DescribeScreen(buyer, lines));
        Guard(lines, "rows", () => DescribeRows(buyer, lines));
        Guard(lines, "levels and prices", () => DescribeManager(buyer, lines));
        Guard(lines, "details panel", () => DescribeDetails(buyer, lines));
        Guard(lines, "events", () => DescribeEvents(lines));
        ScanReport.Section("Enchantments screen", lines);
    }

    private static UpgradeBuyerUI? FindReadyBuyer()
    {
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

    private static void Guard(List<string> lines, string part, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            lines.Add($"!! {part} failed: {ex.GetType().Name}: {ex.Message}");
            ModLog.Warn($"EnchantmentScan {part}: {ex}");
        }
    }

    private static void DescribeScreen(UpgradeBuyerUI buyer, List<string> lines)
    {
        lines.Add($"Screen {Screen.width}x{Screen.height}; buyer at {Ui.Path(buyer.transform)} {Ui.Span(buyer.transform)}");
        var canvas = buyer.GetComponentInParent<Canvas>();
        if (canvas != null) lines.Add($"Canvas '{canvas.name}' renderMode={canvas.renderMode} scaleFactor={canvas.scaleFactor}");
        var group = buyer.GetComponent<CanvasGroup>();
        if (group != null) lines.Add($"Buyer CanvasGroup alpha={group.alpha} interactable={group.interactable}");

        var tiers = buyer.tiers;
        var parent = tiers[0].transform.parent;
        lines.Add($"Rows' parent: {Ui.Path(parent)} [{Ui.Components(parent)}] {Ui.Span(parent)}");
        var vlg = parent.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
            lines.Add($"  VerticalLayoutGroup spacing={vlg.spacing} padding(l,r,t,b)=({vlg.padding.left},{vlg.padding.right},{vlg.padding.top},{vlg.padding.bottom}) controlHeight={vlg.childControlHeight}");
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            lines.Add($"  child {i}: '{child.name}' active={child.gameObject.activeSelf} [{Ui.Components(child)}] {Ui.Span(child)}");
        }

        var refund = buyer.refundButton;
        lines.Add($"Refund button: {(refund != null ? Ui.Path(refund.transform) + " " + Ui.Span(refund.transform) : "none")}");
    }

    private static void DescribeRows(UpgradeBuyerUI buyer, List<string> lines)
    {
        var tiers = buyer.tiers;
        for (var i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            lines.Add($"Row {i + 1}: '{tier.name}' at {Ui.Path(tier.transform)} {Ui.Span(tier.transform)} [{Ui.Components(tier.transform)}]");
            lines.Add($"  locked={Ui.Try(() => tier.IsLocked)} lockReasons=[{Ui.Try(() => Join(tier.cachedLockReasons))}]");
            var cfg = tier.upgradeTierConfiguration;
            if (cfg != null)
            {
                lines.Add($"  config '{cfg.name}': challengeToUnlock={cfg.challengeToUnlock} lockedByProgression={Ui.Try(() => cfg.IsLockedByProgression())} " +
                          $"comingSoon={Ui.Try(() => cfg.GetIsComingSoon())} inDemo={Ui.Try(() => cfg.GetIsIncludedInDemo())} upgrades={cfg.UpgradesIncludedInTier?.Count}");
            }

            Texts(tier.transform, lines, "  ");
            var buttons = tier.upgradeButtons;
            for (var b = 0; b < buttons.Count; b++) DescribeButton(buttons[b], lines);
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
        lines.Add($"Gold={player.GetGold()} upgradesBought={total} refundValue={Ui.Try(() => manager.GetRefundValue())} " +
                  $"BaseIncreaseCost={UpgradeManager.BaseIncreaseCost} UpgradeCostGrowthRate={UpgradeManager.UpgradeCostGrowthRate}");

        var configs = manager.configurationForUpgrades;
        var masteries = 0;
        foreach (UpgradeIdentifier id in Enum.GetValues(typeof(UpgradeIdentifier)))
        {
            try
            {
                var level = manager.GetLevel(id);
                var maxed = manager.IsMaxLevel(id);
                if (maxed) masteries++;
                var line = $"{id}: level {level}/{manager.GetMaxLevel(id)} maxed={maxed}";
                var cfg = configs != null && configs.ContainsKey(id) ? configs[id] : null;
                if (cfg != null)
                {
                    var cost = manager.CalculateCost(cfg);
                    var formula = manager.CalculateCost(cfg.InitialCost, level, total);
                    line += $" InitialCost={cfg.InitialCost} cost={cost} CalculateCost({cfg.InitialCost},{level},{total})={formula} {(cost == formula ? "MATCH" : "DIFFERS")}";
                    line += $" lastBought={Ui.Try(() => manager.CalculateLastBoughtCost(cfg))}";
                    line += $" value '{Ui.Try(() => manager.GetCurrentValueString(id))}' -> '{Ui.Try(() => manager.GetNextValueString(id))}'";
                }

                lines.Add(line);
            }
            catch (Exception ex)
            {
                lines.Add($"{id}: !{ex.GetType().Name}: {ex.Message}");
            }
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
            lines.Add($"Price curve initial={initial}: bought 0..6 at total {total} = [{string.Join(", ", byBought)}]; bought 0 by total [{string.Join(", ", byTotal)}]");
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
        Texts(details.transform, lines, "  ");
    }

    private static void DescribeEvents(List<string> lines)
    {
        lines.Add($"OnUpgradePerformed listeners: {Listeners(LocalEvents.OnUpgradePerformed?.OnAction)}");
        lines.Add($"OnUpgradeRefundsPerformed listeners: {Listeners(LocalEvents.OnUpgradeRefundsPerformed?.OnAction)}");
        lines.Add($"OnHealthPotionCollected listeners: {Listeners(LocalEvents.OnHealthPotionCollected?.OnAction)}");
    }

    private static string Listeners(Il2CppSystem.Delegate? action)
    {
        if (action == null) return "none";
        var names = new List<string>();
        foreach (var d in action.GetInvocationList())
        {
            var target = d.Target == null ? "static" : Ui.ClassName(d.Target);
            names.Add($"{target}.{Ui.Try(() => d.Method?.Name)}");
        }

        return string.Join(", ", names);
    }

    private static void Texts(Transform root, List<string> lines, string indent)
    {
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            lines.Add($"{indent}text {Ui.Path(text.transform)}: '{text.text}' active={text.gameObject.activeInHierarchy}");
        foreach (var loc in root.GetComponentsInChildren<LocalizeStringEvent>(true))
            lines.Add($"{indent}localized {Ui.Path(loc.transform)}: {Ui.Try(() => loc.StringReference?.ToString())}");
    }

    private static string Join(Il2CppSystem.Collections.Generic.List<LockReason>? reasons)
    {
        if (reasons == null) return "null";
        var parts = new List<string>();
        for (var i = 0; i < reasons.Count; i++) parts.Add(reasons[i].ToString());
        return string.Join(", ", parts);
    }
}
