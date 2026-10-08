using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public record PayrollPostingContext(PayrollPostingConfiguration Configuration, long FiscalYearId);
public interface IHrPayrollPostingService
{
    Task<PayrollPostingConfiguration?> GetAsync(long branchId, CancellationToken cancellationToken = default);
    Task SaveAsync(PayrollPostingConfiguration configuration, CancellationToken cancellationToken = default);
    Task<PayrollPostingContext> ResolveAsync(long branchId, DateTime date, CancellationToken cancellationToken = default);
}
