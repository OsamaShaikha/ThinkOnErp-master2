"""Read the current HR controllers and emit a dated, source-linked API inventory."""
import json
import argparse
from datetime import date
import re
from pathlib import Path
from collections import Counter

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--date", default=date.today().isoformat())
parser.add_argument("--check", action="store_true", help="Verify existing dated snapshots without changing files")
args = parser.parse_args()
DATE = date.fromisoformat(args.date).isoformat()
def emit(path, contents):
    if args.check:
        assert path.exists() and path.read_text(encoding="utf-8") == contents, f"Snapshot out of date: {path.name}"
    else:
        path.write_text(contents, encoding="utf-8")

def split_parameters(value):
    result, start, depth = [], 0, 0
    for i, char in enumerate(value):
        if char in "([<": depth += 1
        elif char in ")]>": depth -= 1
        elif char == "," and depth == 0:
            result.append(value[start:i]); start = i + 1
    result.append(value[start:])
    return result
OUT = ROOT / "docs"
labels = {
    "GetPayrollPostingConfiguration": "قراءة إعدادات حساب ترحيل الرواتب", "SavePayrollPostingConfiguration": "تهيئة حسابات الرواتب عبر صلاحية Accounting",
    "GetEmployees": "قائمة الموظفين والبحث والترقيم", "ExportEmployees": "تصدير الموظفين Excel",
    "DownloadTemplate": "قالب استيراد الموظفين", "ValidateImport": "فحص Excel قبل الاستيراد",
    "ImportEmployees": "استيراد الموظفين أو تحديثهم", "GetEmployeeByCode": "تفاصيل الموظف",
    "GetMyTeam": "الفريق المباشر للحساب الحالي ضمن منح الفروع وبحقول تنظيمية محدودة", "GetMyEmployeeLink": "الموظف المرتبط بالحساب الحالي", "LinkEmployeeUser": "ربط أو فك ربط الحساب",
    "UpdateEmployeeBank": "تعديل مجموعة البنك فقط مع حفظ بقية الملف", "UpdateEmployeeIdentity": "تعديل مجموعة الهوية فقط مع حفظ البنك والعمل", "UpdateEmployeeWork": "تعديل ملف العمل مع حفظ الحقول الخاصة من المصدر", "CreateEmployee": "إنشاء موظف", "UpdateEmployee": "تعديل موظف", "DeleteEmployee": "تعطيل الموظف وإنهاء حالته",
    "AddDependent": "إضافة معال", "DeleteDependent": "حذف معال", "AssignSalaryStructure": "تعيين تعريف راتب",
    "GetSalaryStructure": "تعريف الراتب النشط بتاريخ معين", "CheckIn": "تسجيل دخول GPS", "CheckOut": "تسجيل خروج GPS",
    "GetTodayStatus": "حالة حضور اليوم", "GetNearbyBranches": "الفروع الأقرب للإحداثيات",
    "SubmitCorrection": "طلب تصحيح حضور", "ProcessCorrection": "اعتماد أو رفض تصحيح الحضور", "GetCorrections": "قائمة طلبات التصحيح",
    "GetLeaveTypes": "أنواع الإجازات", "GetLeaveTypeByCode": "تفاصيل نوع إجازة", "CreateLeaveType": "إنشاء نوع إجازة",
    "UpdateLeaveType": "تعديل نوع إجازة", "DeleteLeaveType": "تعطيل نوع إجازة", "GetLeaveRequests": "قائمة طلبات الإجازة",
    "SubmitLeaveRequest": "تقديم طلب إجازة", "ProcessLeaveRequest": "اعتماد أو رفض إجازة", "GetEmployeeBalances": "أرصدة إجازات الموظف",
    "GrantOrAdjustBalance": "منح أو تصحيح رصيد إجازة", "RunMonthlyAccrual": "تنفيذ استحقاق شهري يدوي",
    "RunYearlyAllocation": "تنفيذ توزيع سنوي", "RunYearEndRollover": "ترحيل الرصيد بين سنتين",
    "GetLeavePolicies": "سياسات الإجازات", "CreateOrUpdateLeavePolicy": "حفظ سياسة الإجازة حسب النوع",
    "RunDailyAccrual": "استدراك الاستحقاق اليومي حتى تاريخ محدد",
    "CancelLeaveRequest": "إلغاء طلب إجازة وإرجاع الاستخدام المعتمد",
    "CreateLoan": "طلب قرض وإنشاء أقساطه", "ApproveLoan": "اعتماد طلب القرض في HR دون تفعيل أو صرف", "GetLoans": "قائمة القروض",
    "CreateAdvance": "طلب سلفة", "ApproveAdvance": "اعتماد طلب السلفة في HR دون صرف", "GetAdvances": "قائمة السلف",
    "CalculatePayroll": "حساب مسير راتب جديد لفرع", "ApprovePayroll": "اعتماد HR لنسخة المسير دون تسوية مالية", "CorrectPayroll": "إنشاء نسخة تصحيح جديدة بعد عكس أو سحب النسخة السابقة",
    "PostToGl": "مسار ترحيل HR قديم موقوف409", "ReverseGl": "مسار عكس HR قديم موقوف409", "GetPayrollRun": "نتيجة مسير بالمعرف",
    "ExportBankFile": "مسار ملف دفع HR قديم موقوف409", "PreviewBankExport": "مسار معاينة ملف دفع HR قديم موقوف409",
    "GetCalculationExplanation": "شرح احتساب بند الموظف", "GetPeriods": "قائمة فترات الرواتب", "CreatePeriod": "إنشاء فترة رواتب",
    "ClosePeriod": "إقفال فترة رواتب بعد اكتمال المسيرات والتعديلات", "ReopenPeriod": "إعادة فتح فترة رواتب مغلقة بسبب موثق وبترتيب زمني عكسي", "ComparePayroll": "عرض فروقات المسير الحالي مقارنة بمسير سابق أو خط أساس وتفاصيل الموظفين",
    "CreateAdjustment": "إنشاء تعديل راتب بحالة APPROVED حاليًا", "ApproveAdjustment": "اعتماد تعديل", "RejectAdjustment": "رفض تعديل",
    "GetAdjustments": "قائمة التعديلات", "GetAdjustmentById": "تفاصيل تعديل", "DeleteAdjustment": "حذف تعديل",
    "GetOvertimeRules": "قواعد الإضافي الفعالة بتاريخ محدد", "AddOvertimeRule": "إضافة قاعدة إضافي قابلة للتهيئة للشركة",
    "GetProrationPolicies": "سياسات تجزئة الراتب", "AddProrationPolicy": "إضافة سياسة تجزئة",
    "GetSSCPolicies": "سياسات الضمان", "AddSSCPolicy": "إضافة سياسة ضمان", "GetTaxPolicies": "سياسات الضريبة",
    "AddTaxPolicy": "إضافة سياسة ضريبة وشرائحها", "GetDeductionPolicies": "سياسات الاقتطاع", "AddDeductionPolicy": "إضافة سياسة اقتطاع",
    "CreateCalendar": "إنشاء تقويم وأيام عمل", "GetCalendars": "قائمة التقاويم", "GetCalendarById": "تفاصيل تقويم",
    "CreateHoliday": "إضافة عطلة", "GetHolidays": "قائمة العطل", "CountWorkingDays": "حساب عدد أيام العمل",
    "GetComponents": "قائمة مكونات الرواتب", "CreateComponent": "إنشاء مكون راتب", "Get": "إعداد حسابات الرواتب للفرع",
    "Save": "حفظ حسابات الرواتب للفرع",
}

