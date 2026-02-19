using System.Windows.Input;
using DevCLT.Core.Interfaces;
using DevCLT.WindowsApp.Services;

namespace DevCLT.WindowsApp.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IRepository _repository;
    private readonly HotkeyService _hotkeyService;

    private bool _hotkeysEnabled = true;
    private string _jornadaKey = "Ctrl+Alt+I";
    private string _pausaKey = "Ctrl+Alt+P";
    private string _overtimeKey = "Ctrl+Alt+X";
    private string? _recordingField;

    // Saved snapshot for dirty detection
    private bool _savedEnabled = true;
    private string _savedJornadaKey = "Ctrl+Alt+I";
    private string _savedPausaKey = "Ctrl+Alt+P";
    private string _savedOvertimeKey = "Ctrl+Alt+X";

    // Unsaved-changes modal
    private bool _showUnsavedModal;

    // Defaults
    private const string DefaultJornadaKey = "Ctrl+Alt+I";
    private const string DefaultPausaKey = "Ctrl+Alt+P";
    private const string DefaultOvertimeKey = "Ctrl+Alt+X";
    private const bool DefaultEnabled = true;

    public bool HotkeysEnabled
    {
        get => _hotkeysEnabled;
        set
        {
            if (SetField(ref _hotkeysEnabled, value))
                OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    public string JornadaKey
    {
        get => _jornadaKey;
        set
        {
            if (SetField(ref _jornadaKey, value))
                OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    public string PausaKey
    {
        get => _pausaKey;
        set
        {
            if (SetField(ref _pausaKey, value))
                OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    public string OvertimeKey
    {
        get => _overtimeKey;
        set
        {
            if (SetField(ref _overtimeKey, value))
                OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    public bool ShowUnsavedModal
    {
        get => _showUnsavedModal;
        set => SetField(ref _showUnsavedModal, value);
    }

    public bool HasUnsavedChanges =>
        _hotkeysEnabled != _savedEnabled ||
        _jornadaKey != _savedJornadaKey ||
        _pausaKey != _savedPausaKey ||
        _overtimeKey != _savedOvertimeKey;

    public bool IsRecordingJornada => _recordingField == nameof(JornadaKey);
    public bool IsRecordingPausa => _recordingField == nameof(PausaKey);
    public bool IsRecordingOvertime => _recordingField == nameof(OvertimeKey);
    public bool IsRecording => _recordingField != null;

    // Commands
    public ICommand BackCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RestoreDefaultsCommand { get; }
    public ICommand RecordJornadaCommand { get; }
    public ICommand RecordPausaCommand { get; }
    public ICommand RecordOvertimeCommand { get; }

    // Unsaved-changes modal commands
    public ICommand UnsavedDiscardCommand { get; }
    public ICommand UnsavedSaveCommand { get; }
    public ICommand UnsavedContinueCommand { get; }

    public event Action? BackRequested;

    public SettingsViewModel(IRepository repository, HotkeyService hotkeyService)
    {
        _repository = repository;
        _hotkeyService = hotkeyService;

        _jornadaKey = hotkeyService.JornadaKey;
        _pausaKey = hotkeyService.PausaKey;
        _overtimeKey = hotkeyService.OvertimeKey;
        _hotkeysEnabled = hotkeyService.IsEnabled;

        BackCommand = new RelayCommand(HandleBack);
        SaveCommand = new AsyncRelayCommand(SaveSettings);
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
        RecordJornadaCommand = new RelayCommand(() => StartRecording(nameof(JornadaKey)));
        RecordPausaCommand = new RelayCommand(() => StartRecording(nameof(PausaKey)));
        RecordOvertimeCommand = new RelayCommand(() => StartRecording(nameof(OvertimeKey)));

        UnsavedDiscardCommand = new RelayCommand(DiscardAndLeave);
        UnsavedSaveCommand = new AsyncRelayCommand(SaveAndLeave);
        UnsavedContinueCommand = new RelayCommand(() => ShowUnsavedModal = false);
    }

    public async Task LoadSettings()
    {
        var s = await _repository.LoadSettingsAsync();
        _hotkeysEnabled = s.HotkeysEnabled;
        _jornadaKey = s.HotkeyJornada;
        _pausaKey = s.HotkeyPausa;
        _overtimeKey = s.HotkeyOvertime;

        // Take snapshot
        _savedEnabled = _hotkeysEnabled;
        _savedJornadaKey = _jornadaKey;
        _savedPausaKey = _pausaKey;
        _savedOvertimeKey = _overtimeKey;

        OnPropertyChanged(nameof(HotkeysEnabled));
        OnPropertyChanged(nameof(JornadaKey));
        OnPropertyChanged(nameof(PausaKey));
        OnPropertyChanged(nameof(OvertimeKey));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void HandleBack()
    {
        if (HasUnsavedChanges)
        {
            ShowUnsavedModal = true;
        }
        else
        {
            BackRequested?.Invoke();
        }
    }

    private void DiscardAndLeave()
    {
        // Revert to saved state
        _hotkeysEnabled = _savedEnabled;
        _jornadaKey = _savedJornadaKey;
        _pausaKey = _savedPausaKey;
        _overtimeKey = _savedOvertimeKey;

        ShowUnsavedModal = false;
        BackRequested?.Invoke();
    }

    private async Task SaveAndLeave()
    {
        await SaveSettings();
        ShowUnsavedModal = false;
        BackRequested?.Invoke();
    }

    private async Task SaveSettings()
    {
        _hotkeyService.UpdateConfiguration(_hotkeysEnabled, _jornadaKey, _pausaKey, _overtimeKey);

        var s = await _repository.LoadSettingsAsync();
        s.HotkeysEnabled = _hotkeysEnabled;
        s.HotkeyJornada = _jornadaKey;
        s.HotkeyPausa = _pausaKey;
        s.HotkeyOvertime = _overtimeKey;
        await _repository.SaveSettingsAsync(s);

        // Update snapshot
        _savedEnabled = _hotkeysEnabled;
        _savedJornadaKey = _jornadaKey;
        _savedPausaKey = _pausaKey;
        _savedOvertimeKey = _overtimeKey;

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void RestoreDefaults()
    {
        HotkeysEnabled = DefaultEnabled;
        JornadaKey = DefaultJornadaKey;
        PausaKey = DefaultPausaKey;
        OvertimeKey = DefaultOvertimeKey;
    }

    private void StartRecording(string field)
    {
        _recordingField = field;
        OnPropertyChanged(nameof(IsRecordingJornada));
        OnPropertyChanged(nameof(IsRecordingPausa));
        OnPropertyChanged(nameof(IsRecordingOvertime));
    }

    public void OnKeyRecorded(System.Windows.Input.KeyEventArgs e)
    {
        if (_recordingField == null) return;

        var display = HotkeyService.KeyEventToString(e);

        if (!HotkeyService.IsCompleteHotkey(e))
        {
            UpdateRecordingPreview(display);
            return;
        }

        switch (_recordingField)
        {
            case nameof(JornadaKey): JornadaKey = display; break;
            case nameof(PausaKey): PausaKey = display; break;
            case nameof(OvertimeKey): OvertimeKey = display; break;
        }

        _recordingField = null;
        OnPropertyChanged(nameof(IsRecordingJornada));
        OnPropertyChanged(nameof(IsRecordingPausa));
        OnPropertyChanged(nameof(IsRecordingOvertime));
        e.Handled = true;
    }

    private void UpdateRecordingPreview(string preview)
    {
        switch (_recordingField)
        {
            case nameof(JornadaKey): SetField(ref _jornadaKey, preview, nameof(JornadaKey)); break;
            case nameof(PausaKey): SetField(ref _pausaKey, preview, nameof(PausaKey)); break;
            case nameof(OvertimeKey): SetField(ref _overtimeKey, preview, nameof(OvertimeKey)); break;
        }
    }

    public void CancelRecording()
    {
        if (_recordingField != null)
        {
            _recordingField = null;
            _ = LoadSettings();
            OnPropertyChanged(nameof(IsRecordingJornada));
            OnPropertyChanged(nameof(IsRecordingPausa));
            OnPropertyChanged(nameof(IsRecordingOvertime));
        }
    }
}
