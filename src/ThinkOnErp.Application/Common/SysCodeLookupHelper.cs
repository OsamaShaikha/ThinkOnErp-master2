using System.Collections.Generic;
using System.Linq;
using ThinkOnErp.Application.DTOs.SysCode;
using ThinkOnErp.Domain.Constants;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Application.Common;

/// <summary>
/// Helper class for mapping and resolving dynamic SYS_CODE lookup definitions
/// </summary>
public static class SysCodeLookupHelper
{
    public static List<SysCodeLookupDto> MapToLookupDtos(IEnumerable<SysCode> rawCodes, int? lang)
    {
        return rawCodes
            .Where(c => c.CodeMnr > 0)
            .GroupBy(c => c.CodeMnr)
            .Select(g =>
            {
                var ar = g.FirstOrDefault(x => x.CodeLang == 1);
                var en = g.FirstOrDefault(x => x.CodeLang == 2);
                var first = g.First();

                var arDesc = ar?.CodeDesc ?? first.CodeDesc;
                var enDesc = en?.CodeDesc ?? first.CodeDesc;
                var val = !string.IsNullOrEmpty(first.CodeValue)
                    ? first.CodeValue
                    : (en?.CodeValue ?? ar?.CodeValue ?? string.Empty);

                var isEnglish = lang == 2;
                var localizedName = isEnglish
                    ? (!string.IsNullOrEmpty(enDesc) ? enDesc : arDesc)
                    : (!string.IsNullOrEmpty(arDesc) ? arDesc : enDesc);

                return new SysCodeLookupDto
                {
                    Code = g.Key,
                    Value = val,
                    NameAr = arDesc,
                    NameEn = enDesc,
                    Name = localizedName,
                    IsActive = first.IsActive
                };
            })
            .OrderBy(x => x.Code)
            .ToList();
    }

