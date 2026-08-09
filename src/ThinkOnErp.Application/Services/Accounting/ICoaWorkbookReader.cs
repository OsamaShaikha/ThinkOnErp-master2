using ThinkOnErp.Application.DTOs.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public interface ICoaWorkbookReader
{
    Task<CoaWorkbookReadResultDto> ReadAsync(Stream workbook, CancellationToken cancellationToken = default);
}
