using System.Diagnostics;
using FuelIntake.Core.Hosting;

namespace FuelIntake.Desktop.Hosting;

/// <summary>Phase 1: the person logged into Windows. Email comes from "whoami /upn" when available.</summary>
public sealed class WindowsCurrentUser : ICurrentUser
{
    private readonly Lazy<string?> email = new(ReadUpn);

    public string DisplayName => Environment.UserName;

    public string? Email => email.Value;

    private static string? ReadUpn()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("whoami", "/upn")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd().Trim();
            if (!p.WaitForExit(3000) || p.ExitCode != 0) return null;
            return output.Contains('@') ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
