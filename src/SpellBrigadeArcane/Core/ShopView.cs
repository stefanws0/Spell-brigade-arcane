namespace SpellBrigadeArcane.Core;

/// <summary>Why a sixth-tier level can or can't be bought right now.</summary>
public enum BuyResult
{
    Ok,
    Locked,
    Maxed,
    TooExpensive,
}

/// <summary>
/// What one sixth-tier button shows and allows: computed from the books (level), the unlock state, the player's gold and
/// the next level's price. The game layer only draws it.
/// </summary>
public sealed class ButtonView
{
    public ButtonView(int level, int maxLevel, int masteryRevealLevel, bool unlocked, int gold, int price)
    {
        Level = level;
        MaxLevel = maxLevel;
        MasteryRevealLevel = masteryRevealLevel;
        Unlocked = unlocked;
        Gold = gold;
        Price = price;
    }

    public int Level { get; }

    public int MaxLevel { get; }

    public int MasteryRevealLevel { get; }

    public bool Unlocked { get; }

    public int Gold { get; }

    /// <summary>Price of the next level (meaningless when maxed).</summary>
    public int Price { get; }

    public bool Maxed => Level >= MaxLevel;

    public bool MasteryRevealed => Level >= MasteryRevealLevel;

    /// <summary>Enough gold for the next level (whether or not it can be bought: locked or maxed is separate).</summary>
    public bool CanAfford => Gold >= Price;

    /// <summary>The next level can be bought right now.</summary>
    public bool CanBuy => Check == BuyResult.Ok;

    public BuyResult Check =>
        !Unlocked ? BuyResult.Locked
        : Maxed ? BuyResult.Maxed
        : Gold < Price ? BuyResult.TooExpensive
        : BuyResult.Ok;

    public static ButtonView For(EnchantmentDef def, Progress progress, bool unlocked, int gold, int price) =>
        new(progress.Level(def.Id), def.MaxLevel, def.MasteryRevealLevel, unlocked, gold, price);
}
