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
}
