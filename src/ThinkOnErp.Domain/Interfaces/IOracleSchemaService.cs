using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface IOracleSchemaService
{
    Task CreateCompanySchemaAsync(string schemaName, string password);

    Task SeedDefaultAdminAsync(string schemaName, string schemaPassword, string defaultPassword, long companyId, long branchId, string creationUser);

    Task<SysUser?> GetUserByUserNameAsync(string schemaName, string schemaPassword, string userName);

    Task<List<long>> GetUserBranchIdsAsync(string schemaName, string schemaPassword, long userId);

    Task SaveRefreshTokenAsync(string schemaName, string schemaPassword, long userId, string refreshToken, DateTime expiryDate);

    Task<bool> UnlockUserAccountAsync(string schemaName);

    Task GrantUserPrivilegesAsync(string schemaName);

    Task UpgradeExistingTenantSchemasAsync();
    Task ProvisionDeveloperSchemaAsync();
    Task SyncTenantSchemaAsync(string schemaName, string schemaPassword);
    Task<(long BranchId, long FiscalYearId)> ProvisionTenantBranchAndFiscalYearAsync(
        string schemaName,
        string schemaPassword,
        long companyId,
        string? branchNameAr, string? branchNameEn,
        string? branchPhone, string? branchMobile,
        string? branchFax, string? branchEmail,
        string? taxNumber, int defaultLang,
        long? baseCurrencyId, int roundingRules,
        string? branchLogoPath, string creationUser);

    Task UpdateTenantBranchLogoPathAsync(string schemaName, string schemaPassword, long branchId, string? logoPath, string updateUser);
    Task<(string? BranchNameEn, string? BranchNameAr, string? BranchLogoPath)> GetBranchDetailsAsync(string schemaName, long branchId);
    Task SeedDeveloperTemplateAsync();
}
