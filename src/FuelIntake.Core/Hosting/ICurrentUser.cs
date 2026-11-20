namespace FuelIntake.Core.Hosting;

/// <summary>
/// Who is using the app. Phase 1: the Windows login. Phase 2: the Entra (Microsoft 365) sign-in.
/// Screens use this instead of asking Windows directly, so they work unchanged in both phases.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Name to show on screen, e.g. "sbrewer".</summary>
    string DisplayName { get; }

    /// <summary>Work email if known (used to match the Staff list), otherwise null.</summary>
    string? Email { get; }
}