labels.update({
    "GetEffectiveShift": "الشفت الفعال بتاريخ العمل", "PreviewUtc": "معاينة حدود الشفت UTC", "MatchPunchShift": "مطابقة لحظة بصمة بالشفت",
    "ReverseManualBalanceMovement": "عكس تعديل رصيد يدوي بسبب مدقق وحركة دفتر مرتبطة",
    "ClassifyDay": "معاينة تصنيف اليوم", "SaveDayClassification": "حفظ التصنيف تحت القفل المالي",
    "Initialize": "تهيئة منطقة توقيت الشركة مرة واحدة", "GetMovements": "دفتر حركات رصيد الإجازة",
    "CreateYearEndCloseDraft": "إنشاء مسودة إقفال دورة الإجازة", "GetYearEndClose": "تفاصيل إقفال دورة الإجازة",
    "PostYearEndClose": "اعتماد إقفال دورة الإجازة", "UnpostYearEndClose": "عكس إقفال دورة الإجازة",
    "GetLeaveSettings": "قراءة إعدادات الإجازات", "UpdateLeaveSettings": "تحديث إعدادات الإجازات",
    "CreateBalanceMigrationDraft": "إنشاء مسودة توزيع أرصدة الإجازات على دورات جديدة",
    "GetBalanceMigration": "عرض مسودة أو نتيجة ترحيل دورة الإجازات",
    "PostBalanceMigration": "ترحيل توزيع الأرصدة وتحديث سنة الشركة بعد المراجعة",
    "ReverseBalanceMigration": "عكس ترحيل دورة الإجازات مع التحقق من التوابع",
    "CreateLeaveCashoutDraft": "إنشاء مسودة معاينة تسوية الإجازة النقدية",
    "GetLeaveCashoutDraft": "عرض تفاصيل مسودة تسوية الإجازة النقدية",
    "PostLeaveCashout": "إنشاء تعديلات راتب نقد الإجازة للمراجعة والاعتماد",
    "ReverseLeaveCashout": "عكس تسوية نقد الإجازة قبل احتساب المسير مع سبب مدقق",
    "CreateAdjustment": "إنشاء تعديل راتب PENDING", "CreateOvertimeAdjustment": "إرسال إضافي حضور من يوم وساعات موثوقة للمراجعة", "PostAdjustment": "اعتماد/Post تعديل PENDING إلى مدخل الراتب",
    "UnpostAdjustment": "عكس ترحيل تعديل مع فحص التوابع", "CancelPayroll": "إلغاء مسير محسوب غير معتمد",
    "Ingest": "حفظ دليل جهاز موثوق دون معالجة مالية", "Pending": "طابور أدلة الحضور المعلقة",
    "Process": "معالجة دليل خام إلى اليوم المالي", "Batch": "معالجة دفعة أدلة وإظهار فشل كل حدث",
    "Ignore": "تجاهل دليل معلق بسبب موثق", "Generate": "توليد الجدول اليومي مع حماية اليدوي",
    "CalculateSettlement": "احتساب مسودة تسوية نهاية الخدمة", "ApproveSettlement": "اعتماد تسوية نهاية الخدمة",
    "CancelSettlement": "إلغاء تسوية نهاية الخدمة", "GetById": "عرض التفاصيل بالمعرف",
    "GetByEmployeeCode": "عرض التسوية بكود الموظف", "GetList": "قائمة السجلات",
    "SubmitRequest": "تقديم طلب إذن مغادرة قصيرة", "ApproveRequest": "اعتماد طلب إذن مغادرة قصيرة",
    "RejectRequest": "رفض طلب إذن مغادرة قصيرة", "CancelRequest": "إلغاء أو عكس إذن مغادرة قصيرة",
    "GetRequests": "قائمة طلبات إذن المغادرة", "GetPolicy": "قراءة سياسة المغادرات القصيرة",
    "SavePolicy": "حفظ سياسة المغادرات القصيرة",
    "SkipInstallment": "تأجيل قسط قرض وإعادة الجدولة",
    "CancelLoan": "إلغاء طلب أو موافقة قرض غير مفعل",
    "CancelAdvance": "إلغاء طلب أو موافقة سلفة غير مفعلة",
    "SettleLoanEarly": "تسوية مبكرة لقرض نشط خارج المسير",
    "SettleAdvanceEarly": "تسوية مبكرة لسلفة نشطة خارج المسير",
})
resource_labels = {"BranchAttendanceLocationsController": "موقع فرع الحضور", "ShiftsController": "تعريفات الشفت", "ShiftAssignmentsController": "إسنادات الشفت", "ShiftDaysController": "الجدول اليومي"}
verbs = {"List": "قائمة", "Get": "تفاصيل", "Create": "إنشاء", "Update": "تعديل", "Delete": "حذف"}
def purpose(controller, action):
    if controller == "ShortLeavesController":
        return {
            "SubmitRequest": "تقديم طلب مغادرة قصيرة (تأخر، خروج مبكر، خروج وعودة) والتحقق من الحصة",
            "ApproveRequest": "اعتماد طلب المغادرة وحساب أثر الراتب أو رصيد الإجازة حسب سياسة الشركة",
            "RejectRequest": "رفض طلب المغادرة مع توثيق السبب والفاعل",
            "CancelRequest": "إلغاء طلب المغادرة وإرجاع الأثر المسجل بسبب موثق",
            "GetById": "تفاصيل طلب المغادرة بالمعرف",
            "GetRequests": "قائمة طلبات المغادرة حسب الشركة والفرع والحالة والتاريخ",
            "GetPolicy": "قراءة سياسة حصص المغادرات القصيرة بالشركة وإجراءات التجاوز",
            "SavePolicy": "حفظ أو تحديث سياسة المغادرات القصيرة بالشركة",
        }[action]
    if controller == "EndOfServiceController":
        return {
            "CalculateSettlement": "احتساب تسوية نهاية الخدمة ومستحقاتها واستقطاعاتها كمسودة",
            "ApproveSettlement": "اعتماد تسوية نهاية الخدمة وتحديث حالة الموظف تشغيليًا إلى منتهي الخدمة",
            "CancelSettlement": "إلغاء تسوية نهاية الخدمة وإرجاع حالة الموظف السابقة عند الإلغاء",
            "GetById": "تفاصيل تسوية نهاية الخدمة ومكوناتها بالمعرف",
            "GetByEmployeeCode": "تفاصيل أحدث تسوية نهاية خدمة لموظف محدد",
            "GetList": "قائمة تسويات نهاية الخدمة حسب الشركة والفرع والحالة",
            "SubmitPayoutRequest": "إرسال حوالة صرف تسوية نهاية الخدمة إلى صندوق Accounting للصرف",
            "GetHandoffStatus": "استعلام حالة حوالة تسوية نهاية الخدمة في صندوق Accounting",
        }[action]
    if controller == "LeavesController" and action == "RunDailyAccrual": return "تنفيذ هدف الاستحقاق اليومي القابل لإعادة المحاولة حسب سياسة الشركة"
    if controller == "LeavesController" and action == "CalculateLeaveDays": return "معاينة واحتساب أيام الإجازة التقويمية وأيام العمل وفق تقويم الشركة والرصيد المتاح"
    if controller == "LeavesController" and action == "CancelLeaveRequest": return "إلغاء طلب معلق أو معتمد؛ يعكس حركة الاستخدام الأصلية مرة واحدة"
    if controller == "LeavesController" and action == "CreateLeaveCashoutDraft": return "إعداد معاينة CASHOUT من مسودة إقفال إلى فترة راتب مفتوحة بعملة واحدة"
    if controller == "LeavesController" and action == "GetLeaveCashoutDraft": return "عرض مسودة تسوية الإجازة النقدية ولقطات أساس الراتب للمراجعة"
    if controller == "LeavesController" and action == "PostLeaveCashout": return "إنشاء تعديلات راتب PENDING تحت قفل فترة الراتب لمراجعتها واعتمادها"
    if controller == "LeavesController" and action == "ReverseLeaveCashout": return "عكس المسودة أو إبطال تعديلات غير محتسبة مع حفظ السبب"
    if controller == "LoansController" and action in ("SubmitLoanFunding", "SubmitAdvanceFunding"): return "تمويل Legacy موقوف؛ استخدم إيصال مستند Accounting"
    if controller == "LoansController" and action in ("SkipInstallment", "CancelLoan", "CancelAdvance", "SettleLoanEarly", "SettleAdvanceEarly"):
        return {
            "SkipInstallment": "تأجيل قسط قرض قائم إلى نهاية الجدولة وإضافة قسط جديد",
            "CancelLoan": "إلغاء طلب أو موافقة قرض لم يُصرف مالياً بعد مع توثيق السبب",
            "CancelAdvance": "إلغاء طلب أو موافقة سلفة لم تُصرف مالياً بعد مع توثيق السبب",
            "SettleLoanEarly": "تسوية مبكرة لقرض نشط بسداد خارجي وتحديث الأقساط والرصيد",
            "SettleAdvanceEarly": "تسوية مبكرة لسلفة نشطة بسداد خارجي وتحديث الرصيد",
        }[action]
    if controller == "AccountingHandoffController":
        return {
            "Inbox": "صندوق مستندات الرواتب المسلمة إلى Accounting",
            "Accept": "قبول مراجعة مستند التسليم",
            "Return": "إرجاع مستند التسليم بسبب موثق",
            "CreateJournalVoucher": "إنشاء مسودة قيد من مستند مقبول",
            "ReverseJournalVoucher": "عكس قيد الرواتب بعد عكس الدفعات واستعادة تسويات HR مع حفظ الأصل",
            "PostingReceipt": "تسجيل إيصال ترحيل القيد من ERP",
            "PreparePayment": "إنشاء سند صرف مسودة بطريقة دفع Accounting المهيأة وربطه بحجز ضمن صافي المستحق",
            "PaymentStatus": "عرض الدفعات المسجلة والمدفوع والمعد والمتبقي مع إيصالات Accounting ضمن صلاحية الفرع",
            "FailPayment": "فشل أو إلغاء دفعة مسودة بعد إبطال PV وإطلاق الحجز",
            "ReversePayment": "عكس دفعة مؤكدة بإثبات خارجي وقيد عكسي Accounting",
            "ConfirmPayment": "تأكيد الدفع بعد مطابقة سند ERP المرحّل ووسيلة الدفع والمرجع والإثبات",
            "ConfirmLoanFunding": "تأكيد تمويل مطابق لـPV مرحّل برقم ومرجع الحوالة والفرع والعملة والمبلغ",
            "ReverseLoanFunding": "عكس تمويل قرض أو سلفة بعد مطابقة PV المعكوس والتحقق من عدم وجود تسويات لاحقة",
            "GetPayrollPostingConfiguration": "قراءة إعدادات حسابات ترحيل الرواتب والذمم بحسب الفرع",
            "SavePayrollPostingConfiguration": "تهيئة حسابات ترحيل الرواتب والذمم من Accounting",
        }[action]
    if controller == "PayrollController" and action == "GetPayrollHandoffStatus": return "حالة مستند التسليم المرتبط بالمسير"
    if controller == "PayrollController" and action == "GetPayrollHandoffPaymentStatus": return "ملخص الدفع للمسير دون بيانات إثبات Accounting الحساسة"
    if controller == "PayrollController" and action == "WithdrawPayrollHandoff": return "سحب مستند التسليم قبل قبول Accounting"
    if controller == "PayrollController" and action == "CorrectPayroll": return "إنشاء نسخة مسير تصحيح جديدة بعد عكس أو سحب التسليم السابق"
    if controller == "PayrollPoliciesController" and action in ("GetCurrencyPrecision", "SetCurrencyPrecision", "DeleteCurrencyPrecision"):
        return {
            "GetCurrencyPrecision": "قراءة دقة التقريب المحاسبي للعملات المضبوطة لهذه الشركة",
            "SetCurrencyPrecision": "ضبط منازل دقة الرواتب لعملة الشركة مع التحقق من المجال",
            "DeleteCurrencyPrecision": "حذف تجاوز دقة الرواتب وإرجاع السلوك الافتراضي للعملة",
        }[action]
    if controller == "OrganizationController": return {"Departments": "قراءة الإدارات الفعالة المتاحة", "Positions": "قراءة المسميات الفعالة التابعة للإدارات المتاحة", "Department": "قراءة إدارة ونسخة التعديل بما فيها المعطلة", "Position": "قراءة مسمى ونسخة التعديل بما فيه المعطل", "CreateDepartment": "إنشاء إدارة في الكتالوج الحالي", "CreatePosition": "إنشاء مسمى في الكتالوج الحالي", "UpdateDepartment": "تعديل أو تعطيل إدارة مع If-Match وحماية التوابع", "UpdatePosition": "تعديل أو تعطيل مسمى مع If-Match وحماية التوابع"}[action]
    if controller in resource_labels and action in verbs: return verbs[action] + " " + resource_labels[controller]
    if controller == "AttendanceTimeSettingsController" and action == "Get": return "قراءة منطقة توقيت الشركة"
    if controller == "RawAttendanceController" and action == "Get": return "قراءة دليل خام ضمن نطاق الموظف"
    return labels[action]

