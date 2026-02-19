using System.IO;
using System.Windows;
using DevCLT.Core.Engine;
using DevCLT.Core.Interfaces;
using DevCLT.Infrastructure.Data;
using DevCLT.WindowsApp.Services;
using DevCLT.WindowsApp.ViewModels;
using Microsoft.Toolkit.Uwp.Notifications;

namespace DevCLT.WindowsApp;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Services
            IAppPaths paths = new WindowsAppPaths();
            IClock clock = new SystemClock();

            // Hotkeys (created early so notifier can reference configured keys)
            var hotkeyService = new HotkeyService();

            INotifier notifier = new WindowsNotifier(hotkeyService);

            // Ensure data directory exists
            Directory.CreateDirectory(paths.DataDirectory);

            // Database
            var dbInit = new DatabaseInitializer(paths);
            dbInit.EnsureCreated();

            IRepository repository = new SqliteRepository(paths);

            // Core Engine
            var engine = new TimerEngine(clock);

            // Theme Service
            IThemeService themeService = new ThemeService();

            // CSV Export
            ICsvExportService csvExport = new CsvExportService();

            // Main ViewModel
            var mainVM = new MainViewModel(engine, repository, notifier, clock, themeService, csvExport, hotkeyService);
            await mainVM.InitializeAsync();

            // Initialize hotkey config from saved settings
            var settings = await repository.LoadSettingsAsync();
            hotkeyService.UpdateConfiguration(settings.HotkeysEnabled, settings.HotkeyJornada, settings.HotkeyPausa, settings.HotkeyOvertime);

            // Window — create manually (no StartupUri)
            var window = new MainWindow();
            window.Initialize(mainVM, hotkeyService);
            window.Show();

            // Handle Notification Clicks
            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                // Runs on a background thread
                Application.Current.Dispatcher.Invoke(delegate
                {
                    var win = Application.Current.MainWindow;
                    if (win != null)
                    {
                        if (win.WindowState == WindowState.Minimized)
                            win.WindowState = WindowState.Normal;

                        win.Show();
                        win.Activate();
                        win.Topmost = true;  // Hack to bring to front
                        win.Topmost = false;
                        win.Focus();
                    }
                });
            };
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao iniciar o aplicativo:\n\n{ex}", "Dev CLT Timer — Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
