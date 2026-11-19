using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SlidePrompter.Models;
using SlidePrompter.ViewModels;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfCursors = System.Windows.Input.Cursors;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace SlidePrompter.Views;

/// <summary>
/// Control and script editor window for SlidePrompter, including a live slide-number gutter synchronized with the editor.
/// </summary>
public partial class ControlWindow : Window
{
    private static readonly WpfBrush ActivePillBg = CreateFrozenBrush(0xFF, 0x63, 0x66, 0xF1);
    private static readonly WpfBrush ActivePillBorder = CreateFrozenBrush(0xFF, 0x81, 0x8C, 0xF8);
    private static readonly WpfBrush ActivePillText = CreateFrozenBrush(0xFF, 0xFF, 0xFF, 0xFF);

    private static readonly WpfBrush ActiveDraftBg = CreateFrozenBrush(0xFF, 0x20, 0x24, 0x3D);
    private static readonly WpfBrush ActiveDraftBorder = CreateFrozenBrush(0xFF, 0x63, 0x66, 0xF1);
    private static readonly WpfBrush ActiveDraftText = CreateFrozenBrush(0xFF, 0xC7, 0xD2, 0xFE);

    private static readonly WpfBrush InactivePillBg = CreateFrozenBrush(0xFF, 0x1E, 0x22, 0x2E);
    private static readonly WpfBrush InactivePillBorder = CreateFrozenBrush(0xFF, 0x2F, 0x36, 0x4B);
    private static readonly WpfBrush InactivePillText = CreateFrozenBrush(0xFF, 0xA5, 0xB4, 0xFC);

    private static readonly WpfBrush InactiveDraftBg = CreateFrozenBrush(0xFF, 0x17, 0x1A, 0x24);
    private static readonly WpfBrush InactiveDraftBorder = CreateFrozenBrush(0xFF, 0x2A, 0x30, 0x42);
    private static readonly WpfBrush InactiveDraftText = CreateFrozenBrush(0xFF, 0x6B, 0x72, 0x80);

    private static readonly WpfBrush DividerLineBrush = CreateFrozenBrush(0xFF, 0x23, 0x28, 0x38);
    private static readonly WpfBrush FocusBorderBrush = CreateFrozenBrush(0xFF, 0x63, 0x66, 0xF1);
    private static readonly WpfBrush DefaultBorderBrush = CreateFrozenBrush(0xFF, 0x2A, 0x2F, 0x3E);
    private static readonly WpfFontFamily BadgeFont = new("Segoe UI");

    private readonly ControlViewModel _viewModel;
    private bool _gutterRefreshQueued;

    public ControlWindow(ControlViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += OnWindowLoaded;
        Closed += OnWindowClosed;

        _viewModel.SlideMarkersChanged += OnSlideMarkersChanged;
        _viewModel.NavigateEditorToCharIndexRequested += OnNavigateEditorToCharIndexRequested;
    }

    public void RestoreAndActivate()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.SaveSettings();
        base.OnClosing(e);
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.UpdateCaretPosition(ScriptEditorTextBox.CaretIndex);
        QueueSlideGutterRefresh();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Loaded -= OnWindowLoaded;
        Closed -= OnWindowClosed;

