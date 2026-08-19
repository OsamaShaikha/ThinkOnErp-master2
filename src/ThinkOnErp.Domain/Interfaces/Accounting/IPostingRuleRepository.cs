using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface IPostingRuleRepository
{
    Task<GlPostingRule?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<GlPostingRule?> GetRuleAsync(string module, string eventType, long? branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlPostingRule>> GetRulesAsync(string? module, long? branchId, CancellationToken cancellationToken = default);
    Task AddAsync(GlPostingRule rule, CancellationToken cancellationToken = default);
    void Remove(GlPostingRule rule);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
