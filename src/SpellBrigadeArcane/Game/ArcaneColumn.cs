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

        // Made inside an inactive holder, so the copy doesn't wake up while we remove its UpgradeTierUI (it wakes at
        // SetParent, before Prepare; harmless: its events start empty). Same pattern as the Dash mod's stats rows.
        var holder = new GameObject("ARC_Holder");
        holder.SetActive(false);
        GameObject column;
        try
        {
            column = Object.Instantiate(template.gameObject, holder.transform);
            column.name = ColumnName;
            var tierUi = column.GetComponent<UpgradeTierUI>();
            ModLog.Step("ArcaneColumn: removing the copy's UpgradeTierUI");
            if (tierUi != null) Object.DestroyImmediate(tierUi);
            ModLog.Step("ArcaneColumn: moving the copy next to Tier V");
            column.transform.SetParent(parent, false);
            column.transform.SetAsLastSibling();
        }
        finally
        {
            Object.Destroy(holder);
        }

        try
        {
            var buttons = column.GetComponentsInChildren<UpgradeButton>(true);
            var ids = (ArcaneId[])Enum.GetValues(typeof(ArcaneId));
            if (buttons.Length < ids.Length)
                throw new InvalidOperationException($"Tier V's column has {buttons.Length} buttons, the sixth tier needs {ids.Length}");
            for (var i = 0; i < ids.Length; i++) Prepare(buttons[i], ids[i], onHighlight, onClick, onMiddleClick);

            LinkNavigation(template);
            _column = column;
            _builtFor = buyer.Pointer;
            try
            {
                LogListeners(template, buttons[0]);
            }
            catch (Exception ex)
            {
                ModLog.Warn($"ArcaneColumn: could not read the buttons' listeners: {ex.Message}");
            }

            ModLog.Info($"ArcaneColumn: sixth tier built at {Ui.Path(column.transform)}");
        }
        catch
        {
            Object.Destroy(column); // no dead copy of Tier V stays on screen
            _column = null;
            Buttons.Clear();
            KeepAlive.Clear();
            throw;
        }
    }

    /// <summary>Logs who listens to the three click events on a game button and on ours (the game's Setup handlers vs. only ours).</summary>
    private static void LogListeners(UpgradeTierUI tierV, UpgradeButton ours)
    {
        ModLog.StepOnce("arcane-listeners", "ArcaneColumn: reading the buttons' listeners");
        var theirs = tierV.upgradeButtons[0];
        ModLog.Info($"ArcaneColumn: game button listeners: highlight [{Ui.Listeners(theirs.OnHighlight)}], click [{Ui.Listeners(theirs.OnClicked)}], middle [{Ui.Listeners(theirs.OnMiddleMouseButtonClicked)}]");
        ModLog.Info($"ArcaneColumn: our button listeners: highlight [{Ui.Listeners(ours.OnHighlight)}], click [{Ui.Listeners(ours.OnClicked)}], middle [{Ui.Listeners(ours.OnMiddleMouseButtonClicked)}]");
    }

    /// <summary>Our button for an enchantment, or null before the column is built.</summary>
    public static UpgradeButton? Button(ArcaneId id) => Buttons.TryGetValue(id, out var button) ? button : null;

    /// <summary>Shows a button's level pips, "can afford" and lock state.</summary>
    public static void Show(ArcaneId id, ButtonView view)
    {
        if (!Buttons.TryGetValue(id, out var button) || button == null) return;
        // Built while the lobby screen is still inactive (right after a mission), the copy hasn't woken up yet and the
        // game's SetLevel would hit its unset Animator. The screen refreshes again when it opens.
        if (!button.isActiveAndEnabled) return;
        button.SetLevel(view.Level, view.MaxLevel);
        button.SetAffordableState(view.Unlocked && (view.Maxed || view.CanAfford)); // the game shows its maxed buttons as affordable (scan)
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
        var icon = IconSprites.Icon(id);
        var glow = IconSprites.Glow(id);
        if (icon != null) button.iconImage.sprite = icon;
        else if (config != null) button.iconImage.sprite = config.Icon;
        if (glow != null) button.iconGlow.sprite = glow;
        else if (config != null) button.iconGlow.sprite = config.Glow;

        // The game never calls the *Action properties itself (UpgradeBuyerUI.Setup subscribes to the events), so we subscribe too.
        button.add_OnHighlight(Keep(() => onHighlight(id)));
        button.add_OnClicked(Keep(() => onClick(id)));
        button.add_OnMiddleMouseButtonClicked(Keep(() => onMiddleClick(id)));
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
