namespace SlidePrompter.Services.Interfaces;

/// <summary>
/// Manages the Windows system tray icon and its context menu actions.
/// </summary>
public interface ITrayIconService : IDisposable
{
    event EventHandler? ShowControlWindowRequested;

    event EventHandler? ToggleOverlayVisibilityRequested;

    event EventHandler? TogglePresentationRequested;

    event EventHandler? ExitRequested;

    void Initialize();

    void UpdateState(bool isPresenting, bool isOverlayVisible);
}

