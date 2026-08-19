using ThinkOnErp.Application.DTOs.Accounting.Closing;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IFiscalClosingService
{
    Task<PreClosingPeriodValidationDto> ValidatePeriodClosingAsync(long periodId, CancellationToken cancellationToken = default);
    Task<bool> ClosePeriodAsync(long periodId, ExecutePeriodCloseRequest request, string username, CancellationToken cancellationToken = default);
    Task<bool> ReopenPeriodAsync(long periodId, ExecutePeriodReopenRequest request, string username, CancellationToken cancellationToken = default);

    Task<PreClosingYearValidationDto> ValidateYearEndClosingAsync(long yearId, CancellationToken cancellationToken = default);
    Task<YearEndClosingResultDto> ExecuteYearEndClosingAsync(long yearId, ExecuteYearEndCloseRequest request, string username, CancellationToken cancellationToken = default);
}
