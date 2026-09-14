using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IPayrollPeriodRepository
{
    Task<IReadOnlyList<PayrollPeriod>> GetPeriodsAsync(long companyId, int? fiscalYear = null, CancellationToken cancellationToken = default);
    Task<PayrollPeriod?> GetPeriodByCodeAsync(long companyId, string periodCode, CancellationToken cancellationToken = default);
    Task AddPeriodAsync(PayrollPeriod period, CancellationToken cancellationToken = default);
    Task UpdatePeriodAsync(PayrollPeriod period, CancellationToken cancellationToken = default);

    Task<PayrollRun?> GetPayrollRunByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<PayrollRun?> GetPayrollRunByPeriodAsync(string payPeriod, long? branchId = null, CancellationToken cancellationToken = default);
    Task AddPayrollRunAsync(PayrollRun run, CancellationToken cancellationToken = default);
    Task UpdatePayrollRunAsync(PayrollRun run, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollRunLine>> GetRunLinesAsync(long payrollRunId, CancellationToken cancellationToken = default);
    Task<PayrollRunLine?> GetRunLineWithSnapshotAsync(long runLineId, CancellationToken cancellationToken = default);
    Task<PayrollCalculationSnapshot?> GetSnapshotByRunLineIdAsync(long runLineId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Employee>> GetActiveEmployeesAsync(long? branchId = null, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
