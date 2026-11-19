using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using SlidePrompter.Models;
using SlidePrompter.Services.Interfaces;
using SlidePrompter.ViewModels;

namespace SlidePrompter.Views;

/// <summary>
/// Frameless, topmost, capture-excluded teleprompter overlay powered by CompositionTarget.Rendering + Stopwatch delta time.
/// </summary>
public partial class OverlayWindow : Window
{
    private const double InitialReadingZoneRatio = 0.15;
    private const double MaxDeltaSeconds = 0.1;
    private const double VerticalSpeedRatio = 0.25;
    private const double MinVerticalSpeedPxPerSec = 8.0;
    private const double HudDisplayDurationSeconds = 1.5;

    private readonly OverlayViewModel _viewModel;
    private readonly IWindowOverlayService _windowOverlayService;
    private readonly IMonitorService _monitorService;
    private readonly Stopwatch _frameStopwatch = new();

    private TimeSpan _lastFrameTimestamp;
    private bool _isSourceInitialized;
    private bool _isRenderingSubscribed;
    private bool _isUpdatingPosition;
    private bool _needsScrollReset = true;
    private double _preRollElapsedSeconds;
    private double _currentOffsetX;
    private double _currentOffsetY;
    private double _hudRemainingSeconds;

    public OverlayWindow(
        OverlayViewModel viewModel,
        IWindowOverlayService windowOverlayService,
        IMonitorService monitorService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _windowOverlayService = windowOverlayService;
        _monitorService = monitorService;

        DataContext = _viewModel;

        SourceInitialized += OnOverlaySourceInitialized;
        IsVisibleChanged += OnOverlayIsVisibleChanged;
        SizeChanged += OnOverlaySizeChanged;
        VerticalViewport.SizeChanged += OnVerticalViewportSizeChanged;
        Closed += OnOverlayClosed;

        _viewModel.ScrollResetRequested += OnScrollResetRequested;
        _viewModel.OverlayAppearanceChanged += OnOverlayAppearanceChanged;
        _viewModel.HudMessagePosted += OnHudMessagePosted;
    }

    public void ShowOverlay()
    {
        UpdateViewportGeometry();
        ApplyMonitorPosition();

        if (!IsVisible)
        {
            Show();
        }

        if (_isSourceInitialized)
        {
            ApplyWin32WindowSettings();
            ApplyMonitorPosition();
        }

        StartRenderingLoop();
    }

    public void HideOverlay()
    {
        StopRenderingLoop();

        if (IsVisible)
        {
            Hide();
        }
    }

    private void OnOverlaySourceInitialized(object? sender, EventArgs e)
    {
        _isSourceInitialized = true;
        ApplyWin32WindowSettings();
        UpdateViewportGeometry();
        ApplyMonitorPosition();
    }

