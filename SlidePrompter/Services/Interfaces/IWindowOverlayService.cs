using System.Windows;
using SlidePrompter.Models;

namespace SlidePrompter.Services.Interfaces;

/// <summary>
/// Applies Win32 overlay behavior (capture exclusion, click-through, non-activating styles, and multi-monitor positioning).
/// </summary>
public interface IWindowOverlayService
{
    /// <summary>
    /// Applies WDA_EXCLUDEFROMCAPTURE or WDA_NONE. Must be called after Window.SourceInitialized.
    /// </summary>
    bool SetExcludeFromCapture(Window window, bool excludeFromCapture);

    /// <summary>
    /// Configures WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW and optional WS_EX_TRANSPARENT styles.
    /// Must be called after Window.SourceInitialized.
    /// </summary>
    void InitializeOverlayStyles(Window window, bool clickThrough);

    /// <summary>
    /// Dynamically toggles WS_EX_TRANSPARENT at runtime without stealing focus.
    /// </summary>
    void SetClickThrough(Window window, bool clickThrough);

    /// <summary>
    /// Horizontally centers the overlay window near the top of the target monitor with DPI awareness.
    /// </summary>
    void PositionOnMonitor(Window window, MonitorInfo monitor, double barWidthDip, double topMarginDip);
}

