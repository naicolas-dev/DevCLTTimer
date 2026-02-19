using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using DevCLT.Core.Models;
using DevCLT.WindowsApp.Services;
using DevCLT.WindowsApp.ViewModels;
using Hardcodet.Wpf.TaskbarNotification;

namespace DevCLT.WindowsApp;

public partial class MainWindow : Window
{
    private TaskbarIcon? _trayIcon;
    private MainViewModel? _viewModel;
    private HotkeyService? _hotkeyService;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void Initialize(MainViewModel viewModel, HotkeyService hotkeyService)
    {
        _viewModel = viewModel;
        _hotkeyService = hotkeyService;
        DataContext = viewModel;
        SetupTrayIcon();
        SetupHotkeys();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Dev CLT Timer",
        };

        // Fix: Load icon directly from embedded resources for reliability in all modes (Debug/Release/Publish)
        try
        {
            var iconUri = new Uri("pack://application:,,,/app.ico");
            var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                _trayIcon.Icon = new System.Drawing.Icon(stream);
            }
            else
            {
                _trayIcon.Icon = SystemIcons.Application;
            }
        }
        catch
        {
            _trayIcon.Icon = SystemIcons.Application;
        }

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowWindow();
        UpdateTrayMenu();

        // Subscribe to ViewModel changes for dynamic tooltip
        if (_viewModel != null)
        {
            _viewModel.TimerVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TimerViewModel.CurrentState))
                {
                    Dispatcher.Invoke(UpdateTrayMenu);
                    Dispatcher.Invoke(UpdateTrayTooltip);
                }
                else if (e.PropertyName == nameof(TimerViewModel.DisplayTime))
                {
                    Dispatcher.Invoke(UpdateTrayTooltip);
                }
            };
        }
    }

    private void UpdateTrayTooltip()
    {
        if (_trayIcon == null || _viewModel == null) return;
        _trayIcon.ToolTipText = _viewModel.TimerVM.TrayStatusText;
    }

    private void UpdateTrayMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        // 1. Status Item (acts as title/timer display)
        var statusItem = new System.Windows.Controls.MenuItem
        {
            FontWeight = FontWeights.Bold,
            IsEnabled = false // Visual indicator only, not interactive
        };
        
        // Use Binding for real-time updates even when menu is open
        if (_viewModel != null)
        {
            var binding = new System.Windows.Data.Binding("TimerVM.TrayStatusText")
            {
                Source = _viewModel
            };
            statusItem.SetBinding(System.Windows.Controls.HeaderedItemsControl.HeaderProperty, binding);
        }
        else
        {
            statusItem.Header = "Carregando...";
        }

        menu.Items.Add(statusItem);
        menu.Items.Add(new System.Windows.Controls.Separator());

        var openItem = new System.Windows.Controls.MenuItem { Header = "Abrir Dev CLT Timer" };
        openItem.Click += (_, _) => ShowWindow();
        menu.Items.Add(openItem);

        if (_viewModel != null)
        {
            var state = _viewModel.CurrentEngineState;

            if (state == SessionState.Working)
            {
                var breakItem = new System.Windows.Controls.MenuItem { Header = "Iniciar Pausa" };
                breakItem.Click += (_, _) =>
                {
                    ShowWindow();
                    _viewModel.TimerVM.ShowStartBreakConfirmCommand.Execute(null);
                };
                menu.Items.Add(breakItem);
            }
            else if (state == SessionState.BreakEndedWaitingUser)
            {
                var resumeItem = new System.Windows.Controls.MenuItem { Header = "Retomar Trabalho" };
                resumeItem.Click += (_, _) => _viewModel.TimerVM.ResumeWorkCommand.Execute(null);
                menu.Items.Add(resumeItem);
            }
            else if (state == SessionState.Overtime)
            {
                var stopItem = new System.Windows.Controls.MenuItem { Header = "Encerrar Hora Extra" };
                stopItem.Click += (_, _) => _viewModel.TimerVM.StopOvertimeCommand.Execute(null);
                menu.Items.Add(stopItem);
            }
        }

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Sair" };
        exitItem.Click += (_, _) =>
        {
            _forceClose = true;
            Close();
        };
        menu.Items.Add(exitItem);

        _trayIcon!.ContextMenu = menu;
        
        // Initial update of text
        UpdateTrayTooltip();
    }

    private void SetupHotkeys()
    {
        if (_hotkeyService == null) return;

        Loaded += (_, _) =>
        {
            var helper = new WindowInteropHelper(this);
            _hotkeyService.Register(helper.Handle);
        };

        _hotkeyService.JornadaHotkeyPressed += OnJornadaHotkey;
        _hotkeyService.PausaHotkeyPressed += OnPausaHotkey;
        _hotkeyService.OvertimeHotkeyPressed += OnOvertimeHotkey;
    }

    private void OnJornadaHotkey()
    {
        if (_viewModel == null) return;
        Dispatcher.Invoke(() =>
        {
            var vm = _viewModel.TimerVM;

            // If end-day confirm modal is already showing, confirm it
            if (vm.ShowEndDayConfirm)
            {
                vm.ConfirmEndDayCommand.Execute(null);
                return;
            }

            var state = _viewModel.CurrentEngineState;
            switch (state)
            {
                case SessionState.Idle:
                    ShowWindow();
                    if (_viewModel.SetupVM.StartCommand.CanExecute(null))
                        _viewModel.SetupVM.StartCommand.Execute(null);
                    break;
                case SessionState.Working:
                    ShowWindow();
                    vm.ShowEndDayConfirmCommand.Execute(null);
                    break;
            }
        });
    }

    private void OnPausaHotkey()
    {
        if (_viewModel == null) return;
        Dispatcher.Invoke(() =>
        {
            var vm = _viewModel.TimerVM;

            // If start-break confirm modal is already showing, confirm it
            if (vm.ShowStartBreakConfirm)
            {
                vm.ConfirmStartBreakCommand.Execute(null);
                return;
            }

            // If end-break-early confirm modal is already showing, confirm it
            if (vm.ShowEndBreakEarlyConfirm)
            {
                vm.EndBreakEarlyCommand.Execute(null);
                return;
            }

            var state = _viewModel.CurrentEngineState;
            switch (state)
            {
                case SessionState.Working:
                    ShowWindow();
                    vm.ShowStartBreakConfirmCommand.Execute(null);
                    break;
                case SessionState.Break:
                    ShowWindow();
                    vm.ShowEndBreakEarlyConfirmCommand.Execute(null);
                    break;
            }
        });
    }

    private void OnOvertimeHotkey()
    {
        if (_viewModel == null) return;
        Dispatcher.Invoke(() =>
        {
            var state = _viewModel.CurrentEngineState;
            switch (state)
            {
                case SessionState.WorkCompleted:
                    ShowWindow();
                    _viewModel.TimerVM.StartOvertimeCommand.Execute(null);
                    break;
                case SessionState.Overtime:
                    ShowWindow();
                    _viewModel.TimerVM.StopOvertimeCommand.Execute(null);
                    break;
            }
        });
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_forceClose)
        {
            _hotkeyService?.Dispose();
            _trayIcon?.Dispose();
            return;
        }

        // If there's an active session, minimize to tray instead of closing
        if (_viewModel != null &&
            _viewModel.CurrentEngineState != SessionState.Idle)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
    }

    /// <summary>
    /// Handle BreakBackground tint: swap window background during break
    /// </summary>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            DragMove();
    }
}