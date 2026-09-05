# HR & Payroll System --- Dynamic Business Rules & Implementation Specification

## 1. الهدف

هذا المستند هو Specification عملية لبناء نظام HR/Payroll
Production-Ready، مع التركيز الأساسي على:

1.  Payroll
2.  Attendance
3.  Overtime
4.  Leave integration
5.  Employee lifecycle
6.  Loans / Advances
7.  Statutory Rules
8.  GL Posting
9.  Audit / Approval / Security

### المبدأ الأساسي

> **لا يوجد رقم أو قاعدة أو نسبة أو اسم حالة أو Formula مهمة يتم وضعها
> Hard-Coded داخل الـ Business Logic.**

كل ما يمكن أن يتغير حسب: - الشركة - الفرع - الموظف - العقد - الدولة -
السنة - الشهر - Effective Date - Policy

يجب أن يكون Configurable وData-Driven.

------------------------------------------------------------------------

# 2. Architecture Principles

## 2.1 Layers

اعتمد:

``` text
Domain
Application
Infrastructure
API
```

مع Clean Architecture ويفضل CQRS + MediatR إذا كان المشروع مبنيًا عليهما.

## 2.2 قاعدة مهمة

الـ Controller لا يحتوي Business Rules.

مثال غير صحيح:

``` csharp
if (employee.BasicSalary > 3349)
    ssc = 3349 * 0.075m;
```

الصحيح:

``` text
PayrollService
    -> StatutoryRuleService
        -> Load applicable rule by Date + Company + Country
```

------------------------------------------------------------------------

# 3. Dynamic Configuration Strategy

كل Rule قابلة للتغيير يجب أن تكون Entity/Configuration.

## 3.1 Effective Dating

أي Rule مالية أو HR يجب أن تحتوي على الأقل:

``` text
Id
Code
Name
Value
EffectiveFrom
EffectiveTo
IsActive
```

والقاعدة:

``` text
EffectiveFrom <= CalculationDate
AND
(EffectiveTo IS NULL OR EffectiveTo >= CalculationDate)
```

## 3.2 لا تستخدم أرقامًا مباشرة في الكود

ممنوع:

``` csharp
const decimal SSC_RATE = 0.075m;
const decimal SSC_CAP = 3349m;
const decimal OVERTIME_RATE = 1.25m;
```

الصحيح:

``` text
StatutoryRule
PayrollPolicy
OvertimeRule
LeavePolicy
WorkCalendar
SalaryComponent
```

وتُقرأ القيم وقت تنفيذ العملية.

------------------------------------------------------------------------

# 4. Core Master Data

يجب أن يكون النظام قادرًا على تعريف:

``` text
Company
Branch
Department
Section
Position
Job
Employee
EmploymentContract
EmploymentEvent
SalaryStructure
SalaryComponent
PayrollPolicy
StatutoryRule
TaxRule
SSC Rule
WorkCalendar
Shift
Holiday
LeaveType
LeavePolicy
OvertimeRule
LoanType
DeductionType
Bank
CostCenter
GLAccount
```

------------------------------------------------------------------------

# 5. Employee Lifecycle

## 5.1 Employee Status

الحالات يجب أن تكون Configurable أو Enum مركزي وليس Strings موزعة في
الكود.

مثال:

``` text
DRAFT
ACTIVE
PROBATION
SUSPENDED
ON_LEAVE
TERMINATED
INACTIVE
```

## 5.2 Status Transition

يجب تعريف:

``` text
CurrentStatus
AllowedNextStatus
RequiredPermission
EffectiveDate
ValidationRules
```

مثال:

``` text
DRAFT -> ACTIVE
ACTIVE -> SUSPENDED
ACTIVE -> TERMINATED
PROBATION -> ACTIVE
PROBATION -> TERMINATED
```

ولا يسمح النظام بأي Transition غير معرف.

------------------------------------------------------------------------

# 6. ATTENDANCE --- أهم Module بعد Payroll

## 6.1 Work Calendar

لا تعتمد على:

``` text
Friday = Weekend
Saturday = Weekend
```

يجب أن يكون:

``` text
WorkCalendar
    Id
    CompanyId
    Name
```

ثم:

``` text
WorkCalendarDay
    WorkCalendarId
    DayOfWeek
    IsWorkingDay
    ShiftId
```

وبذلك تستطيع الشركة تعريف أي أيام عمل.

------------------------------------------------------------------------

# 7. Shift Management

## 7.1 Shift

مثال:

``` text
Shift
    Id
    Code
    Name
    StartTime
    EndTime
    BreakMinutes
    GracePeriodMinutes
    MinimumWorkingMinutes
    MaximumWorkingMinutes
    IsOvernight
```

## 7.2 Overnight Shift

مثال:

``` text
22:00 -> 06:00
```

يجب ألا يتم حسابه كـ:

``` text
06:00 - 22:00
```

بل:

``` text
StartDateTime = Day 1 22:00
EndDateTime   = Day 2 06:00
```

------------------------------------------------------------------------

# 8. Employee Schedule

يجب أن يكون لكل موظف Schedule فعلي:

``` text
EmployeeSchedule
    EmployeeId
    WorkCalendarId
    ShiftId
    EffectiveFrom
    EffectiveTo
```

لأن الموظف يمكن أن ينتقل من Shift إلى آخر.

------------------------------------------------------------------------

# 9. Attendance Records

