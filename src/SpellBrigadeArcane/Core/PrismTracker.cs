namespace SpellBrigadeArcane.Core;

using System;

/// <summary>
/// Keeps Prism's Elemental Damage in step with the element count during a mission: <see cref="Update"/> returns how much
/// to add now. It only grows: in this game an element never leaves a spell during a mission, and the game's network
/// message for character modifiers can only add (research, 19 Nov).
/// Create one per mission: the game starts every mission with fresh stats, so a tracker must not carry over.
/// </summary>
public sealed class PrismTracker
{
    private readonly float _percentPerElement;

    public PrismTracker(float percentPerElement = Prism.DefaultPercentPerElement)
    {
        if (!(float.IsFinite(percentPerElement) && percentPerElement > 0f)) throw new ArgumentOutOfRangeException(nameof(percentPerElement), percentPerElement, "must be above 0");
        _percentPerElement = percentPerElement;
    }

    /// <summary>Elemental Damage given so far this mission, in percent.</summary>
    public float Applied { get; private set; }

    /// <summary>The element count <see cref="Applied"/> belongs to.</summary>
    public int Elements { get; private set; }

    /// <summary>The percent to add for this many different base elements now: 0 if nothing new.</summary>
    public float Update(int distinctElements)
    {
        var target = Prism.BonusPercent(distinctElements, _percentPerElement);
        if (!(target > Applied)) return 0f;
        var add = target - Applied;
        Applied = target;
        Elements = Math.Clamp(distinctElements, 0, Prism.MaxElements);
        return add;
    }
}
