using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IEndOfServiceRepository
{
    Task<EndOfServiceSettlement?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<EndOfServiceSettlement?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EndOfServiceSettlement>> GetListAsync(long companyId, long? branchId = null, string? status = null, CancellationToken cancellationToken = default);
    Task<EndOfServicePolicy?> GetActivePolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default);
    Task<EndOfServicePolicy?> GetActivePolicyAsync(long companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EndOfServicePolicy>> GetPoliciesAsync(long companyId, CancellationToken cancellationToken = default);
    Task AddPolicyAsync(EndOfServicePolicy policy, CancellationToken cancellationToken = default);
    Task SavePolicyChangesAsync(CancellationToken cancellationToken = default);

    Task AddAsync(EndOfServiceSettlement settlement, CancellationToken cancellationToken = default);
    Task UpdateAsync(EndOfServiceSettlement settlement, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<decimal> GetRemainingLeaveBalanceDaysAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<decimal> GetOutstandingLoanBalanceAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<decimal> GetOutstandingAdvanceBalanceAsync(string employeeCode, CancellationToken cancellationToken = default);

    Task<bool> HasActiveAccountingHandoffAsync(long settlementId, CancellationToken cancellationToken = default);
    Task<HrAccountingHandoff?> GetLatestHandoffAsync(long settlementId, CancellationToken cancellationToken = default);
}