يفضل فصل Raw Attendance عن Calculated Attendance.

## 9.1 RawAttendance

يمثل البيانات الخام:

``` text
EmployeeId
AttendanceDate
PunchTime
PunchType
Source
DeviceId
ExternalReference
```

## 9.2 AttendanceDay

يمثل النتيجة المحسوبة:

``` text
EmployeeId
Date
ShiftId
CheckIn
CheckOut
WorkedMinutes
LateMinutes
EarlyLeaveMinutes
OvertimeMinutes
MissingPunch
Status
```

مثال Status:

``` text
PRESENT
ABSENT
LATE
EARLY_LEAVE
MISSING_PUNCH
ON_LEAVE
HOLIDAY
WEEKEND
```

------------------------------------------------------------------------

# 10. Attendance Calculation Engine

لا تحسب Attendance داخل Controller أو Repository.

أنشئ:

``` text
IAttendanceCalculationEngine
AttendanceCalculationEngine
```

Input:

``` text
Employee
Date
Schedule
Shift
RawPunches
Leave
Holiday
WorkCalendar
AttendancePolicy
```

Output:

``` text
AttendanceResult
```

------------------------------------------------------------------------

# 11. Attendance Calculation Rules

## 11.1 Check-In

يتم تحديد أول Punch صالح:

``` text
First valid punch = CheckIn
```

## 11.2 Check-Out

آخر Punch صالح:

``` text
Last valid punch = CheckOut
```

لكن يجب دعم سياسات مختلفة إذا كانت الشركة تستخدم أكثر من دخول/خروج.

------------------------------------------------------------------------

# 12. Grace Period

مثال:

``` text
Shift Start = 08:00
Grace = 10 minutes
CheckIn = 08:07
Late = 0
```

أما:

``` text
CheckIn = 08:15
Late = 15
```

لكن `10` يجب أن يأتي من:

``` text
AttendancePolicy
```

وليس من الكود.

------------------------------------------------------------------------

# 13. Missing Punch Workflow

إذا كان هناك Check-In بدون Check-Out:

``` text
MISSING_PUNCH
```

ولا يتم تخمين Check-Out تلقائيًا إلا إذا كانت هناك Policy تسمح بذلك.

Workflow:

``` text
Employee submits correction
        ↓
Manager Review
        ↓
HR Review (if required)
        ↓
Approved
        ↓
Attendance Recalculation
        ↓
Audit Log
```

------------------------------------------------------------------------

# 14. Attendance Correction

لا تعدل السجل الأصلي مباشرة.

استخدم:

``` text
AttendanceCorrectionRequest
```

ويحتوي:

``` text
EmployeeId
AttendanceDate
OldValue
RequestedValue
Reason
RequestedBy
ApprovedBy
Status
CreatedAt
ApprovedAt
```

Status:

``` text
PENDING
APPROVED
REJECTED
CANCELLED
```

------------------------------------------------------------------------

# 15. Overtime

Overtime يجب أن يكون Dynamic.

``` text
OvertimeRule
    Code
    Name
    DayType
    Multiplier
    MinimumMinutes
    MaximumMinutes
    RoundingRule
    ApprovalRequired
    EffectiveFrom
    EffectiveTo
```

مثال DayType:

``` text
NORMAL
WEEKEND
HOLIDAY
NIGHT
```

ولا يتم وضع:

``` text
1.25
1.50
2.00
```

داخل الكود.

------------------------------------------------------------------------

# 16. Overtime Hourly Rate

يجب تحديد Formula في Configuration.

مثلاً:

``` text
HourlyRate =
SalaryBase / MonthlyWorkingHours
```

لكن MonthlyWorkingHours نفسها يجب أن تكون Policy.

مثال:

``` text
SalaryBase
HourlyDivisor
```

ويجب دعم سياسات مثل:

``` text
Basic Salary / 30 / Daily Hours
Basic Salary / Working Days / Daily Hours
Basic + Eligible Allowances
```

الـ Formula نفسها يجب أن تكون محددة بوضوح في Policy.

------------------------------------------------------------------------

# 17. Overtime Approval

لا يدخل Overtime المعتمد إلى Payroll إلا إذا كان:

``` text
APPROVED
```

Workflow:

``` text
Attendance Calculation
        ↓
Overtime Detected
        ↓
Employee/Manager Review
        ↓
Approved
        ↓
Payroll Eligible
```

------------------------------------------------------------------------

# 18. Payroll --- أهم Module

Payroll يجب أن يكون Engine وليس مجموعة SQL Queries أو IF statements.

أنشئ:

``` text
IPayrollCalculationEngine
PayrollCalculationEngine
```

------------------------------------------------------------------------

# 19. Payroll Period

كل Payroll Run يجب أن يرتبط بـ:

``` text
PayrollPeriod
    Id
    CompanyId
    StartDate
    EndDate
    PayDate
    FiscalYear
    Month
    Status
```

Unique Constraint:

``` text
CompanyId + FiscalYear + Month + PayrollType
```

لمنع إنشاء Payroll مرتين لنفس الفترة.

------------------------------------------------------------------------

# 20. Payroll Run State Machine

استخدم:

``` text
DRAFT
CALCULATING
CALCULATED
UNDER_REVIEW
APPROVED
POSTED
LOCKED
REVERSED
```

بعد:

``` text
POSTED / LOCKED
```

لا يسمح بالتعديل المباشر.

------------------------------------------------------------------------

