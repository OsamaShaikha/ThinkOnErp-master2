using System.Globalization;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Infrastructure.Services.Pos;

/// <summary>Explicit, bilingual presentation mappings. Values retain the existing report DTO semantics.</summary>
public sealed class PosReportSuiteMapper(string language, string currency)
{
    private readonly PosSalesReportText text = new(language);
    private string T(string label) { var parts = label.Split('|'); return text.T(parts[0], parts[1]); }
    private string P(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "%";
    private string N(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private string V(object? value) => value switch
    {
        null => "-", decimal d => d.ToString("N3", CultureInfo.InvariantCulture),
        DateTime date => date.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
        bool b => b ? T("Yes|نعم") : T("No|لا"),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture), _ => value.ToString() ?? "-"
    };
    private PosReportMetricDto M(string label, object value, bool money = false, string unit = "") =>
        new() { Label = T(label), Value = V(value), Unit = money ? currency : unit };
    private PosReportColumnDto C(string label, bool numeric = false, float width = 1, bool code = false) =>
        new() { Label = T(label), Numeric = numeric, Width = width, Code = code };
    private PosReportTableDto Table<TItem>(string title, IEnumerable<TItem> items, PosReportColumnDto[] columns, Func<TItem, object?[]> row) =>
        new() { Title = T(title), Columns = columns.ToList(), Rows = items.Select(x => row(x).Select(V).ToList()).ToList() };
    private string Item(string? code, string? name) => string.IsNullOrWhiteSpace(code) ? name ?? "-" : $"{name}\n{code}";
    private string State(string state) => state switch
    {
        "Open" => T("Open|مفتوحة"), "Suspended" => T("Suspended|معلّقة"), "BlindClosed" => T("Blind closed|مغلقة مبدئيًا"),
        "AuditedAndClosed" => T("Audited & closed|مدققة ومغلقة"), "Available" => T("Available|متاحة"),
        "Occupied" => T("Occupied|مشغولة"), "Billed" => T("Billed|مفوتَرة"), "Cleaning" => T("Cleaning|تنظيف"),
        "Reserved" => T("Reserved|محجوزة"), "OutOfService" => T("Out of service|خارج الخدمة"), _ => state
    };
    private string Scope(string value) => value == "Order" ? T("Order|الطلب") : value == "Line" ? T("Item|الصنف") : value;
    private string Station(string value) => value == "المطبخ الرئيسي (Main Kitchen)" ? T("Main kitchen|المطبخ الرئيسي") : value;
    private string Tender(string value)
    {
        var method = value switch
        {
            "نقداً" => PosPaymentMethod.Cash, "بطاقة مدى / ائتمان" => PosPaymentMethod.Card,
            "حساب عميل (آجل / ذمم)" => PosPaymentMethod.CustomerAccount, "شيك" => PosPaymentMethod.Cheque,
            "تحويل بنكي" => PosPaymentMethod.BankTransfer, "محفظة إلكترونية" => PosPaymentMethod.DigitalWallet,
            "نقاط ولاء" => PosPaymentMethod.LoyaltyPoints, "بطاقة هدايا" => PosPaymentMethod.GiftCard,
            "ضيافة" => PosPaymentMethod.Hospitality, "دفع متعدد" => PosPaymentMethod.Split,
            "تطبيقات التوصيل" => PosPaymentMethod.AggregatorPaid, _ => (PosPaymentMethod?)null
        };
        return method.HasValue ? text.Payment(method.Value) : value;
    }

    public PosReportBranchSectionDto Map(object source)
    {
        var b = new PosReportBranchSectionDto();
        switch (source)
        {
            case PosSalesPrintDataDto d:
                b.Metrics = new() { M("Net sales|صافي المبيعات", d.Totals.Net, true), M("Total incl. tax|الإجمالي شامل الضريبة", d.Totals.Total, true), M("Invoices|الفواتير", d.Totals.Invoices), M("Average invoice|متوسط الفاتورة", d.AverageInvoice, true) };
                b.Tables.Add(Table("Sales by day|المبيعات حسب اليوم", d.Days,
                    new[] { C("Date|التاريخ", width: 2, code: true), C("Invoices|الفواتير", true), C("Sales|المبيعات", true), C("Discount|الخصم", true), C("Returns|المرتجعات", true), C("Net|الصافي", true), C("Tax|الضريبة", true), C("Charges|الرسوم", true), C("Total|الإجمالي", true) },
                    x => new object?[] { x.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), x.Invoices, x.Sales, x.Discount, x.Returns, x.Net, x.Tax, x.Charges, x.Total }));
                break;
            case PosPaymentMethodReportDto d:
                b.Metrics = new() { M("Net collected|صافي التحصيل", d.NetCollected, true), M("Collected|المحصّل", d.TotalCollected, true), M("Refunded|المسترد", d.TotalRefunded, true), M("Transactions|الحركات", d.TotalTransactions) };
                b.Tables.Add(Table("Payment methods|طرق الدفع", d.Methods,
                    new[] { C("Method|الطريقة", width: 2), C("Transactions|الحركات", true), C("Collected|المحصّل", true), C("Refunded|المسترد", true), C("Net|الصافي", true), C("Share|النسبة", true) },
                    x => new object?[] { text.Name(x.PaymentMethodNameAr, x.PaymentMethodNameEn) is { Length: > 0 } name ? name : text.Payment(x.PaymentMethod), x.TransactionCount, x.TotalAmount, x.TotalRefunded, x.NetAmount, P(x.Percentage) }));
                break;
            case PosCashierSalesReportDto d:
                b.Metrics = new() { M("Net sales|صافي المبيعات", d.OverallNetSales, true), M("Gross sales|إجمالي المبيعات", d.OverallGrossSales, true), M("Discounts|الخصومات", d.OverallDiscounts, true), M("Invoices|الفواتير", d.OverallOrdersCount) };
                b.Tables.Add(Table("Cashier sales|مبيعات الكاشيرات", d.Cashiers,
                    new[] { C("Cashier|الكاشير", width: 2), C("Invoices|الفواتير", true), C("Gross|الإجمالي", true), C("Discount|الخصم", true), C("Tax|الضريبة", true), C("Total|المجموع", true), C("Net sales|صافي المبيعات", true) },
                    x => new object?[] { x.CashierUsername, x.OrderCount, x.GrossSales, x.TotalDiscount, x.TotalTax, x.TotalSales, x.NetSales }));
                b.Tables.Add(Table("Cashier refunds & averages|مرتجعات ومتوسطات الكاشيرات", d.Cashiers,
                    new[] { C("Cashier|الكاشير", width: 2), C("Refund count|عدد المرتجعات", true), C("Refund amount|قيمة المرتجعات", true), C("Average invoice|متوسط الفاتورة", true) },
                    x => new object?[] { x.CashierUsername, x.RefundCount, x.RefundAmount, x.AverageTicket }));
                b.Notes.Add(T("Cashiers|الكاشيرات") + ": " + d.TotalCashiers);
                break;
            case PosItemSalesReportDto d:
                b.Metrics = new() { M("Net sales|صافي المبيعات", d.TotalNetSales, true), M("Quantity sold|الكمية المباعة", d.TotalQuantitySold), M("Gross profit|مجمل الربح", d.TotalGrossProfit, true), M("Profit margin|هامش الربح", P(d.OverallProfitMargin)) };
                b.Tables.Add(Table("Item sales|مبيعات الأصناف", d.Items,
                    new[] { C("Item|الصنف", width: 2.5f), C("Category|الفئة", width: 1.5f), C("Sold|المباع", true), C("Returned|المرتجع", true), C("Net qty|صافي الكمية", true), C("Gross|الإجمالي", true), C("Discount|الخصم", true), C("Net sales|صافي المبيعات", true) },
                    x => new object?[] { Item(x.ItemCode, x.ItemName), x.CategoryName, x.QuantitySold, x.QuantityRefunded, x.NetQuantity, x.GrossSales, x.DiscountAmount, x.NetSales }));
                b.Tables.Add(Table("Item profitability|ربحية الأصناف", d.Items,
                    new[] { C("Item|الصنف", width: 2), C("Tax|الضريبة", true), C("Cost|التكلفة", true), C("Gross profit|مجمل الربح", true), C("Margin|الهامش", true) },
                    x => new object?[] { Item(x.ItemCode, x.ItemName), x.TaxAmount, x.TotalCost, x.GrossProfit, P(x.ProfitMarginPercentage) }));
                b.Notes.Add($"{T("Item count|عدد الأصناف")}: {d.TotalItemsCount} · {T("Gross sales|إجمالي المبيعات")}: {V(d.TotalGrossSales)} · {T("Discounts|الخصومات")}: {V(d.TotalDiscounts)} · {T("Total cost|إجمالي التكلفة")}: {V(d.TotalCost)} {currency}");
                break;
            case PosCategorySalesReportDto d:
                b.Metrics = new() { M("Net sales|صافي المبيعات", d.TotalNetSales, true), M("Quantity sold|الكمية المباعة", d.TotalQuantitySold), M("Cost|التكلفة", d.TotalCost, true), M("Gross profit|مجمل الربح", d.TotalGrossProfit, true) };
                b.Tables.Add(Table("Category sales|مبيعات الفئات", d.Categories,
                    new[] { C("Category|الفئة", width: 2), C("Items|الأصناف", true), C("Quantity|الكمية", true), C("Gross|الإجمالي", true), C("Discount|الخصم", true), C("Tax|الضريبة", true), C("Net|الصافي", true) },
                    x => new object?[] { Item(x.CategoryCode, text.Name(x.CategoryNameAr, x.CategoryNameEn)), x.ItemsCount, x.QuantitySold, x.GrossSales, x.DiscountAmount, x.TaxAmount, x.NetSales }));
                b.Tables.Add(Table("Category profitability|ربحية الفئات", d.Categories,
                    new[] { C("Category|الفئة", width: 2), C("Cost|التكلفة", true), C("Gross profit|مجمل الربح", true), C("Sales share|حصة المبيعات", true) },
                    x => new object?[] { Item(x.CategoryCode, text.Name(x.CategoryNameAr, x.CategoryNameEn)), x.TotalCost, x.GrossProfit, P(x.PercentageOfTotalSales) }));
                b.Notes.Add(T("Categories|الفئات") + ": " + d.TotalCategoriesCount);
                break;
            case PosVoidsAndRefundsReportDto d:
                b.Metrics = new() { M("Void amount|قيمة الإلغاءات", d.TotalVoidAmount, true), M("Void count|عدد الإلغاءات", d.TotalVoidCount), M("Refund amount|قيمة المرتجعات", d.TotalRefundAmount, true), M("Refund count|عدد المرتجعات", d.TotalRefundCount) };
                b.Tables.Add(Table("Voided items|الأصناف الملغاة", d.VoidLines,
                    new[] { C("Order / line|الطلب / السطر", code: true), C("Item|الصنف", width: 2), C("Qty|الكمية", true), C("Unit price|سعر الوحدة", true), C("Amount|القيمة", true), C("Reason / approval|السبب / الاعتماد", width: 2), C("Date UTC|التاريخ UTC", code: true, width: 1.5f) },
                    x => new object?[] { $"{x.OrderNumber}\n#{x.LineNumber}", Item(x.ItemCode, x.ItemName), x.Quantity, x.UnitPrice, x.LineTotal, $"{x.VoidReason}\n{x.VoidApprovedBy}", x.VoidedAt }));
                b.Tables.Add(Table("Refund orders|طلبات الإرجاع", d.RefundOrders,
                    new[] { C("Refund / invoice|المرتجع / الفاتورة", code: true, width: 1.8f), C("Original order / invoice|الطلب / الفاتورة الأصلية", code: true, width: 1.8f), C("Cashier|الكاشير"), C("Amount|القيمة", true), C("First payment method|طريقة الدفع الأولى"), C("Reason|السبب", width: 2), C("Date UTC|التاريخ UTC", code: true, width: 1.5f) },
                    x => new object?[] { $"{x.RefundOrderNumber}\n{x.RefundInvoiceNumber}", $"{x.OriginalOrderNumber}\n{x.OriginalInvoiceNumber}", x.RefundUser, x.RefundAmount, Tender(x.PaymentMethodName), x.RefundReason, x.RefundDate }));
                break;
            case PosDiscountsReportDto d:
                b.Metrics = new() { M("Discount amount|قيمة الخصومات", d.TotalDiscountAmount, true), M("Discounted orders|طلبات مخفّضة", d.TotalOrdersWithDiscount), M("Average discount|متوسط الخصم", d.AverageDiscountPerOrder, true), M("Reasons|الأسباب", d.DiscountsByReason.Count) };
                b.Tables.Add(Table("Discount details|تفاصيل الخصومات", d.DiscountDetails,
                    new[] { C("Order / invoice|الطلب / الفاتورة", code: true, width: 1.5f), C("Cashier / date|الكاشير / التاريخ", width: 1.5f), C("Scope / item|النطاق / الصنف", width: 1.5f), C("Reason|السبب", width: 1.5f), C("Discount|الخصم", true), C("Rate|النسبة", true), C("Subtotal|قبل الخصم", true), C("Total|الإجمالي", true) },
                    x => new object?[] { $"{x.OrderNumber}\n{x.InvoiceNumber}", $"{x.CashierUsername}\n{V(x.OrderDate)}", $"{Scope(x.Scope)}\n{x.ItemName}", x.DiscountReason, x.DiscountAmount, P(x.DiscountPercent), x.SubtotalAmount, x.TotalAmount }));
                b.Tables.Add(Table("Discounts by reason|الخصومات حسب السبب", d.DiscountsByReason, new[] { C("Reason|السبب", width: 3), C("Count|العدد", true), C("Amount|القيمة", true) }, x => new object?[] { x.Reason, x.Count, x.TotalAmount }));
                b.Tables.Add(Table("Discounts by cashier|الخصومات حسب الكاشير", d.DiscountsByCashier, new[] { C("Cashier|الكاشير", width: 3), C("Count|العدد", true), C("Amount|القيمة", true) }, x => new object?[] { x.CashierUsername, x.Count, x.TotalAmount }));
                break;
            case PosCashDrawerVarianceReportDto d:
                b.Metrics = new() { M("Net cash variance|صافي فرق النقد", d.NetVarianceAmount, true), M("Shortage|العجز", d.TotalShortageAmount, true), M("Overage|الزيادة", d.TotalOverageAmount, true), M("Audited shifts|الورديات المعدودة", d.TotalShiftsAudited) };
                b.Tables.Add(Table("Cash reconciliation|مطابقة النقد", d.Shifts,
                    new[] { C("Shift / till|الوردية / الصندوق", width: 1.7f), C("Cashier|الكاشير", width: 1.3f), C("Status|الحالة"), C("Opening|الافتتاحي", true), C("Expected|المتوقع", true), C("Counted|المعدود", true), C("Variance|الفرق", true) },
                    x => new object?[] { $"{x.ShiftNumber}\n{x.TillName ?? V(x.TillId)}", x.CashierUsername, State(x.Status), x.OpeningFloat, x.ExpectedCashInDrawer, x.CountedCashAmount, x.VarianceAmount }));
                b.Tables.Add(Table("Cash activity|حركة النقد", d.Shifts,
                    new[] { C("Shift|الوردية", code: true, width: 1.5f), C("Cash sales|مبيعات النقد", true), C("Cash refunds|مرتجعات النقد", true), C("Float in|إيداعات", true), C("Cash drop|توريدات", true), C("Pay out|مصروفات", true), C("Tips paid|إكراميات مدفوعة", true) },
                    x => new object?[] { x.ShiftNumber, x.TotalCashSales, x.TotalCashRefunds, x.TotalFloatIn, x.TotalCashDrop, x.TotalPayOut, x.TotalTipPayout }));
                b.Tables.Add(Table("Card reconciliation & audit|مطابقة البطاقات والتدقيق", d.Shifts,
                    new[] { C("Shift|الوردية", code: true), C("Expected card|بطاقات متوقعة", true), C("Counted card|بطاقات معدودة", true), C("Card variance|فرق البطاقات", true), C("Opened / closed UTC|الفتح / الإغلاق UTC", width: 1.8f, code: true), C("Audit|التدقيق", width: 2) },
                    x => new object?[] { x.ShiftNumber, x.ExpectedCardAmount, x.CountedCardAmount, x.CardVarianceAmount, $"{V(x.OpenedAt)}\n{V(x.ClosedAt)}", $"{V(x.IsAudited)} - {x.AuditedBy}\n{x.AuditNotes}" }));
                b.Notes.Add($"{T("Expected cash|النقد المتوقع")}: {V(d.TotalExpectedCash)} · {T("Counted cash|النقد المعدود")}: {V(d.TotalCountedCash)} · {T("Net card variance|صافي فرق البطاقات")}: {V(d.NetCardVarianceAmount)} {currency}");
                break;
            case PosCashMovementReportDto d:
                b.Metrics = new() { M("Net movements|صافي الحركات", d.NetCashMovement, true), M("Float in|الإيداعات", d.TotalFloatIn, true), M("Cash drop|التوريدات", d.TotalCashDrop, true), M("Pay out|المصروفات", d.TotalPayOut, true) };
                b.Tables.Add(Table("Cash movements|الحركات النقدية", d.Movements,
                    new[] { C("Voucher / shift|السند / الوردية", code: true, width: 1.7f), C("Movement / voucher type|نوع الحركة / السند", width: 1.7f), C("Amount|القيمة", true), C("Reason / beneficiary|السبب / المستفيد", width: 2), C("User / approval|المستخدم / الاعتماد", width: 1.5f), C("Date UTC|التاريخ UTC", code: true, width: 1.5f) },
                    x => new object?[] { $"{x.VoucherNumber ?? V(x.Id)}\n{x.ShiftNumber}", $"{text.Name(x.MovementTypeNameAr, x.MovementTypeNameEn)}\n{(x.VoucherType == PosVoucherType.Receipt ? T("Receipt|قبض") : T("Payment|صرف"))}", x.Amount, $"{x.Reason}\n{x.BeneficiaryName}", $"{x.CreationUser}\n{x.ApprovedBy}", x.CreationDate }));
                b.Notes.Add(T("Tip payouts|الإكراميات المدفوعة") + ": " + V(d.TotalTipPayout) + " " + currency);
                break;
            case PosHourlySalesReportDto d:
                b.Metrics = new() { M("Sales|المبيعات", d.TotalDaySales, true), M("Orders|الطلبات", d.TotalOrders), M("Peak sales|مبيعات الذروة", d.PeakHourSales, true), M("Peak hour|ساعة الذروة", d.PeakHourFormatted) };
                b.Tables.Add(Table("Sales by hour|المبيعات حسب الساعة", d.Hours,
                    new[] { C("Hour|الساعة", code: true, width: 1.8f), C("Orders|الطلبات", true), C("Sales|المبيعات", true), C("Average invoice|متوسط الفاتورة", true), C("Covers|الأشخاص", true), C("Share|النسبة", true) },
                    x => new object?[] { x.HourFormatted, x.OrderCount, x.TotalSales, x.AverageTicket, x.Covers, P(x.PercentageOfTotal) }));
                break;
            case PosFloorTableSalesReportDto d:
                b.Metrics = new() { M("Table sales|مبيعات الطاولات", d.TotalTableSales, true), M("Orders|الطلبات", d.TotalTableOrders), M("Covers|الأشخاص", d.TotalCovers), M("Spend per cover|الإنفاق للشخص", d.AverageSpendPerCover, true) };
                b.Tables.Add(Table("Floor & table sales|مبيعات الطوابق والطاولات", d.Tables,
                    new[] { C("Floor / table|الطابق / الطاولة", width: 2), C("Capacity|السعة", true), C("Orders|الطلبات", true), C("Covers|الأشخاص", true), C("Sales|المبيعات", true), C("Average invoice|متوسط الفاتورة", true), C("Per cover|للشخص", true) },
                    x => new object?[] { $"{x.FloorName}\n{x.TableNumber} {x.TableName}", x.Capacity, x.OrderCount, x.CoversCount, x.TotalSales, x.AverageTicket, x.AveragePerCover }));
                break;
            case PosOrderTypeSalesReportDto d:
                b.Metrics = new() { M("Sales|المبيعات", d.TotalSales, true), M("Orders|الطلبات", d.TotalOrdersCount), M("Order types|أنواع الطلبات", d.OrderTypes.Count), M("Average order|متوسط الطلب", d.TotalOrdersCount == 0 ? 0 : d.TotalSales / d.TotalOrdersCount, true) };
                b.Tables.Add(Table("Sales by order type|المبيعات حسب نوع الطلب", d.OrderTypes,
                    new[] { C("Type|النوع", width: 2), C("Orders|الطلبات", true), C("Subtotal|قبل الخصم", true), C("Discount|الخصم", true), C("Tax|الضريبة", true), C("Delivery|التوصيل", true), C("Total|الإجمالي", true) },
                    x => new object?[] { text.Name(x.OrderTypeNameAr, x.OrderTypeNameEn), x.OrderCount, x.SubtotalAmount, x.DiscountAmount, x.TaxAmount, x.DeliveryFee, x.TotalAmount }));
                b.Tables.Add(Table("Order type averages|متوسطات أنواع الطلبات", d.OrderTypes,
                    new[] { C("Type|النوع", width: 2), C("Average order|متوسط الطلب", true), C("Share|النسبة", true) },
                    x => new object?[] { text.Name(x.OrderTypeNameAr, x.OrderTypeNameEn), x.AverageOrderValue, P(x.Percentage) }));
                break;
            case PosCustomerSalesReportDto d:
                b.Metrics = new() { M("Customer sales|مبيعات العملاء", d.TotalCustomerSales, true), M("Customers|العملاء", d.TotalCustomers), M("Paid|المدفوع", d.Customers.Sum(x => x.PaidAmount), true), M("Remaining|المتبقي", d.Customers.Sum(x => x.RemainingAmount), true) };
                string Customer(PosCustomerSalesItemDto x) => x.CustomerCode == "WALK-IN" ? T("Walk-in customer|زبون نقدي / عام") : x.CustomerName;
                b.Tables.Add(Table("Customer sales & balances|مبيعات وأرصدة العملاء", d.Customers,
                    new[] { C("Customer|العميل", width: 2.5f), C("Orders|الطلبات", true), C("Sales|المبيعات", true), C("Discount|الخصم", true), C("Paid|المدفوع", true), C("Remaining|المتبقي", true) },
                    x => new object?[] { Item(x.CustomerCode, Customer(x)), x.OrderCount, x.TotalSales, x.DiscountAmount, x.PaidAmount, x.RemainingAmount }));
                b.Tables.Add(Table("Customer visits|زيارات العملاء", d.Customers,
                    new[] { C("Customer|العميل", width: 2), C("Phone|الهاتف", code: true), C("Average invoice|متوسط الفاتورة", true), C("Last visit UTC|آخر زيارة UTC", code: true, width: 1.5f) },
                    x => new object?[] { Item(x.CustomerCode, Customer(x)), x.PhoneNumber, x.AverageTicket, x.LastVisitDate }));
                break;
            case PosTaxSummaryReportDto d:
                b.Metrics = new() { M("Net tax payable|صافي الضريبة المستحقة", d.NetTaxPayable, true), M("Tax collected|الضريبة المحصّلة", d.TotalTaxCollected, true), M("Refund tax|ضريبة المرتجعات", d.TotalRefundTax, true), M("Taxable sales|مبيعات خاضعة", d.TotalTaxableSales, true) };
                b.Tables.Add(Table("Tax rates on sales|نسب الضريبة على المبيعات", d.TaxRatesBreakdown,
                    new[] { C("Tax code|رمز الضريبة", code: true, width: 2), C("Rate|النسبة", true), C("Taxable amount|المبلغ الخاضع", true), C("Tax amount|قيمة الضريبة", true), C("Inclusive|شاملة") },
                    x => new object?[] { x.TaxRateCode, P(x.TaxPercent), x.TaxableAmount, x.TaxAmount, x.IsInclusive }));
                b.Tables.Add(Table("Sales tax bases|تصنيف المبيعات ضريبيًا", new[] { ("Gross sales|إجمالي المبيعات", d.TotalGrossSales), ("Taxable sales|مبيعات خاضعة", d.TotalTaxableSales), ("Zero-rated sales|مبيعات بنسبة صفر", d.TotalZeroRatedSales), ("Exempt sales|مبيعات معفاة", d.TotalExemptSales) },
                    new[] { C("Classification|التصنيف", width: 3), C("Amount|القيمة", true) }, x => new object?[] { T(x.Item1), x.Item2 }));
                b.Notes.Add(T("The rate breakdown reflects sales taxes; refund tax is shown in the summary.|يعرض تفصيل النسب ضريبة المبيعات؛ وتظهر ضريبة المرتجعات في الملخص."));
                break;
            case PosOpenTablesLiveReportDto d:
                b.Metrics = new() { M("Active sales|المبيعات الحالية", d.ActiveDiningSalesTotal, true), M("Tables|الطاولات", d.TotalTables), M("Occupied|المشغولة", d.OccupiedTablesCount), M("Occupancy|الإشغال", P(d.OccupancyRatePercent)) };
                b.Tables.Add(Table("Live table status|حالة الطاولات المباشرة", d.OpenTables,
                    new[] { C("Table|الطاولة", width: 1.5f), C("Floor|الطابق", width: 1.5f), C("Status|الحالة"), C("Order|الطلب", code: true), C("Covers|الأشخاص", true), C("Minutes|الدقائق", true), C("Current total|القيمة الحالية", true) },
                    x => new object?[] { $"{x.TableNumber} {x.TableName}", x.FloorName, State(x.Status.ToString()), x.OrderNumber, x.Covers, x.DurationMinutes, x.CurrentTotalAmount }));
                b.Tables.Add(Table("Table capacity & service|سعة الطاولات والخدمة", d.OpenTables,
                    new[] { C("Table|الطاولة", width: 1.5f), C("Capacity|السعة", true), C("Waiter / cashier|النادل / الكاشير", width: 2), C("Opened UTC|وقت الفتح UTC", code: true, width: 1.5f) },
                    x => new object?[] { $"{x.FloorName} / {x.TableNumber}", x.Capacity, x.WaiterOrCashier, x.OpenedAt }));
                b.Notes.Add($"{T("Available|المتاحة")}: {d.AvailableTablesCount} · {T("Billed|المفوتَرة")}: {d.BilledTablesCount}");
                break;
            case PosKitchenPrepTimeReportDto d:
                b.Metrics = new() { M("Items analyzed|الأصناف المحللة", d.TotalItemsAnalyzed), M("Average preparation|متوسط التحضير", N(d.OverallAveragePrepMinutes), unit: T("min|دقيقة")), M("Delayed items|الأصناف المتأخرة", d.TotalDelayedItemsCount), M("Delay threshold|حد التأخير", d.DelayedOrdersThresholdMinutes, unit: T("min|دقيقة")) };
                b.Tables.Add(Table("Preparation stations|محطات التحضير", d.Stations,
                    new[] { C("Station|المحطة", width: 2), C("Items|الأصناف", true), C("Average min|متوسط الدقائق", true), C("Minimum min|أقل دقائق", true), C("Maximum min|أعلى دقائق", true), C("Delayed|المتأخر", true) },
                    x => new object?[] { Station(x.PrepStation), x.ItemsCount, N(x.AveragePrepMinutes), N(x.MinPrepMinutes), N(x.MaxPrepMinutes), x.DelayedCount }));
                b.Tables.Add(Table("Slowest items|الأصناف الأبطأ", d.SlowestItems,
                    new[] { C("Item|الصنف", width: 2), C("Station|المحطة", width: 2), C("Items|العدد", true), C("Average min|متوسط الدقائق", true) },
                    x => new object?[] { Item(x.ItemCode, x.ItemName), Station(x.PrepStation), x.ItemsCount, N(x.AveragePrepMinutes) }));
                break;
            default: throw new NotSupportedException("The POS report presentation mapping is missing.");
        }
        return b;
    }
}
