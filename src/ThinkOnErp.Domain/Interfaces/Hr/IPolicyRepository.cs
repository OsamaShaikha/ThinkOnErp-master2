using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IPolicyRepository
{
    // Tax Policy & Brackets
    Task<TaxPolicy?> GetActiveTaxPolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaxPolicy>> GetTaxPoliciesAsync(long companyId, CancellationToken cancellationToken = default);
    Task AddTaxPolicyAsync(TaxPolicy policy, CancellationToken cancellationToken = default);

    // SSC Policy
    Task<SSCPolicy?> GetActiveSSCPolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SSCPolicy>> GetSSCPoliciesAsync(long companyId, CancellationToken cancellationToken = default);
    Task AddSSCPolicyAsync(SSCPolicy policy, CancellationToken cancellationToken = default);

    // Proration Policy
    Task<ProrationPolicy?> GetActiveProrationPolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProrationPolicy>> GetProrationPoliciesAsync(long companyId, CancellationToken cancellationToken = default);
    Task AddProrationPolicyAsync(ProrationPolicy policy, CancellationToken cancellationToken = default);

    // Deduction Policy
    Task<DeductionPolicy?> GetActiveDeductionPolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeductionPolicy>> GetDeductionPoliciesAsync(long companyId, CancellationToken cancellationToken = default);
    Task AddDeductionPolicyAsync(DeductionPolicy policy, CancellationToken cancellationToken = default);

    // Overtime Rules
    Task<IReadOnlyList<OvertimeRule>> GetActiveOvertimeRulesAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default);
    Task AddOvertimeRuleAsync(OvertimeRule rule, CancellationToken cancellationToken = default);

    // Attendance Policy
    Task<AttendancePolicy?> GetActiveAttendancePolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendancePolicy>> GetAttendancePoliciesAsync(long companyId, CancellationToken cancellationToken = default);
    Task AddAttendancePolicyAsync(AttendancePolicy policy, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
