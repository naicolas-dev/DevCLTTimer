using DevCLT.Core.Interfaces;
using Microsoft.Toolkit.Uwp.Notifications;

namespace DevCLT.WindowsApp.Services;

public class WindowsNotifier : INotifier
{
    public void NotifyBreakEnded()
    {
        try
        {
            new ToastContentBuilder()
                .AddText("Pausa concluída")
                .AddText("Clique para retomar o trabalho")
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
                .AddText("Jornada concluída! 🎉")
                .AddText("Você completou suas horas de trabalho.")
                .Show();
        }
        catch { }
    }

    public void NotifyOvertime(TimeSpan elapsed)
    {
        try
        {
            new ToastContentBuilder()
                .AddText("Hora extra em andamento")
                .AddText($"Já são {elapsed:hh\\:mm\\:ss} de hora extra.")
                .Show();
        }
        catch { }
    }

    public void NotifyOvertimeMuted(TimeSpan elapsed)
    {
        // Muted — no notification
    }
}
