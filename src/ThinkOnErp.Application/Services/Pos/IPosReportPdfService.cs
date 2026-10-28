using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosReportPdfService
{
    ApiResponse<List<PosReportCatalogEntryDto>> GetCatalog(int? language);
    Task<ApiResponse<PosSalesReportOptionsDto>> GetOptionsAsync(string report, PosReportExportFilterDto filter, CancellationToken ct = default);
    Task<ApiResponse<PosReportPrintDataDto>> GetDataAsync(string report, PosReportExportFilterDto filter, CancellationToken ct = default);
    Task<byte[]> RenderAsync(PosReportPrintDataDto data, CancellationToken ct = default);
}
