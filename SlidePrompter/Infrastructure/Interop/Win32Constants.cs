namespace SlidePrompter.Infrastructure.Interop;

/// <summary>
/// Win32 constants used across overlay window styling, capture exclusion, monitor enumeration, and global keyboard hooking.
/// </summary>
public static class Win32Constants
{
    // Window Display Affinity
    public const uint WDA_NONE = 0x00000000;
    public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    // Extended Window Styles
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TRANSPARENT = 0x00000020L;
    public const long WS_EX_TOOLWINDOW = 0x00000080L;
    public const long WS_EX_NOACTIVATE = 0x08000000L;

    // SetWindowPos flags & special HWND values
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;

    // Low-Level Keyboard Hook
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;
    public const uint LLKHF_ALTDOWN = 0x00000020;

    // Virtual Key Codes
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12; // Alt
    public const int VK_SPACE = 0x20;
    public const int VK_PRIOR = 0x21; // PageUp
    public const int VK_NEXT = 0x22;  // PageDown
    public const int VK_LEFT = 0x25;
    public const int VK_UP = 0x26;
    public const int VK_RIGHT = 0x27;
    public const int VK_DOWN = 0x28;
    public const int VK_H = 0x48;
    public const int VK_R = 0x52;

    // Monitor Enumeration & DPI
    public const uint MONITORINFOF_PRIMARY = 0x00000001;
    public const int MDT_EFFECTIVE_DPI = 0;
    public const int CCHDEVICENAME = 32;
}

