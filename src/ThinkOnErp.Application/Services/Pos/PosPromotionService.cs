using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosPromotionService
{
    Task<ApiResponse<PagedResultDto<PromotionSummaryDto>>> GetPromotionsPagedAsync(
        long branchId,
        PosPromotionType? promotionType = null,
        bool? isActive = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task<ApiResponse<PromotionDetailsDto>> GetPromotionByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<PromotionDetailsDto>> CreatePromotionAsync(CreatePromotionDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PromotionDetailsDto>> UpdatePromotionAsync(long id, UpdatePromotionDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeletePromotionAsync(long id, CancellationToken ct = default);
}

public class PosPromotionService : IPosPromotionService
{
    private readonly IPosPromotionRepository _repository;

    public PosPromotionService(IPosPromotionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<PagedResultDto<PromotionSummaryDto>>> GetPromotionsPagedAsync(
        long branchId,
        PosPromotionType? promotionType = null,
        bool? isActive = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _repository.GetPromotionsPagedAsync(
            branchId, promotionType, isActive, pageIndex, pageSize, ct);

        var dtos = items.Select(p => new PromotionSummaryDto
        {
            Id = p.Id,
            BranchId = p.BranchId,
            PromotionCode = p.PromotionCode,
            PromotionNameLocal = p.PromotionNameLocal,
            PromotionNameEn = p.PromotionNameEn,
            PromotionType = p.PromotionType,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            IsActive = p.IsActive,
            Priority = p.Priority,
            RuleCount = p.Rules?.Count ?? 0
        }).ToList();

        var paged = new PagedResultDto<PromotionSummaryDto>(dtos, totalCount, pageIndex, pageSize);
        return ApiResponse<PagedResultDto<PromotionSummaryDto>>.CreateSuccess(paged);
    }

    public async Task<ApiResponse<PromotionDetailsDto>> GetPromotionByIdAsync(long id, CancellationToken ct = default)
    {
        var p = await _repository.GetPromotionByIdAsync(id, ct);
        if (p == null)
            return ApiResponse<PromotionDetailsDto>.CreateFailure("Promotion not found", null, 404);

        return ApiResponse<PromotionDetailsDto>.CreateSuccess(MapToDetailsDto(p));
    }

    public async Task<ApiResponse<PromotionDetailsDto>> CreatePromotionAsync(CreatePromotionDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<PromotionDetailsDto>.CreateFailure("Request body cannot be null", null, 400);

        if (string.IsNullOrWhiteSpace(dto.PromotionCode) || string.IsNullOrWhiteSpace(dto.PromotionNameLocal))
            return ApiResponse<PromotionDetailsDto>.CreateFailure("Promotion code and name are required", null, 400);

        if (dto.BranchId <= 0)
            return ApiResponse<PromotionDetailsDto>.CreateFailure("Valid BranchId is required", null, 400);

        var promo = new PosPromotion
        {
            BranchId = dto.BranchId,
            PromotionCode = dto.PromotionCode.Trim().ToUpper(),
            PromotionNameLocal = dto.PromotionNameLocal.Trim(),
            PromotionNameEn = dto.PromotionNameEn?.Trim(),
            PromotionType = dto.PromotionType,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            DaysOfWeekMask = dto.DaysOfWeekMask,
            Priority = dto.Priority,
            CanCombineWithOtherDiscounts = dto.CanCombineWithOtherDiscounts,
            MinimumCartAmount = dto.MinimumCartAmount,
            IsActive = true,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        if (dto.Rules != null)
        {
            foreach (var r in dto.Rules)
            {
                promo.Rules.Add(new PosPromotionRule
                {
                    RequiredGroupId = r.RequiredGroupId,
                    RequiredItemId = r.RequiredItemId,
                    RequiredQuantity = r.RequiredQuantity,
                    RewardItemId = r.RewardItemId,
                    RewardGroupId = r.RewardGroupId,
                    RewardQuantity = r.RewardQuantity,
                    DiscountPercent = r.DiscountPercent,
                    FixedBundlePrice = r.FixedBundlePrice
                });
            }
        }

        await _repository.AddPromotionAsync(promo, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<PromotionDetailsDto>.CreateSuccess(MapToDetailsDto(promo), "Promotion created successfully");
    }

    public async Task<ApiResponse<PromotionDetailsDto>> UpdatePromotionAsync(long id, UpdatePromotionDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<PromotionDetailsDto>.CreateFailure("Request body cannot be null", null, 400);

        var promo = await _repository.GetPromotionByIdAsync(id, ct);
        if (promo == null)
            return ApiResponse<PromotionDetailsDto>.CreateFailure("Promotion not found", null, 404);

        if (!string.IsNullOrWhiteSpace(dto.PromotionNameLocal))
            promo.PromotionNameLocal = dto.PromotionNameLocal.Trim();
        if (dto.PromotionNameEn != null)
            promo.PromotionNameEn = dto.PromotionNameEn.Trim();
        promo.PromotionType = dto.PromotionType;
        promo.StartDate = dto.StartDate;
        promo.EndDate = dto.EndDate;
        promo.StartTime = dto.StartTime;
        promo.EndTime = dto.EndTime;
        promo.DaysOfWeekMask = dto.DaysOfWeekMask;
        promo.Priority = dto.Priority;
        promo.CanCombineWithOtherDiscounts = dto.CanCombineWithOtherDiscounts;
        promo.MinimumCartAmount = dto.MinimumCartAmount;
        promo.IsActive = dto.IsActive;
        promo.UpdateUser = username;
        promo.UpdateDate = DateTime.UtcNow;

        if (dto.Rules != null)
        {
            promo.Rules.Clear();
            foreach (var r in dto.Rules)
            {
                promo.Rules.Add(new PosPromotionRule
                {
                    PromotionId = promo.Id,
                    RequiredGroupId = r.RequiredGroupId,
                    RequiredItemId = r.RequiredItemId,
                    RequiredQuantity = r.RequiredQuantity,
                    RewardItemId = r.RewardItemId,
                    RewardGroupId = r.RewardGroupId,
                    RewardQuantity = r.RewardQuantity,
                    DiscountPercent = r.DiscountPercent,
                    FixedBundlePrice = r.FixedBundlePrice
                });
            }
        }

        await _repository.UpdatePromotionAsync(promo, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<PromotionDetailsDto>.CreateSuccess(MapToDetailsDto(promo), "Promotion updated successfully");
    }

    public async Task<ApiResponse<bool>> DeletePromotionAsync(long id, CancellationToken ct = default)
    {
        var promo = await _repository.GetPromotionByIdAsync(id, ct);
        if (promo == null)
            return ApiResponse<bool>.CreateFailure("Promotion not found", null, 404);

        promo.IsActive = false;
        await _repository.UpdatePromotionAsync(promo, ct);
        await _repository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true, "Promotion deactivated successfully");
    }

    private static PromotionDetailsDto MapToDetailsDto(PosPromotion p)
    {
        return new PromotionDetailsDto
        {
            Id = p.Id,
            BranchId = p.BranchId,
            PromotionCode = p.PromotionCode,
            PromotionNameLocal = p.PromotionNameLocal,
            PromotionNameEn = p.PromotionNameEn,
            PromotionType = p.PromotionType,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            StartTime = p.StartTime,
            EndTime = p.EndTime,
            DaysOfWeekMask = p.DaysOfWeekMask,
            Priority = p.Priority,
            CanCombineWithOtherDiscounts = p.CanCombineWithOtherDiscounts,
            MinimumCartAmount = p.MinimumCartAmount,
            IsActive = p.IsActive,
            RuleCount = p.Rules?.Count ?? 0,
            Rules = p.Rules?.Select(r => new PromotionRuleDto
            {
                Id = r.Id,
                PromotionId = r.PromotionId,
                RequiredGroupId = r.RequiredGroupId,
                RequiredItemId = r.RequiredItemId,
                RequiredQuantity = r.RequiredQuantity,
                RewardItemId = r.RewardItemId,
                RewardGroupId = r.RewardGroupId,
                RewardQuantity = r.RewardQuantity,
                DiscountPercent = r.DiscountPercent,
                FixedBundlePrice = r.FixedBundlePrice
            }).ToList() ?? new List<PromotionRuleDto>()
        };
    }
}