# 21. Payroll Calculation Pipeline

الترتيب المقترح:

``` text
1. Load Payroll Period
2. Load Eligible Employees
3. Load Employment Status
4. Load Salary Structure
5. Apply Effective-Dated Salary
6. Calculate Proration
7. Calculate Earnings
8. Calculate Overtime
9. Calculate Leave Impact
10. Calculate Unpaid Leave
11. Calculate Gross Salary
12. Calculate SSC Base
13. Calculate Employee SSC
14. Calculate Employer SSC
15. Calculate Taxable Income
16. Apply Tax Exemptions
17. Calculate Income Tax
18. Calculate National Contribution
19. Calculate Loans
20. Calculate Advances
21. Calculate Other Deductions
22. Calculate Net Pay
23. Validate Payroll
24. Submit for Review
25. Approve
26. Post to GL
27. Lock
```

------------------------------------------------------------------------

# 22. Salary Components

لا تعتمد فقط على:

``` text
EARNING
DEDUCTION
```

يجب أن يدعم النظام:

``` text
BASIC
HOUSING
TRANSPORT
ALLOWANCE
BONUS
COMMISSION
OVERTIME
REIMBURSEMENT
LOAN_DEDUCTION
ADVANCE_DEDUCTION
TAX
SSC
OTHER_DEDUCTION
EMPLOYER_CONTRIBUTION
```

كل Component يجب أن يحتوي:

``` text
Code
Name
Type
CalculationMethod
IsTaxable
IsSscSubject
IsOvertimeBase
IsProratable
IsRecurring
IsActive
EffectiveFrom
EffectiveTo
GLAccount
```

------------------------------------------------------------------------

# 23. Calculation Methods

لا تجعل كل Component يحتاج كود جديد.

دعم طرق مثل:

``` text
FIXED_AMOUNT
PERCENTAGE_OF_BASIC
PERCENTAGE_OF_GROSS
PER_DAY
PER_HOUR
PER_UNIT
FORMULA
MANUAL
```

مثال:

``` text
Housing = 20% of Basic
Transport = Fixed 100
Overtime = Hours × HourlyRate × Multiplier
```

------------------------------------------------------------------------

# 24. Salary Structure

``` text
EmployeeSalaryStructure
    EmployeeId
    EffectiveFrom
    EffectiveTo
```

والتفاصيل:

``` text
EmployeeSalaryComponent
    SalaryStructureId
    SalaryComponentId
    Amount
    Percentage
```

بهذا يمكن تغيير الراتب بدون فقدان التاريخ.

------------------------------------------------------------------------

# 25. Salary Change

لا تعمل:

``` text
UPDATE Employee SET BasicSalary = ...
```

بدون History.

استخدم:

``` text
EmploymentEvent
```

مثال:

``` text
SALARY_CHANGE
EffectiveDate
OldSalary
NewSalary
Reason
ApprovedBy
```

------------------------------------------------------------------------

# 26. Payroll Proration Policy (Company-Level & Effective-Dated)

يجب أن تكون سياسة احتساب الراتب التناسبي (Proration) معرفة بالكامل على مستوى الشركة (`CompanyId`) أو سياسة الرواتب (`PayrollPolicy`)، مع دعم كامل للـ Effective Dating (`EffectiveFrom` / `EffectiveTo`)، بحيث يتم اختيار وحل الـ Policy بناءً على فترة الراتب وتاريخ السريان دون أي تثبيت (Hardcoding) داخل محرك الرواتب:

### 26.1 Proration Methods المدعومة
تحدد الشركة في السياسة إحدى الطرق التالية:
1. `CALENDAR_DAYS`: الراتب اليومي = الراتب الشهري / عدد الأيام الفعلية في الشهر (28/29/30/31).
2. `FIXED_30`: الراتب اليومي = الراتب الشهري / 30 يومًا ثابتة (المعيار التجاري المعتمد).
3. `WORKING_DAYS`: الراتب اليومي = الراتب الشهري / عدد أيام العمل الفعلية في الشهر حسب الـ `WorkCalendar` الخاص بالشركة.

### 26.2 حالات التطبيق
* **New Hire (تعيين جديد خلال الشهر):**
  `Paid Salary = Component Amount × (Eligible Days / Total Base Days)`
* **Termination (إنهاء خدمة خلال الشهر):**
  `Paid Salary = Component Amount × (Worked Days / Total Base Days)`
* **Mid-Month Salary Change (تعديل الراتب خلال الشهر):**
  يتم تقسيم فترة الراتب تلقائياً وتطبيق السياسة على شطرين:
  `Period Part 1 (Old Salary Rate) + Period Part 2 (New Salary Rate)`

يتم حل (Resolve) السياسة الفعالة ديناميكياً لكل موظف وفترة رواتب، ولا يحق لمحرك الرواتب فرض أي طريقة افتراضية ثابتة في الكود.

------------------------------------------------------------------------

# 27. Gross Salary

لا تفترض:

``` text
Gross = Basic + All Money
```

بل:

``` text
Gross Earnings =
SUM(components where Type = EARNING and included in Gross)
```

والتعريف يجب أن يكون Configurable.

------------------------------------------------------------------------

# 28. SSC Base

لا تستخدم Formula ثابتة مثل:

``` text
Basic + Housing + Transport + Overtime
```

داخل الكود.

الصحيح:

``` text
SSC Base =
SUM(eligible salary components
    where IsSscSubject = true)
```

