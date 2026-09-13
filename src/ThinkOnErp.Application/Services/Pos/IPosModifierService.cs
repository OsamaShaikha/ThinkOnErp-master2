using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosModifierService
{
    Task<ApiResponse<IReadOnlyList<PosModifierGroupDto>>> GetGroupsByBranchAsync(long branchId, bool? isActive = null, CancellationToken ct = default);
    Task<ApiResponse<PosModifierGroupDto>> GetGroupByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<PosModifierGroupDto>> CreateGroupAsync(CreatePosModifierGroupDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PosModifierGroupDto>> UpdateGroupAsync(long id, UpdatePosModifierGroupDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteGroupAsync(long id, CancellationToken ct = default);

    Task<ApiResponse<PosModifierOptionDto>> UpsertOptionAsync(long groupId, UpsertPosModifierOptionDto dto, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteOptionAsync(long groupId, long optionId, CancellationToken ct = default);

    Task<ApiResponse<ItemModifierGroupsDto>> GetGroupsByItemIdAsync(long itemId, CancellationToken ct = default);
    Task<ApiResponse<bool>> AssignGroupsToItemAsync(long itemId, List<long> groupIds, CancellationToken ct = default);
    Task<ApiResponse<bool>> RemoveGroupFromItemAsync(long itemId, long groupId, CancellationToken ct = default);
}
