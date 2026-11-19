using SlidePrompter.Models;
using SlidePrompter.ViewModels;
using Xunit;

namespace SlidePrompter.Tests;

public sealed class OverlayViewModelTests
{
    private static IReadOnlyList<SlideItem> CreateSampleSlides() => new[]
    {
        new SlideItem(0, "Primeiro slide", "Primeiro slide"),
        new SlideItem(1, "Segundo\nslide", "Segundo slide"),
        new SlideItem(2, "Terceiro slide", "Terceiro slide")
    };

    [Fact]
    public void LoadSlides_PopulatesFirstSlideAndCounter()
    {
        var vm = new OverlayViewModel();

        vm.LoadSlides(CreateSampleSlides(), startIndex: 0, beginPlayback: true);

        Assert.Equal(3, vm.TotalSlides);
        Assert.Equal(0, vm.CurrentSlideIndex);
        Assert.Equal("1 / 3", vm.SlideCounterText);
        Assert.Equal("Primeiro slide", vm.CurrentSingleLineText);
        Assert.Equal(TeleprompterPlaybackState.PreRollPause, vm.PlaybackState);
    }

    [Fact]
    public void NextSlide_AdvancesUntilEndAndStopsWithoutWrapping()
    {
        var vm = new OverlayViewModel();
        vm.LoadSlides(CreateSampleSlides(), startIndex: 0, beginPlayback: true);

        int resetCount = 0;
        vm.ScrollResetRequested += (_, _) => resetCount++;

        Assert.True(vm.TryNextSlide());
        Assert.Equal(1, vm.CurrentSlideIndex);
        Assert.Equal("2 / 3", vm.SlideCounterText);
        Assert.Equal("Segundo slide", vm.CurrentSingleLineText);
        Assert.Equal(TeleprompterPlaybackState.PreRollPause, vm.PlaybackState);

        Assert.True(vm.TryNextSlide());
        Assert.Equal(2, vm.CurrentSlideIndex);
        Assert.Equal("3 / 3", vm.SlideCounterText);

        // At last slide: should not advance or loop back to slide 0
        Assert.False(vm.TryNextSlide());
        Assert.Equal(2, vm.CurrentSlideIndex);
        Assert.Equal("3 / 3", vm.SlideCounterText);
        Assert.Equal(2, resetCount);
    }

    [Fact]
    public void PreviousSlide_NavigatesBackAndClampsAtFirstSlide()
    {
        var vm = new OverlayViewModel();
        vm.LoadSlides(CreateSampleSlides(), startIndex: 2, beginPlayback: true);

        int resetCount = 0;
        vm.ScrollResetRequested += (_, _) => resetCount++;

        Assert.True(vm.TryPreviousSlide());
        Assert.Equal(1, vm.CurrentSlideIndex);
        Assert.Equal("2 / 3", vm.SlideCounterText);

        Assert.True(vm.TryPreviousSlide());
        Assert.Equal(0, vm.CurrentSlideIndex);
        Assert.Equal("1 / 3", vm.SlideCounterText);

        Assert.False(vm.TryPreviousSlide());
        Assert.Equal(0, vm.CurrentSlideIndex);
        Assert.Equal(2, resetCount);
    }

    [Fact]
    public void PlaybackStateLifecycle_PreRoll_Scrolling_Paused_Finished_NoAutoLoop()
    {
        var vm = new OverlayViewModel();
        vm.LoadSlides(CreateSampleSlides(), startIndex: 0, beginPlayback: true);

        Assert.Equal(TeleprompterPlaybackState.PreRollPause, vm.PlaybackState);
        Assert.True(vm.IsActivelyRunning);

        vm.MarkPreRollComplete();
        Assert.Equal(TeleprompterPlaybackState.Scrolling, vm.PlaybackState);
        Assert.True(vm.IsActivelyRunning);

        vm.TogglePause();
        Assert.Equal(TeleprompterPlaybackState.Paused, vm.PlaybackState);
        Assert.False(vm.IsActivelyRunning);

        vm.TogglePause();
        Assert.Equal(TeleprompterPlaybackState.Scrolling, vm.PlaybackState);

        vm.MarkSlideFinished();
        Assert.Equal(TeleprompterPlaybackState.Finished, vm.PlaybackState);
        Assert.False(vm.IsActivelyRunning);
        Assert.Equal(0, vm.CurrentSlideIndex); // Does not auto-advance or loop
    }

    [Fact]
    public void RestartCurrentSlide_ResetsScrollAndEntersPreRollPause()
    {
        var vm = new OverlayViewModel();
        vm.LoadSlides(CreateSampleSlides(), startIndex: 1, beginPlayback: true);
        vm.MarkPreRollComplete();
        vm.MarkSlideFinished();

        int resetCount = 0;
        vm.ScrollResetRequested += (_, _) => resetCount++;

        vm.RestartCurrentSlide();

        Assert.Equal(1, vm.CurrentSlideIndex);
        Assert.Equal(TeleprompterPlaybackState.PreRollPause, vm.PlaybackState);
        Assert.Equal(1, resetCount);
    }

    [Fact]
    public void IncreaseAndDecreaseSpeed_StepsBy15AndClampsToValidBounds()
    {
        var vm = new OverlayViewModel();
        vm.ScrollSpeedPxPerSec = 85;

        double? lastReportedSpeed = null;
        vm.SpeedChangedByHotkey += (_, speed) => lastReportedSpeed = speed;

        vm.IncreaseSpeed();
        Assert.Equal(100, vm.ScrollSpeedPxPerSec);
        Assert.Equal(100, lastReportedSpeed);

        vm.DecreaseSpeed();
        Assert.Equal(85, vm.ScrollSpeedPxPerSec);
        Assert.Equal(85, lastReportedSpeed);

        vm.ScrollSpeedPxPerSec = AppSettings.MaxScrollSpeed;
        vm.IncreaseSpeed();
        Assert.Equal(AppSettings.MaxScrollSpeed, vm.ScrollSpeedPxPerSec);

        vm.ScrollSpeedPxPerSec = AppSettings.MinScrollSpeed;
        vm.DecreaseSpeed();
        Assert.Equal(AppSettings.MinScrollSpeed, vm.ScrollSpeedPxPerSec);
    }

    [Fact]
    public void HandleHotkey_IgnoredWhenNotPresenting_DispatchedWhenPresenting()
    {
        var vm = new OverlayViewModel();
        vm.LoadSlides(CreateSampleSlides(), startIndex: 0, beginPlayback: true);
        vm.IsPresenting = false;
        vm.IsOverlayVisible = true;

        vm.HandleHotkey(GlobalHotkeyAction.NextSlide);
        Assert.Equal(0, vm.CurrentSlideIndex);

        vm.IsPresenting = true;
        vm.HandleHotkey(GlobalHotkeyAction.NextSlide);
        Assert.Equal(1, vm.CurrentSlideIndex);

        vm.HandleHotkey(GlobalHotkeyAction.PreviousSlide);
        Assert.Equal(0, vm.CurrentSlideIndex);

        vm.HandleHotkey(GlobalHotkeyAction.TogglePause);
        Assert.Equal(TeleprompterPlaybackState.Paused, vm.PlaybackState);

        vm.HandleHotkey(GlobalHotkeyAction.ToggleOverlayVisibility);
        Assert.False(vm.IsOverlayVisible);

        vm.HandleHotkey(GlobalHotkeyAction.ToggleOverlayVisibility);
        Assert.True(vm.IsOverlayVisible);
    }
}
