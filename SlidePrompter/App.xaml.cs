using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SlidePrompter.Services;
using SlidePrompter.Services.Interfaces;
using SlidePrompter.ViewModels;
using SlidePrompter.Views;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace SlidePrompter;

public partial class App : WpfApplication
{
    private ServiceProvider? _serviceProvider;
    private ControlWindow? _controlWindow;
    private OverlayWindow? _overlayWindow;
    private ControlViewModel? _controlViewModel;
    private OverlayViewModel? _overlayViewModel;
    private ITrayIconService? _trayIconService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        _controlViewModel = _serviceProvider.GetRequiredService<ControlViewModel>();
        _overlayViewModel = _serviceProvider.GetRequiredService<OverlayViewModel>();
        _overlayWindow = _serviceProvider.GetRequiredService<OverlayWindow>();
        _controlWindow = _serviceProvider.GetRequiredService<ControlWindow>();
        _trayIconService = _serviceProvider.GetRequiredService<ITrayIconService>();

        WireLifecycleEvents();

        _trayIconService.Initialize();
        _trayIconService.UpdateState(isPresenting: false, isOverlayVisible: false);

        MainWindow = _controlWindow;
        _controlWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Domain & Infrastructure Services
        services.AddSingleton<IScriptParserService, ScriptParserService>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IWindowOverlayService, WindowOverlayService>();
        services.AddSingleton<IGlobalKeyboardHookService, GlobalKeyboardHookService>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<IFileDialogService, WpfFileDialogService>();

        // ViewModels
        services.AddSingleton<OverlayViewModel>();
        services.AddSingleton<ControlViewModel>();

        // Views
        services.AddSingleton<OverlayWindow>();
        services.AddSingleton<ControlWindow>();
    }

    private void WireLifecycleEvents()
    {
        if (_controlViewModel is null || _overlayViewModel is null || _overlayWindow is null || _controlWindow is null || _trayIconService is null)
        {
            return;
        }

        _controlViewModel.PresentationStarted += (_, _) =>
        {
            _overlayWindow.ShowOverlay();
            _controlWindow.WindowState = WindowState.Minimized;
            _trayIconService.UpdateState(isPresenting: true, isOverlayVisible: _overlayViewModel.IsOverlayVisible);
        };

        _controlViewModel.PresentationStopped += (_, _) =>
        {
            _overlayWindow.HideOverlay();
            _controlWindow.RestoreAndActivate();
            _trayIconService.UpdateState(isPresenting: false, isOverlayVisible: false);
        };

        _controlViewModel.PreviewVisibilityChanged += (_, isVisible) =>
        {
            if (isVisible)
            {
                _overlayWindow.ShowOverlay();
            }
            else
            {
                _overlayWindow.HideOverlay();
            }

            _trayIconService.UpdateState(_controlViewModel.IsPresenting, isVisible);
        };

        _overlayViewModel.OverlayVisibilityChanged += (_, isVisible) =>
        {
            if (isVisible)
            {
                _overlayWindow.ShowOverlay();
            }
            else
            {
                _overlayWindow.HideOverlay();
            }

            _trayIconService.UpdateState(_controlViewModel.IsPresenting, isVisible);
        };

        _trayIconService.ShowControlWindowRequested += (_, _) =>
        {
            Dispatcher.BeginInvoke(() => _controlWindow.RestoreAndActivate());
        };

        _trayIconService.ToggleOverlayVisibilityRequested += (_, _) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_controlViewModel.IsPresenting)
                {
                    _overlayViewModel.ToggleVisibility();
                }
                else
                {
                    _controlViewModel.TogglePreview();
                }
            });
        };

        _trayIconService.TogglePresentationRequested += (_, _) =>
        {
            Dispatcher.BeginInvoke(() => _controlViewModel.TogglePresentation());
        };

        _trayIconService.ExitRequested += (_, _) =>
        {
            Dispatcher.BeginInvoke(Shutdown);
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            _serviceProvider?.GetService<IGlobalKeyboardHookService>()?.Stop();
        }
        catch
        {
            // Ignore secondary failure during recovery.
        }

        WpfMessageBox.Show(
            $"Ocorreu um erro inesperado no SlidePrompter:\n\n{e.Exception.Message}",
            "SlidePrompter",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;

        try
        {
            _serviceProvider?.GetService<IGlobalKeyboardHookService>()?.Dispose();
            _controlViewModel?.Dispose();
            _trayIconService?.Dispose();

            if (_overlayWindow is not null)
            {
                _overlayWindow.HideOverlay();
                _overlayWindow.Close();
            }

            _serviceProvider?.Dispose();
        }
        catch
        {
            // Ensure clean process shutdown even if a teardown step fails.
        }

        base.OnExit(e);
    }
}

