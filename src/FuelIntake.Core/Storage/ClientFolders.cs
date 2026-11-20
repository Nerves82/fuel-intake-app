namespace FuelIntake.Core.Storage;

/// <summary>Client folder naming, shared by the desktop and web storage.</summary>
public static class ClientFolders
{
    /// <summary>"Smith, Jane".</summary>
    public static string FolderName(string? first, string? last, string separator = ", ") =>
        SafeName($"{last?.Trim()}{separator}{first?.Trim()}");

    /// <summary>Removes characters not allowed in Windows/SharePoint file and folder names, and trims trailing dots/spaces.</summary>
    public static string SafeName(string name)
    {
        var bad = new HashSet<char>(['\\', '/', ':', '*', '?', '"', '<', '>', '|', '#', '%']);
        var cleaned = new string(name.Where(c => !bad.Contains(c) && !char.IsControl(c)).ToArray());
        return cleaned.Trim().TrimEnd('.').Trim();
    }

    /// <summary>
    /// Name for a template copied into a client folder: "T_Sampleton_FA (pg. 2.3-2.6) - Consent Forms_11_20_26.pdf"
    /// (initial, last name, template name, date; same as the old New Client Folder script).
    /// </summary>
    public static string TemplateCopyName(string templateFileName, string? first, string? last, DateOnly date, string dateFormat = "MM_dd_yy")
    {
        first = first?.Trim() ?? ""; last = last?.Trim() ?? "";
        var ext = Path.GetExtension(templateFileName);
        var baseName = Path.GetFileNameWithoutExtension(templateFileName);
        var initial = first.Length > 0 ? first[..1] : "";
        return SafeName($"{initial}_{last}_{baseName}_{date.ToString(dateFormat, System.Globalization.CultureInfo.InvariantCulture)}") + ext;
    }

    /// <summary>Folders whose names start with this are app data or test folders, not clients.</summary>
    public const string HiddenPrefix = "_";

    /// <summary>Test clients get this prefix so they can never be mistaken for real clients.</summary>
    public const string TestPrefix = "_TEST ";

    /// <summary>The data file in each client folder.</summary>
    public const string IntakeFileName = "intake.json";
}
