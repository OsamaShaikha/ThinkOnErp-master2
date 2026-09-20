using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.Variants;

// Attributes
public class InvAttributeDto
{
    public long Id { get; set; }
    public string AttributeCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public bool IsActive { get; set; }
    public List<InvAttributeValueDto> Values { get; set; } = new();
}

public class InvAttributeValueDto
{
    public long Id { get; set; }
    public long AttributeId { get; set; }
    public string ValueCode { get; set; } = string.Empty;
    public string ValueLocal { get; set; } = string.Empty;
    public string? ValueEn { get; set; }
    public string? ColorHex { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class CreateAttributeDto
{
    public string AttributeCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public List<CreateAttributeValueDto> Values { get; set; } = new();
}

public class CreateAttributeValueDto
{
    public string ValueCode { get; set; } = string.Empty;
    public string ValueLocal { get; set; } = string.Empty;
    public string? ValueEn { get; set; }
    public string? ColorHex { get; set; }
    public int SortOrder { get; set; }
}

// Variants
public class InvVariantDto
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemNameLocal { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string VariantNameLocal { get; set; } = string.Empty;
    public string? VariantNameEn { get; set; }
    public string? Barcode { get; set; }
    public decimal AdditionalPrice { get; set; }
    public decimal CostPrice { get; set; }
    public string? ImageBase64 { get; set; }
    public bool IsActive { get; set; }
    public List<VariantAttributeValueDto> AttributeValues { get; set; } = new();
}

public class VariantAttributeValueDto
{
    public long AttributeId { get; set; }
    public string AttributeCode { get; set; } = string.Empty;
    public string AttributeName { get; set; } = string.Empty;
    public long AttributeValueId { get; set; }
    public string ValueCode { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
}

public class CreateVariantManualDto
{
    public long ItemId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string VariantNameLocal { get; set; } = string.Empty;
    public string? VariantNameEn { get; set; }
    public string? Barcode { get; set; }
    public decimal AdditionalPrice { get; set; }
    public decimal CostPrice { get; set; }
    public List<long> AttributeValueIds { get; set; } = new();
}

public class UpdateVariantDto
{
    public string? Sku { get; set; }
    public string? VariantNameLocal { get; set; }
    public string? VariantNameEn { get; set; }
    public string? Barcode { get; set; }
    public decimal? AdditionalPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public string? ImageBase64 { get; set; }
    public bool? IsActive { get; set; }
}

public class GenerateVariantsMatrixRequestDto
{
    public long ItemId { get; set; }
    public List<long> AttributeValueIds { get; set; } = new();
    public decimal DefaultAdditionalPrice { get; set; } = 0m;
}
