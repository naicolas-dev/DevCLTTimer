using DevCLT.Core.Interfaces;
using DevCLT.Core.Models;
using DevCLT.Infrastructure.Data;

namespace DevCLT.Tests;

public class SqliteRepositorySettingsTests
{
    [Fact]
    public async Task LoadSettingsAsync_DefaultsOnboardingToFalse_WhenSettingDoesNotExist()
    {
        var (repository, dataDirectory) = CreateRepository();
        try
        {
            var settings = await repository.LoadSettingsAsync();
            Assert.False(settings.HasCompletedOnboarding);
        }
        finally
        {
            Cleanup(dataDirectory);
        }
    }

    [Fact]
    public async Task SaveAndLoadSettings_PersistsOnboardingFlag_AlongWithOtherFields()
    {
        var (repository, dataDirectory) = CreateRepository();
        try
        {
            var input = new AppSettings
            {
                WorkDurationMinutes = 450,
                BreakDurationMinutes = 50,
                OvertimeNotifyIntervalMinutes = 20,
                IsDarkTheme = true,
                HasCompletedOnboarding = true,
                HotkeysEnabled = false,
                HotkeyJornada = "Ctrl+Shift+1",
                HotkeyPausa = "Ctrl+Shift+2",
                HotkeyOvertime = "Ctrl+Shift+3"
            };

            await repository.SaveSettingsAsync(input);
            var loaded = await repository.LoadSettingsAsync();

            Assert.Equal(input.WorkDurationMinutes, loaded.WorkDurationMinutes);
            Assert.Equal(input.BreakDurationMinutes, loaded.BreakDurationMinutes);
            Assert.Equal(input.OvertimeNotifyIntervalMinutes, loaded.OvertimeNotifyIntervalMinutes);
            Assert.Equal(input.IsDarkTheme, loaded.IsDarkTheme);
            Assert.Equal(input.HasCompletedOnboarding, loaded.HasCompletedOnboarding);
            Assert.Equal(input.HotkeysEnabled, loaded.HotkeysEnabled);
            Assert.Equal(input.HotkeyJornada, loaded.HotkeyJornada);
            Assert.Equal(input.HotkeyPausa, loaded.HotkeyPausa);
            Assert.Equal(input.HotkeyOvertime, loaded.HotkeyOvertime);
        }
        finally
        {
            Cleanup(dataDirectory);
        }
    }

    [Fact]
    public async Task UpdatingOnlyOnboardingFlag_DoesNotChangeOtherSettingsValues()
    {
        var (repository, dataDirectory) = CreateRepository();
        try
        {
            var baseline = new AppSettings
            {
                WorkDurationMinutes = 510,
                BreakDurationMinutes = 45,
                OvertimeNotifyIntervalMinutes = 15,
                IsDarkTheme = true,
                HotkeysEnabled = false,
                HotkeyJornada = "Alt+1",
                HotkeyPausa = "Alt+2",
                HotkeyOvertime = "Alt+3",
                HasCompletedOnboarding = false
            };

            await repository.SaveSettingsAsync(baseline);

            var update = await repository.LoadSettingsAsync();
            update.HasCompletedOnboarding = true;
            await repository.SaveSettingsAsync(update);

            var loaded = await repository.LoadSettingsAsync();
            Assert.True(loaded.HasCompletedOnboarding);
            Assert.Equal(baseline.WorkDurationMinutes, loaded.WorkDurationMinutes);
            Assert.Equal(baseline.BreakDurationMinutes, loaded.BreakDurationMinutes);
            Assert.Equal(baseline.OvertimeNotifyIntervalMinutes, loaded.OvertimeNotifyIntervalMinutes);
            Assert.Equal(baseline.IsDarkTheme, loaded.IsDarkTheme);
            Assert.Equal(baseline.HotkeysEnabled, loaded.HotkeysEnabled);
            Assert.Equal(baseline.HotkeyJornada, loaded.HotkeyJornada);
            Assert.Equal(baseline.HotkeyPausa, loaded.HotkeyPausa);
            Assert.Equal(baseline.HotkeyOvertime, loaded.HotkeyOvertime);
        }
        finally
        {
            Cleanup(dataDirectory);
        }
    }

    private static (SqliteRepository Repository, string DataDirectory) CreateRepository()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "DevCLTTimerTests", Guid.NewGuid().ToString("N"));
        var appPaths = new TestAppPaths(dataDirectory);
        var initializer = new DatabaseInitializer(appPaths);
        initializer.EnsureCreated();
        return (new SqliteRepository(appPaths), dataDirectory);
    }

    private static void Cleanup(string dataDirectory)
    {
        if (!Directory.Exists(dataDirectory))
            return;

        try
        {
            Directory.Delete(dataDirectory, true);
        }
        catch (IOException)
        {
            // Sqlite pooling can keep file handles alive briefly in test process.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore transient cleanup failures in temporary test folder.
        }
    }

    private sealed class TestAppPaths : IAppPaths
    {
        public string DataDirectory { get; }
        public string DatabasePath { get; }

        public TestAppPaths(string dataDirectory)
        {
            DataDirectory = dataDirectory;
            DatabasePath = Path.Combine(dataDirectory, "test.db");
        }
    }
}
