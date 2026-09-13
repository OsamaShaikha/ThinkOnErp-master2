using System;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosExternalOrder
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public PosAggregatorProvider Provider { get; set; }
    public string ExternalOrderId { get; set; } = string.Empty;
    public string ExternalOrderDisplayNumber { get; set; } = string.Empty;
    public string? RawPayloadJson { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }

    public decimal SubtotalAmount { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal NetPayableAmount { get; set; }

    public string Status { get; set; } = "Received"; // Received, InPreparation, Dispatched, Delivered, Cancelled
    public long? CreatedPosOrderId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public SysBranch? Branch { get; set; }
    public PosOrderHeader? PosOrder { get; set; }
}
