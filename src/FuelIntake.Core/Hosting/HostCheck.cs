namespace FuelIntake.Core.Hosting;

/// <summary>
/// Simple checks the "It works" page runs to show whether this PC can run the app properly.
/// </summary>
public static class HostCheck
{
    /// <summary>One line of the check list.</summary>
    public sealed record Result(string Name, bool Passed, string Detail);

    /// <summary>
    /// Tries to create, read back and remove a small file in <paramref name="folder"/>.
    /// Used to confirm the app may write to the user's (OneDrive-synced) folders.
    /// The file holds no client data.
    /// </summary>
    public static Result CanWriteTo(string name, string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
                return new Result(name, false, $"Folder not found: {folder}");

            var path = Path.Combine(folder, $".fuelintake-check-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(path, "fuel intake write check");
            var ok = File.ReadAllText(path) == "fuel intake write check";
            File.Delete(path);
            return new Result(name, ok, ok ? folder : "File read back incorrectly");
        }
        catch (Exception ex)
        {
            return new Result(name, false, ex.Message);
        }
    }
}
