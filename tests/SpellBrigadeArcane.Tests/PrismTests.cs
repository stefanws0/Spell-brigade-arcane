namespace SpellBrigadeArcane.Tests;

using SpellBrigadeArcane.Core;
using Xunit;

public class PrismTests
{
    [Theory]
    [InlineData(0, new[] { "None" })]
    [InlineData(1, new[] { "Fire", "FireDouble" })]
    [InlineData(2, new[] { "AcidFire" })]
    [InlineData(3, new[] { "Lightning", "LightningDouble", "FireLightning", "DarkLightning" })]
    [InlineData(5, new[] { "FireIce", "IceLightning", "DarkIce", "AcidDark" })]
    public void DistinctBaseElements_CountsEachBaseElementOnce(int expected, string[] elements) =>
        Assert.Equal(expected, Prism.DistinctBaseElements(elements));

    [Fact]
    public void DistinctBaseElements_SkipsNulls() => Assert.Equal(1, Prism.DistinctBaseElements(new string[] { null!, "Fire" }));

    [Fact]
    public void EveryGameElement_GivesFive()
    {
        var all = new[]
        {
            "None", "Fire", "FireDouble", "Lightning", "LightningDouble", "FireLightning", "Acid", "AcidDouble", "AcidFire",
            "AcidLightning", "Ice", "IceDouble", "FireIce", "AcidIce", "IceLightning", "Dark", "DarkDouble", "DarkFire",
            "DarkLightning", "AcidDark", "DarkIce",
        };
        Assert.Equal(5, Prism.DistinctBaseElements(all));
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(3, 12f)]
    [InlineData(5, 20f)]
    [InlineData(9, 20f)]
    [InlineData(-1, 0f)]
    public void BonusPercent_IsFourPerElementUpToFive(int distinct, float expected) =>
        Assert.Equal(expected, Prism.BonusPercent(distinct), 4);
}
