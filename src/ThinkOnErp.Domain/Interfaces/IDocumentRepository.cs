using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface IDocumentRepository
{
    Task<long> CreateAsync(SysDocument document);
    Task UpdateAsync(SysDocument document);
    Task<bool> SoftDeleteAsync(long id, string updateUser);
    Task<int> BulkSoftDeleteAsync(long[] ids, string updateUser);
    Task<SysDocument?> GetByIdAsync(long id);
    Task<(List<SysDocument> Items, int TotalCount)> GetByOwnerAsync(
        int ownerType, long ownerId, int page, int pageSize,
        string? category = null, string? search = null);
    Task<int> CountByOwnerAsync(int ownerType, long ownerId);
    Task<bool> ExistsAsync(long id);
}
