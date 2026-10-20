namespace ThinkOnErp.Domain.Constants;

/// <summary>
/// Central registry of well-known SYS_SETTINGS codes.
/// Values stored in SETTING_VALUE must reference SYS_CODE (numeric CodeMnr or entity IDs).
/// Text strings must NEVER be stored as values.
/// </summary>
public static class SysSettingKeys
{
    /// <summary>
    /// POS Stock Deduction Mode: References SYS_CODE (Mgr = 35):
    /// 1 = RealTime (خصم فوري ولحظي مع كل فاتورة)
    /// 2 = Consolidated (خصم تجميعي عند إغلاق الوردية بتقرير Z)
    /// 3 = None (بدون خصم مخزني آلي)
    /// </summary>
    public const int PosStockDeductionMode = 50;

    /// <summary>
    /// POS Default Warehouse ID (References numeric Warehouse ID, e.g., 61)
    /// </summary>
    public const int PosDefaultWarehouseId = 51;

    /// <summary>
    /// POS Shift Closing Mode: References SYS_CODE (Mgr = 40):
    /// 1 = EnforceBlindClose (إلزام بالإغلاق الأعمى - حجب المتوقع)
    /// 2 = StandardOpenClose (السماح بالإغلاق العادي وإظهار المتوقع)
    /// </summary>
    public const int PosShiftClosingMode = 52;

    /// <summary>
    /// POS Default Cash / Till Account (References GL_ACCOUNT.ACCOUNT_CODE, e.g., 111102 - صندوق نقطة البيع)
    /// </summary>
    public const int PosDefaultCashAccount = 53;

    /// <summary>
    /// POS Default Expense Account for Petty Cash Pay-Outs (References GL_ACCOUNT.ACCOUNT_CODE, e.g., 525101 - مشتريات ومصروفات نثرية)
    /// </summary>
    public const int PosDefaultExpenseAccount = 54;

    /// <summary>
    /// POS Default Receipt Account for Float-In / Misc Receipts (References GL_ACCOUNT.ACCOUNT_CODE, e.g., 421101 - إيرادات أخرى متنوعة)
    /// </summary>
    public const int PosDefaultReceiptAccount = 55;
}
