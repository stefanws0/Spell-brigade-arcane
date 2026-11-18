namespace SpellBrigadeArcane.Tests;

using System.Globalization;
using SpellBrigadeArcane.Core;
using Xunit;

public class ArcaneTextTests
{
    [Theory]
    [InlineData(0f, "+0%")]
    [InlineData(6f, "+6%")]
    [InlineData(25f, "+25%")]
    [InlineData(7.5f, "+7.5%")]
    public void Percent_LooksLikeTheGamesValues(float percent, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
        try
        {
            Assert.Equal(expected, ArcaneText.Percent(percent));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Level_LooksLikeTheGamesTitle() => Assert.Equal("LVL 3", ArcaneText.Level(3));

    [Fact]
    public void MasteryHidden_NamesTheRevealLevel() => Assert.Equal("Upgrade to LVL 5 to reveal", ArcaneText.MasteryHidden(5));

    [Fact]
    public void Mastery_PutsTheNameInBold() =>
        Assert.Equal("<b>Prism</b>: some text", ArcaneText.Mastery("Prism", "some text"));

    [Fact]
    public void Locked_SaysWhatIsNeededAndWhatYouHave() =>
        Assert.Equal("Activate <b>12</b> Masteries to unlock this tier. You have <b>5</b>.", ArcaneText.Locked(5, 12));

    [Fact]
    public void Price_HasNoThousandsSeparator()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
        try
        {
            Assert.Equal("1087", ArcaneText.Price(1087));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
