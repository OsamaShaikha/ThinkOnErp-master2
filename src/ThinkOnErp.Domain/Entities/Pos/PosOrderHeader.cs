using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosOrderHeader
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long ShiftId { get; set; }
    public long TillId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public string ClientUuid { get; set; } = string.Empty; // Idempotency key from offline client

    public PosOrderType OrderType { get; set; } = PosOrderType.DineIn;
    public PosOrderStatus Status { get; set; } = PosOrderStatus.Draft;

    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public long? TableId { get; set; }
    public int Covers { get; set; } = 1;
    public long? PriceListId { get; set; }

    // Financial totals
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public string? DiscountReason { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TipAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public bool IsPaid { get; set; }
    public decimal RemainingAmount => (Payments != null && Payments.Any(p => p.PaymentMethod == PosPaymentMethod.Hospitality))
        ? 0m
        : Math.Max(0m, TotalAmount - PaidAmount);
    public bool IsCreditSale => Payments != null && Payments.Any(p => p.PaymentMethod == PosPaymentMethod.CustomerAccount);

    public PosPaymentStatus PaymentStatus
    {
        get
        {
            if (Payments != null && Payments.Any(p => p.PaymentMethod == PosPaymentMethod.Hospitality))
                return PosPaymentStatus.Hospitality;

            if (PaidAmount >= TotalAmount && TotalAmount > 0)
            {
                decimal advanceDeducted = 0m;
                if (Payments != null)
                {
                    foreach (var p in Payments.Where(x => x.PaymentMethod == PosPaymentMethod.CustomerAccount))
                    {
                        if (!string.IsNullOrWhiteSpace(p.TransactionReference) && p.TransactionReference.StartsWith("ADV:"))
                        {
                            var parts = p.TransactionReference.Split('|');
                            if (parts.Length == 2 && parts[0].StartsWith("ADV:") && decimal.TryParse(parts[0][4..], out var adv))
                            {
                                advanceDeducted += adv;
                            }
                        }
                    }
                }

                if (advanceDeducted >= TotalAmount)
                    return PosPaymentStatus.PaidFromAdvance;

                return PosPaymentStatus.Paid;
            }

            if (PaidAmount > 0)
            {
                decimal advanceDeducted = 0m;
                decimal receivableCharged = 0m;
                decimal cashCard = 0m;

                if (Payments != null)
                {
                    foreach (var p in Payments)
                    {
                        if (p.PaymentMethod == PosPaymentMethod.CustomerAccount)
                        {
                            if (!string.IsNullOrWhiteSpace(p.TransactionReference) && p.TransactionReference.StartsWith("ADV:"))
                            {
                                var parts = p.TransactionReference.Split('|');
                                if (parts.Length == 2 && parts[0].StartsWith("ADV:") && decimal.TryParse(parts[0][4..], out var adv))
                                    advanceDeducted += adv;
                                if (parts.Length == 2 && parts[1].StartsWith("REC:") && decimal.TryParse(parts[1][4..], out var rec))
                                    receivableCharged += rec;
                            }
                            else
                            {
                                receivableCharged += p.Amount;
                            }
                        }
                        else if (p.PaymentMethod != PosPaymentMethod.Hospitality)
                        {
                            cashCard += p.Amount;
                        }
                    }
                }

                if (advanceDeducted > 0 && receivableCharged > 0 && cashCard == 0)
                    return PosPaymentStatus.PartiallyPaidFromAdvance;

                return PosPaymentStatus.PartiallyPaid;
            }

            if (Payments != null && Payments.Any(p => p.PaymentMethod == PosPaymentMethod.CustomerAccount))
                return PosPaymentStatus.CustomerAccount;

            return PosPaymentStatus.Unpaid;
        }
    }

    // Returns & Refunds
    public bool IsRefund { get; set; }
    public long? OriginalOrderId { get; set; }
    public string? RefundReason { get; set; }

    // Reporting & Accounting Integration
    public long? ZReportId { get; set; }
    public long? GlVoucherId { get; set; }

    // E-Invoicing (ZATCA Phase 1 & 2)
    public string? EInvoiceQrCode { get; set; }
    public string? EInvoiceHash { get; set; }
    public long? InvoiceCounterValue { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    // Audit fields
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigations
    public SysBranch? Branch { get; set; }
    public PosShift? Shift { get; set; }
    public PosTill? Till { get; set; }
    public Customer? Customer { get; set; }
    public PosTable? Table { get; set; }
    public GlVoucherHeader? GlVoucher { get; set; }
    public PosOrderHeader? OriginalOrder { get; set; }
    public ICollection<PosOrderLine> Lines { get; set; } = new List<PosOrderLine>();
    public ICollection<PosOrderPayment> Payments { get; set; } = new List<PosOrderPayment>();
    public ICollection<PosOrderTax> Taxes { get; set; } = new List<PosOrderTax>();
}