def usage(controller, action, self_service):
    if controller == "AccountingHandoffController": return "ويب Accounting ضمن صلاحيات ERP"
    if controller == "PayrollController" and action in ("GetPayrollHandoffStatus", "WithdrawPayrollHandoff", "CorrectPayroll"): return "ويب HR للمتابعة والسحب والتصحيح المقيّد"
    if controller == "EmployeesController" and action == "GetMyTeam": return "ويب / موبايل المدير بمنح عرض صريحة"
    if controller == "RawAttendanceController":
        return "تكامل أجهزة موثوق / HR" if action == "Ingest" else "موبايل / ويب HR" if action == "Get" else "ويب HR / معالج مخول صريح"
    if controller == "AttendanceController" and action in ("CheckIn", "CheckOut", "GetTodayStatus", "GetNearbyBranches"): return "موبايل / ويب حضور"
    return "موبايل / ويب HR ضمن النطاق" if self_service else "ويب HR / تكامل ERP مخول"

def next_change(controller, action):
    if controller == "ShortLeavesController":
        return {
            "SubmitRequest": "تقديم طلب مغادرة قصيرة (تأخر، خروج مبكر، خروج وعودة) والتحقق من الحصة",
            "ApproveRequest": "اعتماد طلب المغادرة وحساب أثر الراتب أو رصيد الإجازة حسب سياسة الشركة",
            "RejectRequest": "رفض طلب المغادرة مع توثيق السبب والفاعل",
            "CancelRequest": "إلغاء طلب المغادرة وإرجاع الأثر المسجل بسبب موثق",
            "GetById": "تفاصيل طلب المغادرة بالمعرف",
            "GetRequests": "قائمة طلبات المغادرة حسب الشركة والفرع والحالة والتاريخ",
            "GetPolicy": "قراءة سياسة حصص المغادرات القصيرة بالشركة وإجراءات التجاوز",
            "SavePolicy": "حفظ أو تحديث سياسة المغادرات القصيرة بالشركة",
        }[action]
    if controller == "ShortLeavesController": return "PERM-01..05: طلب وموافقة وعكس المغادرات القصيرة وربطها بالحضور والرصيد والرواتب"
    if controller == "EndOfServiceController": return "EOS-01..06: احتساب نهاية الخدمة ومستحقاتها واعتمادها تشغيليًا وحوالة الصرف إلى Accounting؛ يلزم مزامنة مخطط Oracle"
    if controller == "LeavesController" and action == "ReverseManualBalanceMovement": return "LEV-19: عكس يدوي مرتبط بحركة الأصل مع سبب وحدود وتوابع؛ يلزم Oracle workflow verification ومزامنة API العميل قبل الإطلاق"
    if controller == "LeavesController" and action == "RunDailyAccrual": return "LEV-11: استدراك التاريخ والدفتر موجودان؛ ما زال يلزم مجدول يختار مخطط كل شركة وتاريخها المحلي وفحص تزامن/HTTP"
    if controller == "LeavesController" and action == "CancelLeaveRequest": return "LEV-14: كود المسار وحركة العكس موجودان؛ يلزم اختبار صلاحية self-service/الفرع، منع التكرار، وحماية التوابع على Oracle"
    if controller == "LeavesController" and action in ("CreateLeaveCashoutDraft", "GetLeaveCashoutDraft", "PostLeaveCashout", "ReverseLeaveCashout"): return "LEV-17: المسودة تنشئ تعديلات مراجعة وتربطها عملية الحساب بالمسير؛ إقفال الإجازة ينتظر اعتماد المسير. يلزم عكس بعد اعتماد المسير عبر دورة تصحيح/Accounting، واختبار Oracle multi-session والواجهة ومزامنة المخططات"
    if controller == "LoansController" and action in ("SubmitLoanFunding", "SubmitAdvanceFunding"): return "INT-01/08: المسار المباشر موقوف409؛ التمويل لا يثبت إلا بإيصال Accounting مرتبط بالتسليم"
    if controller == "AccountingHandoffController": return "INT-02..12: مستند مستقل؛ Accounting يملك القبول والقيد والدفع وإيصالات ERP، والواجهة ومزامنة العميل واختبارات HTTP/النطاق ما زالت مطلوبة"
    if controller == "PayrollController" and action in ("GetPayrollHandoffStatus", "WithdrawPayrollHandoff"): return "INT-04/05: الحالة محدودة؛ السحب مسبب قبل القبول فقط، والقبول وما بعده بملكية Accounting"
    if controller == "PayrollController" and action in ("ClosePeriod", "ReopenPeriod"): return "PAY-10: إقفال وإعادة فتح الفترة؛ يتطلب اكتمال المسيرات والتعديلات وعدم إغلاق فترات لاحقة"
    if controller == "PayrollController" and action == "ComparePayroll": return "PAY-16: عرض فروقات المسير وخط الأساس وأسباب تغير الموظفين قبل الاعتماد"
    if controller == "PayrollController" and action == "CorrectPayroll": return "INT-11: يتطلب سحباً/إرجاعاً قبل القبول أو عكس Accounting للقيد والدفعات؛ إعداد الفترة مفتوحة واختبار HTTP/التزامن ما زال مطلوباً"
    if controller == "PayrollController" and action in ("PostToGl", "ReverseGl", "ExportBankFile", "PreviewBankExport"): return "INT-01: واجهة قديمة متوقفة409/HR_ACCOUNTING_HANDOFF_REQUIRED بلا استدعاء مالي؛ Accounting يملك القيد والعكس والدفع عبر مستند مقبول INT-02..14؛ مسار التسليم الجديد لم يُنفذ بعد"
    if controller == "PayrollPostingConfigurationsController" and action == "Save": return "INT-01: كتابة الإعداد المحاسبي من HR متوقفة409؛ ملكية الإعداد المستهدف Accounting ضمن INT-06/12، والقراءة الحالية Legacy للتدقيق"
    if controller == "PayrollController" and action == "ApprovePayroll": return "INT-01: اعتماد HR ولقطة فقط دون سداد قرض/سلفة أو قيد/دفع؛ الحجز والإيصالات وتسوية Accounting ضمن INT-09/10"
    if controller == "LoansController" and action in ("ApproveLoan", "ApproveAdvance"): return "INT-01/08: APPROVED لموافقة HR فقط، دون تفعيل/صرف؛ PENDING فقط وإعادة نفس الاعتماد بلا تغيير المنفذ؛ التمويل المؤكد والتفعيل ضمن INT-08، وLegacy يحتاج INT-13"
    if controller == "LoansController" and action in ("SkipInstallment", "CancelLoan", "CancelAdvance", "SettleLoanEarly", "SettleAdvanceEarly"): return "PAY-11: دورة حياة القروض والسلف (تأجيل/إلغاء/تسوية مبكرة)؛ التحقق من عدم وجود حجز مسير قائم واختبارات Oracle"
    if controller == "EmployeesController" and action == "UpdateEmployeeBank": return "EMP-04: edit العام مع edit-bank فقط، جميع حقول المجموعة مطلوبة وnull مسح صريح؛ لا حقل هوية أو عمل؛ الرد حسب حقوق العرض"
    if controller == "EmployeesController" and action == "UpdateEmployeeIdentity": return "EMP-04: edit العام مع edit-identity فقط، مجموعة هوية كاملة مطلوبة؛ بقية الملف من الصف المقفول والرد حسب حقوق العرض"
    if controller == "EmployeesController" and action in ("GetEmployees", "GetEmployeeByCode", "CreateEmployee", "UpdateEmployee", "UpdateEmployeeWork"): return "EMP-04: الحقول الخاصة nullable مع fieldVisibility؛ full PUT يحتاج edit-identity/edit-bank؛ work-profile يرفض الحقول الخاصة ويحفظها بعد القفل؛ النقل يحتاج edit للمجموعات الثلاث في الفرعين"
    if controller == "EmployeesController" and action == "ExportEmployees": return "EMP-04: export مع view-identity/view-bank/view-salary لكل صف؛ رفض الملف كله دون تصدير جزئي"
    if controller == "EmployeesController" and action in ("GetSalaryStructure", "AssignSalaryStructure"): return "EMP-04: view-salary للقراءة، وedit-salary مع view-salary للإسناد؛ العملة من ShortNameEn موحد وفريد وطريقة دفع مدعومة قبل إغلاق السابق؛ صلاحية العملية العامة لا تكفي"
    if controller == "EmployeesController" and action in ("AddDependent", "DeleteDependent"): return "EMP-04: edit-identity على فرع الأب بعد القفل؛ الإضافة تحتاج view-identity أيضاً لرد المعال"

    if controller == "EmployeesController" and action == "GetMyTeam": return "الفريق المباشر فقط؛ علاقة المدير لا تمنح صلاحية، ولا تحصر المنح العامة القائمة؛ EMP-04 وOracle HTTP مستقلان"
    if controller == "EmployeesController" and action == "ValidateImport": return "EMP-04/10: import مع صلاحيات عرض/تعديل المجموعات الثلاث؛ previewRows استرشادية والاستيراد يعيد التحقق؛ EMP-12 مكتملة: أول راتب يحتاج CurrencyCode/PaymentMethod صريحتين وكتالوج ERP وحد التخزين؛ لا افتراض عملة/دفع"
    if controller == "EmployeesController" and action == "LinkEmployeeUser": return "بوابة حساب محمية EMP-13؛ إلغاء أمني عند CLOSED؛ تغيير الرابط الحقيقي يغير البصمة الحالية ويحتاج إعادة حساب غير المعتمد؛ صلاحيات الفريق وOracle HTTP مستقلة"
    if controller == "EmployeesController" and action == "ImportEmployees": return "EMP-04: import لا يكفي وحده؛ جميع ميزات الهوية/البنك/الراتب قراءة وكتابة للفرعين قبل العمل وبعد القفل"
    if controller == "OrganizationController": return "كتالوج محمي ودورات الهيكل وIf-Match؛ مدير الإدارة وصفحات SET-02 والتزامن وOracle HTTP ما زالت مفتوحة"
    if controller == "AttendanceController" and action in ("CheckIn", "CheckOut"):
        return "تحديث العميل قبل النشر: eventId ثابت، إيصال 202 وRawEventId؛ Data لا تعني نجاح المعالجة المالية"
    if controller in resource_labels or controller == "AttendanceTimeSettingsController": return "API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة"
    if controller == "RawAttendanceController": return "API جديد؛ معالجة صريحة دون عامل background أو مصادقة جهاز عامة"
    if controller == "EmployeesController" and action in ("CreateEmployee", "UpdateEmployee", "DeleteEmployee", "AddDependent", "DeleteDependent"):
        return "بوابة رواتب قبل الحفظ وفق EMP-05/11؛ الملف والمعالون غير مؤرخين حالياً؛ CLOSED أو الاعتماد قد يمنع الكتابة"
    if controller == "AttendanceCorrectionsController": return "UTC ونسخة يوم و409؛ الرفض المسبب مسموح عند CLOSED والموافقة محمية مالياً"
    if controller == "PayrollAdjustmentsController": return "الإضافي المصدر من الحضور ينشأ PENDING مع رابط مصدر فريد، ثم يتطلب قراراً مسبباً قبل دخوله المسير؛ post يعادل approve وunpost يعيده PENDING مع حماية المستهلك/المسير"
    if controller == "PayrollController" and action == "CancelPayroll": return "إضافة إلغاء المحسوب؛ عكس الاعتماد المالي مهمة مستقلة"
    return "إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md"

