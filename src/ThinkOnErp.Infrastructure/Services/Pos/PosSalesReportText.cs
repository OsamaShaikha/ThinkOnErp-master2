using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Infrastructure.Services.Pos;

internal sealed class PosSalesReportText(string? language)
{
    public bool Arabic { get; } = language?.Split('-', '_')[0].Equals("ar", StringComparison.OrdinalIgnoreCase) == true;
    public string Language => Arabic ? "ar" : "en";
    public string T(string english, string arabic) => Arabic ? arabic : english;
    public string All => T("All", "الكل");
    public string Name(string? local, string? english) =>
        (Arabic ? First(local, english) : First(english, local)) ?? "";
    private static string? First(string? preferred, string? fallback) => string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;

    public string Payment(PosPaymentMethod method) => method switch
    {
        PosPaymentMethod.Cash => T("Cash", "نقدًا"),
        PosPaymentMethod.Card => T("Card", "بطاقة"),
        PosPaymentMethod.Split => T("Split", "دفع مختلط"),
        PosPaymentMethod.CustomerAccount => T("Credit (AR)", "ذمم العملاء"),
        PosPaymentMethod.Cheque => T("Cheque", "شيك"),
        PosPaymentMethod.BankTransfer => T("Bank transfer", "تحويل بنكي"),
        PosPaymentMethod.DigitalWallet => T("Digital wallet", "محفظة إلكترونية"),
        PosPaymentMethod.LoyaltyPoints => T("Loyalty points", "نقاط الولاء"),
        PosPaymentMethod.GiftCard => T("Gift card", "بطاقة هدية"),
        PosPaymentMethod.AggregatorPaid => T("Aggregator paid", "مدفوع عبر المنصة"),
        PosPaymentMethod.Hospitality => T("Hospitality", "ضيافة"),
        _ => method.ToString()
    };
}
