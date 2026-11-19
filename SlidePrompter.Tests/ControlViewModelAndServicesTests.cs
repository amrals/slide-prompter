using System.IO;
using SlidePrompter.Infrastructure.Interop;
using SlidePrompter.Models;
using SlidePrompter.Services;
using SlidePrompter.Services.Interfaces;
using SlidePrompter.ViewModels;
using Xunit;

namespace SlidePrompter.Tests;

public sealed class ControlViewModelAndServicesTests
{
    [Fact]
    public void KeyboardShortcutEvaluator_NavigationDebounce_BlocksRapidDoubleTriggers()
    {
        long fakeNowMs = 1000;
        var evaluator = new KeyboardShortcutEvaluator(() => fakeNowMs, navigationDebounceMs: 150);

        Assert.Equal(GlobalHotkeyAction.NextSlide, evaluator.Evaluate(Win32Constants.VK_RIGHT, false, false, false));

        // 50ms later -> debounced
        fakeNowMs += 50;
        Assert.Null(evaluator.Evaluate(Win32Constants.VK_RIGHT, false, false, false));

        // 149ms total elapsed -> still debounced
        fakeNowMs += 99;
        Assert.Null(evaluator.Evaluate(Win32Constants.VK_NEXT, false, false, false));

        // 150ms total elapsed -> accepted
        fakeNowMs += 1;
        Assert.Equal(GlobalHotkeyAction.NextSlide, evaluator.Evaluate(Win32Constants.VK_NEXT, false, false, false));
    }

    [Fact]
    public void KeyboardShortcutEvaluator_SpacebarRespectsSettingAndDoesNotConflictWithCtrlAltSpace()
    {
        long fakeNowMs = 5000;
        var evaluator = new KeyboardShortcutEvaluator(() => fakeNowMs);

        // Space disabled by default
        Assert.Null(evaluator.Evaluate(Win32Constants.VK_SPACE, false, false, useSpaceToAdvance: false));

        // Space enabled -> NextSlide
        Assert.Equal(GlobalHotkeyAction.NextSlide, evaluator.Evaluate(Win32Constants.VK_SPACE, false, false, useSpaceToAdvance: true));

        // Ctrl + Alt + Space ALWAYS maps to TogglePause even when useSpaceToAdvance is true
        fakeNowMs += 200;
        Assert.Equal(GlobalHotkeyAction.TogglePause, evaluator.Evaluate(Win32Constants.VK_SPACE, isCtrlDown: true, isAltDown: true, useSpaceToAdvance: true));
    }

    [Fact]
    public void KeyboardShortcutEvaluator_RecognizesAllCtrlAltShortcutsAndNavigationKeys()
    {
        long fakeNowMs = 10000;
        var evaluator = new KeyboardShortcutEvaluator(() => fakeNowMs);

        Assert.Equal(GlobalHotkeyAction.PreviousSlide, evaluator.Evaluate(Win32Constants.VK_LEFT, false, false, false));
        fakeNowMs += 200;
        Assert.Equal(GlobalHotkeyAction.PreviousSlide, evaluator.Evaluate(Win32Constants.VK_PRIOR, false, false, false));
        fakeNowMs += 200;
        Assert.Equal(GlobalHotkeyAction.IncreaseSpeed, evaluator.Evaluate(Win32Constants.VK_UP, true, true, false));
        fakeNowMs += 200;
        Assert.Equal(GlobalHotkeyAction.DecreaseSpeed, evaluator.Evaluate(Win32Constants.VK_DOWN, true, true, false));
        fakeNowMs += 200;
        Assert.Equal(GlobalHotkeyAction.RestartSlide, evaluator.Evaluate(Win32Constants.VK_R, true, true, false));
        fakeNowMs += 200;
        Assert.Equal(GlobalHotkeyAction.ToggleOverlayVisibility, evaluator.Evaluate(Win32Constants.VK_H, true, true, false));
    }

    [Fact]
    public void ControlViewModel_FirstRunLoadsSampleScript_AndStartPresentationAutoEnablesClickThrough()
    {
        var fakeSettings = new InMemorySettingsService();
        var fakeHook = new FakeKeyboardHookService();
        var overlayVm = new OverlayViewModel();

        using var controlVm = new ControlViewModel(
            new ScriptParserService(),
            fakeSettings,
            new FakeMonitorService(),
            fakeHook,
            new FakeFileDialogService(),
            overlayVm);

        // Sample script should be pre-filled on first use
        Assert.NotEmpty(controlVm.ParsedSlides);
        Assert.True(controlVm.HideFromCapture);
        Assert.False(controlVm.UseSpaceToAdvance);

        // Disable click-through manually, then start presentation -> must auto-enable click-through
        controlVm.IsClickThrough = false;
        controlVm.StartPresentation();

        Assert.True(controlVm.IsPresenting);
        Assert.True(controlVm.IsClickThrough);
        Assert.True(overlayVm.IsClickThrough);
        Assert.True(overlayVm.IsOverlayVisible);
        Assert.True(fakeHook.IsHooked);

        // Simulate global hotkey advancing slide
        fakeHook.Raise(GlobalHotkeyAction.NextSlide);
        Assert.Equal(1, overlayVm.CurrentSlideIndex);
        Assert.Equal(1, controlVm.SelectedSlideIndex);

        // Simulate speed increase via global hotkey syncing back to ControlViewModel
        double initialSpeed = controlVm.ScrollSpeedPxPerSec;
        fakeHook.Raise(GlobalHotkeyAction.IncreaseSpeed);
        Assert.Equal(initialSpeed + AppSettings.SpeedStep, controlVm.ScrollSpeedPxPerSec);

        // Stop presentation cleanly stops hook and hides overlay
        controlVm.StopPresentation();
        Assert.False(controlVm.IsPresenting);
        Assert.False(overlayVm.IsOverlayVisible);
        Assert.False(fakeHook.IsHooked);
    }

