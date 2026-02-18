using System.ComponentModel;
using System.Drawing;
using System.Windows;
using DevCLT.Core.Models;
using DevCLT.WindowsApp.ViewModels;
using Hardcodet.Wpf.TaskbarNotification;

namespace DevCLT.WindowsApp;

public partial class MainWindow : Window
{
    private TaskbarIcon? _trayIcon;
    private MainViewModel? _viewModel;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void Initialize(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        SetupTrayIcon();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Dev CLT Timer",
        };

        // Try to use app icon, fallback to default
        try
        {
            var exePath = System.IO.Path.Combine(AppContext.BaseDirectory, "DevCLTTimer.exe");
            if (System.IO.File.Exists(exePath))
                _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            else
                _trayIcon.Icon = SystemIcons.Application;
        }
        catch
        {
            _trayIcon.Icon = SystemIcons.Application;
        }

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowWindow();
        UpdateTrayMenu();

        // Update menu when engine state changes
        if (_viewModel != null)
        {
            _viewModel.TimerVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TimerViewModel.CurrentState))
                    Dispatcher.Invoke(UpdateTrayMenu);
            };
        }
    }

    private void UpdateTrayMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var openItem = new System.Windows.Controls.MenuItem { Header = "Abrir Dev CLT Timer" };
        openItem.Click += (_, _) => ShowWindow();
        menu.Items.Add(openItem);

        if (_viewModel != null)
        {
            var state = _viewModel.CurrentEngineState;

            if (state == SessionState.Working)
            {
                var breakItem = new System.Windows.Controls.MenuItem { Header = "Iniciar Pausa" };
                breakItem.Click += (_, _) => _viewModel.TimerVM.StartBreakCommand.Execute(null);
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
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_forceClose)
        {
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

        _trayIcon?.Dispose();
    }

    /// <summary>
    /// Handle BreakBackground tint: swap window background during break
    /// </summary>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
    }
}