using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class PostingRuleRepository : IPostingRuleRepository
{
    private readonly OracleDbContext _context;

    public PostingRuleRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<GlPostingRule?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.PostingRules
            .Include(r => r.Branch)
            .Include(r => r.DebitAccount)
            .Include(r => r.CreditAccount)
            .Include(r => r.DefaultCostCenter)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<GlPostingRule?> GetRuleAsync(string module, string eventType, long? branchId, CancellationToken cancellationToken = default)
    {
        // 1. Try branch-specific rule first if branchId is provided
        if (branchId.HasValue && branchId.Value > 0)
        {
            var branchRule = await _context.PostingRules
                .Include(r => r.DebitAccount)
                .Include(r => r.CreditAccount)
                .Include(r => r.DefaultCostCenter)
                .FirstOrDefaultAsync(r => r.Module == module && r.EventType == eventType && r.BranchId == branchId.Value && r.IsActive, cancellationToken);

            if (branchRule != null) return branchRule;
        }

        // 2. Fallback to company-wide default rule (BranchId is null)
        return await _context.PostingRules
            .Include(r => r.DebitAccount)
            .Include(r => r.CreditAccount)
            .Include(r => r.DefaultCostCenter)
            .FirstOrDefaultAsync(r => r.Module == module && r.EventType == eventType && r.BranchId == null && r.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<GlPostingRule>> GetRulesAsync(string? module, long? branchId, CancellationToken cancellationToken = default)
    {
        var query = _context.PostingRules
            .Include(r => r.Branch)
            .Include(r => r.DebitAccount)
            .Include(r => r.CreditAccount)
            .Include(r => r.DefaultCostCenter)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(r => r.Module == module);
        }

        if (branchId.HasValue)
        {
            query = query.Where(r => r.BranchId == branchId.Value || r.BranchId == null);
        }

        return await query.OrderBy(r => r.Module).ThenBy(r => r.EventType).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(GlPostingRule rule, CancellationToken cancellationToken = default)
    {
        await _context.PostingRules.AddAsync(rule, cancellationToken);
    }

    public void Remove(GlPostingRule rule)
    {
        _context.PostingRules.Remove(rule);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
