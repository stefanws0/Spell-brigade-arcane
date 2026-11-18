namespace SpellBrigadeArcane.Core;

using System;

/// <summary>
/// Prices of the sixth row. Per-level growth uses the game's own <c>UpgradeManager.CalculateCost(initialCost, bought,
/// totalBought)</c> (wired in a later milestone, once the scan confirms how it behaves); this class only picks the
/// starting price.
/// </summary>
public static class Pricing
{
    /// <summary>
    /// Spec: one row-step above Row V, i.e. startVI = startV × (startV / startIV), rounded half away from zero.
    /// </summary>
    public static int NextRowStart(int startIV, int startV)
    {
        if (startIV <= 0) throw new ArgumentOutOfRangeException(nameof(startIV), startIV, "must be positive");
        if (startV <= 0) throw new ArgumentOutOfRangeException(nameof(startV), startV, "must be positive");
        return (int)Math.Round((double)startV * startV / startIV, MidpointRounding.AwayFromZero);
    }
}
