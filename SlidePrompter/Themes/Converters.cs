using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SlidePrompter.Models;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace SlidePrompter.Themes;

/// <summary>
/// Converts a boolean to Visibility (supports ConverterParameter="Invert").
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        if (parameter is string param && string.Equals(param, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool visible = value is Visibility v && v == Visibility.Visible;
        if (parameter is string param && string.Equals(param, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }

        return visible;
    }
}

/// <summary>
/// Converts a 0..1 opacity value into the dark teleprompter pill brush (#111115 with dynamic alpha, #D9111115 at 0.85).
/// </summary>
public sealed class OpacityToPillBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double opacity = value is double d ? Math.Clamp(d, 0.25, 1.0) : AppSettings.DefaultBackgroundOpacity;
        byte alpha = (byte)Math.Round(opacity * 255.0);
        var brush = new WpfSolidColorBrush(WpfColor.FromArgb(alpha, 0x11, 0x11, 0x15));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Maps TeleprompterPlaybackState to a subtle status indicator dot brush (green = active/scrolling, amber = paused/waiting).
/// </summary>
public sealed class PlaybackStateToDotBrushConverter : IValueConverter
{
    private static readonly WpfBrush ActiveGreenBrush = CreateFrozen(0xFF, 0x22, 0xC5, 0x5E);
    private static readonly WpfBrush PausedAmberBrush = CreateFrozen(0xFF, 0xF5, 0x9E, 0x0B);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TeleprompterPlaybackState state
            && state is TeleprompterPlaybackState.Scrolling or TeleprompterPlaybackState.PreRollPause)
        {
            return ActiveGreenBrush;
        }

        return PausedAmberBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static WpfSolidColorBrush CreateFrozen(byte a, byte r, byte g, byte b)
    {
        var brush = new WpfSolidColorBrush(WpfColor.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// Subtle border highlight on the overlay pill when Click-Through is temporarily disabled for preview adjustments.
/// </summary>
public sealed class ClickThroughToBorderBrushConverter : IValueConverter
{
    private static readonly WpfBrush GhostBorder = CreateFrozen(0x28, 0xFF, 0xFF, 0xFF);
    private static readonly WpfBrush InteractiveBorder = CreateFrozen(0x88, 0x81, 0x8C, 0xF8);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isClickThrough && !isClickThrough ? InteractiveBorder : GhostBorder;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static WpfSolidColorBrush CreateFrozen(byte a, byte r, byte g, byte b)
    {
        var brush = new WpfSolidColorBrush(WpfColor.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// Maps IsPresenting boolean to the primary CTA button background brush.
/// </summary>
public sealed class PresentingToButtonBrushConverter : IValueConverter
{
    private static readonly WpfBrush StartBrush = CreateFrozen(0xFF, 0x63, 0x66, 0xF1);
    private static readonly WpfBrush StopBrush = CreateFrozen(0xFF, 0xDC, 0x26, 0x26);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool presenting && presenting ? StopBrush : StartBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static WpfSolidColorBrush CreateFrozen(byte a, byte r, byte g, byte b)
    {
        var brush = new WpfSolidColorBrush(WpfColor.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// Maps IsPresenting boolean to a status badge dot brush.
/// </summary>
public sealed class PresentingToStatusDotBrushConverter : IValueConverter
{
    private static readonly WpfBrush ActiveBrush = CreateFrozen(0xFF, 0x22, 0xC5, 0x5E);
    private static readonly WpfBrush IdleBrush = CreateFrozen(0xFF, 0x6B, 0x72, 0x80);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool presenting && presenting ? ActiveBrush : IdleBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static WpfSolidColorBrush CreateFrozen(byte a, byte r, byte g, byte b)
    {
        var brush = new WpfSolidColorBrush(WpfColor.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}