entries = []
for path in sorted((ROOT / "src/ThinkOnErp.API/Controllers/Hr").glob("*.cs")):
    text = path.read_text(encoding="utf-8-sig")
    base = re.search(r'Route\("([^"]+)"\)', text).group(1)
    class_pos = text.index("public sealed class")
    admin_controller = 'Policy = "AdminOnly"' in text[:class_pos]
    previous = class_pos
    methods = re.finditer(r'public\s+(?:async\s+)?(?:Task[^\r\n]*?|IActionResult)\s+(\w+)\((.*?)\)\s*(?:\{|=>)', text, re.S)
    for method in methods:
        attrs = text[previous:method.start()]
        previous = method.end()
        http = re.search(r'\[Http(Get|Post|Put|Delete|Patch)(?:\("([^"]*)"\))?', attrs)
        if not http:
            continue
        permission = re.search(r'HrPermission\("([^"]+)",\s*"([^"]+)"([^)]*)\)', attrs)
        assert permission, (path.name, method.group(1))
        params = []
        for param in split_parameters(method.group(2)):
            param = " ".join(param.split())
            if not param or "CancellationToken" in param or "[FromServices]" in param:
                continue
            location = "query" if "[FromQuery" in param else "body" if "[FromBody" in param else "header" if "[FromHeader" in param else "form" if "IFormFile" in param else "route/body inferred"
            params.append({"binding": location, "bindingAttribute": re.findall(r"\[[^]]+\]", param), "declaration": re.sub(r'\[[^]]+\]\s*', '', param)})
        response = re.search(r'ProducesResponseType\((?:typeof\((.*?)\),\s*)?StatusCodes\.(\w+)\)', attrs)
        return_type = re.match(r"public\s+(?:async\s+)?(.+?)\s+\w+\(", method.group(0)).group(1)
        responses = [{"type": r.group(1), "status": r.group(2)} for r in re.finditer(r'ProducesResponseType\((?:typeof\((.*?)\),\s*)?StatusCodes\.(\w+)\)', attrs)]
        entries.append({"controller": path.stem, "method": http.group(1).upper(), "path": "/" + base + ("/" + http.group(2) if http.group(2) else ""),
            "action": method.group(1), "purpose": purpose(path.stem, method.group(1)), "screen": permission.group(1), "feature": permission.group(2),
            "selfService": "true" in permission.group(3), "adminOnly": admin_controller or 'Policy = "AdminOnly"' in attrs,
            "consumerIntent": usage(path.stem, method.group(1), "true" in permission.group(3)),
            "requiredChange": next_change(path.stem, method.group(1)), "returnType": return_type, "declaredResponses": responses,
            "parameters": params, "response": response.group(1) if response and response.group(1) else return_type,
            "file": path.relative_to(ROOT).as_posix(), "line": text[:method.start()].count("\n") + 1})

