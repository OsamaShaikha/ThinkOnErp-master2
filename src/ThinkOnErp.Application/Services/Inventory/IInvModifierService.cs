using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Modifiers;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvModifierService
{
    Task<ApiResponse<IReadOnlyList<InvModifierGroupDto>>> GetGroupsByBranchAsync(long branchId, bool? isActive = null, CancellationToken ct = default);
    Task<ApiResponse<InvModifierGroupDto>> GetGroupByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<InvModifierGroupDto>> CreateGroupAsync(CreateInvModifierGroupDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<InvModifierGroupDto>> UpdateGroupAsync(long id, UpdateInvModifierGroupDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteGroupAsync(long id, CancellationToken ct = default);

    Task<ApiResponse<InvModifierOptionDto>> UpsertOptionAsync(long groupId, UpsertInvModifierOptionDto dto, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteOptionAsync(long groupId, long optionId, CancellationToken ct = default);

    Task<ApiResponse<ItemModifierGroupsDto>> GetGroupsByItemIdAsync(long itemId, CancellationToken ct = default);
    Task<ApiResponse<bool>> AssignGroupsToItemAsync(long itemId, List<long> groupIds, CancellationToken ct = default);
    Task<ApiResponse<bool>> RemoveGroupFromItemAsync(long itemId, long groupId, CancellationToken ct = default);
}
