using System;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosOrderPayment
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public PosPaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public decimal TenderedAmount { get; set; }
    public decimal ChangeAmount { get; set; }

    // Card / Gateway / Terminal reference details
    public string? CardNumberMasked { get; set; }
    public string? CardType { get; set; }
    public string? TransactionReference { get; set; }
    public string? AuthCode { get; set; }
    public string? TerminalId { get; set; }

    // Voucher / Customer / Gift card references
    public string? ChequeNumber { get; set; }
    public string? GiftCardCode { get; set; }
    public long? LoyaltyPointsRedeemed { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string CreationUser { get; set; } = string.Empty;

    // Navigation
    public PosOrderHeader? Order { get; set; }
}
