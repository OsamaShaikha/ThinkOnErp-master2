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
    Task<IReadOnlyList<PayrollRun>> GetRunsByPeriodAsync(long companyId, string payPeriod, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<PayrollRun> Items, long TotalCount)> GetPayrollRunsAsync(
        long companyId, string? payPeriod, long? branchId, string? status, int pageIndex, int pageSize,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<PayrollRunLine> Items, long TotalCount)> GetEmployeePayslipsAsync(
        long companyId, string employeeCode, int pageIndex, int pageSize, CancellationToken cancellationToken = default);
    Task<PayrollRunLine?> GetEmployeePayslipAsync(
        long companyId, long payrollRunId, string employeeCode, CancellationToken cancellationToken = default);
    Task<bool> HasPendingAdjustmentsForPeriodAsync(long companyId, string payPeriod, CancellationToken cancellationToken = default);
    Task<bool> HasSubsequentClosedPeriodsAsync(long companyId, int fiscalYear, int month, CancellationToken cancellationToken = default);
    Task<PayrollRun?> GetPreviousPayrollRunAsync(long? branchId, string currentPayPeriod, long currentRunId, CancellationToken cancellationToken = default);

    Task<PayrollRun?> GetPayrollRunByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<PayrollRun?> GetPayrollRunSnapshotAsync(long id, CancellationToken cancellationToken = default);
    Task<PayrollRun?> GetLatestPayrollCorrectionAsync(long rootRunId, CancellationToken cancellationToken = default);
    Task SupersedePayrollCorrectionFamilyAsync(long rootRunId, string user, CancellationToken cancellationToken = default);
    Task<bool> HasOtherActivePayrollRunAsync(string payPeriod, long? branchId, long excludingId, CancellationToken cancellationToken = default);
    Task<bool> HasPayrollFinancialLinksAsync(long payrollRunId, CancellationToken cancellationToken = default);
    Task<PayrollPeriod?> GetPeriodForRunAsync(PayrollRun run, CancellationToken cancellationToken = default);
    Task<PayrollRun?> GetPayrollRunByPeriodAsync(string payPeriod, long? branchId = null, CancellationToken cancellationToken = default);
    Task AddPayrollRunAsync(PayrollRun run, CancellationToken cancellationToken = default);
    Task LinkApprovedAdjustmentsToRunAsync(long companyId, PayrollRun run, string user, CancellationToken cancellationToken = default);
    Task UpdatePayrollRunAsync(PayrollRun run, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollRunLine>> GetRunLinesAsync(long payrollRunId, CancellationToken cancellationToken = default);
    Task<PayrollRunLine?> GetRunLineWithSnapshotAsync(long runLineId, CancellationToken cancellationToken = default);
    Task<PayrollCalculationSnapshot?> GetSnapshotByRunLineIdAsync(long runLineId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Employee>> GetActiveEmployeesAsync(long? branchId = null, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