ثم:

``` text
SSC Base = MIN(EligibleBase, ConfiguredSSCCap)
```

كل من:

``` text
IsSscSubject
SSCCap
EmployeeRate
EmployerRate
```

يجب أن تكون Data-Driven وEffective-Dated.

> القيم القانونية الأردنية يجب التحقق منها من التشريع/المصادر الرسمية
> قبل Production، ولا تعتبر أي أرقام واردة في مستند سابق قيمًا ثابتة
> للنظام.

------------------------------------------------------------------------

# 29. Tax Calculation

Tax Engine يجب أن يكون منفصلًا:

``` text
ITaxCalculationEngine
```

يدعم:

``` text
TaxBracket
TaxRate
TaxAllowance
TaxExemption
DependentRule
MaritalRule
EffectiveDate
```

مثال:

``` text
TaxBracket
    FromAmount
    ToAmount
    Rate
```

ولا تضع:

``` text
5%
10%
15%
20%
25%
30%
```

داخل الكود.

------------------------------------------------------------------------

# 30. Tax Period

يجب تحديد هل الضريبة تحسب:

``` text
Monthly
Annualized
Year-to-Date
```

والـ Policy تحدد الطريقة.

يجب دعم:

``` text
YTD taxable income
YTD tax paid
Current period tax
Expected annual tax
```

خصوصًا عند: - New Hire - Termination - Salary Change - Bonus - Unpaid
Leave

------------------------------------------------------------------------

# 31. Bonuses

Bonus يجب أن يكون Transaction مستقل:

``` text
EmployeeBonus
    EmployeeId
    BonusType
    Amount
    EffectiveDate
    Taxable
    SscSubject
    PayrollPeriodId
    ApprovalStatus
```

لا تضف Bonus مباشرة إلى Basic Salary.

------------------------------------------------------------------------

# 32. Loans & Salary Advances

Payroll يجب أن يقرأ:

``` text
EmployeeLoan
EmployeeAdvance
```

مثال:

``` text
LoanAmount
InstallmentAmount
StartDate
EndDate
RemainingBalance
Status
```

عند Payroll:

``` text
CurrentInstallment
    ↓
Deduct
    ↓
Update Remaining Balance
```

لكن لا يتم تعديل Loan إذا فشل Payroll Transaction.

------------------------------------------------------------------------

# 33. Negative Net Pay & Automatic Deduction Capping / Carry-Forward Policy

عندما تتجاوز الاستقطاعات (سواء أقساط القروض `Loans` أو السلف `Advances` أو الخصومات الإدارية الأخرى) سقف الخصم المسموح به أو تؤدي إلى صافي راتب سالب، يطبق النظام سياسة السقف والترحيل التلقائي (**Automatic Deduction Capping & Carry-Forward Policy**):

### 33.1 بنود السياسة (Company-Level Deduction Policy)
تحتوي السياسة على المحددات التالية (مع دعم `EffectiveFrom` و `EffectiveTo`):
``` text
DeductionPolicy
    Id
    CompanyId
    MaximumTotalDeductionPercentage (e.g., 50% of Net Disposable Income)
    MinimumNetPayGuarantee (e.g., Fixed amount or Percentage of Basic)
    AllowNegativeNetPay (Default: FALSE)
    AutoCapAndCarryForward (Default: TRUE)
    DeductionPriorityOrder (1. Statutory SSC, 2. Statutory Tax, 3. Advances, 4. Loans, 5. Other)
```

### 33.2 آلية التنفيذ (Execution Mechanics)
1. **الاستقطاعات الإلزامية أولاً:** يتم اقتطاع الضمان الاجتماعي وضريبة الدخل بالكامل دون سقف لأنها التزامات قانونية سيادية.
2. **سقف الاستقطاعات غير القانونية (القروض والسلف):**
   * إذا كان `(Available Net - Scheduled Installment) < MinimumNetPayGuarantee` أو تجاوز نسبة `MaximumTotalDeductionPercentage`:
   * يتم اقتطاع الجزء المتاح فقط من القسط (`DeductedAmount = AvailableBalance`).
   * يتم ترحيل المبلغ المتبقي غير المسدد (`UnpaidBalance = ScheduledInstallment - DeductedAmount`) تلقائياً كـ **Carry-Forward** يضاف إلى رصيد القسط أو يجدول في الفترة اللاحقة دون إفشال مسير الرواتب.
3. **التوثيق المحاسبي والتدقيقي:**
   * يتم تسجيل سطر الاقتطاع الفعلي في `PayrollRunLineComponent`.
   * يتم تحديث سجل القرض `EmployeeLoan` بالدفعة الفعلية والمبلغ المتبقي المرحل مع تسجيل سبب الخصم الجزئي في سجل التدقيق (Audit Log).
   * لا يتم إيقاف مسير الرواتب للموظف إذا كانت سياسة `AutoCapAndCarryForward` مفعلة.

------------------------------------------------------------------------

# 34. Payroll Validation Engine

قبل Approval يجب تشغيل:

``` text
IPayrollValidationService
```

Validations:

``` text
Duplicate payroll
Missing salary
Invalid employee status
Negative net pay
Missing bank account
Invalid IBAN
Missing required attendance
Unapproved overtime
Invalid leave
Invalid tax configuration
Invalid SSC configuration
Missing GL account
Duplicate payroll posting
```

النتيجة:

