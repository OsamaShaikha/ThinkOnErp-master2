using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Application.DTOs.Inventory.Modifiers;

public class InvModifierGroupDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string GroupNameLocal { get; set; } = string.Empty;
    public string? GroupNameEn { get; set; }
    public bool IsRequired { get; set; }
    public ModifierSelectionType SelectionType { get; set; }
    public int MinSelections { get; set; }
    public int? MaxSelections { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public List<InvModifierOptionDto> Options { get; set; } = new();
    public List<long> LinkedItemIds { get; set; } = new();
}

public class InvModifierOptionDto
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

public class CreateInvModifierGroupDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    public string GroupCode { get; set; } = string.Empty;

    [Required]
    public string GroupNameLocal { get; set; } = string.Empty;
    public string? GroupNameEn { get; set; }
    public bool IsRequired { get; set; }

    [EnumDataType(typeof(ModifierSelectionType), ErrorMessage = "Invalid selection type. Must be 1 (Single) or 2 (Multiple)")]
    public ModifierSelectionType SelectionType { get; set; } = ModifierSelectionType.Multiple;

    [Range(0, int.MaxValue, ErrorMessage = "MinSelections cannot be negative")]
    public int MinSelections { get; set; } = 0;

    [Range(0, int.MaxValue, ErrorMessage = "MaxSelections cannot be negative")]
    public int? MaxSelections { get; set; }
    public int SortOrder { get; set; } = 0;
    public List<UpsertInvModifierOptionDto> Options { get; set; } = new();
    public List<long> LinkedItemIds { get; set; } = new();
}

public class UpdateInvModifierGroupDto
{
    public string? GroupNameLocal { get; set; }
    public string? GroupNameEn { get; set; }
    public bool IsRequired { get; set; }
    public ModifierSelectionType SelectionType { get; set; }
    public int MinSelections { get; set; }
    public int? MaxSelections { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpsertInvModifierOptionDto
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
    public List<InvModifierGroupDto> ModifierGroups { get; set; } = new();
}
