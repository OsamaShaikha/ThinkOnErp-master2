using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Renders generated compliance reports for download and scheduled delivery.
/// </summary>
public interface IComplianceReportExporter
{
    Task<byte[]> ExportToPdfAsync(IReport report, CancellationToken cancellationToken = default);
    Task<byte[]> ExportToCsvAsync(IReport report, CancellationToken cancellationToken = default);
    Task<string> ExportToJsonAsync(IReport report, CancellationToken cancellationToken = default);
}
