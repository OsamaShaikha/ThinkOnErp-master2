using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IPayrollAdjustmentRepository
{
    Task<IReadOnlyList<PayrollAdjustment>> GetAdjustmentsAsync(long companyId, string? employeeCode, string? payPeriod, string? status, CancellationToken cancellationToken = default);
    Task<PayrollAdjustment?> GetAdjustmentByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollAdjustment>> GetApprovedAdjustmentsForPeriodAsync(long companyId, string payPeriod, CancellationToken cancellationToken = default);
    Task AddAdjustmentAsync(PayrollAdjustment adjustment, CancellationToken cancellationToken = default);
    Task UpdateAdjustmentAsync(PayrollAdjustment adjustment, CancellationToken cancellationToken = default);
    Task DeleteAdjustmentAsync(PayrollAdjustment adjustment, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
