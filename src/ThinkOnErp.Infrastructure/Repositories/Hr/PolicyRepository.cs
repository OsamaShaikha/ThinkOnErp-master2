using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class PolicyRepository : IPolicyRepository
{
    private readonly OracleDbContext _context;

    public PolicyRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AttendancePolicy?> GetEffectiveAttendancePolicyAsync(long companyId, DateTime calculationDate, CancellationToken cancellationToken = default)
    {
        return await _context.AttendancePolicies
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.IsActive)
            .Where(p => p.EffectiveFrom <= calculationDate && (p.EffectiveTo == null || p.EffectiveTo >= calculationDate))
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendancePolicy>> GetAllAttendancePoliciesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _context.AttendancePolicies
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAttendancePolicyAsync(AttendancePolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.AttendancePolicies.AddAsync(policy, cancellationToken);
    }

    public void UpdateAttendancePolicy(AttendancePolicy policy)
    {
        _context.AttendancePolicies.Update(policy);
    }

    public async Task<IReadOnlyList<OvertimeRule>> GetEffectiveOvertimeRulesAsync(long companyId, DateTime calculationDate, CancellationToken cancellationToken = default)
    {
        return await _context.OvertimeRules
            .AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.IsActive)
            .Where(r => r.EffectiveFrom <= calculationDate && (r.EffectiveTo == null || r.EffectiveTo >= calculationDate))
            .OrderBy(r => r.DayType)
            .ToListAsync(cancellationToken);
    }

    public async Task<OvertimeRule?> GetEffectiveOvertimeRuleAsync(long companyId, string dayType, DateTime calculationDate, CancellationToken cancellationToken = default)
    {
        return await _context.OvertimeRules
            .AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.DayType == dayType && r.IsActive)
            .Where(r => r.EffectiveFrom <= calculationDate && (r.EffectiveTo == null || r.EffectiveTo >= calculationDate))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddOvertimeRuleAsync(OvertimeRule rule, CancellationToken cancellationToken = default)
    {
        await _context.OvertimeRules.AddAsync(rule, cancellationToken);
    }

    public void UpdateOvertimeRule(OvertimeRule rule)
    {
        _context.OvertimeRules.Update(rule);
    }

    public async Task<ProrationPolicy?> GetEffectiveProrationPolicyAsync(long companyId, DateTime calculationDate, CancellationToken cancellationToken = default)
    {
        return await _context.ProrationPolicies
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.IsActive)
            .Where(p => p.EffectiveFrom <= calculationDate && (p.EffectiveTo == null || p.EffectiveTo >= calculationDate))
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProrationPolicy>> GetAllProrationPoliciesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _context.ProrationPolicies
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task AddProrationPolicyAsync(ProrationPolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.ProrationPolicies.AddAsync(policy, cancellationToken);
    }

    public void UpdateProrationPolicy(ProrationPolicy policy)
    {
        _context.ProrationPolicies.Update(policy);
    }

    public async Task<DeductionPolicy?> GetEffectiveDeductionPolicyAsync(long companyId, DateTime calculationDate, CancellationToken cancellationToken = default)
    {
        return await _context.DeductionPolicies
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.IsActive)
            .Where(p => p.EffectiveFrom <= calculationDate && (p.EffectiveTo == null || p.EffectiveTo >= calculationDate))
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeductionPolicy>> GetAllDeductionPoliciesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _context.DeductionPolicies
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task AddDeductionPolicyAsync(DeductionPolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.DeductionPolicies.AddAsync(policy, cancellationToken);
    }

    public void UpdateDeductionPolicy(DeductionPolicy policy)
    {
        _context.DeductionPolicies.Update(policy);
    }

    public async Task<TaxPolicy?> GetEffectiveTaxPolicyAsync(long companyId, DateTime calculationDate, CancellationToken cancellationToken = default)
    {
        return await _context.TaxPolicies
            .Include(p => p.Brackets)
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.IsActive)
            .Where(p => p.EffectiveFrom <= calculationDate && (p.EffectiveTo == null || p.EffectiveTo >= calculationDate))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaxPolicy>> GetAllTaxPoliciesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _context.TaxPolicies
            .Include(p => p.Brackets)
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task AddTaxPolicyAsync(TaxPolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.TaxPolicies.AddAsync(policy, cancellationToken);
    }

    public void UpdateTaxPolicy(TaxPolicy policy)
    {
        _context.TaxPolicies.Update(policy);
    }

    public async Task<SSCPolicy?> GetEffectiveSSCPolicyAsync(long companyId, DateTime calculationDate, CancellationToken cancellationToken = default)
    {
        return await _context.SscPolicies
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.IsActive)
            .Where(s => s.EffectiveFrom <= calculationDate && (s.EffectiveTo == null || s.EffectiveTo >= calculationDate))
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SSCPolicy>> GetAllSSCPoliciesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _context.SscPolicies
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task AddSSCPolicyAsync(SSCPolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.SscPolicies.AddAsync(policy, cancellationToken);
    }

    public void UpdateSSCPolicy(SSCPolicy policy)
    {
        _context.SscPolicies.Update(policy);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
