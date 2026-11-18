namespace SpellBrigadeArcane.Game;

using SpellBrigadeArcane.Core;
using TMPro;

/// <summary>
/// Fills the game's details panel for one of our buttons by writing its text fields directly. When the player hovers
/// one of the game's enchantments, the game fills the panel again itself.
/// </summary>
internal static class ArcaneDetails
{
    /// <summary>Opacity the game uses for a Mastery that isn't revealed yet (scan: color.a 0.2).</summary>
    private const float HiddenMasteryOpacity = 0.2f;

    public static void Show(UpgradeDetailsUI details, EnchantmentDef def, ButtonView view, int masteries)
    {
        ModLog.StepOnce("arcane-details", "ArcaneDetails: first fill of the details panel");
        details.nameText.text = def.Name;
        details.titleLevelText.text = ArcaneText.Level(view.Level);
        if (view.Maxed) details.SetTitleColorForMaxLevel();
        else details.SetDefaultTitleColor();

        var description = details.descriptionStringLocalizer != null ? details.descriptionStringLocalizer.GetComponent<TMP_Text>() : null;
        if (description != null)
            description.text = view.Unlocked ? def.Description : ArcaneText.Locked(masteries, Progress.UnlockMasteries) + "\n" + def.Description;

        ShowStats(details, def, view);

        details.finalUpgradeDescription.text = view.MasteryRevealed
            ? ArcaneText.Mastery(def.MasteryName, def.MasteryText)
            : ArcaneText.MasteryHidden(def.MasteryRevealLevel);
        details.SetFinalUpgradeOpacity(view.MasteryRevealed ? 1f : HiddenMasteryOpacity);

        var cost = details.costText;
        cost.transform.parent.gameObject.SetActive(view.Unlocked && !view.Maxed);
        cost.text = ArcaneText.Price(view.Price);
        cost.color = view.CanAfford ? details.canAffordTextColor : details.cannotAffordTextColor;

        if (details.animator != null) details.animator.SetBool(UpgradeDetailsUI.IsMastered, view.Maxed);
    }

    private static void ShowStats(UpgradeDetailsUI details, EnchantmentDef def, ButtonView view)
    {
        details.HideAllStatUpgradeUIElements();
        var rows = details.statUpgradeUIElements;
        var noMoreStats = view.Level >= def.StatLevels;
        for (var i = 0; i < def.Gains.Count && i < rows.Count; i++)
        {
            var gain = def.Gains[i];
            var row = rows[i];
            row.gameObject.SetActive(true);
            row.statName.text = gain.Label;
            var now = ArcaneText.Percent(def.PercentAt(gain.Stat, view.Level));
            row.currentStatPanel.SetActive(noMoreStats);
            row.currentToNextStatPanel.SetActive(!noMoreStats);
            for (var v = 0; v < row.currentValues.Count; v++) row.currentValues[v].text = now;
            row.nextValue.text = ArcaneText.Percent(def.PercentAt(gain.Stat, view.Level + 1));
        }
    }
}
