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
        _context = context;
    }

    public async Task<TaxPolicy?> GetActiveTaxPolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        return await _context.TaxPolicies
            .Include(t => t.Brackets)
            .Where(t => t.CompanyId == companyId && t.IsActive && t.EffectiveFrom <= effectiveDate && (t.EffectiveTo == null || t.EffectiveTo >= effectiveDate))
            .OrderByDescending(t => t.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaxPolicy>> GetTaxPoliciesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _context.TaxPolicies
            .Include(t => t.Brackets)
            .Where(t => t.CompanyId == companyId)
            .OrderByDescending(t => t.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task AddTaxPolicyAsync(TaxPolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.TaxPolicies.AddAsync(policy, cancellationToken);
    }

    public async Task<SSCPolicy?> GetActiveSSCPolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        return await _context.SSCPolicies
            .Where(s => s.CompanyId == companyId && s.IsActive && s.EffectiveFrom <= effectiveDate && (s.EffectiveTo == null || s.EffectiveTo >= effectiveDate))
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SSCPolicy>> GetSSCPoliciesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _context.SSCPolicies
            .Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task AddSSCPolicyAsync(SSCPolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.SSCPolicies.AddAsync(policy, cancellationToken);
    }

    public async Task<ProrationPolicy?> GetActiveProrationPolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        return await _context.ProrationPolicies
            .Where(p => p.CompanyId == companyId && p.IsActive && p.EffectiveFrom <= effectiveDate && (p.EffectiveTo == null || p.EffectiveTo >= effectiveDate))
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProrationPolicy>> GetProrationPoliciesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _context.ProrationPolicies
            .Where(p => p.CompanyId == companyId)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task AddProrationPolicyAsync(ProrationPolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.ProrationPolicies.AddAsync(policy, cancellationToken);
    }

    public async Task<DeductionPolicy?> GetActiveDeductionPolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        return await _context.DeductionPolicies
            .Where(d => d.CompanyId == companyId && d.IsActive && d.EffectiveFrom <= effectiveDate && (d.EffectiveTo == null || d.EffectiveTo >= effectiveDate))
            .OrderByDescending(d => d.IsDefault)
            .ThenByDescending(d => d.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeductionPolicy>> GetDeductionPoliciesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _context.DeductionPolicies
            .Where(d => d.CompanyId == companyId)
            .OrderByDescending(d => d.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task AddDeductionPolicyAsync(DeductionPolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.DeductionPolicies.AddAsync(policy, cancellationToken);
    }

    public async Task<IReadOnlyList<OvertimeRule>> GetActiveOvertimeRulesAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        return await _context.OvertimeRules
            .Where(r => r.CompanyId == companyId && r.IsActive && r.EffectiveFrom <= effectiveDate && (r.EffectiveTo == null || r.EffectiveTo >= effectiveDate))
            .ToListAsync(cancellationToken);
    }

    public async Task AddOvertimeRuleAsync(OvertimeRule rule, CancellationToken cancellationToken = default)
    {
        await _context.OvertimeRules.AddAsync(rule, cancellationToken);
    }

    public async Task<AttendancePolicy?> GetActiveAttendancePolicyAsync(long companyId, DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        return await _context.AttendancePolicies
            .Where(p => p.CompanyId == companyId && p.IsActive && p.EffectiveFrom <= effectiveDate && (p.EffectiveTo == null || p.EffectiveTo >= effectiveDate))
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendancePolicy>> GetAttendancePoliciesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _context.AttendancePolicies
            .Where(p => p.CompanyId == companyId)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAttendancePolicyAsync(AttendancePolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.AttendancePolicies.AddAsync(policy, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
