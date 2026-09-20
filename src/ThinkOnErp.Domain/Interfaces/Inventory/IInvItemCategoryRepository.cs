using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvItemCategoryRepository
{
    Task<InvItemCategory?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<InvItemCategory?> GetByCodeAsync(long code, CancellationToken ct = default);
    Task<List<InvItemCategory>> GetMainCategoriesAsync(long? branchId = null, CancellationToken ct = default);
    Task<List<InvItemCategory>> GetSubCategoriesAsync(long mainCategoryId, CancellationToken ct = default);
    Task<List<InvItemCategory>> GetAllSubCategoriesAsync(long? branchId = null, CancellationToken ct = default);
    Task<(List<InvItemCategory> Items, int TotalCount)> GetAllPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default);
    Task<InvItemCategory> CreateAsync(InvItemCategory category, CancellationToken ct = default);
    Task UpdateAsync(InvItemCategory category, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task HardDeleteAsync(long id, CancellationToken ct = default);
    Task<bool> ExistsAsync(long code, long? excludeId = null, CancellationToken ct = default);
    Task<bool> HasSubCategoriesAsync(long mainCategoryId, CancellationToken ct = default);
    Task<bool> HasItemsAsync(long categoryId, bool isMainCategory, CancellationToken ct = default);
    Task<int> GetItemsCountAsync(long categoryId, bool isMainCategory, CancellationToken ct = default);
    Task<int> GetCategoryItemsCountAsync(long categoryId, CancellationToken ct = default);
    Task<List<InvItemCategory>> GetAllActiveCategoriesAsync(long? branchId = null, bool? posOnly = null, CancellationToken ct = default);
    Task<List<InvItemCategory>> GetChildrenAsync(long parentCategoryId, CancellationToken ct = default);
    Task<bool> IsDescendantOfAsync(long potentialChildId, long ancestorId, CancellationToken ct = default);
}
