using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public record PayrollPostingContext(PayrollPostingConfiguration Configuration, long FiscalYearId, string CurrencyCode, string SerialResetPolicy);
public record PayrollReversalContext(long FiscalYearId, string SerialResetPolicy);
public interface IHrPayrollPostingService
{
    Task<PayrollPostingConfiguration?> GetAsync(long branchId, CancellationToken cancellationToken = default);
    Task SaveAsync(PayrollPostingConfiguration configuration, CancellationToken cancellationToken = default);
    Task<PayrollPostingContext> ResolveAsync(long branchId, DateTime date, CancellationToken cancellationToken = default);
    Task<PayrollReversalContext> ResolveReversalAsync(long branchId, DateTime date, int voucherType, CancellationToken cancellationToken = default);
}
