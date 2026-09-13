using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.DTOs.Pos;

public class PosModifierGroupDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string GroupNameLocal { get; set; } = string.Empty;
    public string? GroupNameEn { get; set; }
    public bool IsRequired { get; set; }
    public PosSelectionType SelectionType { get; set; }
    public int MinSelections { get; set; }
    public int? MaxSelections { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public List<PosModifierOptionDto> Options { get; set; } = new();
    public List<long> LinkedItemIds { get; set; } = new();
}

public class PosModifierOptionDto
{
    public long Id { get; set; }
    public long ModifierGroupId { get; set; }
    public string OptionNameLocal { get; set; } = string.Empty;
    public string? OptionNameEn { get; set; }
    public decimal PriceAdjustment { get; set; }
    public long? RelatedItemId { get; set; }
    public string? RelatedItemName { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class CreatePosModifierGroupDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    public string GroupCode { get; set; } = string.Empty;

    [Required]
    public string GroupNameLocal { get; set; } = string.Empty;
    public string? GroupNameEn { get; set; }
    public bool IsRequired { get; set; }

    [EnumDataType(typeof(PosSelectionType), ErrorMessage = "Invalid selection type. Must be 1 (Single) or 2 (Multiple)")]
    public PosSelectionType SelectionType { get; set; } = PosSelectionType.Multiple;

    [Range(0, int.MaxValue, ErrorMessage = "MinSelections cannot be negative")]
    public int MinSelections { get; set; } = 0;

    [Range(0, int.MaxValue, ErrorMessage = "MaxSelections cannot be negative")]
    public int? MaxSelections { get; set; }
    public int SortOrder { get; set; } = 0;
    public List<UpsertPosModifierOptionDto> Options { get; set; } = new();
    public List<long> LinkedItemIds { get; set; } = new();
}

public class UpdatePosModifierGroupDto
{
    public string? GroupNameLocal { get; set; }
    public string? GroupNameEn { get; set; }
    public bool IsRequired { get; set; }
    public PosSelectionType SelectionType { get; set; }
    public int MinSelections { get; set; }
    public int? MaxSelections { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpsertPosModifierOptionDto
{
    public long? Id { get; set; }
    public string OptionNameLocal { get; set; } = string.Empty;
    public string? OptionNameEn { get; set; }
    public decimal PriceAdjustment { get; set; }
    public long? RelatedItemId { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AssignItemModifierGroupsDto
{
    public List<long> GroupIds { get; set; } = new();
}

public class ItemModifierGroupsDto
{
    public long ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public List<PosModifierGroupDto> ModifierGroups { get; set; } = new();
}
