using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosSalesReportExportService
{
    Task<ApiResponse<PosSalesReportOptionsDto>> GetOptionsAsync(PosSalesPdfFilterDto filter, CancellationToken ct = default);
    Task<ApiResponse<PosSalesPrintDataDto>> GetDataAsync(PosSalesPdfFilterDto filter, CancellationToken ct = default);
    Task<byte[]> RenderAsync(PosSalesPrintDataDto data, CancellationToken ct = default);
}
