using System.Text;
using FuelIntake.Core.Model;

namespace FuelIntake.UI.Services;

/// <summary>
/// Where a field lives (tab + section/card), cascaded down so every field can name itself for
/// "right-click → Add note" and be found again when the note's link is clicked.
/// </summary>
public sealed record FieldScope(Screen Screen, string? Context = null)
{
    public FieldScope Within(string context) => this with { Context = Context is null ? context : $"{Context} › {context}" };

    public NoteFieldRef RefFor(string label)
    {
        var key = Slug($"{Context}|{label}");
        var parts = new[] { TabTitle(Screen), Context, label }.Where(p => !string.IsNullOrWhiteSpace(p));
        return new NoteFieldRef(Screen.ToString(), key, string.Join(" › ", parts));
    }

    /// <summary>Attributes put on a field's wrapper so the right-click menu and the note links can find it.</summary>
    public Dictionary<string, object> Attributes(string label)
    {
        var r = RefFor(label);
        return new()
        {
            ["id"] = ElementId(r),
            ["data-field-screen"] = r.Screen,
            ["data-field-key"] = r.Key,
            ["data-field-label"] = r.Label,
        };
    }

    public static string ElementId(NoteFieldRef r) => $"fld-{r.Screen}-{r.Key}";

    public static string TabTitle(Screen s) => s switch
    {
        Screen.Client => "Client",
        Screen.Household => "Household",
        Screen.Application => "Application",
        Screen.IncomeExpenses => "Income & Expenses",
        Screen.FuelElectric => "Fuel & Electric",
        _ => s.ToString(),
    };

    private static string Slug(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text.ToLowerInvariant())
            sb.Append(char.IsAsciiLetterOrDigit(ch) ? ch : '-');
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
    }
}
