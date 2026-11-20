using FuelIntake.Core.Eligibility;

namespace FuelIntake.Core.Tests;

public class EligibilityTests
{
    private static readonly PovertyGuidelines G = PovertyGuidelines.Hhs2026;

    [Theory]
    [InlineData(1, 15960)]
    [InlineData(4, 33000)]
    [InlineData(8, 55720)]
    [InlineData(10, 55720 + 2 * 5680)]
    public void Guideline_BySize(int size, int expected) => Assert.Equal(expected, G.AnnualGuideline(size));

    [Fact]
    public void Guideline_NoHousehold_IsNull() => Assert.Null(G.AnnualGuideline(0));

    [Fact]
    public void Percent_IsMonthlyTimesTwelveOverGuideline()
    {
        // $1,330/mo × 12 = $15,960 = exactly 100% for one person
        Assert.Equal(1.0m, FplCalculator.Percent(1330m, 1, G));
        Assert.Equal(2.0m, FplCalculator.Percent(2660m, 1, G));
    }

    [Fact]
    public void Limit_IsStrictlyUnder()
    {
        Assert.True(FplCalculator.IsUnderLimit(1.99m, 2.0m));
        Assert.False(FplCalculator.IsUnderLimit(2.0m, 2.0m));
        Assert.Null(FplCalculator.IsUnderLimit(null, 2.0m));
    }
}
