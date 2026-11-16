# BASE-02 — خريطة علاقات HR

5 نوفمبر 2026. خريطة للكود الجاري ولحدود التنفيذ؛ ليست DDL أو شهادة اكتمال جميع قيود Oracle. مراجعة DEV_TEMPLATE الحالية تثبت قراءة 43 نوع EF مقابل 66 جدول HR؛ الجداول القديمة غير الممثلة في EF تحتاج مراجعة مستقلة قبل ترحيل العملاء.

في الرسوم: السهم المتصل علاقة معرفة في إعداد EF، والسهم المنقط رابط حقول/استعلام أو أثر أعمال في الخدمة دون ادعاء FK. العقد التي تحمل «مخطط» لم تُنفذ كدورة متكاملة. CompanyScope سياق شركة موثوق من ERP، وليس ادعاء وجود جدول SYS_COMPANY داخل مخطط العميل؛ العزل الأساسي مخطط مستقل ثم نطاق الشركة/الفروع/الموظف داخل الطلب.

## التنظيم والدوام

```mermaid
flowchart TD
    CS["CompanyScope — سياق الشركة"] -.->|CompanyId| B["SYS_BRANCH"]
    B -.->|BranchId| E["HR_EMPLOYEE"]
    B -->|BranchId| GPS["HR_BRANCH_ATTENDANCE_LOCATION"]
    GPS -.->|saved coordinates and radius| RAW["HR_RAW_ATTENDANCE"]
    D["HR_DEPARTMENT — جدول قديم"] -.->|DepartmentCode| E
    P["HR_POSITION — جدول قديم"] -.->|PositionCode| E
    E -.->|ManagerEmployeeCode| E
    U["SYS_USERS"] -->|UserId optional| E
    E -->|EmployeeCode| DEP["HR_EMPLOYEE_DEPENDENT"]
    CS -.->|CompanyId| TZ["HR_ATTENDANCE_TIME_SETTINGS"]
    E -.->|EmployeeCode| ASG["HR_EMPLOYEE_SHIFT_ASSIGNMENT"]
    SH["HR_SHIFT_SCHEDULE"] -.->|ShiftCode| ASG
    E -->|EmployeeCode| SD["HR_EMPLOYEE_SHIFT_DAY"]
    SH -->|ShiftCode optional for rest| SD
    E -.->|EmployeeCode| RAW["HR_RAW_ATTENDANCE"]
    E -.->|EmployeeCode + AttendanceDate| DAY["HR_ATTENDANCE_DAY"]
    RAW -->|AttendanceDayId optional until applied| DAY
    SD -.->|resolve date + UTC snapshot| DAY
    TZ -.->|immutable company zone| DAY
    E -.->|EmployeeCode| COR["HR_ATTENDANCE_CORRECTION"]
    COR -.->|approved versioned change| DAY
```

Employee.DepartmentCode وPositionCode وManagerEmployeeCode حقول حالية، دون علاقة إدارة/مدير EF متكاملة في EmployeeConfiguration. تحديث 6 نوفمبر: رُبط HR_DEPARTMENT/HR_POSITION للقراءة بالنطاق دون DDL، مع التحقق من توافق الإدارة/المسمى/الفرع ومنع دورات المدير في الخدمة. أُضيفت إدارة الكتالوج ودورات الهيكل عند تحريره ضمن [حزمة الكتالوج](HR_ORGANIZATION_CATALOG_2026-11-06_AR.md)، دون تغيير FK أو DDL؛ مدير الإدارة وصفحات SET-02 بقيتا ضمن EMP-02 آنذاك؛ EMP-03 وBASE-04 مستقلتان. ارتباط حساب المستخدم اختياري وفريد بحسب تعريف EF، ولا يساوي منح الموظف صلاحية إدارية.

EmployeeShiftAssignment يربط الموظف والشفت برموز في الخدمة، مع BASE/TEMPORARY وفترة سريان؛ تعريفه الحالي لا يعلن HasOne بينهما. EmployeeShiftDay يعلن FK للموظف والشفت، وتفرد الموظف/التاريخ حتى للسجل غير الفعال؛ يوم الراحة يسمح ShiftCode=null. اختيار اليوم أسبق من الإسناد، والمؤقت يتقدم على الأساسي. شفت الليل يتبع تاريخ البداية، ولقطة UTC في يوم الحضور تحمي الحساب التاريخي.

raw دليل مصدر مستقل؛ EventId مع الموظف يمنع تكرار الدليل الجديد. ResolvedWorkDate رابط منطقي لتاريخ العمل، وAttendanceDayId رابط EF/Oracle محدود الحذف بعد التطبيق فقط. المرفوض/المتجاهل يحتفظ بالدليل والتدقيق دون يوم؛ المعلق يبقى عند قفل الرواتب. التصحيح يرتبط بالموظف/التاريخ وOriginalDayVersion؛ الموافقة تعدل اليوم ولا تمحو دليل raw، والدليل المتأخر لا يكتب فوق تصحيح معتمد بلا مراجعة. ATT-09 ما زالت تفصل الجلسات المتعددة عن اختزال أول IN/آخر OUT.

