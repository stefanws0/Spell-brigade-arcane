namespace SpellBrigadeArcane.Core;

using System;
using System.Collections.Generic;

/// <summary>
/// Alchemy's Mastery "Potion Belt": a Health Potion picked up at full Health is kept (up to <see cref="Capacity"/>)
/// and drunk automatically once Health drops below <see cref="Threshold"/> of max. The caller clears it at the end
/// of a mission; revives keep it.
/// </summary>
public sealed class PotionBelt
{
    public const float DefaultThreshold = 0.30f;

    private readonly Queue<float> _stored = new();

    public PotionBelt(float threshold = DefaultThreshold, int capacity = 1)
    {
        if (threshold <= 0f || threshold >= 1f) throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "must be between 0 and 1");
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "must be at least 1");
        Threshold = threshold;
        Capacity = capacity;
    }

    public float Threshold { get; }

    public int Capacity { get; }

    public int Stored => _stored.Count;

    /// <summary>Call for every Health Potion the local wizard picks up. True if it was stored.</summary>
    public bool OnPotionCollected(bool healthWasFull, float healAmount)
    {
        if (!healthWasFull || !(healAmount > 0f) || _stored.Count >= Capacity) return false;
        _stored.Enqueue(healAmount);
        return true;
    }

    /// <summary>The heal to apply now if a stored potion should be drunk, else null. A dead wizard (Health 0) never drinks.</summary>
    public float? TryDrink(float health, float maxHealth)
    {
        if (_stored.Count == 0 || maxHealth <= 0f || health <= 0f) return null;
        if (!(health / maxHealth < Threshold)) return null;
        return _stored.Dequeue();
    }

    public void Clear() => _stored.Clear();
}