``` text
Errors
Warnings
```

لا يسمح بالـ Approval عند وجود Critical Errors.

------------------------------------------------------------------------

# 35. Payroll Calculation Snapshot

عند حساب Payroll، يجب حفظ النتيجة.

مثال:

``` text
PayrollEmployee
    EmployeeId
    Basic
    Gross
    SSCEmployee
    SSCEmployer
    Tax
    NationalContribution
    Loans
    OtherDeductions
    NetPay
```

ثم:

``` text
PayrollEmployeeLine
    Component
    Quantity
    Rate
    Amount
    CalculationSource
```

هذا مهم جدًا للـ Audit.

------------------------------------------------------------------------

# 36. Explainable Payroll

يجب أن يستطيع النظام الإجابة:

> لماذا صافي راتبي 1,245 JOD؟

ويعرض:

``` text
Basic Salary             1,000
Housing                    200
Transport                  100
Overtime                    75
Gross                    1,375
SSC                      -103
Tax                       -20
Loan                      -50
Net Pay                  1,202
```

والأفضل حفظ:

``` text
Formula
Input values
Rule IDs
Rule versions
```

------------------------------------------------------------------------

# 37. Payroll Recalculation

يجب دعم:

``` text
Calculate
Recalculate
Compare
Approve
```

لكن:

``` text
POSTED payroll
```

لا يتم Recalculate عليه مباشرة.

إذا حدث خطأ بعد Posting:

``` text
Original Payroll
      ↓
Adjustment / Reversal
      ↓
Corrected Payroll
```

------------------------------------------------------------------------

# 38. Payroll Idempotency

من أخطر المشاكل:

``` text
POST Payroll
POST Payroll again
```

لا يجوز إنشاء GL Voucher مرتين.

يجب وجود:

``` text
PayrollRunId UNIQUE
```

مع:

``` text
PayrollRun -> GlVoucher
```

وTransaction.

------------------------------------------------------------------------

# 39. GL Posting

Payroll Posting يجب أن ينتج:

``` text
GlVoucherHeader
GlVoucherLine
```

مثال:

``` text
Dr Salary Expense
Dr Employer SSC Expense
Cr Employee Net Payable
Cr Employee SSC Payable
Cr Employer SSC Payable
Cr Tax Payable
Cr Other Liabilities
```

الحسابات يجب أن تأتي من:

``` text
SalaryComponent.GLAccount
Company Accounting Configuration
CostCenter
Branch
Department
```

وليس من IF statements داخل الكود.

------------------------------------------------------------------------

# 40. Payroll Transaction Boundary

Payroll Posting يجب أن يكون Transaction:

``` text
Begin Transaction

Validate Payroll
Create GL Voucher
Create GL Lines
Update Payroll Status
Update Loan Balances
Create Audit Records

Commit
```

إذا فشل أي جزء:

``` text
Rollback
```

------------------------------------------------------------------------

# 41. Attendance → Payroll Integration

هذه العلاقة يجب أن تكون واضحة:

``` text
Attendance
    ↓
Approved Attendance
    ↓
Overtime
    ↓
Unpaid Absence / Leave
    ↓
Payroll Inputs
```

Payroll لا يقرأ Raw Punches مباشرة.

------------------------------------------------------------------------

# 42. Leave → Payroll Integration

Leave Engine يجب أن يحدد:

``` text
PAID
UNPAID
PARTIALLY_PAID
```

Payroll يقرأ النتيجة.

مثال:

``` text
UnpaidLeaveDays
    ↓
Proration Policy
    ↓
Deduction
```

------------------------------------------------------------------------

# 43. Public Holidays

Holiday يجب أن يكون Master Data:

``` text
Holiday
    Date
    Name
    Type
    CompanyId
    BranchId
    IsPaid
```

ولا تضع تواريخ الأعياد في الكود.

------------------------------------------------------------------------

# 44. Leave Balance

يجب أن يكون:

``` text
LeaveEntitlement
LeaveAccrual
LeaveTaken
LeaveAdjustment
LeaveCarryForward
LeaveBalance
```

Formula:

``` text
Balance =
Opening
+ Accrual
+ Adjustments
+ CarryForward
- Approved Leave
```

------------------------------------------------------------------------

# 45. Leave Cancellation

عند إلغاء Leave:

``` text
Approved Leave
    ↓
Cancel
    ↓
Restore Balance
    ↓
Recalculate Attendance
    ↓
Recalculate Payroll if required
```

------------------------------------------------------------------------

# 46. Payroll Eligibility

ليس كل Employee يجب أن يدخل Payroll.

مثلاً:

``` text
ACTIVE
PROBATION
```

قد يكون eligible.

بينما:

``` text
TERMINATED
INACTIVE
```

يحتاج Rule خاص.

يجب أن يكون:

``` text
PayrollEligibilityPolicy
```

------------------------------------------------------------------------

# 47. Termination

Termination يجب أن ينتج:

``` text
TerminationDate
Reason
Type
NoticePeriod
FinalWorkingDate
```

ثم:

``` text
Final Attendance
Final Salary
Unused Leave
Loans
Advances
EOS
Other Deductions
Final Net Settlement
```

------------------------------------------------------------------------

# 48. End of Service

لا تضع Formula واحدة ثابتة لجميع الحالات.

استخدم:

``` text
EndOfServicePolicy
EndOfServiceRule
```

والـ Engine:

