using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IStatutoryRuleRepository
{
    Task<IReadOnlyList<StatutoryRule>> GetAllAsync(string? ruleType = null, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<StatutoryRule?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<StatutoryRule?> GetEffectiveRuleAsync(string ruleType, DateTime effectiveDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatutoryRule>> GetEffectiveRulesByTypeAsync(string ruleType, DateTime effectiveDate, CancellationToken cancellationToken = default);
    Task AddAsync(StatutoryRule rule, CancellationToken cancellationToken = default);
    void Update(StatutoryRule rule);
    void Remove(StatutoryRule rule);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
