using ThinkOnErp.Application.DTOs.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IGlAccountService
{
    Task<IReadOnlyList<GlAccountTreeDto>> GetTreeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlAccountCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<GlAccountDto> GetAccountByCodeAsync(string accountCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlAccountDto>> GetPostableAccountsAsync(long branchId, CancellationToken cancellationToken = default);
    Task<string> GetNextChildCodeAsync(string parentAccountCode, CancellationToken cancellationToken = default);
    Task<GlAccountDto> CreateAccountAsync(CreateGlAccountDto request, CancellationToken cancellationToken = default);
    Task<GlAccountDto> UpdateAccountAsync(string accountCode, UpdateGlAccountDto request, CancellationToken cancellationToken = default);
    Task UpdateAccountStatusAsync(string accountCode, bool isActive, CancellationToken cancellationToken = default);
    Task<GlAccountDto> DeleteAccountAsync(string accountCode, CancellationToken cancellationToken = default);

    // COA Level Digit Structure Configuration
    Task<IReadOnlyList<GlAccountStructureConfigDto>> GetStructureConfigsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlAccountStructureConfigDto>> UpdateStructureConfigsAsync(IEnumerable<UpdateGlAccountStructureConfigDto> request, CancellationToken cancellationToken = default);

    // Default COA Generation based on Level Digit Configurations
    Task<IReadOnlyList<GlAccountTreeDto>> GenerateDefaultTreePreviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlAccountDto>> SeedDefaultTreeAsync(long defaultBranchId, CancellationToken cancellationToken = default);
}
