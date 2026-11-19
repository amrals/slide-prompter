using System.Diagnostics;
using System.Runtime.InteropServices;
using SlidePrompter.Infrastructure.Interop;
using SlidePrompter.Models;
using SlidePrompter.Services.Interfaces;

namespace SlidePrompter.Services;

/// <summary>
/// Installs a passive WH_KEYBOARD_LL hook that observes presentation shortcuts without blocking any key from reaching other apps.
/// </summary>
public sealed class GlobalKeyboardHookService : IGlobalKeyboardHookService
{
    // Strong instance reference prevents GC collection of the native callback delegate while hooked.
    private readonly NativeMethods.LowLevelKeyboardProc _hookProc;
    private readonly KeyboardShortcutEvaluator _evaluator;
    private readonly object _lifecycleLock = new();

    private IntPtr _hookHandle = IntPtr.Zero;
    private volatile bool _useSpaceToAdvance;
    private bool _disposed;

    public bool IsHooked => _hookHandle != IntPtr.Zero;

    public bool UseSpaceToAdvance
    {
        get => _useSpaceToAdvance;
        set => _useSpaceToAdvance = value;
    }

    public event EventHandler<GlobalHotkeyAction>? HotkeyTriggered;

    public GlobalKeyboardHookService()
        : this(new KeyboardShortcutEvaluator())
    {
    }

    internal GlobalKeyboardHookService(KeyboardShortcutEvaluator evaluator)
    {
        _evaluator = evaluator;
        _hookProc = HookCallback;
    }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_hookHandle != IntPtr.Zero)
            {
                return;
            }

            _evaluator.Reset();

            IntPtr moduleHandle = IntPtr.Zero;
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                string? moduleName = currentProcess.MainModule?.ModuleName;
                if (!string.IsNullOrWhiteSpace(moduleName))
                {
                    moduleHandle = NativeMethods.GetModuleHandle(moduleName);
                }
            }
            catch
            {
                moduleHandle = IntPtr.Zero;
            }

            _hookHandle = NativeMethods.SetWindowsHookEx(
                Win32Constants.WH_KEYBOARD_LL,
                _hookProc,
                moduleHandle,
                0);
        }
    }

    public void Stop()
    {
        lock (_lifecycleLock)
        {
            if (_hookHandle == IntPtr.Zero)
            {
                return;
            }

            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                if (message is Win32Constants.WM_KEYDOWN or Win32Constants.WM_SYSKEYDOWN)
                {
                    var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

                    bool isCtrlDown = (NativeMethods.GetAsyncKeyState(Win32Constants.VK_CONTROL) & 0x8000) != 0;
                    bool isAltDown = (NativeMethods.GetAsyncKeyState(Win32Constants.VK_MENU) & 0x8000) != 0
                                     || (hookStruct.flags & Win32Constants.LLKHF_ALTDOWN) != 0;

                    GlobalHotkeyAction? action = _evaluator.Evaluate(
                        unchecked((int)hookStruct.vkCode),
                        isCtrlDown,
                        isAltDown,
                        _useSpaceToAdvance);

                    if (action.HasValue)
                    {
                        DispatchHotkeyAsync(action.Value);
                    }
                }
            }
        }
        catch
        {
            // Never allow managed exceptions to escape a Win32 hook callback.
        }

        // Always pass the event down the hook chain so PowerPoint, Google Slides, and browsers receive keys normally.
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void DispatchHotkeyAsync(GlobalHotkeyAction action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
        {
            dispatcher.BeginInvoke(() =>
            {
                try
                {
                    HotkeyTriggered?.Invoke(this, action);
                }
                catch
                {
                    // Protect UI dispatcher from unexpected subscriber failures.
                }
            });
        }
        else
        {
            HotkeyTriggered?.Invoke(this, action);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~GlobalKeyboardHookService()
    {
        Stop();
    }
}

