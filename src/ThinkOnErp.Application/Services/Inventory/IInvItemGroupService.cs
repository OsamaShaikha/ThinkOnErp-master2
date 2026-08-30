using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.ItemGroups;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvItemGroupService
{
    Task<ApiResponse<InvItemGroupDto>> CreateGroupAsync(CreateInvItemGroupDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<InvItemGroupDto>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<List<InvItemGroupDto>>> GetMainGroupsAsync(long? branchId = null, CancellationToken ct = default);
    Task<ApiResponse<List<InvItemGroupDto>>> GetSubGroupsAsync(long mainGroupId, CancellationToken ct = default);
    Task<ApiResponse<(List<InvItemGroupDto> Items, int TotalCount)>> GetAllPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<InvItemGroupDto>> UpdateGroupAsync(long id, UpdateInvItemGroupDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteGroupAsync(long id, CancellationToken ct = default);
}
