using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Domain.Interfaces.Pos;

public interface IPosModifierRepository
{
    Task<IReadOnlyList<PosModifierGroup>> GetGroupsByBranchAsync(long branchId, bool? isActive = null, CancellationToken ct = default);
    Task<PosModifierGroup?> GetGroupByIdAsync(long id, CancellationToken ct = default);
    Task<PosModifierGroup?> GetGroupByCodeAsync(long branchId, string code, CancellationToken ct = default);
    Task AddGroupAsync(PosModifierGroup group, CancellationToken ct = default);
    Task UpdateGroupAsync(PosModifierGroup group, CancellationToken ct = default);
    Task DeleteGroupAsync(PosModifierGroup group, CancellationToken ct = default);

    Task<PosModifierOption?> GetOptionByIdAsync(long id, CancellationToken ct = default);
    Task AddOptionAsync(PosModifierOption option, CancellationToken ct = default);
    Task UpdateOptionAsync(PosModifierOption option, CancellationToken ct = default);
    Task DeleteOptionAsync(PosModifierOption option, CancellationToken ct = default);

    Task<IReadOnlyList<PosModifierGroup>> GetGroupsByItemIdAsync(long itemId, CancellationToken ct = default);
    Task<PosItemModifierGroup?> GetItemModifierGroupAsync(long itemId, long groupId, CancellationToken ct = default);
    Task AddItemModifierGroupAsync(PosItemModifierGroup itemGroup, CancellationToken ct = default);
    Task DeleteItemModifierGroupAsync(PosItemModifierGroup itemGroup, CancellationToken ct = default);
    Task ClearItemModifierGroupsAsync(long itemId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
