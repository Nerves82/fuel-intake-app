namespace FuelIntake.UI.Printing;

/// <summary>Logo and stylesheet embedded in the UI assembly, so printed HTML is fully self-contained.</summary>
public static class FormAssets
{
    public static string LogoDataUri { get; } = "data:image/png;base64," + Convert.ToBase64String(Read("nekca-logo.png"));

    public static string Css { get; } = System.Text.Encoding.UTF8.GetString(Read("forms.css"))
        .Replace("{{LOGO}}", LogoDataUri);

    private static byte[] Read(string name)
    {
        using var s = typeof(FormAssets).Assembly.GetManifestResourceStream(name)
                      ?? throw new InvalidOperationException($"Missing embedded resource {name}");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
