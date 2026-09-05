using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvItemGroupRepository
{
    Task<InvItemGroup?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<InvItemGroup?> GetByCodeAsync(long code, CancellationToken ct = default);
    Task<List<InvItemGroup>> GetMainGroupsAsync(long? branchId = null, CancellationToken ct = default);
    Task<List<InvItemGroup>> GetSubGroupsAsync(long mainGroupId, CancellationToken ct = default);
    Task<List<InvItemGroup>> GetAllSubGroupsAsync(long? branchId = null, CancellationToken ct = default);
    Task<(List<InvItemGroup> Items, int TotalCount)> GetAllPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default);
    Task<InvItemGroup> CreateAsync(InvItemGroup group, CancellationToken ct = default);
    Task UpdateAsync(InvItemGroup group, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task HardDeleteAsync(long id, CancellationToken ct = default);
    Task<bool> ExistsAsync(long code, long? excludeId = null, CancellationToken ct = default);
    Task<bool> HasSubGroupsAsync(long mainGroupId, CancellationToken ct = default);
    Task<bool> HasItemsAsync(long groupId, bool isMainGroup, CancellationToken ct = default);
    Task<int> GetItemsCountAsync(long groupId, bool isMainGroup, CancellationToken ct = default);
}
