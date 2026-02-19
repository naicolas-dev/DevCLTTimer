using System.Diagnostics;
using System.IO;
using System.Text;
using DevCLT.Core.Interfaces;
using DevCLT.Core.Models;
using Microsoft.Win32;

namespace DevCLT.WindowsApp.Services;

public class CsvExportService : ICsvExportService
{
    public Task<string?> ExportAsync(IEnumerable<DaySummary> summaries, string suggestedFileName)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = suggestedFileName,
            DefaultExt = ".csv"
        };

        if (dlg.ShowDialog() != true)
            return Task.FromResult<string?>(null);

        var path = dlg.FileName;

        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine("Data,Trabalho (h:mm),Pausa (h:mm),Hora Extra (h:mm)");

        foreach (var s in summaries)
        {
            var date = s.DateLocal;
            var work = FormatCsvDuration(s.TotalWorkSeconds);
            var brk = FormatCsvDuration(s.TotalBreakSeconds);
            var ot = FormatCsvDuration(s.TotalOvertimeSeconds);
            writer.WriteLine($"{date},{work},{brk},{ot}");
        }

        // Open file in default app
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

        return Task.FromResult<string?>(path);
    }

    private static string FormatCsvDuration(int totalSeconds)
    {
        if (totalSeconds <= 0) return "";
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}";
    }
}
