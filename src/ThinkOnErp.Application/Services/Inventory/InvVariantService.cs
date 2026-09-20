using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Variants;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class InvVariantService : IInvVariantService
{
    private readonly IInvVariantRepository _variantRepository;
    private readonly IInvItemRepository _itemRepository;
    private readonly ILogger<InvVariantService> _logger;

    public InvVariantService(
        IInvVariantRepository variantRepository,
        IInvItemRepository itemRepository,
        ILogger<InvVariantService> logger)
    {
        _variantRepository = variantRepository;
        _itemRepository = itemRepository;
        _logger = logger;
    }

    public async Task<ApiResponse<List<InvAttributeDto>>> GetAttributesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var attributes = await _variantRepository.GetAllAttributesAsync(activeOnly, cancellationToken);
        var dtos = attributes.Select(MapAttributeToDto).ToList();
        return ApiResponse<List<InvAttributeDto>>.CreateSuccess(dtos, "تم استرجاع السمات بنجاح");
    }

    public async Task<ApiResponse<InvAttributeDto>> CreateAttributeAsync(CreateAttributeDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AttributeCode))
            return ApiResponse<InvAttributeDto>.CreateFailure("كود السمة مطلوب", statusCode: 400);

        if (string.IsNullOrWhiteSpace(request.NameLocal))
            return ApiResponse<InvAttributeDto>.CreateFailure("اسم السمة مطلوب", statusCode: 400);

        var existing = await _variantRepository.GetAttributeByCodeAsync(request.AttributeCode.Trim(), cancellationToken);
        if (existing != null)
            return ApiResponse<InvAttributeDto>.CreateFailure($"كود السمة '{request.AttributeCode}' مسجل مسبقاً", statusCode: 400);

        var attribute = new InvItemAttribute
        {
            BranchId = 1,
            AttributeCode = request.AttributeCode.Trim().ToUpperInvariant(),
            NameLocal = request.NameLocal.Trim(),
            NameEn = request.NameEn?.Trim(),
            CreationUser = "SYSTEM",
            CreationDate = DateTime.UtcNow
        };

        if (request.Values != null && request.Values.Any())
        {
            foreach (var val in request.Values)
            {
                attribute.Values.Add(new InvItemAttributeValue
                {
                    ValueCode = val.ValueCode.Trim().ToUpperInvariant(),
                    ValueLocal = val.ValueLocal.Trim(),
                    ValueEn = val.ValueEn?.Trim(),
                    ColorHex = val.ColorHex?.Trim(),
                    SortOrder = val.SortOrder,
                    IsActive = true
                });
            }
        }

        await _variantRepository.AddAttributeAsync(attribute, cancellationToken);
        await _variantRepository.SaveChangesAsync(cancellationToken);

        var created = await _variantRepository.GetAttributeByIdAsync(attribute.Id, cancellationToken);
        return ApiResponse<InvAttributeDto>.CreateSuccess(MapAttributeToDto(created ?? attribute), "تم إضافة السمة بنجاح");
    }

    public async Task<ApiResponse<PagedResultDto<InvVariantDto>>> GetVariantsPagedAsync(
        int pageNumber, int pageSize, long? itemId = null, string? search = null, bool? activeOnly = null, CancellationToken cancellationToken = default)
    {
        if (pageNumber <= 0 || pageSize <= 0)
            return ApiResponse<PagedResultDto<InvVariantDto>>.CreateFailure("Invalid page pagination parameters.", statusCode: 400);

        var (items, totalCount) = await _variantRepository.GetVariantsPagedAsync(
            pageNumber, pageSize, itemId, search, activeOnly, cancellationToken);

        var dtos = items.Select(MapVariantToDto).ToList();
        var paged = new PagedResultDto<InvVariantDto>(dtos, totalCount, pageNumber, pageSize);
        return ApiResponse<PagedResultDto<InvVariantDto>>.CreateSuccess(paged, "تم استرجاع قائمة المتغيرات بنجاح");
    }

    public async Task<ApiResponse<List<InvVariantDto>>> GetVariantsByItemIdAsync(long itemId, CancellationToken cancellationToken = default)
    {
        var variants = await _variantRepository.GetVariantsByItemIdAsync(itemId, cancellationToken);
        var dtos = variants.Select(MapVariantToDto).ToList();
        return ApiResponse<List<InvVariantDto>>.CreateSuccess(dtos, "تم استرجاع متغيرات الصنف بنجاح");
    }

    public async Task<ApiResponse<InvVariantDto>> GetVariantByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetVariantByIdAsync(id, cancellationToken);
        if (variant == null)
            return ApiResponse<InvVariantDto>.CreateFailure("متغير الصنف غير موجود", statusCode: 404);

        return ApiResponse<InvVariantDto>.CreateSuccess(MapVariantToDto(variant), "تم استرجاع بيانات المتغير بنجاح");
    }

    public async Task<ApiResponse<InvVariantDto>> CreateVariantManualAsync(CreateVariantManualDto request, CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(request.ItemId, cancellationToken);
        if (item == null)
            return ApiResponse<InvVariantDto>.CreateFailure("الصنف الأساسي غير موجود", statusCode: 400);

        if (string.IsNullOrWhiteSpace(request.Sku))
            return ApiResponse<InvVariantDto>.CreateFailure("رمز SKU مطلوب للمتغير", statusCode: 400);

        var skuExists = await _variantRepository.ExistsSkuAsync(request.Sku.Trim(), cancellationToken: cancellationToken);
        if (skuExists)
            return ApiResponse<InvVariantDto>.CreateFailure($"رمز SKU '{request.Sku}' مسجل مسبقاً", statusCode: 400);

        var variant = new InvItemVariant
        {
            ItemId = request.ItemId,
            Sku = request.Sku.Trim().ToUpperInvariant(),
            VariantNameLocal = string.IsNullOrWhiteSpace(request.VariantNameLocal)
                ? $"{item.ItemNameLocal} - {request.Sku.Trim()}"
                : request.VariantNameLocal.Trim(),
            VariantNameEn = request.VariantNameEn?.Trim(),
            Barcode = request.Barcode?.Trim(),
            AdditionalPrice = request.AdditionalPrice,
            CostPrice = request.CostPrice > 0 ? request.CostPrice : item.StandardCost,
            CreationUser = "SYSTEM",
            CreationDate = DateTime.UtcNow
        };

        if (request.AttributeValueIds != null && request.AttributeValueIds.Any())
        {
            foreach (var valId in request.AttributeValueIds.Distinct())
            {
                var attrVal = await _variantRepository.GetAttributeValueByIdAsync(valId, cancellationToken);
                if (attrVal != null)
                {
                    variant.AttributeValues.Add(new InvItemVariantValue
                    {
                        AttributeId = attrVal.AttributeId,
                        AttributeValueId = attrVal.Id
                    });
                }
            }
        }

        item.HasVariants = true;
        await _variantRepository.AddVariantAsync(variant, cancellationToken);
        await _itemRepository.UpdateAsync(item, cancellationToken);
        await _variantRepository.SaveChangesAsync(cancellationToken);

        var created = await _variantRepository.GetVariantByIdAsync(variant.Id, cancellationToken);
        return ApiResponse<InvVariantDto>.CreateSuccess(MapVariantToDto(created ?? variant), "تم إضافة المتغير بنجاح");
    }

    public async Task<ApiResponse<List<InvVariantDto>>> GenerateVariantsMatrixAsync(GenerateVariantsMatrixRequestDto request, CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(request.ItemId, cancellationToken);
        if (item == null)
            return ApiResponse<List<InvVariantDto>>.CreateFailure("الصنف الأساسي غير موجود", statusCode: 400);

        if (request.AttributeValueIds == null || !request.AttributeValueIds.Any())
            return ApiResponse<List<InvVariantDto>>.CreateFailure("يجب اختيار قيم السمات لتوليد المتغيرات", statusCode: 400);

        // Fetch selected attribute values with their attribute info
        var selectedValues = new List<InvItemAttributeValue>();
        foreach (var valId in request.AttributeValueIds.Distinct())
        {
            var val = await _variantRepository.GetAttributeValueByIdAsync(valId, cancellationToken);
            if (val != null)
                selectedValues.Add(val);
        }

        if (!selectedValues.Any())
            return ApiResponse<List<InvVariantDto>>.CreateFailure("لم يتم العثور على أي من قيم السمات المحددة", statusCode: 400);

        // Group values by AttributeId to perform Cartesian Product
        var groupedByAttr = selectedValues
            .GroupBy(v => v.AttributeId)
            .Select(g => g.ToList())
            .ToList();

        // Generate combinations (Cartesian Product)
        var combinations = CartesianProduct(groupedByAttr);
        var createdVariants = new List<InvVariantDto>();

        foreach (var combo in combinations)
        {
            var skuSuffix = string.Join("-", combo.Select(c => c.ValueCode));
            var generatedSku = $"{item.ItemCode}-{skuSuffix}".ToUpperInvariant();

            // Check if variant with this SKU already exists
            var existing = await _variantRepository.GetVariantBySkuAsync(generatedSku, cancellationToken);
            if (existing != null)
                continue; // Skip already generated variant

            var nameSuffix = string.Join(" / ", combo.Select(c => c.ValueLocal));
            var variant = new InvItemVariant
            {
                ItemId = item.Id,
                Sku = generatedSku,
                VariantNameLocal = $"{item.ItemNameLocal} - {nameSuffix}",
                VariantNameEn = !string.IsNullOrWhiteSpace(item.ItemNameEn) ? $"{item.ItemNameEn} - {string.Join(" / ", combo.Select(c => c.ValueEn ?? c.ValueCode))}" : null,
                AdditionalPrice = request.DefaultAdditionalPrice,
                CostPrice = item.StandardCost,
                CreationUser = "SYSTEM",
                CreationDate = DateTime.UtcNow
            };

            foreach (var val in combo)
            {
                variant.AttributeValues.Add(new InvItemVariantValue
                {
                    AttributeId = val.AttributeId,
                    AttributeValueId = val.Id
                });
            }

            await _variantRepository.AddVariantAsync(variant, cancellationToken);
        }

        item.HasVariants = true;
        await _itemRepository.UpdateAsync(item, cancellationToken);
        await _variantRepository.SaveChangesAsync(cancellationToken);

        var allItemVariants = await _variantRepository.GetVariantsByItemIdAsync(item.Id, cancellationToken);
        return ApiResponse<List<InvVariantDto>>.CreateSuccess(
            allItemVariants.Select(MapVariantToDto).ToList(),
            "تم توليد مصفوفة المتغيرات بنجاح");
    }

    public async Task<ApiResponse<InvVariantDto>> UpdateVariantAsync(long id, UpdateVariantDto request, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetVariantByIdAsync(id, cancellationToken);
        if (variant == null)
            return ApiResponse<InvVariantDto>.CreateFailure("متغير الصنف غير موجود", statusCode: 404);

        if (!string.IsNullOrWhiteSpace(request.Sku) && !request.Sku.Equals(variant.Sku, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _variantRepository.ExistsSkuAsync(request.Sku.Trim(), excludeVariantId: id, cancellationToken: cancellationToken);
            if (exists)
                return ApiResponse<InvVariantDto>.CreateFailure($"رمز SKU '{request.Sku}' مسجل مسبقاً لمتغير آخر", statusCode: 400);

            variant.Sku = request.Sku.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(request.VariantNameLocal))
            variant.VariantNameLocal = request.VariantNameLocal.Trim();

        if (request.VariantNameEn != null)
            variant.VariantNameEn = request.VariantNameEn.Trim();

        if (request.Barcode != null)
            variant.Barcode = request.Barcode.Trim();

        if (request.AdditionalPrice.HasValue)
            variant.AdditionalPrice = request.AdditionalPrice.Value;

        if (request.CostPrice.HasValue)
            variant.CostPrice = request.CostPrice.Value;

        if (request.ImageBase64 != null)
            variant.ImageBase64 = request.ImageBase64;

        if (request.IsActive.HasValue)
            variant.IsActive = request.IsActive.Value;

        variant.UpdateUser = "SYSTEM";
        variant.UpdateDate = DateTime.UtcNow;

        await _variantRepository.UpdateVariantAsync(variant, cancellationToken);
        await _variantRepository.SaveChangesAsync(cancellationToken);

        var updated = await _variantRepository.GetVariantByIdAsync(id, cancellationToken);
        return ApiResponse<InvVariantDto>.CreateSuccess(MapVariantToDto(updated ?? variant), "تم تحديث بيانات المتغير بنجاح");
    }

    public async Task<ApiResponse<bool>> DeleteVariantAsync(long id, CancellationToken cancellationToken = default)
    {
        var variant = await _variantRepository.GetVariantByIdAsync(id, cancellationToken);
        if (variant == null)
            return ApiResponse<bool>.CreateFailure("متغير الصنف غير موجود", statusCode: 404);

        await _variantRepository.DeleteVariantAsync(variant, cancellationToken);
        await _variantRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.CreateSuccess(true, "تم حذف المتغير بنجاح");
    }

    private static List<List<T>> CartesianProduct<T>(List<List<T>> sequences)
    {
        var result = new List<List<T>> { new() };
        foreach (var sequence in sequences)
        {
            var current = new List<List<T>>();
            foreach (var acc in result)
            {
                foreach (var item in sequence)
                {
                    var next = new List<T>(acc) { item };
                    current.Add(next);
                }
            }
            result = current;
        }
        return result;
    }

    private static InvAttributeDto MapAttributeToDto(InvItemAttribute a)
    {
        return new InvAttributeDto
        {
            Id = a.Id,
            AttributeCode = a.AttributeCode,
            NameLocal = a.NameLocal,
            NameEn = a.NameEn,
            IsActive = a.IsActive,
            Values = a.Values?.Select(v => new InvAttributeValueDto
            {
                Id = v.Id,
                AttributeId = v.AttributeId,
                ValueCode = v.ValueCode,
                ValueLocal = v.ValueLocal,
                ValueEn = v.ValueEn,
                ColorHex = v.ColorHex,
                SortOrder = v.SortOrder,
                IsActive = v.IsActive
            }).ToList() ?? new List<InvAttributeValueDto>()
        };
    }

    private static InvVariantDto MapVariantToDto(InvItemVariant v)
    {
        return new InvVariantDto
        {
            Id = v.Id,
            ItemId = v.ItemId,
            ItemCode = v.Item?.ItemCode ?? string.Empty,
            ItemNameLocal = v.Item?.ItemNameLocal ?? string.Empty,
            Sku = v.Sku,
            VariantNameLocal = v.VariantNameLocal,
            VariantNameEn = v.VariantNameEn,
            Barcode = v.Barcode,
            AdditionalPrice = v.AdditionalPrice,
            CostPrice = v.CostPrice,
            ImageBase64 = v.ImageBase64,
            IsActive = v.IsActive,
            AttributeValues = v.AttributeValues?.Select(av => new VariantAttributeValueDto
            {
                AttributeId = av.AttributeId,
                AttributeCode = av.Attribute?.AttributeCode ?? string.Empty,
                AttributeName = av.Attribute?.NameLocal ?? string.Empty,
                AttributeValueId = av.AttributeValueId,
                ValueCode = av.AttributeValue?.ValueCode ?? string.Empty,
                ValueName = av.AttributeValue?.ValueLocal ?? string.Empty,
                ColorHex = av.AttributeValue?.ColorHex
            }).ToList() ?? new List<VariantAttributeValueDto>()
        };
    }
}
