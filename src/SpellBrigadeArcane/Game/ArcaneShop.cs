namespace SpellBrigadeArcane.Game;

using System;
using Il2CppInterop.Runtime;
using SpellBrigadeArcane.Core;
using Object = UnityEngine.Object;

/// <summary>
/// Runs the sixth tier: finds the Enchantments screen, builds the column once per screen, keeps it up to date, and
/// handles hover (details panel), click (buy), middle-click (refund one level) and the game's Refund All.
/// Order of writes (spec): buying = our save → spend gold → game save; refunding = give gold → game save → our save.
/// A crash in between can at worst give something for free, never lose gold.
/// </summary>
internal static class ArcaneShop
{
    private const float Interval = 0.25f;
    private const float FindInterval = 1f;

    private static float _next;
    private static float _nextFind;
    private static UpgradeBuyerUI? _buyer;
    private static bool _wasOpen;
    private static int _lastGold = -1;
    private static int _lastTotal = -1;
    private static bool _broken;
    private static IntPtr _refundHookedOn;
    private static Il2CppSystem.Action? _refundHandler;

    public static void Tick(float now)
    {
        if (_broken || now < _next) return;
        _next = now + Interval;

        if (_buyer == null)
        {
            if (now < _nextFind) return;
            _nextFind = now + FindInterval;
            ModLog.StepOnce("shop-find", "ArcaneShop: first FindObjectsOfType");
            _buyer = FindReadyBuyer();
            if (_buyer == null) return;
        }

        var buyer = _buyer;
        // Waits until the game's save is loaded; reloads (and applies the reset rule) after CheckGameSave discarded it.
        if (!ArcaneState.EnsureLoaded()) return;
        if (!ArcaneColumn.IsBuilt(buyer))
        {
            try
            {
                ArcaneColumn.Build(buyer, OnHighlight, OnClick, OnMiddleClick);
                HookRefundAll();
            }
            catch (Exception ex)
            {
                _broken = true;
                ModLog.Error($"ArcaneShop: could not build the sixth tier; it stays off this session: {ex}");
                return;
            }

            Refresh();
        }

        var open = buyer.IsOpen;
        if (open && !_wasOpen)
        {
            ArcaneState.CheckGameSave();
            if (ArcaneState.Current == null)
            {
                _wasOpen = false; // reloaded next tick, then refreshed
                return;
            }
        }

        var gold = GameUpgrades.Gold;
        var total = GameUpgrades.TotalBought;
        if (open && (!_wasOpen || gold != _lastGold || total != _lastTotal)) Refresh();
        _wasOpen = open;
        _lastGold = gold;
        _lastTotal = total;
    }

    private static UpgradeBuyerUI? FindReadyBuyer()
    {
        foreach (var obj in Object.FindObjectsOfType(Il2CppType.Of<UpgradeBuyerUI>(), true))
        {
            var buyer = obj?.TryCast<UpgradeBuyerUI>();
            if (buyer == null) continue;
            var tiers = buyer.tiers;
            if (tiers == null || tiers.Count == 0) continue;
            var buttons = tiers[0].upgradeButtons;
            if (buttons == null || buttons.Count == 0 || buttons[0].configuration == null) continue;
            return buyer;
        }

        return null;
    }

    private static ButtonView View(ArcaneId id, Progress progress, bool unlocked, int gold) =>
        ButtonView.For(ArcaneState.Def(id), progress, unlocked, gold, GameUpgrades.Price(progress.Level(id), progress.TotalLevels));

    private static void Refresh()
    {
        var progress = ArcaneState.Current;
        if (progress == null) return;
        ModLog.StepOnce("shop-refresh", "ArcaneShop: first refresh");
        var unlocked = ArcaneState.IsUnlocked(GameUpgrades.Masteries());
        var gold = GameUpgrades.Gold;
        foreach (ArcaneId id in Enum.GetValues(typeof(ArcaneId))) ArcaneColumn.Show(id, View(id, progress, unlocked, gold));
    }

    private static void OnHighlight(ArcaneId id)
    {
        try
        {
            var progress = ArcaneState.Current;
            var details = _buyer?.upgradeDetailsUI;
            if (progress == null || details == null) return;
            var masteries = GameUpgrades.Masteries();
            var view = View(id, progress, ArcaneState.IsUnlocked(masteries), GameUpgrades.Gold);
            ArcaneDetails.Show(details, ArcaneState.Def(id), view, masteries);
        }
        catch (Exception ex)
        {
            ModLog.Error($"ArcaneShop: showing {id} failed: {ex}");
        }
    }

    private static void OnClick(ArcaneId id)
    {
        try
        {
            Buy(id);
        }
        catch (Exception ex)
        {
            ModLog.Error($"ArcaneShop: buying {id} failed: {ex}");
        }
    }

    private static void OnMiddleClick(ArcaneId id)
    {
        try
        {
            RefundOne(id);
        }
        catch (Exception ex)
        {
            ModLog.Error($"ArcaneShop: refunding {id} failed: {ex}");
        }
    }

