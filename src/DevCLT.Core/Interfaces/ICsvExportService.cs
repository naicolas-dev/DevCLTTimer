using DevCLT.Core.Models;

namespace DevCLT.Core.Interfaces;

public interface ICsvExportService
{
    /// <summary>
    /// Exporta a lista de resumos para CSV.
    /// Retorna o caminho salvo, ou null se o usuário cancelou.
    /// </summary>
    Task<string?> ExportAsync(IEnumerable<DaySummary> summaries, string suggestedFileName);
}