## الإجازات والأرصدة

```mermaid
flowchart TD
    E["HR_EMPLOYEE"] -->|EmployeeCode| REQ["HR_LEAVE_REQUEST"]
    LT["HR_LEAVE_TYPE"] -->|LeaveTypeCode| REQ
    E -->|EmployeeCode| BAL["HR_LEAVE_BALANCE"]
    LT -->|LeaveTypeCode| BAL
    LT -->|LeaveTypeCode| POL["HR_LEAVE_POLICY"]
    BAL -->|BalanceId restrict delete| MOV["HR_LEAVE_MOVEMENT"]
    MOV -->|ReversesMovementId optional| MOV
    REQ -.->|source operation key and used delta| MOV
    CLOSE["HR_LEAVE_YEAR_CLOSE"] -->|CloseId| CL["HR_LEAVE_YEAR_CLOSE_LINE"]
    CL -.->|employee + type + cycle and source key| MOV
    SET["HR_LEAVE_SETTINGS — CompanyId"] -.->|company cycle settings| BAL
```

الرصيد له مفتاح دورة الموظف/النوع/بداية الدورة؛ YearNo محفوظ للتوافق ولا يختزل دورة تبدأ في شهر مختلف. دفتر الحركة تسلسلي وغير قابل للتعديل/الحذف، ويسجل before/after بالدلتا والحالة بعد الحركة، والسبب والفاعل ومصدر العملية ومفتاح تكرارها؛ الرابط إلى طلب/إقفال هو مصدر عملية في الخدمة وليس FK عاماً من Movement إلى كل أنواع الوثائق.

رأس الإقفال يحمي الدورة، وخطوطه تشرح الترحيل/الانتهاء/التحويل. الإقفال الفارغ ما زال يمنع الكتابة في الدورة المقفلة. عكس الإقفال يتبع الحركات التابعة ولا يحذف سجل الحركة؛ إلغاء طلب الإجازة وعكس تعديل الرصيد الكامل لهما مهامهما المفتوحة.

## الراتب وروابط ERP

```mermaid
flowchart TD
    E["HR_EMPLOYEE"] -->|EmployeeCode| SS["HR_SALARY_STRUCTURE — dated"]
    SS -->|StructureId| SSL["HR_SALARY_STRUCTURE_LINE"]
    COMP["HR_SALARY_COMPONENT"] -->|ComponentCode| SSL
    PERIOD["HR_PAYROLL_PERIOD"] -.->|PayPeriod / PeriodCode + company| RUN["HR_PAYROLL_RUN"]
    RUN -->|PayrollRunId| LINE["HR_PAYROLL_RUN_LINE"]
    E -->|EmployeeCode| LINE
    LINE -->|PayrollLineId| LC["HR_PAYROLL_RUN_LINE_COMPONENT"]
    LINE -->|PayrollRunLineId one-to-one| SNAP["HR_PAYROLL_CALC_SNAPSHOT"]
    E -->|EmployeeCode| LOAN["HR_EMPLOYEE_LOAN"]
    LOAN -->|EmployeeLoanId| INST["HR_LOAN_REPAYMENT_SCHEDULE"]
    INST -.->|PayrollRunLineId optional| LINE
    E -->|EmployeeCode| ADV["HR_EMPLOYEE_ADVANCE"]
    ADV -.->|TargetPayPeriod / settlement| LINE
    E -->|EmployeeCode| ADJ["HR_PAYROLL_ADJUSTMENT"]
    COMP -->|ComponentCode| ADJ
    ADJ -.->|PayrollRunLineId optional| LINE
    DAY["HR_ATTENDANCE_DAY"] -.->|work date range / input fingerprint| LINE
    LEAVE["HR_LEAVE_REQUEST / balance"] -.->|unpaid segments / input fingerprint| LINE
    PC["HR_GL_POSTING_CONFIG — branch accounts"] -.->|posting service| RUN
    RUN -.->|JournalVoucherId optional| JV["ERP journal voucher"]
    LINE -.->|currency / beneficiary / bank format| BANK["Bank export — no payment confirmation"]
```

SalaryStructure بتاريخ السريان هو مدخل الحساب؛ مقاطع الأساسي/العلاوات والاقتطاعات والإجازة غير المدفوعة تُجمع قبل الضرائب/الضمان. PayrollPeriod يرتبط بالمسير عبر رمز فترة وسياق الشركة والخدمة، لا عبر PayrollPeriodId FK على PayrollRun. أقفال الفترة والموظف وحالة المسير وبصمة المدخلات تمنع تغيير المدخلات بعد الإقفال/الاعتماد دون الدورة المطلوبة.