    private static void Buy(ArcaneId id)
    {
        ArcaneState.CheckGameSave();
        var progress = ArcaneState.Current;
        if (progress == null)
        {
            Fail(id, "the sixth tier's save isn't loaded");
            return;
        }

        if (!GameUpgrades.CanSaveGame)
        {
            Fail(id, "the game can't save right now");
            return;
        }

        var view = View(id, progress, ArcaneState.IsUnlocked(GameUpgrades.Masteries()), GameUpgrades.Gold);
        if (view.Check != BuyResult.Ok)
        {
            Fail(id, view.Check.ToString());
            return;
        }

        ModLog.Step($"ArcaneShop: buying {id} level {view.Level + 1} for {view.Price}");
        progress.TryBuy(id, view.Price);
        if (!ArcaneState.Save())
        {
            progress.RefundLast(id);
            Fail(id, "our save could not be written");
            return;
        }

        GameUpgrades.Spend(view.Price);
        GameUpgrades.SaveGame();
        _buyer?.soundPlayer?.PlayUpgradeDoneSound(progress.IsMaxed(id));
        ModLog.Info($"ArcaneShop: bought {id} level {progress.Level(id)} for {view.Price} gold; {GameUpgrades.Gold} gold left.");
        Refresh();
        OnHighlight(id);
    }

    private static void RefundOne(ArcaneId id)
    {
        ArcaneState.CheckGameSave();
        var progress = ArcaneState.Current;
        if (progress == null || progress.Level(id) == 0) return;
        if (!GameUpgrades.CanSaveGame)
        {
            ModLog.Warn($"ArcaneShop: not refunding {id}: the game can't save right now.");
            return;
        }

        ModLog.Step($"ArcaneShop: refunding one level of {id}");
        var gold = progress.RefundLast(id);
        GameUpgrades.Give(gold);
        if (!GameUpgrades.SaveGame() || !ArcaneState.Save())
        {
            // Our file still has the level: undo the refund in memory and take the gold back.
            progress.TryBuy(id, gold);
            GameUpgrades.Spend(gold);
            GameUpgrades.SaveGame();
            ModLog.Warn($"ArcaneShop: refund of {id} undone, the game or our save could not be written.");
            return;
        }

        ArcaneColumn.PlayRefund(id);
        ModLog.Info($"ArcaneShop: refunded {id} to level {progress.Level(id)}, {gold} gold back; {GameUpgrades.Gold} gold now.");
        Refresh();
        OnHighlight(id);
    }

    /// <summary>Adds our handler to the game's UpgradeManager.OnUpgradesRefunded (a plain Action field), once per manager.</summary>
    private static void HookRefundAll()
    {
        var manager = GameUpgrades.Manager;
        if (manager.Pointer == _refundHookedOn) return;
        ModLog.Step("ArcaneShop: hooking the game's Refund All");
        _refundHandler ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(OnGameRefundAll));
        manager.OnUpgradesRefunded = Il2CppSystem.Delegate.Combine(manager.OnUpgradesRefunded, _refundHandler).Cast<Il2CppSystem.Action>();
        _refundHookedOn = manager.Pointer;
    }

    private static void OnGameRefundAll()
    {
        try
        {
            if (GameUpgrades.TotalBought != 0) return; // only a full Refund All empties the game's levels
            ArcaneState.CheckGameSave();
            var progress = ArcaneState.Current;
            if (progress == null || progress.TotalLevels == 0) return;
            if (!GameUpgrades.CanSaveGame)
            {
                ModLog.Warn("ArcaneShop: Refund All left the sixth tier as it is: the game can't save right now (middle-click to refund).");
                return;
            }

            ModLog.Step("ArcaneShop: Refund All");
            var levels = progress.TotalLevels;
            var gold = progress.RefundAll();
            GameUpgrades.Give(gold);
            if (!GameUpgrades.SaveGame() || !ArcaneState.Save())
            {
                // Our file still has the levels: reload them and take the gold back.
                ArcaneState.Discard();
                ArcaneState.EnsureLoaded();
                GameUpgrades.Spend(gold);
                GameUpgrades.SaveGame();
                ModLog.Warn("ArcaneShop: Refund All of the sixth tier undone, our save could not be written.");
                return;
            }

            ModLog.Info($"ArcaneShop: Refund All gave back {gold} gold for {levels} sixth-tier level(s).");
            Refresh();
        }
        catch (Exception ex)
        {
            ModLog.Error($"ArcaneShop: Refund All of the sixth tier failed: {ex}");
        }
    }

    private static void Fail(ArcaneId id, string why)
    {
        ModLog.Info($"ArcaneShop: can't buy {id}: {why}");
        _buyer?.soundPlayer?.PlayUpgradeFailSound();
        ArcaneColumn.PlayFail(id);
    }
}
