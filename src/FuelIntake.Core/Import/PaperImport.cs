using FuelIntake.Core.Model;

namespace FuelIntake.Core.Import;

/// <summary>What kind of value a field holds: decides how it's checked and stored.</summary>
public enum ImportFieldKind { Text, Date, Money, Ssn, Phone, Zip, Check, Choice }

/// <summary>
/// A paper application being turned into a client: the scanned pages and every field read from them,
/// each of which a staff member must confirm ("This is correct") before the client can be created.
/// Kept in a holding folder ("_PENDING …") in the client files folder until then, so it can take
/// several sessions and other staff can see it.
/// </summary>
public sealed class PaperImport
{
    public const int CurrentFormatVersion = 1;
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    public StaffStamp? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The scanned files, as copied into the holding folder.</summary>
    public List<string> ScanFiles { get; set; } = [];

    /// <summary>How many household members (including the client) have fields on the approval page.</summary>
    public int MemberCount { get; set; } = 1;

    public List<ImportField> Fields { get; set; } = [];

    /// <summary>When the scans were read (null until then), and what came of it ("12 fields filled in").</summary>
    public DateTimeOffset? ReadAt { get; set; }
    public string? ReadNote { get; set; }
}

/// <summary>One field on the approval page.</summary>
public sealed class ImportField
{
    /// <summary>Where the value goes in intake.json, e.g. "client.firstName", "household[1].ssn", "incomeExpenses.income.Earned".</summary>
    public string Path { get; set; } = "";
    public string Label { get; set; } = "";
    /// <summary>Heading it's listed under ("Client", "Member 2", "Income"…).</summary>
    public string Group { get; set; } = "";
    public ImportFieldKind Kind { get; set; }
    /// <summary>Allowed values for <see cref="ImportFieldKind.Choice"/>.</summary>
    public List<string>? Options { get; set; }

    /// <summary>The value as it stands (what was read from the scan, then any staff edits). Checkboxes: "Yes" or blank.</summary>
    public string? Value { get; set; }
    /// <summary>What the scan reader produced, kept to show what was changed.</summary>
    public string? ReadValue { get; set; }
    /// <summary>How sure the reader was (0–1); null when nothing was read.</summary>
    public double? Confidence { get; set; }
    /// <summary>Where on the scans it was read from, to zoom to.</summary>
    public ScanSpot? Spot { get; set; }

    public bool Confirmed { get; set; }
    public StaffStamp? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
}

/// <summary>A box on a scanned page; X, Y, Width and Height are fractions of the page (0–1).</summary>
public sealed record ScanSpot(string File, int Page, double X, double Y, double Width, double Height);