assert len(entries) == sum(len(re.findall(r'\[Http(?:Get|Post|Put|Delete|Patch)\b', p.read_text(encoding="utf-8-sig"))) for p in (ROOT / "src/ThinkOnErp.API/Controllers/Hr").glob("*.cs")), "Endpoint count mismatch"
assert len({(e["method"], e["path"]) for e in entries}) == len(entries)
emit(OUT / f"HR_API_INVENTORY_{DATE}.json", json.dumps(entries, ensure_ascii=False, indent=2))
lines = [f"# دليل جميع API الخاصة بـHR — {DATE}", "", f"تم استخراج **{len(entries)} نقطة نهاية** من **{len(set(e['controller'] for e in entries))} متحكمًا** في الكود الحالي. هذا جرد للكود، وليس إثبات نشر أو نجاح كل طلب على الخادم.", "",
    "## طريقة القراءة والاستخدام", "", "- المسارات تبدأ من عنوان API الذي تشغله، مثل `{baseUrl}/api/hr/...`؛ لا تفترض عنوان خادم أو منفذًا معينًا.",
    "- أرسل `Authorization: Bearer <accessToken>` وسياق الشركة المعتاد. لا يرسل العميل اسم schema حرًا لاختيار قاعدة أخرى؛ SuperAdmin يحدد الشركة عبر `X-Company-Id` أو `X-Company-Code` حسب وسيط توجيه الشركة.",
    "- `companyId` داخل Query/Body، حيث يظهر، يجب أن يطابق الشركة المختارة؛ هو غير `branchId`، وكلاهما غير `employeeCode`.",
    "- `?` في النوع يعني قيمة nullable، و`= ...` قيمة افتراضية في توقيع الدالة؛ لا يعني ذلك وحده وجود تحقق أعمال كافٍ.",
    "- `route/body inferred` تعني أن ASP.NET يستنتج الربط: المعرف الظاهر بين `{}` من المسار، والكائن المعقد غير الموسوم من JSON body.",
    "- `ذاتي` يعني دعم وصول الموظف المرتبط إلى سجلاته فقط، إضافة إلى وصول الإدارة صاحبة المنح. بقية العمليات تحتاج منحًا إداريًا صريحًا، أو مسؤولًا مخولًا.",
    "- إذن قرار process للتصحيح والإجازة يُحسم من `Approved` بواسطة HrAccessFilter؛ رمز الشاشة والمنح لا يستنتجان من المسار.",
    "- المستهلك المذكور مقصد العقد من الكود، وليس إثبات استعمال شاشة أو عميل منشور. خانة التغيير تربط ما يلزم قبل التكامل؛ لا تُحذف مسارات لغياب شاشة.",
    "- المصدر مع أرقام الأسطر يمثل هذه اللقطة فقط. حالات HTTP المعلنة من السمات لا تشمل بالضرورة جميع أخطاء middleware.",
    "- غالبية الردود `ApiResponse<T>`؛ إعدادات الترحيل المالي تعيد كائنًا مباشرًا، وتنزيل Excel/البنك يعيد ملفًا. أخطاء التفويض قد تكون 401/403 بلا غلاف موحد.",
    "- تسجيل الدخول خدمة مشتركة: `POST /api/Auth/login` مع `CompanyLoginCommand`؛ التوكن في `data.accessToken` وفق `TokenDto`.", ""]
