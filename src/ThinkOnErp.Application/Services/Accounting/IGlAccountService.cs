using ThinkOnErp.Application.DTOs.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IGlAccountService
{
    Task<IReadOnlyList<GlAccountTreeDto>> GetTreeAsync(CancellationToken cancellationToken = default);
    Task<GlAccountDto> GetAccountAsync(long accountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlAccountDto>> GetPostableAccountsAsync(long branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<GlAccountDto> CreateAccountAsync(CreateGlAccountDto request, CancellationToken cancellationToken = default);
    Task<GlAccountDto> UpdateAccountAsync(long accountId, UpdateGlAccountDto request, CancellationToken cancellationToken = default);
    Task UpdateAccountStatusAsync(long accountId, bool isActive, CancellationToken cancellationToken = default);
    Task<GlAccountDto> DeleteAccountAsync(long accountId, CancellationToken cancellationToken = default);
}