``` text
IEndOfServiceCalculationEngine
```

يجب أن يدعم: - Service duration - Termination reason - Contract type -
Eligibility - Basic salary base - Leave encashment - Loans/advances -
Other settlement items

ويجب فصل:

``` text
Legal Employee Entitlement
```

عن:

``` text
Accounting Provision / Accrual
```

------------------------------------------------------------------------

# 49. Bank Salary Transfer

لا تسمِّ الملف WPS إلا إذا كان فعلًا مطابقًا لمواصفات WPS المطلوبة.

الأفضل:

``` text
BankSalaryTransfer
BankFileFormat
BankFileHeader
BankFileDetail
BankFileTrailer
```

ويكون Format configurable حسب البنك.

------------------------------------------------------------------------

# 50. Audit

كل عملية حساسة يجب تسجيل:

``` text
Who
What
When
Before
After
Reason
IP
Entity
EntityId
```

خصوصًا:

``` text
Salary
Bank Account
IBAN
Payroll
Tax
SSC
Attendance Correction
Leave Balance
Loan
Termination
GL Posting
```

------------------------------------------------------------------------

# 51. Authorization

يجب دعم RBAC:

``` text
HR
Payroll Officer
Payroll Manager
Finance
Manager
Employee
Admin
Auditor
```

مع Scope:

``` text
Company
Branch
Department
Employee hierarchy
```

------------------------------------------------------------------------

# 52. Manager Hierarchy

MSS يجب ألا يعتمد فقط على Department.

يجب أن يدعم:

``` text
Employee.ManagerId
```

وبناء hierarchy:

``` text
Manager
 ├── Employee A
 │    └── Employee C
 └── Employee B
```

ويمكن تحديد:

``` text
Direct Reports
Indirect Reports
```

------------------------------------------------------------------------

# 53. Concurrency

يجب حماية:

``` text
Payroll
Attendance Correction
Leave Balance
Loan Balance
GL Posting
```

استخدم:

``` text
RowVersion
Optimistic Concurrency
Unique Constraints
Transactions
State Validation
```

------------------------------------------------------------------------

# 54. Database Constraints

أمثلة:

``` text
Unique(EmployeeId, PayrollPeriodId)

Unique(PayrollRunId, EmployeeId)

Unique(PayrollRunId, GlVoucherId)

Unique(EmployeeId, EffectiveFrom, SalaryStructureVersion)
```

حسب تصميم DB.

------------------------------------------------------------------------

# 55. Soft Delete

لا تستخدم Delete للبيانات التاريخية المهمة.

خصوصًا:

``` text
Payroll
Attendance
Salary History
Employment Events
GL
Audit
Loans
Leave Transactions
```

استخدم:

``` text
IsActive
EffectiveFrom
EffectiveTo
```

أو Versioning حسب الحاجة.

------------------------------------------------------------------------

# 56. Notifications

Notifications يجب أن تكون Event-driven قدر الإمكان:

``` text
Missing Punch
Leave Pending
Leave Approved
Overtime Pending
Payroll Ready
Payroll Approved
Payroll Posted
Contract Expiring
Probation Ending
```

والـ templates تكون Data-Driven.

------------------------------------------------------------------------

# 57. Reporting

يجب توفير:

## Attendance

``` text
Daily Attendance
Late Report
Early Leave
Absence
Missing Punch
Overtime
```

## Payroll

``` text
Payroll Register
Gross-to-Net
Salary Components
Tax
SSC
Employer Cost
Deductions
Net Pay
```

## HR

``` text
Headcount
New Hires
Terminations
Turnover
Employee Movement
```

------------------------------------------------------------------------

# 58. API Design

مثال:

``` text
POST   /api/payroll/periods
POST   /api/payroll/runs
POST   /api/payroll/runs/{id}/calculate
POST   /api/payroll/runs/{id}/validate
POST   /api/payroll/runs/{id}/submit
POST   /api/payroll/runs/{id}/approve
POST   /api/payroll/runs/{id}/post
POST   /api/payroll/runs/{id}/reverse

GET    /api/payroll/runs/{id}
GET    /api/payroll/runs/{id}/employees
GET    /api/payroll/employees/{employeeId}/explanation
```

Attendance:

``` text
POST /api/attendance/punch
POST /api/attendance/corrections
POST /api/attendance/corrections/{id}/approve
POST /api/attendance/recalculate
GET  /api/attendance/employee/{id}
```

------------------------------------------------------------------------

# 59. Testing Strategy

Payroll لا يكفي له Unit Tests بسيطة.

يجب وجود:

``` text
Unit Tests
Integration Tests
Database Tests
Authorization Tests
Concurrency Tests
End-to-End Payroll Tests
```

------------------------------------------------------------------------

# 60. Mandatory Payroll Test Cases

يجب اختبار:

``` text
Normal employee
New employee
Terminated employee
Mid-month hire
Mid-month termination
Mid-month salary change
Overtime
Weekend overtime
Holiday overtime
Unpaid leave
Paid leave
Bonus
Loan deduction
Advance deduction
Large deduction
Zero salary
Missing salary
Missing bank account
Tax exemption
Dependents
SSC cap
Retroactive salary change
Duplicate payroll
Payroll rerun
GL posting retry
GL posting failure
Payroll reversal
```

------------------------------------------------------------------------

# 61. Mandatory Attendance Test Cases

