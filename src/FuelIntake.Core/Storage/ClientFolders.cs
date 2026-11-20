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

    /// <summary>The data file in each client folder.</summary>
    public const string IntakeFileName = "intake.json";
}
