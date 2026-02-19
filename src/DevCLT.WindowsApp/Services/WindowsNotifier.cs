using DevCLT.Core.Interfaces;
using DevCLT.WindowsApp.Services;
using Microsoft.Toolkit.Uwp.Notifications;

namespace DevCLT.WindowsApp.Services;

public class WindowsNotifier : INotifier
{
    private readonly HotkeyService? _hotkeyService;

    public WindowsNotifier() { }

    public WindowsNotifier(HotkeyService hotkeyService)
    {
        _hotkeyService = hotkeyService;
    }

    private string PausaKey => _hotkeyService?.PausaKey ?? "Ctrl+Alt+P";
    private string JornadaKey => _hotkeyService?.JornadaKey ?? "Ctrl+Alt+I";
    private string OvertimeKey => _hotkeyService?.OvertimeKey ?? "Ctrl+Alt+X";

    public void NotifyBreakEnded()
    {
        try
        {
            new ToastContentBuilder()
                .AddArgument("action", "viewApp")
                .AddText("Pausa concluída")
                .AddText($"{PausaKey} para retomar o trabalho")
                .Show();
        }
        catch
        {
            // Toast not available (e.g., no AUMID registered)
        }
    }

    public void NotifyWorkCompleted()
    {
        try
        {
            new ToastContentBuilder()
                .AddArgument("action", "viewApp")
                .AddText("Jornada concluída! 🎉")
                .AddText($"{OvertimeKey} para hora extra · {JornadaKey} para encerrar")
                .Show();
        }
        catch { }
    }

    public void NotifyOvertime(TimeSpan elapsed)
    {
        try
        {
            new ToastContentBuilder()
                .AddArgument("action", "viewApp")
                .AddText("Hora extra em andamento")
                .AddText($"Já são {elapsed:hh\\:mm\\:ss} de hora extra. {OvertimeKey} para encerrar.")
                .Show();
        }
        catch { }
    }

    public void NotifyOvertimeMuted(TimeSpan elapsed)
    {
        // Muted — no notification
    }
}
