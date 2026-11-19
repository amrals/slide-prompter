using System.Windows;
using System.Windows.Interop;
using SlidePrompter.Infrastructure.Interop;
using SlidePrompter.Models;
using SlidePrompter.Services.Interfaces;

namespace SlidePrompter.Services;

/// <summary>
/// Manages Win32 display affinity (screen capture exclusion), non-activating ghost styles, and DPI-aware monitor placement.
/// </summary>
public sealed class WindowOverlayService : IWindowOverlayService
{
    public bool SetExcludeFromCapture(Window window, bool excludeFromCapture)
    {
        ArgumentNullException.ThrowIfNull(window);

        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        uint affinity = excludeFromCapture
            ? Win32Constants.WDA_EXCLUDEFROMCAPTURE
            : Win32Constants.WDA_NONE;

        return NativeMethods.SetWindowDisplayAffinity(hwnd, affinity);
    }

    public void InitializeOverlayStyles(Window window, bool clickThrough)
    {
        ArgumentNullException.ThrowIfNull(window);

        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        long exStyle = NativeMethods.GetWindowLongPtrSafe(hwnd, Win32Constants.GWL_EXSTYLE);
        exStyle |= Win32Constants.WS_EX_NOACTIVATE | Win32Constants.WS_EX_TOOLWINDOW;

        if (clickThrough)
        {
            exStyle |= Win32Constants.WS_EX_TRANSPARENT;
        }
        else
        {
            exStyle &= ~Win32Constants.WS_EX_TRANSPARENT;
        }

        NativeMethods.SetWindowLongPtrSafe(hwnd, Win32Constants.GWL_EXSTYLE, exStyle);
        RefreshWindowFrame(hwnd);
    }

    public void SetClickThrough(Window window, bool clickThrough)
    {
        InitializeOverlayStyles(window, clickThrough);
    }

    public void PositionOnMonitor(Window window, MonitorInfo monitor, double barWidthDip, double topMarginDip)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(monitor);

        double dpiScale = monitor.DpiScale > 0 ? monitor.DpiScale : 1.0;
        int desiredPhysicalWidth = (int)Math.Round(barWidthDip * dpiScale);
        int desiredPhysicalTopMargin = (int)Math.Round(topMarginDip * dpiScale);

        int physicalX = monitor.Left + (monitor.Width - desiredPhysicalWidth) / 2;
        int physicalY = monitor.Top + desiredPhysicalTopMargin;

        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            window.Width = barWidthDip;
            window.Left = (monitor.Left / dpiScale) + ((monitor.Width / dpiScale) - barWidthDip) / 2.0;
            window.Top = (monitor.Top / dpiScale) + topMarginDip;
            return;
        }

        // First move onto the target monitor so Per-Monitor V2 DPI scaling synchronizes cleanly if monitor changed.
        NativeMethods.SetWindowPos(
            hwnd,
            Win32Constants.HWND_TOPMOST,
            physicalX,
            physicalY,
            0,
            0,
            Win32Constants.SWP_NOSIZE | Win32Constants.SWP_NOACTIVATE);

        if (Math.Abs(window.Width - barWidthDip) > 0.1)
        {
            window.Width = barWidthDip;
        }

        // Re-center horizontally in physical pixels once WPF applies the target width at the monitor's DPI.
        NativeMethods.SetWindowPos(
            hwnd,
            Win32Constants.HWND_TOPMOST,
            physicalX,
            physicalY,
            0,
            0,
            Win32Constants.SWP_NOSIZE | Win32Constants.SWP_NOACTIVATE);
    }

    private static void RefreshWindowFrame(IntPtr hwnd)
    {
        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            Win32Constants.SWP_NOMOVE
            | Win32Constants.SWP_NOSIZE
            | Win32Constants.SWP_NOZORDER
            | Win32Constants.SWP_NOACTIVATE
            | Win32Constants.SWP_FRAMECHANGED);
    }
}
