using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.DTOs.Pos;

public class KdsTicketDto
{
    public long OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public PosOrderType OrderType { get; set; }
    public string? TableNumber { get; set; }
    public string? CustomerName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ElapsedMinutes => (int)(DateTime.UtcNow - CreatedAt).TotalMinutes;
    public string? Notes { get; set; }

    public List<KdsTicketLineDto> Lines { get; set; } = new();
}

public class KdsTicketLineDto
{
    public long LineId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public PosKdsStatus Status { get; set; }
    public string? PrepStation { get; set; }
    public string? SpecialInstructions { get; set; }
    public List<string> Modifiers { get; set; } = new();
}

public class UpdateKdsLineStatusDto
{
    public long OrderId { get; set; }
    public long LineId { get; set; }
    public PosKdsStatus NewStatus { get; set; }
}

public class CfdCartStateDto
{
    public string OrderNumber { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? PaymentQrCodeUrl { get; set; }
    public List<CfdCartItemDto> Items { get; set; } = new();
}

public class CfdCartItemDto
{
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}
