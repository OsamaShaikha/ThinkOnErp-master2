using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Swagger;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.ItemCategories;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Inventory;

namespace ThinkOnErp.API.Controllers.Pos;

/// <summary>
/// Point of Sale Categories API: Retrieves categories configured strictly for POS visibility (ShowInPos = true).
/// </summary>
[ApiController]
[Route("api/pos/categories")]
[ApiExplorerSettings(GroupName = ApiCategories.Pos)]
[TenantScoped]
[Authorize]
public class PosCategoriesController : ControllerBase
{
    private readonly IInvItemCategoryService _categoryService;

    public PosCategoriesController(IInvItemCategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// Retrieves category tree or flat list strictly visible in POS (ShowInPos = true, IsActive = true).
    /// </summary>
    /// <param name="branchId">Optional branch filter.</param>
    /// <param name="asTree">Whether to return hierarchical tree with children (default true) or flat list.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>POS categories with images, color codes, and child categories.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<PosCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPosCategories(
        [FromQuery] long? branchId = null,
        [FromQuery] bool asTree = true,
        CancellationToken ct = default)
    {
        if (asTree)
        {
            var treeResult = await _categoryService.GetTreeAsync(branchId, posOnly: true, ct);
            if (!treeResult.Success || treeResult.Data == null)
                return StatusCode(treeResult.StatusCode, treeResult);

            var posTree = MapToPosCategoryDtos(treeResult.Data);
            return Ok(ApiResponse<List<PosCategoryDto>>.CreateSuccess(posTree, "POS category tree retrieved successfully"));
        }

        var pagedResult = await _categoryService.GetAllPagedAsync(branchId, 1, 1000, ct);
        if (pagedResult.Success && pagedResult.Data != null)
        {
            var posOnlyList = pagedResult.Data.Items
                .Where(c => c.ShowInPos && c.IsActive)
                .Select(c => new PosCategoryDto
                {
                    Id = c.Id,
                    ParentCategoryId = c.ParentCategoryId,
                    ParentCategoryName = c.ParentCategoryName,
                    CategoryCode = c.CategoryCode,
                    CategoryNameLocal = c.CategoryNameLocal,
                    CategoryNameEn = c.CategoryNameEn,
                    CategoryLevel = c.CategoryLevel,
                    ImageBase64 = c.ImageBase64,
                    ColorCode = c.ColorCode,
                    ItemsCount = c.ItemsCount
                }).ToList();

            return Ok(ApiResponse<List<PosCategoryDto>>.CreateSuccess(posOnlyList, "POS categories retrieved successfully"));
        }

        return StatusCode(pagedResult.StatusCode, pagedResult);
    }

    /// <summary>
    /// Retrieves a specific POS category by its ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PosCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PosCategoryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] long id, CancellationToken ct)
    {
        var result = await _categoryService.GetByIdAsync(id, ct);
        if (!result.Success || result.Data == null || !result.Data.ShowInPos || !result.Data.IsActive)
        {
            return NotFound(ApiResponse<PosCategoryDto>.CreateFailure("Category not found or not enabled for POS.", null, 404));
        }

        var cat = result.Data;
        var posDto = new PosCategoryDto
        {
            Id = cat.Id,
            ParentCategoryId = cat.ParentCategoryId,
            ParentCategoryName = cat.ParentCategoryName,
            CategoryCode = cat.CategoryCode,
            CategoryNameLocal = cat.CategoryNameLocal,
            CategoryNameEn = cat.CategoryNameEn,
            CategoryLevel = cat.CategoryLevel,
            ImageBase64 = cat.ImageBase64,
            ColorCode = cat.ColorCode,
            ItemsCount = cat.ItemsCount
        };

        return Ok(ApiResponse<PosCategoryDto>.CreateSuccess(posDto, "POS category retrieved successfully"));
    }

    private static List<PosCategoryDto> MapToPosCategoryDtos(List<InvCategoryTreeNodeDto> treeNodes)
    {
        return treeNodes.Select(node => new PosCategoryDto
        {
            Id = node.Id,
            ParentCategoryId = node.ParentCategoryId,
            ParentCategoryName = node.ParentCategoryName,
            CategoryCode = node.CategoryCode,
            CategoryNameLocal = node.CategoryNameLocal,
            CategoryNameEn = node.CategoryNameEn,
            CategoryLevel = node.CategoryLevel,
            ImageBase64 = node.ImageBase64,
            ColorCode = node.ColorCode,
            ItemsCount = node.ItemsCount,
            Children = node.Children != null ? MapToPosCategoryDtos(node.Children) : new()
        }).ToList();
    }
}
