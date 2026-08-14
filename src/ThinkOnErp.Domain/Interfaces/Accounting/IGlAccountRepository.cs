using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface IGlAccountRepository
{
    Task<IReadOnlyList<GlAccount>> GetAllAsync(long companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlAccount>> GetPostableAsync(long companyId, long branchId, CancellationToken cancellationToken = default);
    Task<GlAccount?> GetByCodeAsync(long companyId, string accountCode, CancellationToken cancellationToken = default);
    Task<bool> AccountCodeExistsAsync(long companyId, string accountCode, CancellationToken cancellationToken = default);
    Task<bool> HasChildrenAsync(long companyId, string accountCode, CancellationToken cancellationToken = default);
    Task<bool> HasAccountsAsync(long companyId, CancellationToken cancellationToken = default);
    Task<string> GetNextChildCodeAsync(long companyId, string parentAccountCode, CancellationToken cancellationToken = default);
    Task<bool> BranchBelongsToCompanyAsync(long companyId, long branchId, CancellationToken cancellationToken = default);
    Task AddAsync(GlAccount account, CancellationToken cancellationToken = default);
    Task DeleteAsync(GlAccount account, CancellationToken cancellationToken = default);
    Task ImportAsync(IReadOnlyCollection<GlAccount> accounts, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // COA Level Structure Configuration Methods
    Task<IReadOnlyList<GlAccountStructureConfig>> GetStructureConfigsAsync(CancellationToken cancellationToken = default);
    Task SaveStructureConfigsAsync(IEnumerable<GlAccountStructureConfig> configs, CancellationToken cancellationToken = default);
}
