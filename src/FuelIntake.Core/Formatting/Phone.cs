namespace FuelIntake.Core.Formatting;

/// <summary>
/// US phone numbers. Stored as entered (or as 10 digits); input may contain dashes, spaces,
/// parentheses, periods and stray line breaks. Prints as (802) 555-0123. Leading zeros are kept.
/// </summary>
public static class Phone
{
    /// <summary>Only the digits of <paramref name="input"/>.</summary>
    public static string Digits(string? input) =>
        input is null ? "" : new string(input.Where(char.IsAsciiDigit).ToArray());

    /// <summary>True when the entry contains exactly 10 digits and nothing but digits and separators.</summary>
    public static bool IsValid(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (input.Any(ch => !(char.IsAsciiDigit(ch) || ch is '-' or ' ' or '(' or ')' or '.' or '\r' or '\n' or ' ')))
            return false;
        return Digits(input).Length == 10;
    }

    /// <summary>"(802) 555-0123", or "" when blank. An invalid entry is returned trimmed, unchanged.</summary>
    public static string Format(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        if (!IsValid(input)) return input.Trim();
        var d = Digits(input);
        return $"({d[..3]}) {d[3..6]}-{d[6..]}";
    }
}
