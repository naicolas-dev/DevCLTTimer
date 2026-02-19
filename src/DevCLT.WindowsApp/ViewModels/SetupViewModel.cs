using System.Windows.Input;
using DevCLT.Core.Interfaces;
using DevCLT.Core.Models;
using DevCLT.WindowsApp.Services;

namespace DevCLT.WindowsApp.ViewModels;

public class SetupViewModel : ViewModelBase
{
    private readonly IRepository _repository;
    private readonly IThemeService _themeService;

    private int _workHours = 8;
    private int _workMinutes = 0;
    private int _breakHours = 1;
    private int _breakMinutes = 0;
    private int _selectedNotifyIndex = 2; // 30min default

    public int WorkHours { get => _workHours; set => SetField(ref _workHours, Math.Clamp(value, 0, 23)); }
    public int WorkMinutes { get => _workMinutes; set => SetField(ref _workMinutes, Math.Clamp(value, 0, 59)); }
    public int BreakHours { get => _breakHours; set => SetField(ref _breakHours, Math.Clamp(value, 0, 23)); }
    public int BreakMinutes { get => _breakMinutes; set => SetField(ref _breakMinutes, Math.Clamp(value, 0, 59)); }

    // Index: 0=15, 1=20, 2=30, 3=60, 4=never(0)
    public int SelectedNotifyIndex { get => _selectedNotifyIndex; set => SetField(ref _selectedNotifyIndex, value); }

    public string[] NotifyOptions => new[] { "15 min", "20 min", "30 min", "60 min", "Nunca" };

    public int TotalWorkMinutes => WorkHours * 60 + WorkMinutes;
    public int TotalBreakMinutes => BreakHours * 60 + BreakMinutes;
    public int OvertimeNotifyMinutes => SelectedNotifyIndex switch
    {
        0 => 15, 1 => 20, 2 => 30, 3 => 60, _ => 0
    };

    public bool IsDarkTheme => _themeService.IsDarkTheme;

    public event Action<int, int, int>? StartRequested;

    public ICommand StartCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    public SetupViewModel(IRepository repository, IThemeService themeService)
    {
        _repository = repository;
        _themeService = themeService;
        StartCommand = new RelayCommand(OnStart, () => TotalWorkMinutes > 0);
        ToggleThemeCommand = new RelayCommand(async () =>
        {
            _themeService.ToggleTheme();
            OnPropertyChanged(nameof(IsDarkTheme));
            
            // Persist immediately
            var s = await _repository.LoadSettingsAsync();
            s.IsDarkTheme = IsDarkTheme;
            await _repository.SaveSettingsAsync(s);
        });
    }

    public async Task LoadSettings()
    {
        var s = await _repository.LoadSettingsAsync();
        WorkHours = s.WorkDurationMinutes / 60;
        WorkMinutes = s.WorkDurationMinutes % 60;
        BreakHours = s.BreakDurationMinutes / 60;
        BreakMinutes = s.BreakDurationMinutes % 60;
        SelectedNotifyIndex = s.OvertimeNotifyIntervalMinutes switch
        {
            15 => 0, 20 => 1, 30 => 2, 60 => 3, _ => 4
        };
        
        // Sync theme
        if (s.IsDarkTheme != IsDarkTheme)
        {
            _themeService.ToggleTheme();
            OnPropertyChanged(nameof(IsDarkTheme));
        }
    }

    private async void OnStart()
    {
        await _repository.SaveSettingsAsync(new AppSettings
        {
            WorkDurationMinutes = TotalWorkMinutes,
            BreakDurationMinutes = TotalBreakMinutes,
            OvertimeNotifyIntervalMinutes = OvertimeNotifyMinutes,
            IsDarkTheme = IsDarkTheme
        });
        StartRequested?.Invoke(TotalWorkMinutes, TotalBreakMinutes, OvertimeNotifyMinutes);
    }
}
