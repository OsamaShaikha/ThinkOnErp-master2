using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IPolicyRepository
{
    // Attendance Policy
    Task<AttendancePolicy?> GetEffectiveAttendancePolicyAsync(long companyId, DateTime calculationDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendancePolicy>> GetAllAttendancePoliciesAsync(long companyId, CancellationToken cancellationToken = default);
    Task AddAttendancePolicyAsync(AttendancePolicy policy, CancellationToken cancellationToken = default);
    void UpdateAttendancePolicy(AttendancePolicy policy);

    // Overtime Rules
    Task<IReadOnlyList<OvertimeRule>> GetEffectiveOvertimeRulesAsync(long companyId, DateTime calculationDate, CancellationToken cancellationToken = default);
    Task<OvertimeRule?> GetEffectiveOvertimeRuleAsync(long companyId, string dayType, DateTime calculationDate, CancellationToken cancellationToken = default);
    Task AddOvertimeRuleAsync(OvertimeRule rule, CancellationToken cancellationToken = default);
    void UpdateOvertimeRule(OvertimeRule rule);

    // Proration Policy
    Task<ProrationPolicy?> GetEffectiveProrationPolicyAsync(long companyId, DateTime calculationDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProrationPolicy>> GetAllProrationPoliciesAsync(long companyId, CancellationToken cancellationToken = default);
    Task AddProrationPolicyAsync(ProrationPolicy policy, CancellationToken cancellationToken = default);
    void UpdateProrationPolicy(ProrationPolicy policy);

    // Deduction Policy
    Task<DeductionPolicy?> GetEffectiveDeductionPolicyAsync(long companyId, DateTime calculationDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeductionPolicy>> GetAllDeductionPoliciesAsync(long companyId, CancellationToken cancellationToken = default);
    Task AddDeductionPolicyAsync(DeductionPolicy policy, CancellationToken cancellationToken = default);
    void UpdateDeductionPolicy(DeductionPolicy policy);

    // Tax Policy & Brackets
    Task<TaxPolicy?> GetEffectiveTaxPolicyAsync(long companyId, DateTime calculationDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaxPolicy>> GetAllTaxPoliciesAsync(long companyId, CancellationToken cancellationToken = default);
    Task AddTaxPolicyAsync(TaxPolicy policy, CancellationToken cancellationToken = default);
    void UpdateTaxPolicy(TaxPolicy policy);

    // SSC Policy
    Task<SSCPolicy?> GetEffectiveSSCPolicyAsync(long companyId, DateTime calculationDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SSCPolicy>> GetAllSSCPoliciesAsync(long companyId, CancellationToken cancellationToken = default);
    Task AddSSCPolicyAsync(SSCPolicy policy, CancellationToken cancellationToken = default);
    void UpdateSSCPolicy(SSCPolicy policy);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
