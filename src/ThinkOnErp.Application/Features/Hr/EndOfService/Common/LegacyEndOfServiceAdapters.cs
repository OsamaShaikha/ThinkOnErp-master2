using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Common;

/// <summary>
/// Used only by the obsolete EndOfServiceService facade when transactionRunner was not supplied.
/// </summary>
internal sealed class PassThroughEndOfServiceTransactionRunner : IEndOfServiceTransactionRunner
{
    public Task<T> ExecuteWithSettlementLockAsync<T>(long settlementId, Func<Task<T>> action, CancellationToken cancellationToken = default)
        => action();

    public Task LockEmployeeForCurrentSettlementAsync(string employeeCode, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// Used only by the obsolete EndOfServiceService facade when policyRepository was not supplied.
/// </summary>
internal sealed class NullPolicyRepository : IPolicyRepository
{
    public Task<int?> GetBranchRoundingRulesAsync(long branchId, CancellationToken cancellationToken = default)
        => Task.FromResult<int?>(null);

    public Task<IReadOnlyDictionary<string, int>> GetConfiguredPayrollCurrencyDecimalsAsync(long companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());

    public Task<IReadOnlyList<PayrollCurrencyPrecision>> GetPayrollCurrencyPrecisionsAsync(long companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PayrollCurrencyPrecision>>(Array.Empty<PayrollCurrencyPrecision>());

    public Task SavePayrollCurrencyPrecisionAsync(PayrollCurrencyPrecision precision, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> DeletePayrollCurrencyPrecisionAsync(long companyId, string currencyCode, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<TaxPolicy?> GetActiveTaxPolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
        => Task.FromResult<TaxPolicy?>(null);

    public Task<IReadOnlyList<TaxPolicy>> GetTaxPoliciesAsync(long companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TaxPolicy>>(Array.Empty<TaxPolicy>());

    public Task AddTaxPolicyAsync(TaxPolicy policy, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<SSCPolicy?> GetActiveSSCPolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
        => Task.FromResult<SSCPolicy?>(null);

    public Task<IReadOnlyList<SSCPolicy>> GetSSCPoliciesAsync(long companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SSCPolicy>>(Array.Empty<SSCPolicy>());

    public Task AddSSCPolicyAsync(SSCPolicy policy, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<ProrationPolicy?> GetActiveProrationPolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
        => Task.FromResult<ProrationPolicy?>(null);

    public Task<IReadOnlyList<ProrationPolicy>> GetProrationPoliciesAsync(long companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProrationPolicy>>(Array.Empty<ProrationPolicy>());

    public Task AddProrationPolicyAsync(ProrationPolicy policy, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<DeductionPolicy?> GetActiveDeductionPolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
        => Task.FromResult<DeductionPolicy?>(null);

    public Task<IReadOnlyList<DeductionPolicy>> GetDeductionPoliciesAsync(long companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DeductionPolicy>>(Array.Empty<DeductionPolicy>());

    public Task AddDeductionPolicyAsync(DeductionPolicy policy, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<OvertimeRule>> GetActiveOvertimeRulesAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OvertimeRule>>(Array.Empty<OvertimeRule>());

    public Task AddOvertimeRuleAsync(OvertimeRule rule, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<AttendancePolicy?> GetActiveAttendancePolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
        => Task.FromResult<AttendancePolicy?>(null);

    public Task<IReadOnlyList<AttendancePolicy>> GetAttendancePoliciesAsync(long companyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AttendancePolicy>>(Array.Empty<AttendancePolicy>());

    public Task AddAttendancePolicyAsync(AttendancePolicy policy, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
