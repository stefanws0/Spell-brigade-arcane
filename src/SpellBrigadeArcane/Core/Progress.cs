namespace SpellBrigadeArcane.Core;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// What the player owns in the sixth row: per enchantment, the gold paid for each bought level. Gold itself, the
/// "can afford" check and the game save are the caller's job (Game layer); this class only keeps the books.
/// </summary>
public sealed class Progress
{
    /// <summary>Spec: the row opens at 12 Masteries (game enchantments at max level). Row V opens at 8.</summary>
    public const int UnlockMasteries = 12;

    /// <summary>Prices above this are rejected (TryBuy) or clamped (FromSave), so the sums of a tampered save can't overflow.</summary>
    public const int MaxPrice = 10_000_000;

    private readonly IReadOnlyList<EnchantmentDef> _catalog;
    private readonly Dictionary<ArcaneId, List<int>> _paid = new();

    public Progress(IReadOnlyList<EnchantmentDef> catalog)
    {
        _catalog = catalog;
        foreach (var def in catalog) _paid[def.Id] = new List<int>();
    }

    public int TotalLevels => _paid.Values.Sum(p => p.Count);

    public int TotalPaid => _paid.Values.Sum(p => p.Sum());

    public static bool IsUnlocked(int gameMasteries, int threshold, bool debugUnlock) => debugUnlock || gameMasteries >= threshold;

    public int Level(ArcaneId id) => _paid[id].Count;

    public bool IsMaxed(ArcaneId id) => Level(id) >= Catalog.Get(_catalog, id).MaxLevel;

    public IReadOnlyList<int> Paid(ArcaneId id) => _paid[id].AsReadOnly();

    /// <summary>Records one bought level at <paramref name="price"/>. False (nothing changes) if it is already maxed.</summary>
    public bool TryBuy(ArcaneId id, int price)
    {
        if (price < 0 || price > MaxPrice) throw new ArgumentOutOfRangeException(nameof(price), price, "must be between 0 and MaxPrice");
        if (IsMaxed(id)) return false;
        _paid[id].Add(price);
        return true;
    }

    /// <summary>Removes the top level of <paramref name="id"/> and returns the gold paid for it (0 at level 0).</summary>
    public int RefundLast(ArcaneId id)
    {
        var paid = _paid[id];
        if (paid.Count == 0) return 0;
        var gold = paid[paid.Count - 1];
        paid.RemoveAt(paid.Count - 1);
        return gold;
    }

    /// <summary>Removes every level and returns all the gold paid.</summary>
    public int RefundAll()
    {
        var gold = TotalPaid;
        foreach (var paid in _paid.Values) paid.Clear();
        return gold;
    }

    public SaveData ToSave()
    {
        var data = new SaveData();
        foreach (var pair in _paid) data.Paid[pair.Key.ToString()] = new List<int>(pair.Value);
        return data;
    }

    /// <summary>
    /// Rebuilds progress from a save. Unknown names are skipped (noted). Levels above an enchantment's max level are
    /// dropped and their gold is returned in <paramref name="excessGold"/> for the caller to give back (noted).
    /// Prices are clamped to 0..MaxPrice; a missing list counts as level 0.
    /// Caller contract: write the trimmed save BEFORE giving back <paramref name="excessGold"/>; otherwise the same gold
    /// would be refunded again on every load.
    /// </summary>
    public static Progress FromSave(IReadOnlyList<EnchantmentDef> catalog, SaveData data, out int excessGold, out List<string> notes)
    {
        var progress = new Progress(catalog);
        excessGold = 0;
        notes = new List<string>();
        var known = Enum.GetNames(typeof(ArcaneId));
        foreach (var pair in data.Paid)
        {
            if (!known.Contains(pair.Key))
            {
                notes.Add($"unknown enchantment '{pair.Key}' ignored");
                continue;
            }

            var id = (ArcaneId)Enum.Parse(typeof(ArcaneId), pair.Key);
            if (!progress._paid.ContainsKey(id) || pair.Value == null) continue;
            var max = Catalog.Get(catalog, id).MaxLevel;
            for (var i = 0; i < pair.Value.Count; i++)
            {
                var price = Math.Clamp(pair.Value[i], 0, MaxPrice);
                if (i < max) progress._paid[id].Add(price);
                else excessGold += price;
            }

            if (pair.Value.Count > max) notes.Add($"{id}: {pair.Value.Count - max} level(s) above max level {max} refunded");
        }

        return progress;
    }
}
