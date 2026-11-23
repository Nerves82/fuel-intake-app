using System.Text.RegularExpressions;

namespace FuelIntake.Core.Storage;

/// <summary>One file to rename inside a client folder (names only, no paths).</summary>
public sealed record FileRename(string From, string To);

/// <summary>What renaming a client will do.</summary>
/// <param name="FolderFrom">The folder's current name.</param>
/// <param name="FolderTo">Its new name ("Last, First", with "_TEST " in front for test clients).</param>
/// <param name="Files">Files whose names contain the old name, with their new names.</param>
public sealed record ClientRenamePlan(string FolderFrom, string FolderTo, IReadOnlyList<FileRename> Files)
{
    public bool FolderChanges => !string.Equals(FolderFrom, FolderTo, StringComparison.Ordinal);
    public bool HasChanges => FolderChanges || Files.Count > 0;
}

/// <summary>
/// Correcting a misspelled client name: works out the new folder name and which files to rename.
/// The old name is read from the folder name ("Last, First"), because by the time staff rename,
/// the name on tab 1 has already been corrected.
/// </summary>
public static class ClientRename
{
    /// <summary>Reads "Smith, Jane" (or "_TEST Smith, Jane") back into its parts. Null if it isn't in that form.</summary>
    public static (string First, string Last, bool IsTest)? ParseFolderName(string folderName, string separator = ", ")
    {
        var name = folderName.Trim();
        var isTest = name.StartsWith(ClientFolders.TestPrefix, StringComparison.Ordinal);
        if (isTest) name = name[ClientFolders.TestPrefix.Length..];
        var i = name.IndexOf(separator, StringComparison.Ordinal);
        if (i <= 0 || i + separator.Length >= name.Length) return null;
        var last = name[..i].Trim();
        var first = name[(i + separator.Length)..].Trim();
        return last.Length == 0 || first.Length == 0 ? null : (first, last, isTest);
    }

    /// <summary>The folder name for a client: "_TEST " in front for test clients.</summary>
    public static string FolderNameFor(string first, string last, bool isTest, string separator = ", ") =>
        ClientFolders.FolderName(first, (isTest ? ClientFolders.TestPrefix : "") + last.Trim(), separator);

    /// <summary>
    /// The plan for renaming the folder <paramref name="folderName"/> to the corrected name, given the
    /// files now in it. Null when the folder name can't be read as "Last, First".
    /// </summary>
    public static ClientRenamePlan? Plan(string folderName, string newFirst, string newLast, IEnumerable<string> fileNames, string separator = ", ")
    {
        if (ParseFolderName(folderName, separator) is not { } old) return null;
        newFirst = newFirst.Trim(); newLast = newLast.Trim();
        var to = FolderNameFor(newFirst, newLast, old.IsTest, separator);

        var files = new List<FileRename>();
        var taken = new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);
        foreach (var file in fileNames.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(file, ClientFolders.IntakeFileName, StringComparison.OrdinalIgnoreCase)) continue;
            var renamed = RenameFile(file, old.First, old.Last, newFirst, newLast);
            if (renamed is null || string.Equals(renamed, file, StringComparison.Ordinal)) continue;
            // never overwrite another file: add " (2)", " (3)"… when the new name is already used
            var unique = renamed;
            for (var n = 2; taken.Contains(unique) && !string.Equals(unique, file, StringComparison.OrdinalIgnoreCase); n++)
                unique = $"{Path.GetFileNameWithoutExtension(renamed)} ({n}){Path.GetExtension(renamed)}";
            taken.Remove(file);
            taken.Add(unique);
            files.Add(new FileRename(file, unique));
        }
        return new ClientRenamePlan(folderName, to, files);
    }

    private const string Sep = @"[\s_\-,.()]";

    /// <summary>
    /// Swaps the old name for the new one in a file name. The app's files start "J_Smith_…" (first
    /// initial, last name); the whole first or last name is also replaced where it stands on its own
    /// (between spaces, underscores, dashes, commas, dots or brackets). Null if nothing matches.
    /// </summary>
    public static string? RenameFile(string fileName, string oldFirst, string oldLast, string newFirst, string newLast)
    {
        var ext = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var changed = false;

        // "J_Smith_" at the start: the initial follows the first name
        if (oldFirst.Length > 0 && newFirst.Length > 0)
        {
            var prefix = new Regex($"^{Regex.Escape(oldFirst[..1])}_(?={Regex.Escape(oldLast)}{Sep})", RegexOptions.IgnoreCase);
            if (prefix.IsMatch(stem) && !string.Equals(oldFirst[..1], newFirst[..1], StringComparison.Ordinal))
            {
                stem = prefix.Replace(stem, newFirst[..1] + "_", 1);
                changed = true;
            }
            else if (prefix.IsMatch(stem)) changed = true; // initial unchanged, the last name still is
        }

        // whole names, longest first so "Mary Ann" wins over "Mary"; one pass, so a new name is never replaced again
        var names = new[] { (Old: oldLast, New: newLast), (Old: oldFirst, New: newFirst) }
            .Where(x => x.Old.Length > 0 && !string.Equals(x.Old, x.New, StringComparison.Ordinal))
            .OrderByDescending(x => x.Old.Length)
            .ToList();
        if (names.Count > 0)
        {
            var pattern = new Regex($"(?<=^|{Sep})({string.Join("|", names.Select(n => Regex.Escape(n.Old)))})(?={Sep}|$)", RegexOptions.IgnoreCase);
            var replaced = pattern.Replace(stem, m =>
                names.First(n => string.Equals(n.Old, m.Value, StringComparison.OrdinalIgnoreCase)).New);
            if (replaced != stem) { stem = replaced; changed = true; }
        }

        if (!changed) return null;
        var result = ClientFolders.SafeName(stem) + ext;
        return result == fileName ? null : result;
    }
}
