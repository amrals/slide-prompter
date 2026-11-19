using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlidePrompter.Models;
using SlidePrompter.Services.Interfaces;

namespace SlidePrompter.ViewModels;

/// <summary>
/// ViewModel for ControlWindow: manages script editing, real-time settings persistence, monitors, and presentation lifecycle.
/// </summary>
public sealed partial class ControlViewModel : ObservableObject, IDisposable
{
    private readonly IScriptParserService _scriptParserService;
    private readonly ISettingsService _settingsService;
    private readonly IMonitorService _monitorService;
    private readonly IGlobalKeyboardHookService _keyboardHookService;
    private readonly IFileDialogService _fileDialogService;
    private readonly OverlayViewModel _overlayViewModel;
    private readonly System.Threading.Timer _saveDebounceTimer;

    private bool _suppressPersistence;
    private bool _syncingFromOverlay;
    private bool _hasPendingSave;
    private bool _disposed;

    [ObservableProperty]
    private string _scriptText = string.Empty;

    [ObservableProperty]
    private string _currentFileName = "Roteiro de Exemplo";

    [ObservableProperty]
    private string? _currentFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SlideCountLabel))]
    private int _slideCount;

    [ObservableProperty]
    private int _selectedSlideIndex;

    [ObservableProperty]
    private MonitorInfo? _selectedMonitor;

    [ObservableProperty]
    private double _scrollSpeedPxPerSec = AppSettings.DefaultScrollSpeed;

    [ObservableProperty]
    private double _barWidth = AppSettings.DefaultBarWidth;

    [ObservableProperty]
    private double _fontSize = AppSettings.DefaultFontSize;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundOpacityPercent))]
    private double _backgroundOpacity = AppSettings.DefaultBackgroundOpacity;

    [ObservableProperty]
    private double _topMarginPx = AppSettings.DefaultTopMargin;

    [ObservableProperty]
    private double _initialPauseSeconds = AppSettings.DefaultInitialPauseSeconds;

    [ObservableProperty]
    private bool _hideFromCapture = true;

    [ObservableProperty]
    private bool _isClickThrough = true;

    [ObservableProperty]
    private bool _useSpaceToAdvance;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSingleLineMode))]
    [NotifyPropertyChangedFor(nameof(IsCompactBlockMode))]
    private TeleprompterDisplayMode _displayMode = TeleprompterDisplayMode.SingleLineHorizontal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PresentationButtonText))]
    [NotifyPropertyChangedFor(nameof(PresentationStatusBadge))]
    private bool _isPresenting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewButtonText))]
    private bool _isPreviewOpen;

    [ObservableProperty]
    private string _statusMessage = "Pronto para apresentar.";

    [ObservableProperty]
    private int _caretSlideNumber = 1;

    [ObservableProperty]
    private string _editingSlideLabel = "Editando: Slide 1";

    public ObservableCollection<SlideItem> ParsedSlides { get; } = new();

    public IReadOnlyList<SlideMarker> SlideMarkers { get; private set; } = Array.Empty<SlideMarker>();

    public ObservableCollection<MonitorInfo> Monitors { get; } = new();

    public OverlayViewModel OverlayViewModel => _overlayViewModel;

    public string SettingsPathDisplay => _settingsService.SettingsFilePath;

    public string SlideCountLabel => SlideCount == 1 ? "1 slide" : $"{SlideCount} slides";

    public int BackgroundOpacityPercent => (int)Math.Round(BackgroundOpacity * 100);

    public string PresentationButtonText => IsPresenting ? "Parar Apresentação" : "Iniciar Apresentação";

    public string PresentationStatusBadge => IsPresenting ? "Apresentação Ativa" : "Modo Edição";

    public string PreviewButtonText => IsPreviewOpen ? "Fechar Prévia" : "Pré-visualizar";

    public bool IsSingleLineMode
    {
        get => DisplayMode == TeleprompterDisplayMode.SingleLineHorizontal;
        set
        {
            if (value)
            {
                DisplayMode = TeleprompterDisplayMode.SingleLineHorizontal;
            }
        }
    }

    public bool IsCompactBlockMode
    {
        get => DisplayMode == TeleprompterDisplayMode.CompactBlockTwoLines;
        set
        {
            if (value)
            {
                DisplayMode = TeleprompterDisplayMode.CompactBlockTwoLines;
            }
        }
    }

    public event EventHandler? PresentationStarted;
    public event EventHandler? PresentationStopped;
    public event EventHandler<bool>? PreviewVisibilityChanged;
    public event EventHandler? SlideMarkersChanged;
    public event EventHandler<int>? NavigateEditorToCharIndexRequested;

    public ControlViewModel(
        IScriptParserService scriptParserService,
        ISettingsService settingsService,
        IMonitorService monitorService,
        IGlobalKeyboardHookService keyboardHookService,
        IFileDialogService fileDialogService,
        OverlayViewModel overlayViewModel)
    {
        _scriptParserService = scriptParserService;
        _settingsService = settingsService;
        _monitorService = monitorService;
        _keyboardHookService = keyboardHookService;
        _fileDialogService = fileDialogService;
        _overlayViewModel = overlayViewModel;

        _keyboardHookService.HotkeyTriggered += OnGlobalHotkeyTriggered;
        _overlayViewModel.SpeedChangedByHotkey += OnOverlaySpeedChangedByHotkey;
        _overlayViewModel.ActiveSlideChanged += OnOverlayActiveSlideChanged;
        _overlayViewModel.OverlayVisibilityChanged += OnOverlayVisibilityChanged;

        _saveDebounceTimer = new System.Threading.Timer(_ => FlushPendingSave(), null, Timeout.Infinite, Timeout.Infinite);

        InitializeFromSettings();
    }

    private void InitializeFromSettings()
    {
        _suppressPersistence = true;
        try
        {
            AppSettings settings = _settingsService.Load();

            ScrollSpeedPxPerSec = settings.ScrollSpeedPxPerSec;
            BarWidth = settings.BarWidth;
            FontSize = settings.FontSize;
            BackgroundOpacity = settings.BackgroundOpacity;
            TopMarginPx = settings.TopMarginPx;
            InitialPauseSeconds = settings.InitialPauseSeconds;
            HideFromCapture = settings.HideFromCapture;
            IsClickThrough = settings.IsClickThrough;
            UseSpaceToAdvance = settings.UseSpaceToAdvance;
            DisplayMode = settings.DisplayMode;

            LoadMonitors(settings.SelectedMonitorDeviceName);

            bool isFirstUse = string.IsNullOrWhiteSpace(settings.LastScriptContent);
            if (isFirstUse)
            {
                ScriptText = _scriptParserService.GetSampleScript();
                CurrentFileName = "Roteiro de Exemplo (primeiro uso)";
                CurrentFilePath = null;
            }
            else
            {
                ScriptText = settings.LastScriptContent!;
                CurrentFilePath = settings.LastScriptFilePath;
                CurrentFileName = string.IsNullOrWhiteSpace(CurrentFilePath)
                    ? "Roteiro salvo localmente"
                    : Path.GetFileName(CurrentFilePath);
            }

            _keyboardHookService.UseSpaceToAdvance = UseSpaceToAdvance;
            ReparseSlides(preserveCurrentIndex: false);
            PushSettingsToOverlay();
        }
        finally
        {
            _suppressPersistence = false;
        }
    }

    [RelayCommand]
    public void TogglePresentation()
    {
        if (IsPresenting)
        {
            StopPresentation();
        }
        else
        {
            StartPresentation();
        }
    }

    [RelayCommand]
    public void StartPresentation()
    {
        ReparseSlides(preserveCurrentIndex: true);

        if (ParsedSlides.Count == 0)
        {
            StatusMessage = "Adicione texto no editor (separando slides com ---) antes de iniciar.";
            return;
        }

        // Ghost / Click-Through mode is automatically enabled when starting a presentation.
        if (!IsClickThrough)
        {
            IsClickThrough = true;
        }

        int startSlide = Math.Clamp(SelectedSlideIndex, 0, ParsedSlides.Count - 1);

        _keyboardHookService.UseSpaceToAdvance = UseSpaceToAdvance;
        PushSettingsToOverlay();

        _overlayViewModel.IsPresenting = true;
        _overlayViewModel.LoadSlides(ParsedSlides.ToList(), startSlide, beginPlayback: true);
        _overlayViewModel.IsOverlayVisible = true;

        _keyboardHookService.Start();

        IsPresenting = true;
        IsPreviewOpen = true;
        StatusMessage = $"Apresentação ativa ({ParsedSlides.Count} slides) — escuta global ligada.";

        PresentationStarted?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void StopPresentation()
    {
        _keyboardHookService.Stop();

        IsPresenting = false;
        IsPreviewOpen = false;

        _overlayViewModel.IsPresenting = false;
        _overlayViewModel.IsOverlayVisible = false;
        _overlayViewModel.PlaybackState = TeleprompterPlaybackState.Idle;

        StatusMessage = "Apresentação encerrada. Escuta global desativada.";
        PresentationStopped?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void TogglePreview()
    {
        if (IsPresenting)
        {
            _overlayViewModel.ToggleVisibility();
            return;
        }

        ReparseSlides(preserveCurrentIndex: true);

        if (!IsPreviewOpen && ParsedSlides.Count == 0)
        {
            StatusMessage = "Nenhum slide encontrado para pré-visualizar.";
            return;
        }

        bool nextState = !IsPreviewOpen;
        IsPreviewOpen = nextState;

        if (nextState)
        {
            PushSettingsToOverlay();
            int startSlide = Math.Clamp(SelectedSlideIndex, 0, Math.Max(0, ParsedSlides.Count - 1));
            _overlayViewModel.LoadSlides(ParsedSlides.ToList(), startSlide, beginPlayback: true);
            _overlayViewModel.IsOverlayVisible = true;
            StatusMessage = "Pré-visualização aberta. Ajuste os controles em tempo real.";
        }
        else
        {
            _overlayViewModel.IsOverlayVisible = false;
            _overlayViewModel.PlaybackState = TeleprompterPlaybackState.Idle;
            StatusMessage = "Pré-visualização fechada.";
        }

        PreviewVisibilityChanged?.Invoke(this, nextState);
    }

    [RelayCommand]
    public void ImportScript()
    {
        string? filePath = _fileDialogService.ShowOpenScriptDialog();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            CurrentFilePath = filePath;
            CurrentFileName = Path.GetFileName(filePath);
            ScriptText = content;
            SelectedSlideIndex = 0;
            SaveSettings();
            StatusMessage = $"Roteiro importado: {CurrentFileName} ({SlideCountLabel}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao importar arquivo: {ex.Message}";
        }
    }

    [RelayCommand]
    public void SaveScript()
    {
        string? filePath = _fileDialogService.ShowSaveScriptDialog(CurrentFilePath);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            File.WriteAllText(filePath, ScriptText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            CurrentFilePath = filePath;
            CurrentFileName = Path.GetFileName(filePath);
            SaveSettings();
            StatusMessage = $"Roteiro salvo em: {CurrentFileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao salvar arquivo: {ex.Message}";
        }
    }

    [RelayCommand]
    public void LoadSampleScript()
    {
        CurrentFilePath = null;
        CurrentFileName = "Roteiro de Exemplo";
        ScriptText = _scriptParserService.GetSampleScript();
        SelectedSlideIndex = 0;
        SaveSettings();
        StatusMessage = $"Roteiro de exemplo carregado ({SlideCountLabel}).";
    }

    [RelayCommand]
    public void RefreshMonitors()
    {
        string? currentDevice = SelectedMonitor?.DeviceName;
        LoadMonitors(currentDevice);
        PushSettingsToOverlay();
        StatusMessage = $"{Monitors.Count} monitor(es) detectado(s).";
    }

    [RelayCommand]
    public void SelectSlide(SlideItem? slide)
    {
        if (slide is null)
        {
            return;
        }

        SelectedSlideIndex = slide.Index;
        if (IsPresenting || IsPreviewOpen)
        {
            _overlayViewModel.GoToSlide(slide.Index);
        }

        SlideMarker? marker = SlideMarkers.FirstOrDefault(m => !m.IsEmptyDraft && m.SlideNumber == slide.Number);
        if (marker is not null)
        {
            NavigateEditorToCharIndexRequested?.Invoke(this, marker.FirstContentCharIndex);
        }
    }

    public void SelectSlideMarker(SlideMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);

        if (!marker.IsEmptyDraft && marker.SlideNumber >= 1 && marker.SlideNumber <= ParsedSlides.Count)
        {
            SelectedSlideIndex = marker.SlideNumber - 1;
            if (IsPresenting || IsPreviewOpen)
            {
                _overlayViewModel.GoToSlide(SelectedSlideIndex);
            }
        }

        NavigateEditorToCharIndexRequested?.Invoke(this, marker.FirstContentCharIndex);
    }

    public void UpdateCaretPosition(int caretIndex)
    {
        if (SlideMarkers.Count == 0)
        {
            CaretSlideNumber = 0;
            EditingSlideLabel = "Sem slides";
            return;
        }

        SlideMarker? active = SlideMarkers.FirstOrDefault(m =>
            caretIndex >= m.SectionStartCharIndex && caretIndex <= m.SectionEndCharIndex);

        if (active is null)
        {
            CaretSlideNumber = 0;
            EditingSlideLabel = "Separador (---)";
            return;
        }

        CaretSlideNumber = active.SlideNumber;
        EditingSlideLabel = active.IsEmptyDraft
            ? $"Novo Slide {active.SlideNumber}"
            : $"Editando: Slide {active.SlideNumber} / {Math.Max(SlideCount, active.SlideNumber)}";
    }

    public void SaveSettings()
    {
        if (_suppressPersistence)
        {
            return;
        }

        _hasPendingSave = false;
        AppSettings snapshot = BuildSettingsSnapshot();
        _settingsService.Save(snapshot);
    }

    private void ScheduleSaveSettings()
    {
        if (_suppressPersistence || _disposed)
        {
            return;
        }

        _hasPendingSave = true;
        _saveDebounceTimer.Change(250, Timeout.Infinite);
    }

    private void FlushPendingSave()
    {
        if (!_hasPendingSave || _suppressPersistence)
        {
            return;
        }

        _hasPendingSave = false;
        AppSettings snapshot = BuildSettingsSnapshot();
        _settingsService.Save(snapshot);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _saveDebounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
        FlushPendingSave();
        _saveDebounceTimer.Dispose();

        _keyboardHookService.HotkeyTriggered -= OnGlobalHotkeyTriggered;
        _overlayViewModel.SpeedChangedByHotkey -= OnOverlaySpeedChangedByHotkey;
        _overlayViewModel.ActiveSlideChanged -= OnOverlayActiveSlideChanged;
        _overlayViewModel.OverlayVisibilityChanged -= OnOverlayVisibilityChanged;
    }

    public AppSettings BuildSettingsSnapshot() => new()
    {
        ScrollSpeedPxPerSec = ScrollSpeedPxPerSec,
        BarWidth = BarWidth,
        FontSize = FontSize,
        BackgroundOpacity = BackgroundOpacity,
        TopMarginPx = TopMarginPx,
        InitialPauseSeconds = InitialPauseSeconds,
        SelectedMonitorDeviceName = SelectedMonitor?.DeviceName ?? string.Empty,
        HideFromCapture = HideFromCapture,
        IsClickThrough = IsClickThrough,
        UseSpaceToAdvance = UseSpaceToAdvance,
        DisplayMode = DisplayMode,
        LastScriptContent = ScriptText,
        LastScriptFilePath = CurrentFilePath
    };

    private void LoadMonitors(string? preferredDeviceName)
    {
        Monitors.Clear();
        IReadOnlyList<MonitorInfo> detected = _monitorService.GetMonitors();
        foreach (MonitorInfo monitor in detected)
        {
            Monitors.Add(monitor);
        }

        MonitorInfo? matched = !string.IsNullOrWhiteSpace(preferredDeviceName)
            ? Monitors.FirstOrDefault(m => string.Equals(m.DeviceName, preferredDeviceName, StringComparison.OrdinalIgnoreCase))
            : null;

        SelectedMonitor = matched ?? Monitors.FirstOrDefault(m => m.IsPrimary) ?? Monitors.FirstOrDefault();
    }

    private void ReparseSlides(bool preserveCurrentIndex)
    {
        IReadOnlyList<SlideItem> slides = _scriptParserService.Parse(ScriptText);
        SlideMarkers = _scriptParserService.LocateSlideMarkers(ScriptText);

        ParsedSlides.Clear();
        foreach (SlideItem slide in slides)
        {
            ParsedSlides.Add(slide);
        }

        SlideCount = ParsedSlides.Count;

        int targetIndex = preserveCurrentIndex && ParsedSlides.Count > 0
            ? Math.Clamp(SelectedSlideIndex, 0, ParsedSlides.Count - 1)
            : 0;

        SelectedSlideIndex = targetIndex;

        if (IsPresenting || IsPreviewOpen)
        {
            _overlayViewModel.LoadSlides(slides, targetIndex, beginPlayback: IsPresenting || IsPreviewOpen);
        }

        SlideMarkersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PushSettingsToOverlay()
    {
        _overlayViewModel.ApplySettings(BuildSettingsSnapshot());
    }

    private void OnSettingChanged()
    {
        if (_suppressPersistence)
        {
            return;
        }

        PushSettingsToOverlay();
        ScheduleSaveSettings();
    }

    partial void OnScriptTextChanged(string value)
    {
        if (_suppressPersistence)
        {
            return;
        }

        ReparseSlides(preserveCurrentIndex: true);
        ScheduleSaveSettings();
    }

    partial void OnScrollSpeedPxPerSecChanged(double value)
    {
        if (_syncingFromOverlay)
        {
            ScheduleSaveSettings();
            return;
        }

        OnSettingChanged();
    }

    partial void OnBarWidthChanged(double value) => OnSettingChanged();

    partial void OnFontSizeChanged(double value) => OnSettingChanged();

    partial void OnBackgroundOpacityChanged(double value) => OnSettingChanged();

    partial void OnTopMarginPxChanged(double value) => OnSettingChanged();

    partial void OnInitialPauseSecondsChanged(double value) => OnSettingChanged();

    partial void OnSelectedMonitorChanged(MonitorInfo? value) => OnSettingChanged();

    partial void OnHideFromCaptureChanged(bool value) => OnSettingChanged();

    partial void OnIsClickThroughChanged(bool value) => OnSettingChanged();

    partial void OnDisplayModeChanged(TeleprompterDisplayMode value) => OnSettingChanged();

    partial void OnUseSpaceToAdvanceChanged(bool value)
    {
        _keyboardHookService.UseSpaceToAdvance = value;
        ScheduleSaveSettings();
    }

    private void OnGlobalHotkeyTriggered(object? sender, GlobalHotkeyAction action)
    {
        _overlayViewModel.HandleHotkey(action);
    }

    private void OnOverlaySpeedChangedByHotkey(object? sender, double newSpeed)
    {
        _syncingFromOverlay = true;
        try
        {
            ScrollSpeedPxPerSec = newSpeed;
        }
        finally
        {
            _syncingFromOverlay = false;
        }
    }

    private void OnOverlayActiveSlideChanged(object? sender, int slideIndex)
    {
        SelectedSlideIndex = slideIndex;
    }

    private void OnOverlayVisibilityChanged(object? sender, bool visible)
    {
        IsPreviewOpen = visible;
    }
}
