using System.Runtime.InteropServices;
using SlidePrompter.Infrastructure.Interop;
using SlidePrompter.Models;
using SlidePrompter.Services.Interfaces;

namespace SlidePrompter.Services;

/// <summary>
/// Enumerates connected displays using Win32 Monitor APIs with Per-Monitor DPI detection.
/// </summary>
public sealed class MonitorService : IMonitorService
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var rawMonitors = new List<(string DeviceName, bool IsPrimary, NativeMethods.RECT Bounds, double DpiScale)>();

        bool Callback(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData)
        {
            var info = new NativeMethods.MONITORINFOEX
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
            };

            if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                bool isPrimary = (info.dwFlags & Win32Constants.MONITORINFOF_PRIMARY) != 0;
                double dpiScale = GetMonitorDpiScale(hMonitor);
                rawMonitors.Add((info.szDevice, isPrimary, info.rcMonitor, dpiScale));
            }

            return true;
        }

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);

        if (rawMonitors.Count == 0)
        {
            return new[]
            {
                new MonitorInfo(
                    "PRIMARY",
                    "Monitor 1 — 1920×1080 (Principal)",
                    true,
                    0,
                    0,
                    1920,
                    1080,
                    1.0)
            };
        }

        var ordered = rawMonitors
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.Bounds.Left)
            .ThenBy(m => m.Bounds.Top)
            .ToList();

        var result = new List<MonitorInfo>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            int scalePercent = (int)Math.Round(item.DpiScale * 100);
            string primaryTag = item.IsPrimary ? " (Principal)" : string.Empty;
            string displayName = $"Monitor {i + 1} — {item.Bounds.Width}×{item.Bounds.Height} @ {scalePercent}%{primaryTag}";

            result.Add(new MonitorInfo(
                item.DeviceName,
                displayName,
                item.IsPrimary,
                item.Bounds.Left,
                item.Bounds.Top,
                item.Bounds.Width,
                item.Bounds.Height,
                item.DpiScale));
        }

        return result;
    }

    public MonitorInfo ResolveMonitor(string? deviceName)
    {
        var monitors = GetMonitors();

        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            var matched = monitors.FirstOrDefault(m =>
                string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

            if (matched is not null)
            {
                return matched;
            }
        }

        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }

    private static double GetMonitorDpiScale(IntPtr hMonitor)
    {
        try
        {
            int hr = NativeMethods.GetDpiForMonitor(
                hMonitor,
                Win32Constants.MDT_EFFECTIVE_DPI,
                out uint dpiX,
                out _);

            if (hr == 0 && dpiX >= 72)
            {
                return dpiX / 96.0;
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }

        return 1.0;
    }
}

