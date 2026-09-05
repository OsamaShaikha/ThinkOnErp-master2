using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IPayrollPeriodRepository
{
    Task<IReadOnlyList<PayrollPeriod>> GetAllAsync(long companyId, int? fiscalYear = null, CancellationToken cancellationToken = default);
    Task<PayrollPeriod?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<PayrollPeriod?> GetByPeriodCodeAsync(long companyId, string periodCode, string payrollType = "MONTHLY", CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(long companyId, int fiscalYear, int month, string payrollType = "MONTHLY", CancellationToken cancellationToken = default);
    Task AddAsync(PayrollPeriod period, CancellationToken cancellationToken = default);
    void Update(PayrollPeriod period);

    // Snapshot storage
    Task AddSnapshotAsync(PayrollCalculationSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<PayrollCalculationSnapshot?> GetSnapshotByLineIdAsync(long payrollRunLineId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