for controller in sorted(set(e["controller"] for e in entries)):
    group = [e for e in entries if e["controller"] == controller]
    lines += [f"## {controller} — {len(group)} API", "", f"رمز الشاشة: `{group[0]['screen']}`.", ""]
    for e in group:
        source = (ROOT / e["file"]).as_posix()
        access = "مسؤول فقط" if e["adminOnly"] else "إدارة أو خدمة ذاتية مقيدة" if e["selfService"] else "صلاحية إدارة للفرع"
        feature = e["feature"] + (" / reject عند الرفض" if e["action"] in ("ProcessCorrection", "ProcessLeaveRequest") else "")
        lines += [f"### `{e['method']} {e['path']}`", "", e["purpose"] + ".", "", f"- الصلاحية: `{feature}`؛ الوصول: **{access}**.",
            "- المدخلات: " + ("؛ ".join(f"`{p['binding']}: {p['declaration']}`" + (" (" + " ".join(p["bindingAttribute"]) + ")" if p["bindingAttribute"] else "") for p in e["parameters"]) if e["parameters"] else "لا توجد معاملات أعمال") + ".",
            f"- المستهلك المقصود: {e['consumerIntent']}.", f"- التعديل/الإضافة/الإبقاء: {e['requiredChange']}.",
            f"- الرد: `{e['response']}`؛ نوع التوقيع: `{e['returnType']}`.",
            "- حالات HTTP المعلنة: " + ("؛ ".join(r["status"] for r in e["declaredResponses"]) or "غير مصرح بها في السمات؛ راجع جسم الدالة") + ".", f"- المصدر: [{e['action']}]({source}:{e['line']}).", ""]
