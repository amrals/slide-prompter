namespace SlidePrompter.Models;

/// <summary>
/// Display layout modes supported by the teleprompter overlay.
/// </summary>
public enum TeleprompterDisplayMode
{
    /// <summary>
    /// Single horizontal ticker line scrolling right-to-left with side gradient fades.
    /// </summary>
    SingleLineHorizontal = 0,

    /// <summary>
    /// Compact two-line vertical block scrolling upward smoothly.
    /// </summary>
    CompactBlockTwoLines = 1
}

/// <summary>
/// Runtime playback state of the teleprompter scrolling engine.
/// </summary>
public enum TeleprompterPlaybackState
{
    /// <summary>
    /// No active presentation or no slides loaded.
    /// </summary>
    Idle = 0,

    /// <summary>
    /// Initial micro-pause after entering a slide before motion starts.
    /// </summary>
    PreRollPause = 1,

    /// <summary>
    /// Actively scrolling text across the viewport.
    /// </summary>
    Scrolling = 2,

    /// <summary>
    /// Paused manually by the presenter.
    /// </summary>
    Paused = 3,

    /// <summary>
    /// Current slide finished scrolling and is waiting for the next slide (no auto-loop).
    /// </summary>
    Finished = 4
}

/// <summary>
/// Snapshot of the teleprompter runtime state.
/// </summary>
public sealed record TeleprompterState(
    int CurrentSlideIndex,
    int TotalSlides,
    TeleprompterPlaybackState PlaybackState,
    TeleprompterDisplayMode DisplayMode,
    bool IsOverlayVisible,
    bool IsPresenting,
    double ScrollSpeedPxPerSec)
{
    public bool IsRunning => PlaybackState is TeleprompterPlaybackState.Scrolling or TeleprompterPlaybackState.PreRollPause;
}

