using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface IOracleSchemaService
{
    Task CreateCompanySchemaAsync(string schemaName, string password);

    Task SeedDefaultAdminAsync(string schemaName, string schemaPassword, string defaultPassword, long companyId, long branchId, string creationUser);

    Task<SysUser?> GetUserByUserNameAsync(string schemaName, string schemaPassword, string userName);

    Task SaveRefreshTokenAsync(string schemaName, string schemaPassword, long userId, string refreshToken, DateTime expiryDate);

    Task<bool> UnlockUserAccountAsync(string schemaName);

    Task GrantUserPrivilegesAsync(string schemaName);
}
