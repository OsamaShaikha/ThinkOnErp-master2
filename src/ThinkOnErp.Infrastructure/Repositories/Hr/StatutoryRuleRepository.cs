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

public sealed class StatutoryRuleRepository : IStatutoryRuleRepository
{
    private readonly OracleDbContext _context;

    public StatutoryRuleRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<StatutoryRule>> GetAllAsync(string? ruleType = null, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.StatutoryRules.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(r => r.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(ruleType))
        {
            query = query.Where(r => r.RuleType == ruleType);
        }

        return await query
            .OrderBy(r => r.RuleType)
            .ThenByDescending(r => r.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task<StatutoryRule?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.StatutoryRules.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<StatutoryRule?> GetEffectiveRuleAsync(string ruleType, DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        return await _context.StatutoryRules
            .AsNoTracking()
            .Where(r => r.RuleType == ruleType && r.IsActive)
            .Where(r => r.EffectiveFrom <= effectiveDate && (r.EffectiveTo == null || r.EffectiveTo >= effectiveDate))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StatutoryRule>> GetEffectiveRulesByTypeAsync(string ruleType, DateTime effectiveDate, CancellationToken cancellationToken = default)
    {
        return await _context.StatutoryRules
            .AsNoTracking()
            .Where(r => r.RuleType == ruleType && r.IsActive)
            .Where(r => r.EffectiveFrom <= effectiveDate && (r.EffectiveTo == null || r.EffectiveTo >= effectiveDate))
            .OrderBy(r => r.BracketLow ?? 0)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(StatutoryRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        await _context.StatutoryRules.AddAsync(rule, cancellationToken);
    }

    public void Update(StatutoryRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _context.StatutoryRules.Update(rule);
    }

    public void Remove(StatutoryRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _context.StatutoryRules.Remove(rule);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
