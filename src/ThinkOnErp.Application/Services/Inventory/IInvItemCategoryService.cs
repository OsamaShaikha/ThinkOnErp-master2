using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.ItemCategories;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvItemCategoryService
{
    #region Category CRUD & Tree

    Task<ApiResponse<InvItemCategoryDto>> CreateCategoryAsync(CreateInvItemCategoryDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<InvItemCategoryDto>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<List<InvItemCategoryDto>>> GetSubCategoriesAsync(long mainCategoryId, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<InvItemCategoryDto>>> GetAllPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<InvItemCategoryDto>> UpdateCategoryAsync(long id, UpdateInvItemCategoryDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteCategoryAsync(long id, CancellationToken ct = default);

    Task<ApiResponse<List<InvCategoryTreeNodeDto>>> GetTreeAsync(long? branchId = null, bool? posOnly = null, CancellationToken ct = default);
    Task<ApiResponse<List<InvCategoryTreeNodeDto>>> GetPosCategoriesAsync(long? branchId = null, CancellationToken ct = default);
    Task<ApiResponse<List<InvCategoryTreeNodeDto>>> GetChildrenAsync(long parentCategoryId, CancellationToken ct = default);

    #endregion

    #region Main Categories CRUD

    Task<ApiResponse<InvMainCategoryDto>> CreateMainCategoryAsync(CreateMainCategoryDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<List<InvMainCategoryDto>>> GetMainCategoriesAsync(long? branchId = null, CancellationToken ct = default);
    Task<ApiResponse<InvMainCategoryDto>> GetMainCategoryByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<InvMainCategoryDto>> UpdateMainCategoryAsync(long id, UpdateMainCategoryDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteMainCategoryAsync(long id, CancellationToken ct = default);

    #endregion

    #region Sub Categories CRUD

    Task<ApiResponse<InvSubCategoryDto>> CreateSubCategoryAsync(CreateSubCategoryDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<List<InvSubCategoryDto>>> GetAllSubCategoriesAsync(long? branchId = null, CancellationToken ct = default);
    Task<ApiResponse<List<InvSubCategoryDto>>> GetSubCategoriesByMainCategoryIdAsync(long mainCategoryId, CancellationToken ct = default);
    Task<ApiResponse<InvSubCategoryDto>> GetSubCategoryByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<InvSubCategoryDto>> UpdateSubCategoryAsync(long id, UpdateSubCategoryDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteSubCategoryAsync(long id, CancellationToken ct = default);

    #endregion
}