    [Fact]
    public void ControlViewModel_UpdateCaretPosition_TracksActiveSlideDelimiterAndDraft()
    {
        var fakeSettings = new InMemorySettingsService();
        var overlayVm = new OverlayViewModel();

        using var controlVm = new ControlViewModel(
            new ScriptParserService(),
            fakeSettings,
            new FakeMonitorService(),
            new FakeKeyboardHookService(),
            new FakeFileDialogService(),
            overlayVm);

        controlVm.ScriptText = "Texto 1\r\n---\r\nTexto 2\r\n---\r\n";

        // Inside Slide 1
        controlVm.UpdateCaretPosition(2);
        Assert.Equal(1, controlVm.CaretSlideNumber);
        Assert.Equal("Editando: Slide 1 / 2", controlVm.EditingSlideLabel);

        // Directly on '---' separator
        controlVm.UpdateCaretPosition(10);
        Assert.Equal(0, controlVm.CaretSlideNumber);
        Assert.Equal("Separador (---)", controlVm.EditingSlideLabel);

        // Inside Slide 2
        controlVm.UpdateCaretPosition(16);
        Assert.Equal(2, controlVm.CaretSlideNumber);
        Assert.Equal("Editando: Slide 2 / 2", controlVm.EditingSlideLabel);

        // At trailing empty line after second '---' -> draft Slide 3
        controlVm.UpdateCaretPosition(controlVm.ScriptText.Length);
        Assert.Equal(3, controlVm.CaretSlideNumber);
        Assert.Equal("Novo Slide 3", controlVm.EditingSlideLabel);
    }

    [Fact]
    public void JsonSettingsService_RoundTripSaveLoad_AndClampsInvalidValues()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "SlidePrompterTests_" + Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(tempDir, "settings.json");

        try
        {
            var service = new JsonSettingsService(filePath);
            var settings = new AppSettings
            {
                ScrollSpeedPxPerSec = 9999, // Should clamp to MaxScrollSpeed
                BarWidth = 100,             // Should clamp to MinBarWidth
                FontSize = 26,
                BackgroundOpacity = 0.75,
                TopMarginPx = 12,
                HideFromCapture = true,
                UseSpaceToAdvance = true,
                DisplayMode = TeleprompterDisplayMode.CompactBlockTwoLines
            };

            service.Save(settings);
            AppSettings loaded = service.Load();

            Assert.Equal(AppSettings.MaxScrollSpeed, loaded.ScrollSpeedPxPerSec);
            Assert.Equal(AppSettings.MinBarWidth, loaded.BarWidth);
            Assert.Equal(26, loaded.FontSize);
            Assert.Equal(0.75, loaded.BackgroundOpacity);
            Assert.Equal(12, loaded.TopMarginPx);
            Assert.True(loaded.HideFromCapture);
            Assert.True(loaded.UseSpaceToAdvance);
            Assert.Equal(TeleprompterDisplayMode.CompactBlockTwoLines, loaded.DisplayMode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private sealed class InMemorySettingsService : ISettingsService
    {
        private AppSettings _stored = new();
        public string SettingsFilePath => "in-memory-settings.json";
        public AppSettings Load() => _stored.Clone();
        public void Save(AppSettings settings) => _stored = settings.Clone();
    }

    private sealed class FakeMonitorService : IMonitorService
    {
        private readonly MonitorInfo _primary = new(@"\\.\DISPLAY1", "Monitor 1 — 1920×1080 (Principal)", true, 0, 0, 1920, 1080, 1.0);
        public IReadOnlyList<MonitorInfo> GetMonitors() => new[] { _primary };
        public MonitorInfo ResolveMonitor(string? deviceName) => _primary;
    }

    private sealed class FakeKeyboardHookService : IGlobalKeyboardHookService
    {
        public bool IsHooked { get; private set; }
        public bool UseSpaceToAdvance { get; set; }
        public event EventHandler<GlobalHotkeyAction>? HotkeyTriggered;

        public void Start() => IsHooked = true;
        public void Stop() => IsHooked = false;
        public void Raise(GlobalHotkeyAction action) => HotkeyTriggered?.Invoke(this, action);
        public void Dispose() => Stop();
    }

    private sealed class FakeFileDialogService : IFileDialogService
    {
        public string? ShowOpenScriptDialog() => null;
        public string? ShowSaveScriptDialog(string? suggestedFilePath = null) => null;
    }
}

