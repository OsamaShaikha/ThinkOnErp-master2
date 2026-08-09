using ThinkOnErp.Application.DTOs.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public interface ICoaExcelImportService
{
    Task<CoaImportResultDto> ValidateAsync(Stream workbook, CancellationToken cancellationToken = default);
    Task<CoaImportResultDto> ImportAsync(Stream workbook, long defaultBranchId, CancellationToken cancellationToken = default);
}
