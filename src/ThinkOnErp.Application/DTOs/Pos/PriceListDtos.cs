using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.DTOs.Pos;

public class CreatePriceListDto
{
    public long BranchId { get; set; }
    public string PriceListCode { get; set; } = string.Empty;
    public string PriceListNameLocal { get; set; } = string.Empty;
    public string? PriceListNameEn { get; set; }
    public PosOrderType? ApplicableOrderType { get; set; }
    public long? CurrencyId { get; set; }
    public bool IsDefault { get; set; }
    public List<UpsertPriceListItemDto> Items { get; set; } = new();
}

public class UpdatePriceListDto
{
    public string PriceListNameLocal { get; set; } = string.Empty;
    public string? PriceListNameEn { get; set; }
    public PosOrderType? ApplicableOrderType { get; set; }
    public long? CurrencyId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpsertPriceListItemDto
{
    public long ItemId { get; set; }
    public decimal Price { get; set; }
    public decimal? MinPrice { get; set; }
}

public class PriceListItemDto
{
    public long Id { get; set; }
    public long PriceListId { get; set; }
    public long ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public decimal Price { get; set; }
    public decimal? MinPrice { get; set; }
}

public class PriceListDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string PriceListCode { get; set; } = string.Empty;
    public string PriceListNameLocal { get; set; } = string.Empty;
    public string? PriceListNameEn { get; set; }
    public PosOrderType? ApplicableOrderType { get; set; }
    public long? CurrencyId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int ItemCount { get; set; }
}

public class PriceListDetailsDto : PriceListDto
{
    public List<PriceListItemDto> Items { get; set; } = new();
}
