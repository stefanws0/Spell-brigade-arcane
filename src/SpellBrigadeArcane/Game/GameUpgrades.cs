namespace SpellBrigadeArcane.Game;

using System;

/// <summary>
/// The game's enchantment system in one place (HANDOFF "Scan findings"): gold, Masteries, the price formula and the
/// game's save. Everything else in the mod goes through here.
/// </summary>
internal static class GameUpgrades
{
    /// <summary>The sixth tier's InitialCost: <c>Pricing.NextRowStart(250, 300)</c> from Tier IV/V's costs (scan).</summary>
    public const int ArcaneInitialCost = 360;

    public static LocalPlayer Player => SingletonPersistent<LocalPlayer>.Instance;

    public static UpgradeManager Manager => Player.GetUpgradeManager();

    public static int Gold => Player.GetGold();

    /// <summary>All game enchantment levels bought (the game's price formula grows with it).</summary>
    public static int TotalBought => Manager.GetNumberOfUpgrades();

    /// <summary>Changes when the player resets the game's save.</summary>
    public static long SaveClearedTicks => Player.lastClearedSaveTimeStamp.Ticks;

    /// <summary>Game enchantments at max level, i.e. with their Mastery.</summary>
    public static int Masteries()
    {
        var manager = Manager;
        var count = 0;
        foreach (UpgradeIdentifier id in Enum.GetValues(typeof(UpgradeIdentifier)))
        {
            try
            {
                if (manager.IsMaxLevel(id)) count++;
            }
            catch (Exception ex)
            {
                ModLog.WarnOnce("mastery-" + id, $"GameUpgrades: IsMaxLevel({id}) failed: {ex.Message}");
            }
        }

        return count;
    }

    /// <summary>
    /// The game's own price formula (scan: initial + initial/2 × bought + floor(35 × 1.025^total)) for a sixth-tier
    /// level: every level bought, the game's and the sixth tier's, raises it like a game purchase does.
    /// </summary>
    public static int Price(int levelOfThisEnchantment, int sixthTierLevels) =>
        Manager.CalculateCost(ArcaneInitialCost, levelOfThisEnchantment, TotalBought + sixthTierLevels);

    /// <summary>A game enchantment's configuration (for its icon), or null.</summary>
    public static UpgradeConfiguration? Configuration(UpgradeIdentifier id)
    {
        var configs = Manager.configurationForUpgrades;
        return configs != null && configs.ContainsKey(id) ? configs[id] : null;
    }

    public static void Spend(int gold) => Player.RemoveGold(gold);

    public static void Give(int gold) => Player.AddGold(gold);

    /// <summary>
    /// Makes the game save now: its <c>SaveOnUpgradePurchasedOrRefunded</c> is the only listener of this event (scan),
    /// so nothing else in the game reacts.
    /// </summary>
    public static void SaveGame()
    {
        ModLog.StepOnce("game-save", "GameUpgrades: first triggered game save");
        LocalEvents.OnUpgradeRefundsPerformed.Trigger(new UpgradeRefundsPerformedEventArgs());
    }
}