        _viewModel.SlideMarkersChanged -= OnSlideMarkersChanged;
        _viewModel.NavigateEditorToCharIndexRequested -= OnNavigateEditorToCharIndexRequested;
    }

    private void OnSlideMarkersChanged(object? sender, EventArgs e)
    {
        _viewModel.UpdateCaretPosition(ScriptEditorTextBox.CaretIndex);
        QueueSlideGutterRefresh();
    }

    private void OnNavigateEditorToCharIndexRequested(object? sender, int charIndex)
    {
        string text = ScriptEditorTextBox.Text ?? string.Empty;
        int clamped = Math.Clamp(charIndex, 0, text.Length);

        ScriptEditorTextBox.Focus();
        ScriptEditorTextBox.CaretIndex = clamped;
        ScriptEditorTextBox.SelectionLength = 0;

        int lineIndex = ScriptEditorTextBox.GetLineIndexFromCharacterIndex(clamped);
        if (lineIndex >= 0)
        {
            ScriptEditorTextBox.ScrollToLine(lineIndex);
        }

        _viewModel.UpdateCaretPosition(clamped);
        QueueSlideGutterRefresh();
    }

    private void OnInsertNewSlideClick(object sender, RoutedEventArgs e)
    {
        string text = ScriptEditorTextBox.Text ?? string.Empty;
        int insertPos = Math.Clamp(ScriptEditorTextBox.SelectionStart, 0, text.Length);

        bool needsLeadingNewLine = insertPos > 0 && text[insertPos - 1] != '\n' && text[insertPos - 1] != '\r';
        string prefix = needsLeadingNewLine ? Environment.NewLine : string.Empty;
        string insertion = $"{prefix}---{Environment.NewLine}";

        ScriptEditorTextBox.SelectedText = insertion;
        int newCaret = insertPos + insertion.Length;
        ScriptEditorTextBox.CaretIndex = Math.Clamp(newCaret, 0, (ScriptEditorTextBox.Text ?? string.Empty).Length);
        ScriptEditorTextBox.SelectionLength = 0;
        ScriptEditorTextBox.Focus();

        int lineIndex = ScriptEditorTextBox.GetLineIndexFromCharacterIndex(ScriptEditorTextBox.CaretIndex);
        if (lineIndex >= 0)
        {
            ScriptEditorTextBox.ScrollToLine(lineIndex);
        }

        _viewModel.UpdateCaretPosition(ScriptEditorTextBox.CaretIndex);
        QueueSlideGutterRefresh();
    }

    private void OnEditorScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        RefreshSlideGutterImmediate();
    }

    private void OnEditorSelectionChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.UpdateCaretPosition(ScriptEditorTextBox.CaretIndex);
        RefreshSlideGutterImmediate();
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        QueueSlideGutterRefresh();
    }

    private void OnEditorSizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueSlideGutterRefresh();
    }

    private void OnEditorGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        EditorContainerBorder.BorderBrush = FocusBorderBrush;
    }

    private void OnEditorLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        EditorContainerBorder.BorderBrush = DefaultBorderBrush;
    }

    private void QueueSlideGutterRefresh()
    {
        if (_gutterRefreshQueued)
        {
            return;
        }

        _gutterRefreshQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _gutterRefreshQueued = false;
            RefreshSlideGutterImmediate();
        }, DispatcherPriority.Loaded);
    }

    private void RefreshSlideGutterImmediate()
    {
        if (!IsLoaded || SlideGutterCanvas.ActualHeight <= 0)
        {
            return;
        }

        SlideGutterCanvas.Children.Clear();

        IReadOnlyList<SlideMarker> markers = _viewModel.SlideMarkers;
        if (markers.Count == 0)
        {
            return;
        }

        string text = ScriptEditorTextBox.Text ?? string.Empty;
        int activeSlideNumber = _viewModel.CaretSlideNumber;
        double gutterHeight = SlideGutterCanvas.ActualHeight;
        double gutterWidth = SlideGutterCanvas.ActualWidth;

        foreach (SlideMarker marker in markers)
        {
            int charIndex = Math.Clamp(marker.FirstContentCharIndex, 0, text.Length);
            Rect lineRect = ScriptEditorTextBox.GetRectFromCharacterIndex(charIndex);

            if (lineRect.IsEmpty || double.IsNaN(lineRect.Top) || double.IsInfinity(lineRect.Top))
            {
                continue;
            }

            if (lineRect.Bottom < -28 || lineRect.Top > gutterHeight + 28)
            {
                continue;
            }

            bool isActive = marker.SlideNumber == activeSlideNumber;
            const double badgeHeight = 20.0;
            double badgeTop = lineRect.Top + Math.Max(0.0, (lineRect.Height - badgeHeight) / 2.0);

            // Subtle divider line above subsequent slides to visually anchor each slide block.
            if (marker.SlideNumber > 1 && badgeTop > 6)
            {
                var divider = new Border
                {
                    Width = Math.Max(0, gutterWidth - 12),
                    Height = 1,
                    Background = DividerLineBrush,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(divider, 6);
                Canvas.SetTop(divider, badgeTop - 6);
                SlideGutterCanvas.Children.Add(divider);
            }

            // Left accent indicator bar on the currently focused slide.
            if (isActive)
            {
                var accentBar = new Border
                {
                    Width = 3,
                    Height = badgeHeight,
                    CornerRadius = new CornerRadius(0, 2, 2, 0),
                    Background = ActivePillBg,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(accentBar, 0);
                Canvas.SetTop(accentBar, badgeTop);
                SlideGutterCanvas.Children.Add(accentBar);
            }

            (WpfBrush bg, WpfBrush border, WpfBrush fg) = (isActive, marker.IsEmptyDraft) switch
            {
                (true, false) => (ActivePillBg, ActivePillBorder, ActivePillText),
                (true, true) => (ActiveDraftBg, ActiveDraftBorder, ActiveDraftText),
                (false, false) => (InactivePillBg, InactivePillBorder, InactivePillText),
                (false, true) => (InactiveDraftBg, InactiveDraftBorder, InactiveDraftText)
            };

            var badge = new Border
            {
                Width = 58,
                Height = badgeHeight,
                CornerRadius = new CornerRadius(5),
                Background = bg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                Cursor = WpfCursors.Hand,
                ToolTip = marker.IsEmptyDraft
                    ? $"Novo Slide {marker.SlideNumber} (comece a digitar)"
                    : $"Ir para o Slide {marker.SlideNumber}",
                Child = new TextBlock
                {
                    Text = $"Slide {marker.SlideNumber}",
                    FontFamily = BadgeFont,
                    FontSize = 10.5,
                    FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = fg,
                    HorizontalAlignment = WpfHorizontalAlignment.Center,
                    VerticalAlignment = WpfVerticalAlignment.Center
                }
            };

            SlideMarker capturedMarker = marker;
            badge.MouseLeftButtonDown += (_, args) =>
            {
                args.Handled = true;
                _viewModel.SelectSlideMarker(capturedMarker);
            };

            Canvas.SetLeft(badge, 8);
            Canvas.SetTop(badge, badgeTop);
            SlideGutterCanvas.Children.Add(badge);
        }
    }

    private static WpfSolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new WpfSolidColorBrush(WpfColor.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