    public static List<SysCodeLookupDto> GetFallbackLookups(int mgr, int? lang)
    {
        var isEnglish = lang == 2;
        return mgr switch
        {
            SysCodeKeys.ItemTypes.Mgr => new List<SysCodeLookupDto>
            {
                new() { Code = 1, Value = "Stock", NameAr = "مخزني", NameEn = "Stock", Name = isEnglish ? "Stock" : "مخزني", IsActive = 1 },
                new() { Code = 2, Value = "NonStock", NameAr = "غير مخزني", NameEn = "Non-Stock", Name = isEnglish ? "Non-Stock" : "غير مخزني", IsActive = 1 },
                new() { Code = 3, Value = "Service", NameAr = "خدمي", NameEn = "Service", Name = isEnglish ? "Service" : "خدمي", IsActive = 1 },
                new() { Code = 4, Value = "Kit", NameAr = "طقم / كيت", NameEn = "Kit", Name = isEnglish ? "Kit" : "طقم / كيت", IsActive = 1 },
                new() { Code = 5, Value = "Assembly", NameAr = "تجميعي / تصنيعي", NameEn = "Assembly", Name = isEnglish ? "Assembly" : "تجميعي / تصنيعي", IsActive = 1 },
            },
            SysCodeKeys.CostingMethods.Mgr => new List<SysCodeLookupDto>
            {
                new() { Code = 1, Value = "WeightedAverage", NameAr = "المتوسط المرجح", NameEn = "Weighted Average", Name = isEnglish ? "Weighted Average" : "المتوسط المرجح", IsActive = 1 },
                new() { Code = 2, Value = "Fifo", NameAr = "الوارد أولاً صادر أولاً (FIFO)", NameEn = "First In, First Out (FIFO)", Name = isEnglish ? "First In, First Out (FIFO)" : "الوارد أولاً صادر أولاً (FIFO)", IsActive = 1 },
                new() { Code = 3, Value = "SpecificId", NameAr = "التمييز المحدد / التكلفة الفعلية", NameEn = "Specific Identification", Name = isEnglish ? "Specific Identification" : "التمييز المحدد / التكلفة الفعلية", IsActive = 1 },
                new() { Code = 4, Value = "Standard", NameAr = "التكلفة المعيارية", NameEn = "Standard Cost", Name = isEnglish ? "Standard Cost" : "التكلفة المعيارية", IsActive = 1 },
            },
            SysCodeKeys.BomTypes.Mgr => new List<SysCodeLookupDto>
            {
                new() { Code = 1, Value = "SalesKit", NameAr = "طقم مبيعات (Sales Kit)", NameEn = "Sales Kit", Name = isEnglish ? "Sales Kit" : "طقم مبيعات (Sales Kit)", IsActive = 1 },
                new() { Code = 2, Value = "ProductionAssembly", NameAr = "تجميع إنتاجي (Production Assembly)", NameEn = "Production Assembly", Name = isEnglish ? "Production Assembly" : "تجميع إنتاجي (Production Assembly)", IsActive = 1 },
                new() { Code = 3, Value = "Disassembly", NameAr = "تفكيك (Disassembly)", NameEn = "Disassembly", Name = isEnglish ? "Disassembly" : "تفكيك (Disassembly)", IsActive = 1 },
            },
            SysCodeKeys.PosOrderTypes.Mgr => new List<SysCodeLookupDto>
            {
                new() { Code = 1, Value = "DineIn", NameAr = "محلي (صالة)", NameEn = "Dine In", Name = isEnglish ? "Dine In" : "محلي (صالة)", IsActive = 1 },
                new() { Code = 2, Value = "Takeaway", NameAr = "سفري (تيك أواي)", NameEn = "Takeaway", Name = isEnglish ? "Takeaway" : "سفري (تيك أواي)", IsActive = 1 },
                new() { Code = 3, Value = "Delivery", NameAr = "توصيل", NameEn = "Delivery", Name = isEnglish ? "Delivery" : "توصيل", IsActive = 1 },
                new() { Code = 4, Value = "Aggregator", NameAr = "تطبيقات التوصيل الخارجية", NameEn = "Aggregator", Name = isEnglish ? "Aggregator" : "تطبيقات التوصيل الخارجية", IsActive = 1 },
                new() { Code = 5, Value = "Kiosk", NameAr = "شاشة الخدمة الذاتية (كيوسك)", NameEn = "Self-Service Kiosk", Name = isEnglish ? "Self-Service Kiosk" : "شاشة الخدمة الذاتية (كيوسك)", IsActive = 1 },
                new() { Code = 6, Value = "QrTable", NameAr = "طلب ذاتي عبر الباركود (QR)", NameEn = "QR Table Order", Name = isEnglish ? "QR Table Order" : "طلب ذاتي عبر الباركود (QR)", IsActive = 1 },
            },
            SysCodeKeys.PosStockDeductionModes.Mgr => new List<SysCodeLookupDto>
            {
                new() { Code = 1, Value = "1", NameAr = "خصم فوري لحظي مع كل فاتورة", NameEn = "Real-Time deduction on each sale", Name = isEnglish ? "Real-Time deduction on each sale" : "خصم فوري لحظي مع كل فاتورة", IsActive = 1 },
                new() { Code = 2, Value = "2", NameAr = "خصم تجميعي عند إغلاق الوردية (تقرير Z)", NameEn = "Consolidated deduction at shift close (Z-Report)", Name = isEnglish ? "Consolidated deduction at shift close (Z-Report)" : "خصم تجميعي عند إغلاق الوردية (تقرير Z)", IsActive = 1 },
                new() { Code = 3, Value = "3", NameAr = "بدون خصم مخزني", NameEn = "No stock deduction", Name = isEnglish ? "No stock deduction" : "بدون خصم مخزني", IsActive = 1 },
            },
            SysCodeKeys.PosOrderStatuses.Mgr => new List<SysCodeLookupDto>
            {
                new() { Code = 1, Value = "Draft", NameAr = "مسودة", NameEn = "Draft", Name = isEnglish ? "Draft" : "مسودة", IsActive = 1 },
                new() { Code = 2, Value = "Parked", NameAr = "معلق (Parked)", NameEn = "Parked", Name = isEnglish ? "Parked" : "معلق (Parked)", IsActive = 1 },
                new() { Code = 3, Value = "SentToKitchen", NameAr = "مرسل للمطبخ", NameEn = "Sent to Kitchen", Name = isEnglish ? "Sent to Kitchen" : "مرسل للمطبخ", IsActive = 1 },
                new() { Code = 4, Value = "Ready", NameAr = "جاهز للتسليم", NameEn = "Ready", Name = isEnglish ? "Ready" : "جاهز للتسليم", IsActive = 1 },
                new() { Code = 5, Value = "Completed", NameAr = "مكتمل ومسدد", NameEn = "Completed", Name = isEnglish ? "Completed" : "مكتمل ومسدد", IsActive = 1 },
                new() { Code = 6, Value = "Voided", NameAr = "ملغي", NameEn = "Voided", Name = isEnglish ? "Voided" : "ملغي", IsActive = 1 },
                new() { Code = 7, Value = "Refunded", NameAr = "مسترجع", NameEn = "Refunded", Name = isEnglish ? "Refunded" : "مسترجع", IsActive = 1 },
            },
            SysCodeKeys.PosPaymentMethods.Mgr => new List<SysCodeLookupDto>
            {
                new() { Code = 1, Value = "Cash", NameAr = "نقداً (كاش)", NameEn = "Cash", Name = isEnglish ? "Cash" : "نقداً (كاش)", IsActive = 1 },
                new() { Code = 2, Value = "Card", NameAr = "بطاقة دفع / مدى", NameEn = "Card", Name = isEnglish ? "Card" : "بطاقة دفع / مدى", IsActive = 1 },
                new() { Code = 3, Value = "Split", NameAr = "دفع مجزأ / متعدد", NameEn = "Split Payment", Name = isEnglish ? "Split Payment" : "دفع مجزأ / متعدد", IsActive = 1 },
                new() { Code = 4, Value = "CustomerAccount", NameAr = "حساب عميل (آجل)", NameEn = "Customer Account", Name = isEnglish ? "Customer Account" : "حساب عميل (آجل)", IsActive = 1 },
                new() { Code = 5, Value = "Cheque", NameAr = "شيك", NameEn = "Cheque", Name = isEnglish ? "Cheque" : "شيك", IsActive = 1 },
                new() { Code = 6, Value = "BankTransfer", NameAr = "تحويل بنكي", NameEn = "Bank Transfer", Name = isEnglish ? "Bank Transfer" : "تحويل بنكي", IsActive = 1 },
                new() { Code = 7, Value = "DigitalWallet", NameAr = "محفظة رقمية", NameEn = "Digital Wallet", Name = isEnglish ? "Digital Wallet" : "محفظة رقمية", IsActive = 1 },
                new() { Code = 8, Value = "LoyaltyPoints", NameAr = "نقاط ولاء", NameEn = "Loyalty Points", Name = isEnglish ? "Loyalty Points" : "نقاط ولاء", IsActive = 1 },
                new() { Code = 9, Value = "GiftCard", NameAr = "بطاقة هدايا", NameEn = "Gift Card", Name = isEnglish ? "Gift Card" : "بطاقة هدايا", IsActive = 1 },
                new() { Code = 10, Value = "AggregatorPaid", NameAr = "مسدد عبر منصة التوصيل", NameEn = "Aggregator Paid", Name = isEnglish ? "Aggregator Paid" : "مسدد عبر منصة التوصيل", IsActive = 1 },
            },
            SysCodeKeys.PosKdsStatuses.Mgr => new List<SysCodeLookupDto>
            {
                new() { Code = 1, Value = "Pending", NameAr = "قيد الانتظار", NameEn = "Pending", Name = isEnglish ? "Pending" : "قيد الانتظار", IsActive = 1 },
                new() { Code = 2, Value = "Preparing", NameAr = "جاري التحضير", NameEn = "Preparing", Name = isEnglish ? "Preparing" : "جاري التحضير", IsActive = 1 },
                new() { Code = 3, Value = "Ready", NameAr = "جاهز للتسليم", NameEn = "Ready", Name = isEnglish ? "Ready" : "جاهز للتسليم", IsActive = 1 },
                new() { Code = 4, Value = "Served", NameAr = "تم التقديم", NameEn = "Served", Name = isEnglish ? "Served" : "تم التقديم", IsActive = 1 },
            },
            SysCodeKeys.PosCustomerSettlementTypes.Mgr => new List<SysCodeLookupDto>
            {
                new() { Code = 1, Value = "OnAccount", NameAr = "دفعة على الحساب (توزيع تلقائي FIFO)", NameEn = "On Account (Auto FIFO)", Name = isEnglish ? "On Account (Auto FIFO)" : "دفعة على الحساب (توزيع تلقائي FIFO)", IsActive = 1 },
                new() { Code = 2, Value = "SpecificInvoice", NameAr = "سداد فاتورة محددة", NameEn = "Specific Invoice", Name = isEnglish ? "Specific Invoice" : "سداد فاتورة محددة", IsActive = 1 },
                new() { Code = 3, Value = "AllInvoices", NameAr = "سداد كامل الفواتير المفتوحة", NameEn = "All Open Invoices", Name = isEnglish ? "All Open Invoices" : "سداد كامل الفواتير المفتوحة", IsActive = 1 },
            },
            _ => new List<SysCodeLookupDto>()
        };
    }
}
