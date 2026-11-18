namespace SpellBrigadeArcane.Core;

using System;
using System.Globalization;

/// <summary>
/// English texts for the details panel, formatted like the game's own ("+25%", "LVL 6") and culture-invariant (the PC
/// runs a Dutch locale, which would otherwise print "7,5").
/// </summary>
public static class ArcaneText
{
    public static string Percent(float percent) => FormattableString.Invariant($"+{percent:0.#}%");

    public static string Level(int level) => FormattableString.Invariant($"LVL {level}");

    public static string MasteryHidden(int revealLevel) => FormattableString.Invariant($"Upgrade to LVL {revealLevel} to reveal");

    public static string Mastery(string name, string text) => $"<b>{name}</b>: {text}";

    public static string Locked(int masteries, int needed) =>
        FormattableString.Invariant($"Activate <b>{needed}</b> Masteries to unlock this tier. You have <b>{masteries}</b>.");

    public static string Price(int gold) => gold.ToString(CultureInfo.InvariantCulture);
}
