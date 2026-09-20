using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvModifierRepository
{
    Task<IReadOnlyList<InvModifierGroup>> GetGroupsByBranchAsync(long branchId, bool? isActive = null, CancellationToken ct = default);
    Task<InvModifierGroup?> GetGroupByIdAsync(long id, CancellationToken ct = default);
    Task<InvModifierGroup?> GetGroupByCodeAsync(long branchId, string code, CancellationToken ct = default);
    Task AddGroupAsync(InvModifierGroup group, CancellationToken ct = default);
    Task UpdateGroupAsync(InvModifierGroup group, CancellationToken ct = default);
    Task DeleteGroupAsync(InvModifierGroup group, CancellationToken ct = default);

    Task<InvModifierOption?> GetOptionByIdAsync(long id, CancellationToken ct = default);
    Task AddOptionAsync(InvModifierOption option, CancellationToken ct = default);
    Task UpdateOptionAsync(InvModifierOption option, CancellationToken ct = default);
    Task DeleteOptionAsync(InvModifierOption option, CancellationToken ct = default);

    Task<IReadOnlyList<InvModifierGroup>> GetGroupsByItemIdAsync(long itemId, CancellationToken ct = default);
    Task<InvItemModifierGroup?> GetItemModifierGroupAsync(long itemId, long groupId, CancellationToken ct = default);
    Task AddItemModifierGroupAsync(InvItemModifierGroup itemGroup, CancellationToken ct = default);
    Task DeleteItemModifierGroupAsync(InvItemModifierGroup itemGroup, CancellationToken ct = default);
    Task ClearItemModifierGroupsAsync(long itemId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
