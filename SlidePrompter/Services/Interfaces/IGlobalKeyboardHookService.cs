using SlidePrompter.Models;

namespace SlidePrompter.Services.Interfaces;

/// <summary>
/// Passive global keyboard listener using WH_KEYBOARD_LL that forwards every keystroke to the OS.
/// </summary>
public interface IGlobalKeyboardHookService : IDisposable
{
    bool IsHooked { get; }

    bool UseSpaceToAdvance { get; set; }

    event EventHandler<GlobalHotkeyAction>? HotkeyTriggered;

    void Start();

    void Stop();
}

