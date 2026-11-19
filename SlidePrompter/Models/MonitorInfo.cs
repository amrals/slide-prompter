namespace SlidePrompter.Models;

/// <summary>
/// Describes a connected display monitor with physical bounds and DPI scale factor.
/// </summary>
public sealed record MonitorInfo(
    string DeviceName,
    string DisplayName,
    bool IsPrimary,
    int Left,
    int Top,
    int Width,
    int Height,
    double DpiScale);

