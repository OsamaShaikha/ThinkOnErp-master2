namespace ThinkOnErp.Application.DTOs.SysCode;

/// <summary>
/// نموذج بيانات خفيف وموحّد لعناصر القوائم المنسدلة وأكواد النظام (Lookup / Dropdown Item)
/// </summary>
public class SysCodeLookupDto
{
    /// <summary>
    /// رقم الكود الفرعي (CODE_MNR) المستخدم كقيمة في قاعدة البيانات (مثل 1 للكاش، 2 للآجل)
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// القيمة الإنجليزية / الرمزية الثابتة (CODE_VALUE) مثل "Cash", "Credit", "Customer"
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// الاسم والوصف باللغة العربية
    /// </summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>
    /// الاسم والوصف باللغة الإنجليزية
    /// </summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>
    /// الاسم المحلي المختار حسب لغة الطلب الحالية (أو العربية كافتراضي)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// حالة التفعيل (1 مفعل، 0 معطل)
    /// </summary>
    public int IsActive { get; set; } = 1;
}
