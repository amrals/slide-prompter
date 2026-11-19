using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlidePrompter.Models;

namespace SlidePrompter.ViewModels;

/// <summary>
/// Manages runtime teleprompter playback state, slide navigation, and live visual properties for the overlay bar.
/// </summary>
public sealed partial class OverlayViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalSlides))]
    [NotifyPropertyChangedFor(nameof(HasSlides))]
    private IReadOnlyList<SlideItem> _slides = Array.Empty<SlideItem>();

    [ObservableProperty]
    private int _currentSlideIndex;

    [ObservableProperty]
    private string _currentSingleLineText = string.Empty;

    [ObservableProperty]
    private string _currentMultiLineText = string.Empty;

    [ObservableProperty]
    private string _slideCounterText = "0 / 0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActivelyRunning))]
    [NotifyPropertyChangedFor(nameof(IsPausedOrStopped))]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    private TeleprompterPlaybackState _playbackState = TeleprompterPlaybackState.Idle;

    [ObservableProperty]
    private bool _isOverlayVisible;

    [ObservableProperty]
    private bool _isPresenting;

    [ObservableProperty]
    private double _scrollSpeedPxPerSec = AppSettings.DefaultScrollSpeed;

    [ObservableProperty]
    private double _barWidth = AppSettings.DefaultBarWidth;

    [ObservableProperty]
    private double _fontSize = AppSettings.DefaultFontSize;

    [ObservableProperty]
    private double _backgroundOpacity = AppSettings.DefaultBackgroundOpacity;

    [ObservableProperty]
    private double _topMarginPx = AppSettings.DefaultTopMargin;

    [ObservableProperty]
    private double _initialPauseSeconds = AppSettings.DefaultInitialPauseSeconds;

    [ObservableProperty]
    private bool _hideFromCapture = true;

    [ObservableProperty]
    private bool _isClickThrough = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSingleLineMode))]
    [NotifyPropertyChangedFor(nameof(IsCompactBlockMode))]
    private TeleprompterDisplayMode _displayMode = TeleprompterDisplayMode.SingleLineHorizontal;

    [ObservableProperty]
    private string _selectedMonitorDeviceName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHudMessage))]
    private string _hudMessage = string.Empty;

    public int TotalSlides => Slides.Count;

    public bool HasSlides => Slides.Count > 0;

    public bool IsActivelyRunning => PlaybackState is TeleprompterPlaybackState.Scrolling or TeleprompterPlaybackState.PreRollPause;

    public bool IsPausedOrStopped => !IsActivelyRunning;

    public bool IsSingleLineMode => DisplayMode == TeleprompterDisplayMode.SingleLineHorizontal;

    public bool IsCompactBlockMode => DisplayMode == TeleprompterDisplayMode.CompactBlockTwoLines;

    public bool HasHudMessage => !string.IsNullOrWhiteSpace(HudMessage);

    public string StatusLabel => PlaybackState switch
    {
        TeleprompterPlaybackState.Scrolling => "Rolando",
        TeleprompterPlaybackState.PreRollPause => "Iniciando",
        TeleprompterPlaybackState.Paused => "Pausado",
        TeleprompterPlaybackState.Finished => "Fim do slide",
        _ => "Pronto"
    };

    /// <summary>
    /// Raised whenever the overlay rendering engine should reset scroll offset to the start of the active slide.
    /// </summary>
    public event EventHandler? ScrollResetRequested;

    /// <summary>
    /// Raised when geometry, monitor placement, capture affinity, or click-through properties change.
    /// </summary>
    public event EventHandler? OverlayAppearanceChanged;

    /// <summary>
    /// Raised when overlay visibility toggles (e.g. via Ctrl+Alt+H or tray menu).
    /// </summary>
    public event EventHandler<bool>? OverlayVisibilityChanged;

    /// <summary>
    /// Raised when scroll speed is adjusted via global hotkeys so settings remain synchronized.
    /// </summary>
    public event EventHandler<double>? SpeedChangedByHotkey;

    /// <summary>
    /// Raised when a brief HUD message should be shown and auto-cleared by the overlay render loop.
    /// </summary>
    public event EventHandler<string>? HudMessagePosted;

    /// <summary>
    /// Raised when the active slide index changes.
    /// </summary>
    public event EventHandler<int>? ActiveSlideChanged;

    public void LoadSlides(IReadOnlyList<SlideItem> slides, int startIndex = 0, bool beginPlayback = false)
    {
        Slides = slides ?? Array.Empty<SlideItem>();

        if (Slides.Count == 0)
        {
            CurrentSlideIndex = 0;
            CurrentSingleLineText = string.Empty;
            CurrentMultiLineText = string.Empty;
            SlideCounterText = "0 / 0";
            PlaybackState = TeleprompterPlaybackState.Idle;
            ScrollResetRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        int clampedIndex = Math.Clamp(startIndex, 0, Slides.Count - 1);
        ApplyActiveSlide(clampedIndex, beginPlayback);
    }

    public void ApplySettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        bool displayModeChanged = DisplayMode != settings.DisplayMode;

        ScrollSpeedPxPerSec = settings.ScrollSpeedPxPerSec;
        BarWidth = settings.BarWidth;
        FontSize = settings.FontSize;
        BackgroundOpacity = settings.BackgroundOpacity;
        TopMarginPx = settings.TopMarginPx;
        InitialPauseSeconds = settings.InitialPauseSeconds;
        HideFromCapture = settings.HideFromCapture;
        IsClickThrough = settings.IsClickThrough;
        DisplayMode = settings.DisplayMode;
        SelectedMonitorDeviceName = settings.SelectedMonitorDeviceName;

        OverlayAppearanceChanged?.Invoke(this, EventArgs.Empty);

        if (displayModeChanged && HasSlides)
        {
            if (IsPresenting || IsOverlayVisible)
            {
                PlaybackState = TeleprompterPlaybackState.PreRollPause;
            }

            ScrollResetRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    public void NextSlide()
    {
        TryNextSlide();
    }

    public bool TryNextSlide()
    {
        if (!HasSlides || CurrentSlideIndex >= Slides.Count - 1)
        {
            return false;
        }

        ApplyActiveSlide(CurrentSlideIndex + 1, beginPlayback: true);
        return true;
    }

    [RelayCommand]
    public void PreviousSlide()
    {
        TryPreviousSlide();
    }

    public bool TryPreviousSlide()
    {
        if (!HasSlides || CurrentSlideIndex <= 0)
        {
            return false;
        }

        ApplyActiveSlide(CurrentSlideIndex - 1, beginPlayback: true);
        return true;
    }

    [RelayCommand]
    public void GoToSlide(int index)
    {
        if (!HasSlides)
        {
            return;
        }

        int clamped = Math.Clamp(index, 0, Slides.Count - 1);
        ApplyActiveSlide(clamped, beginPlayback: IsPresenting || IsOverlayVisible);
    }

    [RelayCommand]
    public void RestartCurrentSlide()
    {
        if (!HasSlides)
        {
            return;
        }

        PlaybackState = TeleprompterPlaybackState.PreRollPause;
        ScrollResetRequested?.Invoke(this, EventArgs.Empty);
        PostHudMessage("Reiniciado");
    }

    [RelayCommand]
    public void TogglePause()
    {
        if (!HasSlides)
        {
            return;
        }

        switch (PlaybackState)
        {
            case TeleprompterPlaybackState.Scrolling:
            case TeleprompterPlaybackState.PreRollPause:
                PlaybackState = TeleprompterPlaybackState.Paused;
                PostHudMessage("Pausado");
                break;

            case TeleprompterPlaybackState.Paused:
                PlaybackState = TeleprompterPlaybackState.Scrolling;
                PostHudMessage("Rolando");
                break;

            case TeleprompterPlaybackState.Finished:
            case TeleprompterPlaybackState.Idle:
                PlaybackState = TeleprompterPlaybackState.PreRollPause;
                ScrollResetRequested?.Invoke(this, EventArgs.Empty);
                PostHudMessage("Rolando");
                break;
        }
    }

    [RelayCommand]
    public void IncreaseSpeed()
    {
        double updated = Math.Clamp(
            ScrollSpeedPxPerSec + AppSettings.SpeedStep,
            AppSettings.MinScrollSpeed,
            AppSettings.MaxScrollSpeed);

        if (Math.Abs(updated - ScrollSpeedPxPerSec) > 0.001)
        {
            ScrollSpeedPxPerSec = updated;
            SpeedChangedByHotkey?.Invoke(this, updated);
        }

        PostHudMessage($"{ScrollSpeedPxPerSec:0} px/s");
    }

    [RelayCommand]
    public void DecreaseSpeed()
    {
        double updated = Math.Clamp(
            ScrollSpeedPxPerSec - AppSettings.SpeedStep,
            AppSettings.MinScrollSpeed,
            AppSettings.MaxScrollSpeed);

        if (Math.Abs(updated - ScrollSpeedPxPerSec) > 0.001)
        {
            ScrollSpeedPxPerSec = updated;
            SpeedChangedByHotkey?.Invoke(this, updated);
        }

        PostHudMessage($"{ScrollSpeedPxPerSec:0} px/s");
    }

    [RelayCommand]
    public void ToggleVisibility()
    {
        IsOverlayVisible = !IsOverlayVisible;
    }

    public void MarkPreRollComplete()
    {
        if (PlaybackState == TeleprompterPlaybackState.PreRollPause)
        {
            PlaybackState = TeleprompterPlaybackState.Scrolling;
        }
    }

    public void MarkSlideFinished()
    {
        if (PlaybackState == TeleprompterPlaybackState.Scrolling)
        {
            PlaybackState = TeleprompterPlaybackState.Finished;
        }
    }

    public void ClearHudMessage()
    {
        if (!string.IsNullOrEmpty(HudMessage))
        {
            HudMessage = string.Empty;
        }
    }

    public void HandleHotkey(GlobalHotkeyAction action)
    {
        if (!IsPresenting)
        {
            return;
        }

        switch (action)
        {
            case GlobalHotkeyAction.NextSlide:
                NextSlide();
                break;

            case GlobalHotkeyAction.PreviousSlide:
                PreviousSlide();
                break;

            case GlobalHotkeyAction.TogglePause:
                TogglePause();
                break;

            case GlobalHotkeyAction.IncreaseSpeed:
                IncreaseSpeed();
                break;

            case GlobalHotkeyAction.DecreaseSpeed:
                DecreaseSpeed();
                break;

            case GlobalHotkeyAction.RestartSlide:
                RestartCurrentSlide();
                break;

            case GlobalHotkeyAction.ToggleOverlayVisibility:
                ToggleVisibility();
                break;
        }
    }

    public TeleprompterState GetStateSnapshot() => new(
        CurrentSlideIndex,
        TotalSlides,
        PlaybackState,
        DisplayMode,
        IsOverlayVisible,
        IsPresenting,
        ScrollSpeedPxPerSec);

    partial void OnIsOverlayVisibleChanged(bool value)
    {
        OverlayVisibilityChanged?.Invoke(this, value);
    }

    private void ApplyActiveSlide(int index, bool beginPlayback)
    {
        CurrentSlideIndex = index;
        SlideItem slide = Slides[index];

        CurrentSingleLineText = slide.SingleLineText;
        CurrentMultiLineText = slide.RawText;
        SlideCounterText = $"{slide.Number} / {Slides.Count}";

        PlaybackState = beginPlayback
            ? TeleprompterPlaybackState.PreRollPause
            : TeleprompterPlaybackState.Idle;

        ActiveSlideChanged?.Invoke(this, index);
        ScrollResetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PostHudMessage(string message)
    {
        HudMessage = message;
        HudMessagePosted?.Invoke(this, message);
    }
}
