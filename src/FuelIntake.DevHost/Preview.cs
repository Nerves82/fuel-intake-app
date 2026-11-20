namespace FuelIntake.DevHost;

public static class Preview
{
#if OFFLINE_PREVIEW
    /// <summary>Built with -p:OfflinePreview=true: static rendering only (no Blazor script available).</summary>
    public const bool Offline = true;
#else
    public const bool Offline = false;
#endif
}
