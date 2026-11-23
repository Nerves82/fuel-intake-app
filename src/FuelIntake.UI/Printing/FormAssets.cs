namespace FuelIntake.UI.Printing;

/// <summary>Logo and stylesheet embedded in the UI assembly, so printed HTML is fully self-contained.</summary>
public static class FormAssets
{
    public static string LogoDataUri { get; } = "data:image/png;base64," + Convert.ToBase64String(Read("nekca-logo.png"));

    public static string Css { get; } = System.Text.Encoding.UTF8.GetString(Read("forms.css"))
        .Replace("{{LOGO}}", LogoDataUri);

    /// <summary>The state's 201SF pages (1-3) as images, drawn behind the filled-in values.</summary>
    public static string Form201SFPage(int page) => form201SF[page - 1].Value;

    private static readonly Lazy<string>[] form201SF = Enumerable.Range(1, 3)
        .Select(i => new Lazy<string>(() => "data:image/png;base64," + Convert.ToBase64String(Read($"201sf-page{i}.png"))))
        .ToArray();

    private static byte[] Read(string name)
    {
        using var s = typeof(FormAssets).Assembly.GetManifestResourceStream(name)
                      ?? throw new InvalidOperationException($"Missing embedded resource {name}");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
