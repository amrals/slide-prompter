using SlidePrompter.Models;

namespace SlidePrompter.Services.Interfaces;

/// <summary>
/// Enumerates connected displays and resolves target monitors for overlay positioning.
/// </summary>
public interface IMonitorService
{
    IReadOnlyList<MonitorInfo> GetMonitors();

    MonitorInfo ResolveMonitor(string? deviceName);
}

