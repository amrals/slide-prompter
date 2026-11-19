using SlidePrompter.Infrastructure.Interop;
using SlidePrompter.Models;

namespace SlidePrompter.Services;

/// <summary>
/// Deterministic classifier and debouncer for global keyboard shortcuts.
/// </summary>
public sealed class KeyboardShortcutEvaluator
{
    public const long DefaultNavigationDebounceMs = 150;
    public const long DefaultToggleDebounceMs = 150;
    public const long DefaultSpeedThrottleMs = 50;

    private readonly Func<long> _nowMsProvider;
    private readonly long _navigationDebounceMs;
    private readonly long _toggleDebounceMs;
    private readonly long _speedThrottleMs;
    private readonly object _syncLock = new();

    private long _lastNavigationTimestampMs = long.MinValue;
    private long _lastToggleTimestampMs = long.MinValue;
    private long _lastSpeedTimestampMs = long.MinValue;

    public KeyboardShortcutEvaluator(
        Func<long>? nowMsProvider = null,
        long navigationDebounceMs = DefaultNavigationDebounceMs,
        long toggleDebounceMs = DefaultToggleDebounceMs,
        long speedThrottleMs = DefaultSpeedThrottleMs)
    {
        _nowMsProvider = nowMsProvider ?? (() => Environment.TickCount64);
        _navigationDebounceMs = navigationDebounceMs;
        _toggleDebounceMs = toggleDebounceMs;
        _speedThrottleMs = speedThrottleMs;
    }

    public GlobalHotkeyAction? Evaluate(int vkCode, bool isCtrlDown, bool isAltDown, bool useSpaceToAdvance)
    {
        GlobalHotkeyAction? candidate = MatchShortcut(vkCode, isCtrlDown, isAltDown, useSpaceToAdvance);
        if (!candidate.HasValue)
        {
            return null;
        }

        lock (_syncLock)
        {
            long now = _nowMsProvider();

            switch (candidate.Value)
            {
                case GlobalHotkeyAction.NextSlide:
                case GlobalHotkeyAction.PreviousSlide:
                    if (_lastNavigationTimestampMs != long.MinValue && (now - _lastNavigationTimestampMs) < _navigationDebounceMs)
                    {
                        return null;
                    }

                    _lastNavigationTimestampMs = now;
                    return candidate;

                case GlobalHotkeyAction.IncreaseSpeed:
                case GlobalHotkeyAction.DecreaseSpeed:
                    if (_lastSpeedTimestampMs != long.MinValue && (now - _lastSpeedTimestampMs) < _speedThrottleMs)
                    {
                        return null;
                    }

                    _lastSpeedTimestampMs = now;
                    return candidate;

                case GlobalHotkeyAction.TogglePause:
                case GlobalHotkeyAction.RestartSlide:
                case GlobalHotkeyAction.ToggleOverlayVisibility:
                    if (_lastToggleTimestampMs != long.MinValue && (now - _lastToggleTimestampMs) < _toggleDebounceMs)
                    {
                        return null;
                    }

                    _lastToggleTimestampMs = now;
                    return candidate;

                default:
                    return candidate;
            }
        }
    }

    public void Reset()
    {
        lock (_syncLock)
        {
            _lastNavigationTimestampMs = long.MinValue;
            _lastToggleTimestampMs = long.MinValue;
            _lastSpeedTimestampMs = long.MinValue;
        }
    }

    private static GlobalHotkeyAction? MatchShortcut(int vkCode, bool isCtrlDown, bool isAltDown, bool useSpaceToAdvance)
    {
        if (isCtrlDown && isAltDown)
        {
            return vkCode switch
            {
                Win32Constants.VK_SPACE => GlobalHotkeyAction.TogglePause,
                Win32Constants.VK_UP => GlobalHotkeyAction.IncreaseSpeed,
                Win32Constants.VK_DOWN => GlobalHotkeyAction.DecreaseSpeed,
                Win32Constants.VK_R => GlobalHotkeyAction.RestartSlide,
                Win32Constants.VK_H => GlobalHotkeyAction.ToggleOverlayVisibility,
                _ => null
            };
        }

        if (!isCtrlDown && !isAltDown)
        {
            return vkCode switch
            {
                Win32Constants.VK_RIGHT or Win32Constants.VK_NEXT => GlobalHotkeyAction.NextSlide,
                Win32Constants.VK_LEFT or Win32Constants.VK_PRIOR => GlobalHotkeyAction.PreviousSlide,
                Win32Constants.VK_SPACE when useSpaceToAdvance => GlobalHotkeyAction.NextSlide,
                _ => null
            };
        }

        return null;
    }
}