    private void OnOverlayIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            UpdateViewportGeometry();
            ApplyMonitorPosition();
            StartRenderingLoop();
        }
        else
        {
            StopRenderingLoop();
        }
    }

    private void OnOverlaySizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateViewportGeometry();
        if (_isSourceInitialized && IsVisible && !_isUpdatingPosition)
        {
            ApplyMonitorPosition();
        }
    }

    private void OnVerticalViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 40)
        {
            MultiLineTextBlock.Width = e.NewSize.Width;
        }
    }

    private void OnOverlayClosed(object? sender, EventArgs e)
    {
        StopRenderingLoop();

        SourceInitialized -= OnOverlaySourceInitialized;
        IsVisibleChanged -= OnOverlayIsVisibleChanged;
        SizeChanged -= OnOverlaySizeChanged;
        VerticalViewport.SizeChanged -= OnVerticalViewportSizeChanged;
        Closed -= OnOverlayClosed;

        _viewModel.ScrollResetRequested -= OnScrollResetRequested;
        _viewModel.OverlayAppearanceChanged -= OnOverlayAppearanceChanged;
        _viewModel.HudMessagePosted -= OnHudMessagePosted;
    }

    private void OnScrollResetRequested(object? sender, EventArgs e)
    {
        _needsScrollReset = true;
        _preRollElapsedSeconds = 0;
    }

    private void OnOverlayAppearanceChanged(object? sender, EventArgs e)
    {
        UpdateViewportGeometry();

        if (_isSourceInitialized)
        {
            ApplyWin32WindowSettings();
            ApplyMonitorPosition();
        }
    }

    private void OnHudMessagePosted(object? sender, string message)
    {
        _hudRemainingSeconds = HudDisplayDurationSeconds;
    }

    private void ApplyWin32WindowSettings()
    {
        if (!_isSourceInitialized)
        {
            return;
        }

        _windowOverlayService.InitializeOverlayStyles(this, _viewModel.IsClickThrough);
        _windowOverlayService.SetExcludeFromCapture(this, _viewModel.HideFromCapture);
    }

    private void ApplyMonitorPosition()
    {
        if (_isUpdatingPosition)
        {
            return;
        }

        _isUpdatingPosition = true;
        try
        {
            MonitorInfo targetMonitor = _monitorService.ResolveMonitor(_viewModel.SelectedMonitorDeviceName);
            _windowOverlayService.PositionOnMonitor(this, targetMonitor, _viewModel.BarWidth, _viewModel.TopMarginPx);
        }
        finally
        {
            _isUpdatingPosition = false;
        }
    }

    private void UpdateViewportGeometry()
    {
        double lineHeight = Math.Ceiling(_viewModel.FontSize * 1.42);
        HorizontalCanvas.Height = lineHeight;
        MultiLineTextBlock.LineHeight = lineHeight;
        VerticalViewport.Height = Math.Ceiling(lineHeight * 2.0 + 4.0);

        double verticalWidth = VerticalViewport.ActualWidth;
        if (verticalWidth > 40)
        {
            MultiLineTextBlock.Width = verticalWidth;
        }
        else
        {
            // Fallback estimate before the initial layout pass completes.
            MultiLineTextBlock.Width = Math.Max(200, _viewModel.BarWidth - 110);
        }
    }

    private void StartRenderingLoop()
    {
        if (_isRenderingSubscribed)
        {
            return;
        }

        _frameStopwatch.Restart();
        _lastFrameTimestamp = TimeSpan.Zero;
        CompositionTarget.Rendering += OnCompositionTargetRendering;
        _isRenderingSubscribed = true;
    }

    private void StopRenderingLoop()
    {
        if (!_isRenderingSubscribed)
        {
            return;
        }

        CompositionTarget.Rendering -= OnCompositionTargetRendering;
        _frameStopwatch.Stop();
        _isRenderingSubscribed = false;
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        TimeSpan currentTimestamp = _frameStopwatch.Elapsed;
        double deltaSeconds = (currentTimestamp - _lastFrameTimestamp).TotalSeconds;
        _lastFrameTimestamp = currentTimestamp;

        if (deltaSeconds <= 0)
        {
            return;
        }

        if (deltaSeconds > MaxDeltaSeconds)
        {
            deltaSeconds = MaxDeltaSeconds;
        }

        UpdateHudTimer(deltaSeconds);

        if (_needsScrollReset)
        {
            ResetScrollPositionForCurrentSlide();
            return;
        }

        switch (_viewModel.PlaybackState)
        {
            case TeleprompterPlaybackState.PreRollPause:
                _preRollElapsedSeconds += deltaSeconds;
                if (_preRollElapsedSeconds >= _viewModel.InitialPauseSeconds)
                {
                    _viewModel.MarkPreRollComplete();
                }
                break;

            case TeleprompterPlaybackState.Scrolling:
                AdvanceScroll(deltaSeconds);
                break;
        }
    }

    private void ResetScrollPositionForCurrentSlide()
    {
        UpdateViewportGeometry();

        if (_viewModel.IsSingleLineMode)
        {
            HorizontalViewport.UpdateLayout();
            SingleLineTextBlock.UpdateLayout();

            double viewportWidth = HorizontalViewport.ActualWidth;
            if (viewportWidth <= 0)
            {
                return;
            }

            if (SingleLineTextBlock.ActualHeight > 0)
            {
                double verticalTop = Math.Max(0, (HorizontalCanvas.Height - SingleLineTextBlock.ActualHeight) / 2.0);
                System.Windows.Controls.Canvas.SetTop(SingleLineTextBlock, verticalTop);
            }

            // Start immediately visible inside the left reading zone (~15% from the left edge).
            _currentOffsetX = viewportWidth * InitialReadingZoneRatio;
            _currentOffsetY = 0;

            HorizontalTranslate.X = _currentOffsetX;
            VerticalTranslate.Y = 0;
        }
        else
        {
            UpdateLayout();
            double verticalWidth = VerticalViewport.ActualWidth;
            if (verticalWidth <= 0)
            {
                return;
            }

            MultiLineTextBlock.Width = verticalWidth;
            MultiLineTextBlock.UpdateLayout();

            _currentOffsetX = 0;
            _currentOffsetY = 0;

            HorizontalTranslate.X = 0;
            VerticalTranslate.Y = 0;
        }

        _preRollElapsedSeconds = 0;
        _needsScrollReset = false;
    }

    private void AdvanceScroll(double deltaSeconds)
    {
        if (_viewModel.IsSingleLineMode)
        {
            double textWidth = SingleLineTextBlock.ActualWidth;
            if (textWidth <= 0)
            {
                return;
            }

            _currentOffsetX -= _viewModel.ScrollSpeedPxPerSec * deltaSeconds;

            if (_currentOffsetX <= -textWidth)
            {
                _currentOffsetX = -textWidth;
                HorizontalTranslate.X = _currentOffsetX;
                _viewModel.MarkSlideFinished();
                return;
            }

            HorizontalTranslate.X = _currentOffsetX;
        }
        else
        {
            double viewportHeight = VerticalViewport.ActualHeight;
            double textHeight = MultiLineTextBlock.ActualHeight;
            double maxScrollDistance = Math.Max(0.0, textHeight - viewportHeight);

            if (maxScrollDistance <= 1.0)
            {
                VerticalTranslate.Y = 0;
                _viewModel.MarkSlideFinished();
                return;
            }

            double verticalSpeed = Math.Max(MinVerticalSpeedPxPerSec, _viewModel.ScrollSpeedPxPerSec * VerticalSpeedRatio);
            _currentOffsetY -= verticalSpeed * deltaSeconds;

            if (_currentOffsetY <= -maxScrollDistance)
            {
                _currentOffsetY = -maxScrollDistance;
                VerticalTranslate.Y = _currentOffsetY;
                _viewModel.MarkSlideFinished();
                return;
            }

            VerticalTranslate.Y = _currentOffsetY;
        }
    }

    private void UpdateHudTimer(double deltaSeconds)
    {
        if (_hudRemainingSeconds <= 0)
        {
            return;
        }

        _hudRemainingSeconds -= deltaSeconds;
        if (_hudRemainingSeconds <= 0)
        {
            _hudRemainingSeconds = 0;
            _viewModel.ClearHudMessage();
        }
    }
}