``` text
Normal day
Late arrival
Early departure
Missing check-in
Missing check-out
Multiple punches
Overnight shift
Weekend
Holiday
Leave
Holiday during leave
Shift change
Schedule change
Attendance correction
Rejected correction
Approved correction
Duplicate punch
Device duplicate punch
Timezone/date boundary
```

------------------------------------------------------------------------

# 62. Payroll Calculation Example

Input:

``` text
Basic = configured
Housing = configured
Transport = configured
OvertimeHours = calculated
Bonus = approved
UnpaidLeave = calculated
LoanInstallment = current installment
```

Engine:

``` text
Salary Components
        ↓
Proration
        ↓
Earnings
        ↓
Overtime
        ↓
Unpaid Leave
        ↓
Gross
        ↓
SSC
        ↓
Tax
        ↓
National Contribution
        ↓
Loans
        ↓
Other Deductions
        ↓
Net Pay
```

النتيجة يجب أن تكون قابلة للتفسير Line-by-Line.

------------------------------------------------------------------------

# 63. Configuration Tables --- Minimum Set

يفضل أن يحتوي النظام على:

``` text
PayrollPolicy
PayrollRule
StatutoryRule
TaxRule
TaxBracket
SSCPolicy
OvertimeRule
AttendancePolicy
ProrationPolicy
LeavePolicy
DeductionPolicy
EndOfServicePolicy
BankFileFormat
SalaryComponent
```

------------------------------------------------------------------------

# 64. Golden Rule: Configuration vs Code

## Code

يحتوي:

``` text
How the engine works
Validation framework
Calculation framework
State machine framework
Transaction handling
Persistence
Security
```

## Database / Configuration

يحتوي:

``` text
Rates
Caps
Thresholds
Multipliers
Working days
Grace periods
Leave entitlements
Tax brackets
SSC rules
Salary component behavior
Proration method
Approval requirements
Bank formats
GL mappings
```

------------------------------------------------------------------------

# 65. أهم قاعدة في النظام

لا تجعل:

``` text
Jordan rules
Company rules
Employee rules
```

مختلطة في نفس المكان.

استخدم hierarchy:

``` text
Global / Legal Rule
        ↓
Country
# 65. Universal Dynamic Policy Architecture (Company-Level & Effective-Dated)

يطبق النظام مبدأ **"السياسات الديناميكية على مستوى الشركة" (Company-Level Dynamic Policies with Effective Dating)** على جميع الوحدات الفرعية دون استثناء:

### 65.1 مصفوفة السياسات المعرفة على مستوى الشركة (`CompanyId` + `EffectiveFrom` / `EffectiveTo`)
1. **Working Days & Work Calendar:**
   * تحديد أيام العمل والعطل الأسبوعية لكل شركة عبر `WorkCalendar` و `WorkCalendarDay` بدلاً من افتراض الجمعة/السبت.
2. **Attendance & Grace Period:**
   * سياسة `AttendancePolicy` تحدد دقائق السماح (Grace Period)، وسقف التأخير، وسياسة التعامل مع البصمة المفقودة (Missing Punch).
3. **Overtime Policy & Rules:**
   * سياسة `OvertimeRule` تحدد المضاعفات (Multiplier)، ومقسوم احتساب الساعة (Hourly Divisor e.g., 240 / Actual Hours)، والحدود الدنيا والعليا للموافقة.
4. **Proration Policy:**
   * سياسة `ProrationPolicy` تحدد طريقة الاحتساب الجزئي (`CALENDAR_DAYS`, `FIXED_30`, `WORKING_DAYS`) دون فرضها في الكود.
5. **Deduction Capping & Carry-Forward:**
   * سياسة `DeductionPolicy` تحدد سقف الاقتطاعات وترحيل المبالغ غير المسددة للأشهر القادمة.
6. **Statutory Rules (SSC & Tax):**
   * جداول `StatutoryRule` و `TaxBracket` و `TaxPolicy` تحدد الشرائح ونسب الضمان والضريبة والإعفاءات وتطبق بحسب تاريخ السريان القانوني.
7. **Leave Policies & Accruals:**
   * سياسة `LeavePolicy` تحدد استحقاقات وتدوير الأرصدة وسقوف الترحيل السنوي.
8. **Salary Components & GL Mapping:**
   * جدول `SalaryComponent` يحدد معادلة كل بند، خضوعه للضريبة/الضمان، وحسابه المحاسبي في دليل الحسابات.

### 65.2 هرمية حل القواعد (Policy Resolution Hierarchy)
``` text
Global / Sovereign Statutory Rules (قوانين إلزامية: الضمان، الضريبة، الحد الأدنى للأجور)
        ↓
Company Policy (سياسات الشركة: التقويم، الإضافي، التناسب، فترات السماح، سقف الخصومات)
        ↓
Branch / Department Override (إن وجد)
        ↓
Employee Contract / Specific Rule
```
يتم تطبيق وحل السياسة المناسبة بناءً على تاريخ الاستحقاق الفعلي (`CalculationDate`) تلقائياً وبأعلى درجات الموثوقية.

------------------------------------------------------------------------

# 66. Rule Resolution

عند وجود أكثر من Rule:

``` text
Employee-specific
    ↓
Contract-specific
    ↓
Company
    ↓
Country / Legal
    ↓