PayrollRunLine يملك مكوناته ولقطة الحساب؛ الروابط إلى قسط/تعديل مستهلك تحفظ هوية سطر المسير. السلفة الحالية تربط الفترة والموظف والتسوية، دون الادعاء بوجود رابط سطر دائم غير موجود في النموذج. عكس اعتماد المسير PAY-09 يجب أن يعالج كل تسويات التوابع؛ cancel للحساب لا يحقق ذلك.

JournalVoucherId وGlAccountCode وروابط إعداد حسابات الفرع هي نقاط اتصال ERP الحالية؛ هذا الرسم لا يثبت أن القيود المحاسبية والعكس والدفع اكتملت. تصدير البنك ملف/معاينة من بيانات المستفيد والعملة لا يثبت تنفيذ دفع أو تأكيد البنك. ERP-01.. وبقية مهام الرواتب تحدد التحقق المتبقي. الإضافي مؤجل ولا يدخل الصافي تلقائياً.

## المغادرات وتسوية نهاية الخدمة — مخطط

```mermaid
flowchart LR
    E["Employee / active employment dates"] -.-> SR["مخطط: طلب مغادرة قصيرة — PERM"]
    SR -.-> DAY["Attendance sessions / late and early minutes"]
    SR -.-> PAY["Payroll inputs / approved effect"]
    E -.-> EOS["مخطط: إجراء إنهاء وتسوية — EOS"]
    EOS -.-> CLAIM["Salary + leave + loans + advances + pending movements"]
    CLAIM -.-> FINAL["Final payroll / ERP / payment / reversal"]
```

المغادرة القصيرة طلب تأخر أو خروج وعودة أو خروج مبكر، وهي غير الإجازة وغير إنهاء الخدمة. لم تُعرّف هنا جداول أو FK منفذة لها؛ PERM-01.. توثق الدورة المتبقية. TerminationDate/TerminationReason الحالية على Employee تتحكم ببعض حدود الأهلية، لكنها لا تنشئ ملف إنهاء واعتماده وتجميع حقوقه وتسويته. EOS-01..06 تبقى مفتوحة، والربط أعلاه تصميم حدود العمل فقط.

## المصادر وحدود التحقق

رُوجعت نماذج EmployeeEntities وAttendanceEntities وEmployeeShiftDay وLeaveEntities وLeaveBalanceMovement وPayrollEntities وLoanAndAdjustmentEntities، وإعدادات EF المقابلة. المصادر الرئيسية: [إعداد الموظف](../../src/ThinkOnErp.Infrastructure/Data/Configurations/Hr/EmployeeConfigurations.cs)، [إعداد الحضور](../../src/ThinkOnErp.Infrastructure/Data/Configurations/Hr/AttendanceConfigurations.cs)، [اليومي](../../src/ThinkOnErp.Infrastructure/Data/Configurations/Hr/EmployeeShiftDayConfiguration.cs)، [الإجازات](../../src/ThinkOnErp.Infrastructure/Data/Configurations/Hr/LeaveConfigurations.cs)، [دفتر الإجازات](../../src/ThinkOnErp.Infrastructure/Data/Configurations/Hr/LeaveMovementConfiguration.cs)، [الرواتب](../../src/ThinkOnErp.Infrastructure/Data/Configurations/Hr/PayrollConfigurations.cs)، [القروض والتعديلات](../../src/ThinkOnErp.Infrastructure/Data/Configurations/Hr/LoanAndAdjustmentConfigurations.cs).

الروابط أعلاه تشير من docs/hr إلى جذر المستودع. فحوص Oracle للقراءة والأقفال والحركات/rollback ومطابقة البيانات الأصلية في att05-oracle.log وatt07-regression.log مستقلة؛ الخريطة لم تطبق DDL أو تزامن عملاء أو اختبار كل FK قديم. الجرد الحديث [116 API](../HR_API_REFERENCE_2026-11-05.md) يربط هذه الدورات بمساراتها الحالية؛ [الخطة](../../HR_ROADMAP_AR.md) تحسم ما نُفذ وما بقي.

تحديث 6 نوفمبر: رابط ManagerEmployeeCode يُستخدم أيضاً في [قراءة الفريق المباشر](HR_EMPLOYEE_TEAM_2026-11-06_AR.md)، مقاطعاً منح الشركة/الفروع دون تفويض تلقائي أو توسع للأحفاد. [معاينة الاستيراد](HR_EMPLOYEE_IMPORT_PREVIEW_2026-11-06_AR.md) تعرض أثر الملف غير المؤرخ على الفترات ولا تحفظ أو تنشئ علاقة مالية جديدة. النموذج43 EF/66 جدول HR كما كان في ذلك التاريخ. تحديث 13 نوفمبر: أضيف عمود `HR_DEPARTMENT.MANAGER_EMPLOYEE_CODE` nullable بلا backfill إلى `DEV_TEMPLATE`؛ [العقد المعتمد](HR_DEPARTMENT_MANAGER_CONTRACT_2026-11-13_AR.md) يصف علاقة الإدارة بالمدير، ولا يغير رابط الفريق المباشر بين الموظفين أو يمنح صلاحية تلقائية.
