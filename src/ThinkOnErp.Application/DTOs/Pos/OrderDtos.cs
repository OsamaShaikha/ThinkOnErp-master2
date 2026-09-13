using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.DTOs.Pos;

public class CreatePosOrderDto
{
    public long BranchId { get; set; }
    public long ShiftId { get; set; }
    public long TillId { get; set; }
    public string ClientUuid { get; set; } = string.Empty;

    public PosOrderType OrderType { get; set; } = PosOrderType.DineIn;
    public PosOrderStatus Status { get; set; } = PosOrderStatus.Draft;

    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public long? TableId { get; set; }
    public int Covers { get; set; } = 1;
    public long? PriceListId { get; set; }

    public decimal ManualDiscountAmount { get; set; }
    public decimal ManualDiscountPercent { get; set; }
    public string? DiscountReason { get; set; }

    public decimal ServiceChargeAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TipAmount { get; set; }
    public string? Notes { get; set; }

    public List<CreatePosOrderLineDto> Lines { get; set; } = new();
    public List<CreatePosOrderPaymentDto> Payments { get; set; } = new();
}

public class CreatePosOrderLineDto
{
    public int LineNumber { get; set; }
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int UomId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public string? PrepStation { get; set; }
    public bool IsScaleItem { get; set; }
    public decimal? ScaleWeight { get; set; }
    public string? ScaleBarcode { get; set; }
    public long? SalesEmployeeId { get; set; }
    public string? SpecialInstructions { get; set; }
    public List<CreatePosOrderLineModifierDto> Modifiers { get; set; } = new();
}

public class CreatePosOrderLineModifierDto
{
    public long ModifierItemId { get; set; }
    public string ModifierName { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal ExtraPrice { get; set; }
}

public class CreatePosOrderPaymentDto
{
    public PosPaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public decimal TenderedAmount { get; set; }
    public decimal ChangeAmount { get; set; }

    public string? CardNumberMasked { get; set; }
    public string? CardType { get; set; }
    public string? TransactionReference { get; set; }
    public string? AuthCode { get; set; }
    public string? TerminalId { get; set; }
    public string? ChequeNumber { get; set; }
    public string? GiftCardCode { get; set; }
    public long? LoyaltyPointsRedeemed { get; set; }
}

public class PosOrderSummaryDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long ShiftId { get; set; }
    public long TillId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public string ClientUuid { get; set; } = string.Empty;

    public PosOrderType OrderType { get; set; }
    public PosOrderStatus Status { get; set; }

    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public long? TableId { get; set; }
    public string? TableNumber { get; set; }
    public int Covers { get; set; }

    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TipAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public bool IsPaid { get; set; }

    public bool IsRefund { get; set; }
    public string? EInvoiceQrCode { get; set; }
    public string? EInvoiceHash { get; set; }
    public DateTime CreationDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;

    public List<PosOrderLineSummaryDto> Lines { get; set; } = new();
    public List<PosOrderPaymentSummaryDto> Payments { get; set; } = new();
}

public class PosOrderLineSummaryDto
{
    public long Id { get; set; }
    public int LineNumber { get; set; }
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int UomId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public PosKdsStatus KdsStatus { get; set; }
    public string? PrepStation { get; set; }
    public bool IsScaleItem { get; set; }
    public decimal? ScaleWeight { get; set; }
    public bool IsVoided { get; set; }
    public string? VoidReason { get; set; }
    public List<PosOrderLineModifierDto> Modifiers { get; set; } = new();
}

public class PosOrderLineModifierDto
{
    public long Id { get; set; }
    public long ModifierItemId { get; set; }
    public string ModifierName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal ExtraPrice { get; set; }
}

public class PosOrderPaymentSummaryDto
{
    public long Id { get; set; }
    public PosPaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public decimal TenderedAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public string? CardNumberMasked { get; set; }
    public string? TransactionReference { get; set; }
    public DateTime PaymentDate { get; set; }
}

public class RefundOrderDto
{
    public long OrderId { get; set; }
    public string RefundReason { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public bool ReturnToInventory { get; set; } = true;
    public List<RefundOrderLineDto> RefundLines { get; set; } = new();
    public List<CreatePosOrderPaymentDto> RefundPayments { get; set; } = new();
}

public class RefundOrderLineDto
{
    public long OrderLineId { get; set; }
    public decimal QuantityToRefund { get; set; }
}

public class VoidLineDto
{
    public long OrderId { get; set; }
    public long OrderLineId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
}

public class UpdatePosOrderDto
{
    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public long? TableId { get; set; }
    public int Covers { get; set; } = 1;
    public decimal ManualDiscountAmount { get; set; }
    public decimal ManualDiscountPercent { get; set; }
    public string? DiscountReason { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TipAmount { get; set; }
    public string? Notes { get; set; }
}