lines += ["## ملاحظات مهمة على العقود", "", "- `DELETE /employees/{code}` تعطيل منطقي، بينما حذف المعال/تعديل الراتب عمليات مختلفة؛ راجع تقرير الفجوات قبل الاعتماد عليها.",
    "- مسار التعديلات هو `/api/hr/adjustments`، بينما رمز صلاحيته `hr-payroll-adjustments`؛ ليسا نفس النص.",
    "- `POST /payroll/calculate` ينشئ مسيرًا جديدًا، وليس PUT لإعادة حساب المسير نفسه.",
    "- `GET /payroll/{id}/export-bank/preview` يعيد `FileBytes` أيضًا حاليًا؛ لا يُعامل كمعاينة خالية من محتوى الملف.",
    "- `GET /payroll-posting/{branchId}` يعيد `Ok(configuration)`، وقد يكون الجسم null عند غياب الإعداد؛ لا يعيد 404 صريحًا حاليًا.",
    "- `POST /leaves/policies` يحفظ حسب نوع الإجازة رغم اسم الدالة CreateOrUpdate؛ صلاحيته الحالية create حتى عند التحديث.",
    "- `GET /employees/export-template` صلاحيته view، بينما فحص الملف واستيراده يحتاجان import.",
    "- إدارة الشفتات والإسنادات واليومي والتوقيت وraw موجودة الآن. لا تظهر واجهة إعداد سياسة الحضور أو تعديل/حذف مكونات الرواتب والتقويم والعطل؛ راجع المهام المفتوحة قبل التكامل.",
    "- بصمة الموبايل الجديدة تعيد 202 لحفظ الدليل أو 400 لرفض محفوظ؛ إعادة EventId بمحتوى مختلف تعيد 409. نجاح الإيصال لا يعني اكتمال اليوم المالي.",
    "- الإضافي خارج النسخة التأسيسية، والاستقبال الموثوق للأجهزة ليس API عاماً بلا مصادقة. لا توجد توصية بإيقاف مسار حالياً؛ كل مسار باقٍ ضمن نطاقه.", "",
    "## تعريفات أجسام الطلبات", "", "الحقول أدناه من نماذج C# الحالية؛ أسماء JSON المتوقعة عادة camelCase. هذه ليست أمثلة بيانات ولا تضمن التحقق من القيم. الكائنات المتداخلة مذكورة في نفس التعريفات.", ""]
