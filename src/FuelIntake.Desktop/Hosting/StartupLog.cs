using System.IO;

namespace FuelIntake.Desktop.Hosting;

/// <summary>
/// Writes app start-up and error details to %LOCALAPPDATA%\FuelIntake\logs.
/// Never write client data here: only app events and error details.
/// </summary>
public static class StartupLog
{
    public static string AppDataFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FuelIntake");

    public static string LogFile { get; } =
        Path.Combine(AppDataFolder, "logs", $"fuelintake-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Write(string message, Exception? ex = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";
            if (ex is not null) text += ex + Environment.NewLine;
            File.AppendAllText(LogFile, text);
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
