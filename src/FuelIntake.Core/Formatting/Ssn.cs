namespace FuelIntake.Core.Formatting;

/// <summary>
/// Social Security numbers. Input: 9 digits with or without dashes. Shorter all-digit entries are
/// left-padded with zeros (an SSN that lost its leading zeros in a spreadsheet). Prints as 012-34-5678.
/// </summary>
public static class Ssn
{
    public static bool IsValid(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var s = input.Trim().Replace("-", "");
        return s.Length == 9 && s.All(char.IsAsciiDigit);
    }

    /// <summary>"012-34-5678"; "" when blank; trimmed input unchanged when it isn't digits.</summary>
    public static string Format(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var s = input.Trim().Replace("-", "");
        if (s.Length is 0 or > 9 || !s.All(char.IsAsciiDigit)) return input.Trim();
        s = s.PadLeft(9, '0');
        return $"{s[..3]}-{s[3..5]}-{s[5..]}";
    }

    /// <summary>"***-**-5678" for screens where the full number isn't needed.</summary>
    public static string Masked(string? input)
    {
        var f = Format(input);
        return f.Length == 11 ? "***-**-" + f[7..] : f;
    }
}
