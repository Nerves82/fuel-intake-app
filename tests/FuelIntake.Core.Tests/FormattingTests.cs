using FuelIntake.Core.Formatting;

namespace FuelIntake.Core.Tests;

public class FormattingTests
{
    [Theory]
    [InlineData("8025030127", "(802) 503-0127")]
    [InlineData("802-503-0127", "(802) 503-0127")]
    [InlineData("(802) 503-0127", "(802) 503-0127")]
    [InlineData("802.503.0127 ", "(802) 503-0127")]
    [InlineData("\n8025030127", "(802) 503-0127")]   // stray line break from pasting (the Staff tab bug)
    [InlineData("0802555012", "(080) 255-5012")]     // leading zero kept
    [InlineData("8025550120", "(802) 555-0120")]     // trailing zero kept
    public void Phone_AcceptsCommonEntries(string input, string expected)
    {
        Assert.True(Phone.IsValid(input));
        Assert.Equal(expected, Phone.Format(input));
    }

    [Theory]
    [InlineData("802555012")]
    [InlineData("80255501234")]
    [InlineData("802-555-01x3")]
    public void Phone_RejectsBadEntries(string input) => Assert.False(Phone.IsValid(input));

    [Fact]
    public void Phone_BlankFormatsAsBlank() => Assert.Equal("", Phone.Format("  "));

    [Theory]
    [InlineData("012345678", "012-34-5678")]
    [InlineData("012-34-5678", "012-34-5678")]
    [InlineData("12345678", "012-34-5678")]   // lost its leading zero in a spreadsheet
    [InlineData("8705217", "008-70-5217")]
    public void Ssn_FormatsAndPads(string input, string expected) => Assert.Equal(expected, Ssn.Format(input));

    [Theory]
    [InlineData("012345678", true)]
    [InlineData("012-34-5678", true)]
    [InlineData("12345678", false)]
    [InlineData("01234567a", false)]
    public void Ssn_Validates(string input, bool valid) => Assert.Equal(valid, Ssn.IsValid(input));

    [Fact]
    public void Ssn_Masks() => Assert.Equal("***-**-5678", Ssn.Masked("012345678"));

    [Theory]
    [InlineData("05819", true)]
    [InlineData("5819", false)]
    [InlineData("05819-1234", false)]
    [InlineData("0581a", false)]
    public void Zip_Validates(string input, bool valid) => Assert.Equal(valid, Zip.IsValid(input));

    [Fact]
    public void Money_FormatsAndSums()
    {
        Assert.Equal("$1,234.50", Money.Format(1234.5m));
        Assert.Equal("", Money.Format(null));
        Assert.Null(Money.SumOrNull(null, null));
        Assert.Equal(150m, Money.SumOrNull(100m, null, 50m));
    }
}
