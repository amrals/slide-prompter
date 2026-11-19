namespace SlidePrompter.Models;

/// <summary>
/// Global keyboard shortcuts recognized passively by the low-level keyboard hook.
/// </summary>
public enum GlobalHotkeyAction
{
    NextSlide,
    PreviousSlide,
    TogglePause,
    IncreaseSpeed,
    DecreaseSpeed,
    RestartSlide,
    ToggleOverlayVisibility
}

