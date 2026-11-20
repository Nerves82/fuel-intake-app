using System.Text.Json;
using System.Text.Json.Serialization;
using FuelIntake.Core.Model;

namespace FuelIntake.Core.Storage;

/// <summary>Reads and writes intake.json. Same format in phase 1 and phase 2.</summary>
public static class IntakeJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(IntakeRecord record) => JsonSerializer.Serialize(record, Options);

    /// <summary>Reads a record. Throws <see cref="IntakeFormatException"/> for unreadable or too-new files.</summary>
    public static IntakeRecord Deserialize(string json)
    {
        IntakeRecord? record;
        try { record = JsonSerializer.Deserialize<IntakeRecord>(json, Options); }
        catch (JsonException ex) { throw new IntakeFormatException("This intake file is damaged or isn't an intake file.", ex); }

        if (record is null) throw new IntakeFormatException("This intake file is empty.");
        if (record.FormatVersion > IntakeRecord.CurrentFormatVersion)
            throw new IntakeFormatException(
                $"This intake file was saved by a newer version of the app (format {record.FormatVersion}). Update the app to open it.");
        // Older versions are upgraded here as the format evolves.
        record.FormatVersion = IntakeRecord.CurrentFormatVersion;
        return record;
    }
}

public sealed class IntakeFormatException(string message, Exception? inner = null) : Exception(message, inner);
