using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.ItemGroups;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvItemGroupService
{
    #region Main Groups CRUD

    Task<ApiResponse<InvMainGroupDto>> CreateMainGroupAsync(CreateMainGroupDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<List<InvMainGroupDto>>> GetMainGroupsAsync(long? branchId = null, CancellationToken ct = default);
    Task<ApiResponse<InvMainGroupDto>> GetMainGroupByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<InvMainGroupDto>> UpdateMainGroupAsync(long id, UpdateMainGroupDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteMainGroupAsync(long id, CancellationToken ct = default);

    #endregion

    #region Sub Groups CRUD

    Task<ApiResponse<InvSubGroupDto>> CreateSubGroupAsync(CreateSubGroupDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<List<InvSubGroupDto>>> GetAllSubGroupsAsync(long? branchId = null, CancellationToken ct = default);
    Task<ApiResponse<List<InvSubGroupDto>>> GetSubGroupsByMainGroupIdAsync(long mainGroupId, CancellationToken ct = default);
    Task<ApiResponse<InvSubGroupDto>> GetSubGroupByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<InvSubGroupDto>> UpdateSubGroupAsync(long id, UpdateSubGroupDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteSubGroupAsync(long id, CancellationToken ct = default);

    #endregion

    #region Generic / Legacy

    Task<ApiResponse<InvItemGroupDto>> CreateGroupAsync(CreateInvItemGroupDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<InvItemGroupDto>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<List<InvItemGroupDto>>> GetSubGroupsAsync(long mainGroupId, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<InvItemGroupDto>>> GetAllPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<InvItemGroupDto>> UpdateGroupAsync(long id, UpdateInvItemGroupDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteGroupAsync(long id, CancellationToken ct = default);

    #endregion
}
