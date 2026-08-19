using ThinkOnErp.Application.DTOs.Accounting.FiscalPeriods;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IGlFiscalPeriodService
{
    Task<IReadOnlyList<GlFiscalPeriodDto>> GetPeriodsByFiscalYearIdAsync(long fiscalYearId, CancellationToken cancellationToken = default);
    Task<GlFiscalPeriodDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlFiscalPeriodDto>> GeneratePeriodsAsync(long fiscalYearId, bool includeAdjustmentPeriod, string username, CancellationToken cancellationToken = default);
    Task<GlFiscalPeriodDto> SoftClosePeriodAsync(long id, string username, string? reason, CancellationToken cancellationToken = default);
    Task<GlFiscalPeriodDto> HardClosePeriodAsync(long id, string username, string? reason, CancellationToken cancellationToken = default);
    Task<GlFiscalPeriodDto> ReopenPeriodAsync(long id, string username, string? reason, CancellationToken cancellationToken = default);
    Task ValidatePostingAllowedAsync(long fiscalYearId, DateTime entryDate, CancellationToken cancellationToken = default);
}