record_sources = list((ROOT / "src/ThinkOnErp.Application/DTOs/Hr").glob("*.cs")) + list((ROOT / "src/ThinkOnErp.API/Controllers/Hr").glob("*.cs")) + [ROOT / "src/ThinkOnErp.Domain/Entities/Hr/OrganizationCatalogRules.cs", ROOT / "src/ThinkOnErp.Domain/Entities/Hr/EmployeeImportImpact.cs"]
seen_records = set()
for source_path in sorted(record_sources):
    contents = source_path.read_text(encoding="utf-8-sig")
    for record in re.finditer(r'public\s+(?:sealed\s+)?record\s+(\w+)\s*\(', contents):
        start, depth = record.end(), 1
        end = start
        while depth and end < len(contents):
            if contents[end] == "(": depth += 1
            elif contents[end] == ")": depth -= 1
            end += 1
        assert depth == 0, (source_path.name, record.group(1))
        # Body-bearing records may include calculated properties; primary fields still describe the constructor.
        name = record.group(1)
        assert name not in seen_records, f"Duplicate DTO name: {name}"
        seen_records.add(name)
        lines += [f"### {name}", "", "```csharp", "public record " + name + "(" + contents[start:end-1].strip() + ");", "```", ""]
for entity in ("PayrollPostingConfiguration.cs", "PayrollEntities.cs"):
    contents = (ROOT / "src/ThinkOnErp.Domain/Entities/Hr" / entity).read_text(encoding="utf-8-sig")
    name = "PayrollPeriod" if entity == "PayrollEntities.cs" else "PayrollPostingConfiguration"
    block = re.search(r'public sealed class ' + name + r'\s*\{(.*?)\n\}', contents, re.S)
    if block:
        lines += [f"### {name} — كائن المجال المستعمل مباشرة في الطلب", "", "```csharp", "public sealed class " + name + " {", block.group(1).strip(), "}", "```", ""]
emit(OUT / f"HR_API_REFERENCE_{DATE}.md", "\n".join(lines))
print(json.dumps({"endpoints": len(entries), "controllers": dict(Counter(e["controller"] for e in entries)), "selfServiceEndpoints": sum(e["selfService"] for e in entries), "adminOnlyEndpoints": sum(e["adminOnly"] for e in entries)}, ensure_ascii=False))
