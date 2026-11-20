namespace FuelIntake.Core.Formatting;

/// <summary>Zip codes: exactly 5 digits, kept as text so leading zeros survive (05819).</summary>
public static class Zip
{
    public static bool IsValid(string? input) =>
        input is not null && input.Trim().Length == 5 && input.Trim().All(char.IsAsciiDigit);
}
