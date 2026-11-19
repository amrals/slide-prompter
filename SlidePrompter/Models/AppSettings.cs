namespace SlidePrompter.Models;

/// <summary>
/// Persisted application settings stored in %AppData%/SlidePrompter/settings.json.
/// </summary>
public sealed class AppSettings
{
    public const double MinScrollSpeed = 15.0;
    public const double MaxScrollSpeed = 300.0;
    public const double DefaultScrollSpeed = 85.0;
    public const double SpeedStep = 15.0;

    public const double MinBarWidth = 350.0;
    public const double MaxBarWidth = 1200.0;
    public const double DefaultBarWidth = 600.0;

    public const double MinFontSize = 14.0;
    public const double MaxFontSize = 48.0;
    public const double DefaultFontSize = 22.0;

    public const double MinBackgroundOpacity = 0.25;
    public const double MaxBackgroundOpacity = 1.0;
    public const double DefaultBackgroundOpacity = 0.85;

    public const double MinTopMargin = 0.0;
    public const double MaxTopMargin = 400.0;
    public const double DefaultTopMargin = 8.0;

    public const double MinInitialPauseSeconds = 0.0;
    public const double MaxInitialPauseSeconds = 2.0;
    public const double DefaultInitialPauseSeconds = 0.4;

    public double ScrollSpeedPxPerSec { get; set; } = DefaultScrollSpeed;

    public double BarWidth { get; set; } = DefaultBarWidth;

    public double FontSize { get; set; } = DefaultFontSize;

    public double BackgroundOpacity { get; set; } = DefaultBackgroundOpacity;

    public double TopMarginPx { get; set; } = DefaultTopMargin;

    public double InitialPauseSeconds { get; set; } = DefaultInitialPauseSeconds;

    public string SelectedMonitorDeviceName { get; set; } = string.Empty;

    public bool HideFromCapture { get; set; } = true;

    public bool IsClickThrough { get; set; } = true;

    public bool UseSpaceToAdvance { get; set; } = false;

    public TeleprompterDisplayMode DisplayMode { get; set; } = TeleprompterDisplayMode.SingleLineHorizontal;

    public string? LastScriptContent { get; set; }

    public string? LastScriptFilePath { get; set; }

    /// <summary>
    /// Ensures all numeric and enum settings remain within valid operational ranges.
    /// </summary>
    public void Normalize()
    {
        ScrollSpeedPxPerSec = ClampOrDefault(ScrollSpeedPxPerSec, MinScrollSpeed, MaxScrollSpeed, DefaultScrollSpeed);
        BarWidth = ClampOrDefault(BarWidth, MinBarWidth, MaxBarWidth, DefaultBarWidth);
        FontSize = ClampOrDefault(FontSize, MinFontSize, MaxFontSize, DefaultFontSize);
        BackgroundOpacity = ClampOrDefault(BackgroundOpacity, MinBackgroundOpacity, MaxBackgroundOpacity, DefaultBackgroundOpacity);
        TopMarginPx = ClampOrDefault(TopMarginPx, MinTopMargin, MaxTopMargin, DefaultTopMargin);
        InitialPauseSeconds = ClampOrDefault(InitialPauseSeconds, MinInitialPauseSeconds, MaxInitialPauseSeconds, DefaultInitialPauseSeconds);
        SelectedMonitorDeviceName ??= string.Empty;

        if (!Enum.IsDefined(DisplayMode))
        {
            DisplayMode = TeleprompterDisplayMode.SingleLineHorizontal;
        }
    }

    public AppSettings Clone() => new()
    {
        ScrollSpeedPxPerSec = ScrollSpeedPxPerSec,
        BarWidth = BarWidth,
        FontSize = FontSize,
        BackgroundOpacity = BackgroundOpacity,
        TopMarginPx = TopMarginPx,
        InitialPauseSeconds = InitialPauseSeconds,
        SelectedMonitorDeviceName = SelectedMonitorDeviceName,
        HideFromCapture = HideFromCapture,
        IsClickThrough = IsClickThrough,
        UseSpaceToAdvance = UseSpaceToAdvance,
        DisplayMode = DisplayMode,
        LastScriptContent = LastScriptContent,
        LastScriptFilePath = LastScriptFilePath
    };

    private static double ClampOrDefault(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }
}