System Default
```

لكن يجب عدم السماح بتجاوز Rule قانونية إلزامية بواسطة Company Policy إذا
كان القانون يمنع ذلك.

------------------------------------------------------------------------

# 67. Effective Date Resolution

عند حساب Payroll بتاريخ:

``` text
2026-09-30
```

يجب تحميل الـ Rules التي كانت فعالة في ذلك التاريخ، وليس آخر Rule موجودة
حاليًا.

هذا مهم جدًا للتاريخية وإعادة حساب Payroll قديم.

------------------------------------------------------------------------

# 68. Payroll Snapshot & Versioning

Payroll يجب أن يحفظ:

``` text
RuleId
RuleVersion
SalaryStructureId
SalaryStructureVersion
PolicyId
PolicyVersion
CalculationTimestamp
```

حتى تستطيع معرفة:

> لماذا كانت نتيجة Payroll في ذلك الشهر بهذا الشكل؟

------------------------------------------------------------------------

# 69. Error Handling

لا تستخدم:

``` text
catch(Exception)
{
    return "Payroll failed";
}
```

استخدم أخطاء واضحة:

``` text
PayrollValidationException
InvalidPayrollStateException
MissingSalaryException
MissingStatutoryRuleException
MissingGLAccountException
ConcurrencyException
DuplicatePayrollException
```

مع Error Codes.

------------------------------------------------------------------------

# 70. Recommended Service Structure

``` text
Payroll
├── PayrollService
├── PayrollCalculationEngine
├── PayrollValidationService
├── PayrollProrationService
├── SalaryComponentService
├── StatutoryRuleService
├── TaxCalculationService
├── SSCCalculationService
├── OvertimeCalculationService
├── LoanDeductionService
├── PayrollPostingService
├── PayrollReversalService
└── PayrollExplanationService

Attendance
├── AttendanceService
├── AttendanceCalculationEngine
├── ShiftService
├── WorkCalendarService
├── AttendanceCorrectionService
├── OvertimeService
└── AttendanceValidationService
```

------------------------------------------------------------------------

# 71. Recommended Implementation Order

## Phase 1 --- Foundation

``` text
Employee
Employment
Salary Structure
Salary Components
Work Calendar
Shift
Holiday
```

## Phase 2 --- Attendance

``` text
Punch
Attendance Day
Shift Calculation
Late
Early Leave
Missing Punch
Correction
Overtime
```

## Phase 3 --- Payroll

``` text
Payroll Period
Payroll Run
Proration
Earnings
Deductions
SSC
Tax
Loans
Net Pay
Validation
```

## Phase 4 --- Payroll Control

``` text
Review
Approval
Lock
Recalculation
Reversal
Audit
```

## Phase 5 --- Finance

``` text
GL Mapping
GL Posting
Bank Transfer
Reconciliation
```

## Phase 6 --- Advanced HR

``` text
Recruitment
Onboarding
Performance
Training
Benefits
Assets
Expenses
```

------------------------------------------------------------------------

# 72. Final Definition of Done

Payroll وAttendance لا تعتبر مكتملة إلا إذا:

-   لا يوجد Hard-Coded statutory rate.
-   لا يوجد Hard-Coded overtime multiplier.
-   لا يوجد Hard-Coded weekend.
-   لا يوجد Hard-Coded holiday.
-   لا يوجد Hard-Coded tax bracket.
-   لا يوجد Hard-Coded SSC cap.
-   Salary history محفوظ.
-   Effective dating يعمل.
-   Payroll period واضح.
-   Proration واضح.
-   Attendance engine منفصل.
-   Raw punches منفصلة عن calculated attendance.
-   Missing punch workflow موجود.
-   Overtime approval موجود.
-   Leave integration موجود.
-   Loan/advance integration موجود.
-   Payroll validation موجود.
-   Payroll state machine موجود.
-   Payroll snapshot موجود.
-   Payroll explanation موجود.
-   Posted payroll immutable.
-   Reversal/adjustment موجود.
-   GL posting idempotent.
-   Transactions موجودة.
-   Concurrency protection موجود.
-   Audit موجود.
-   RBAC موجود.
-   Tests للحالات الحرجة موجودة.

------------------------------------------------------------------------

# 73. Priority Matrix

## 🔴 Critical --- يجب تنفيذها أولًا

``` text
Payroll Calculation Engine
Attendance Calculation Engine
Payroll Period
Salary Components
Effective Dating
Proration
SSC / Tax Rule Engine
Overtime Engine
Leave → Payroll
Loan → Payroll
Payroll Validation
Payroll State Machine
Payroll Lock
Payroll Reversal
GL Idempotency
Transactions
Audit
Concurrency
```

## 🟠 High

``` text
Attendance Correction
Work Calendar
Shift Management
Holiday Management
Bank Transfer
Payroll Explanation
Reporting
Notifications
RBAC
```

## 🟢 Later

``` text
Performance
Training
Benefits
Advanced Recruitment
Advanced Asset Management
Advanced Expense Management
```

------------------------------------------------------------------------

# 74. الخلاصة

الهدف ليس فقط أن يكون النظام قادرًا على:

``` text
Calculate Salary
```

بل أن يكون قادرًا على:

``` text
Calculate
Explain
Validate
Approve
Post
Audit
Recalculate
Reverse
```

مع الاحتفاظ بتاريخ القواعد والرواتب والحضور.

**Payroll وAttendance يجب أن يكونا Engines مبنيين على Configuration +
Effective Dating + Transactions + Audit، وليس مجموعة أرقام وIF
statements داخل الكود.**
