using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IPayrollRepository
{
    Task<IReadOnlyList<PayrollRun>> GetAllRunsAsync(
        string? payPeriod = null,
        long? branchId = null,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<PayrollRun?> GetRunByIdAsync(long id, bool includeLines = true, CancellationToken cancellationToken = default);
    Task<PayrollRun?> GetRunByPeriodAndBranchAsync(string payPeriod, long? branchId, CancellationToken cancellationToken = default);
    Task AddRunAsync(PayrollRun run, CancellationToken cancellationToken = default);
    void UpdateRun(PayrollRun run);
    void RemoveRunLines(IEnumerable<PayrollRunLine> lines);

    Task<IReadOnlyList<PayrollRunLine>> GetEmployeePayslipsAsync(
        string employeeCode,
        string? payPeriod = null,
        CancellationToken cancellationToken = default);

    Task<PayrollRunLine?> GetEmployeePayslipForPeriodAsync(string employeeCode, string payPeriod, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
