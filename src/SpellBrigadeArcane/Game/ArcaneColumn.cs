namespace SpellBrigadeArcane.Game;

using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using SpellBrigadeArcane.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// The sixth tier's column on the Enchantments screen: a copy of Tier V's column without its UpgradeTierUI. Our four
/// UpgradeButtons keep the game's look, level pips, lock image and animations; clicks go to our actions.
/// </summary>
internal static class ArcaneColumn
{
    public const string ColumnName = "ARC_Upgrade_Tier_6";

    /// <summary>Game enchantments whose icons the sixth tier borrows until it has its own (milestone E).</summary>
    private static readonly Dictionary<ArcaneId, UpgradeIdentifier> PlaceholderIcons = new()
    {
        [ArcaneId.Volley] = UpgradeIdentifier.FireRate,
        [ArcaneId.Reach] = UpgradeIdentifier.PickupRadius,
        [ArcaneId.Attunement] = UpgradeIdentifier.Damage,
        [ArcaneId.Alchemy] = UpgradeIdentifier.HealthRegeneration,
    };

    private static readonly Dictionary<ArcaneId, UpgradeButton> Buttons = new();

    // Delegates handed to the game must stay alive as long as the buttons do.
    private static readonly List<Il2CppSystem.Action> KeepAlive = new();

    private static IntPtr _builtFor;
    private static GameObject? _column;

    public static bool IsBuilt(UpgradeBuyerUI buyer) => _column != null && _builtFor == buyer.Pointer;

    /// <summary>Builds the column for this Enchantments screen. The actions get the enchantment that was hovered/clicked.</summary>
    public static void Build(UpgradeBuyerUI buyer, Action<ArcaneId> onHighlight, Action<ArcaneId> onClick, Action<ArcaneId> onMiddleClick)
    {
        ModLog.Step("ArcaneColumn: building the sixth tier");
        var tiers = buyer.tiers;
        var template = tiers[tiers.Count - 1];
        var parent = template.transform.parent;
        var old = parent.Find(ColumnName);
        if (old != null) Object.Destroy(old.gameObject);
        Buttons.Clear();
        KeepAlive.Clear();

        // Made inside an inactive holder, so none of the copied components wake up before they are set up
        // (the Dash mod's stats rows use the same pattern).
        var holder = new GameObject("ARC_Holder");
        holder.SetActive(false);
        GameObject column;
        try
        {
            column = Object.Instantiate(template.gameObject, holder.transform);
            column.name = ColumnName;
            var tierUi = column.GetComponent<UpgradeTierUI>();
            if (tierUi != null) Object.DestroyImmediate(tierUi);
            ModLog.Step("ArcaneColumn: moving the copy next to Tier V");
            column.transform.SetParent(parent, false);
            column.transform.SetAsLastSibling();
        }
        finally
        {
            Object.Destroy(holder);
        }

        var buttons = column.GetComponentsInChildren<UpgradeButton>(true);
        var ids = (ArcaneId[])Enum.GetValues(typeof(ArcaneId));
        if (buttons.Length < ids.Length)
            throw new InvalidOperationException($"Tier V's column has {buttons.Length} buttons, the sixth tier needs {ids.Length}");
        for (var i = 0; i < ids.Length; i++) Prepare(buttons[i], ids[i], onHighlight, onClick, onMiddleClick);

        LinkNavigation(template);
        _column = column;
        _builtFor = buyer.Pointer;
        ModLog.Info($"ArcaneColumn: sixth tier built at {Ui.Path(column.transform)} {Ui.Span(column.transform)}");
    }

    /// <summary>Shows a button's level pips, "can afford" and lock state.</summary>
    public static void Show(ArcaneId id, ButtonView view)
    {
        if (!Buttons.TryGetValue(id, out var button) || button == null) return;
        button.SetLevel(view.Level, view.MaxLevel);
        button.SetAffordableState(view.Maxed || view.CanAfford); // the game shows its maxed buttons as affordable (scan)
        var reasons = new Il2CppSystem.Collections.Generic.List<LockReason>();
        if (!view.Unlocked) reasons.Add(LockReason.NotUnlockedYet);
        button.UpdateLockState(reasons);
    }

    /// <summary>The game's "can't buy" shake on our button.</summary>
    public static void PlayFail(ArcaneId id)
    {
        if (Buttons.TryGetValue(id, out var button) && button != null) button.Animator?.SetTrigger(UpgradeButton.UpgradeFailed);
    }

    /// <summary>The game's single-refund animation on our button.</summary>
    public static void PlayRefund(ArcaneId id)
    {
        if (Buttons.TryGetValue(id, out var button) && button != null) button.TriggerSingleRefundAnimation();
    }

    private static void Prepare(UpgradeButton button, ArcaneId id, Action<ArcaneId> onHighlight, Action<ArcaneId> onClick, Action<ArcaneId> onMiddleClick)
    {
        ModLog.StepOnce("arcane-prepare", "ArcaneColumn: first button setup");
        button.gameObject.name = "ARC_" + id;
        button.configuration = null;
        var config = GameUpgrades.Configuration(PlaceholderIcons[id]);
        if (config != null)
        {
            button.iconImage.sprite = config.Icon;
            button.iconGlow.sprite = config.Glow;
        }

        button.OnHighlightAction = Keep(() => onHighlight(id));
        button.OnClickedAction = Keep(() => onClick(id));
        button.OnMiddleMouseButtonClickedAction = Keep(() => onMiddleClick(id));
        Buttons[id] = button;
    }

    private static Il2CppSystem.Action Keep(Action action)
    {
        var converted = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(action)
            ?? throw new InvalidOperationException("DelegateSupport returned no delegate");
        KeepAlive.Add(converted);
        return converted;
    }

    /// <summary>
    /// If the game links its buttons explicitly (gamepad/keyboard), link ours: up/down within the column, left to the
    /// Tier V button in the same row, and Tier V's right to ours. Automatic modes find our column on their own.
    /// </summary>
    private static void LinkNavigation(UpgradeTierUI tierV)
    {
        ModLog.StepOnce("arcane-nav", "ArcaneColumn: navigation");
        var theirs = tierV.upgradeButtons;
        if (theirs == null || theirs.Count == 0) return;
        var mode = theirs[0].Button.navigation.mode;
        ModLog.Info($"ArcaneColumn: the game's enchantment buttons use navigation mode {mode}");
        if (mode != Navigation.Mode.Explicit) return;

        var ours = new List<UpgradeButton>();
        foreach (ArcaneId id in Enum.GetValues(typeof(ArcaneId))) ours.Add(Buttons[id]);
        for (var i = 0; i < ours.Count; i++)
        {
            var nav = ours[i].Button.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = i > 0 ? ours[i - 1].Button : null;
            nav.selectOnDown = i < ours.Count - 1 ? ours[i + 1].Button : null;
            nav.selectOnLeft = i < theirs.Count ? theirs[i].Button : null;
            ours[i].Button.navigation = nav;
            if (i < theirs.Count)
            {
                var back = theirs[i].Button.navigation;
                back.selectOnRight = ours[i].Button;
                theirs[i].Button.navigation = back;
            }
        }
    }
}
