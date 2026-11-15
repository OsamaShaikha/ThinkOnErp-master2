# دليل جميع API الخاصة بـHR — 2026-11-05

تم استخراج **119 نقطة نهاية** من **17 متحكمًا** في الكود الحالي. هذا جرد للكود، وليس إثبات نشر أو نجاح كل طلب على الخادم.

## طريقة القراءة والاستخدام

- المسارات تبدأ من عنوان API الذي تشغله، مثل `{baseUrl}/api/hr/...`؛ لا تفترض عنوان خادم أو منفذًا معينًا.
- أرسل `Authorization: Bearer <accessToken>` وسياق الشركة المعتاد. لا يرسل العميل اسم schema حرًا لاختيار قاعدة أخرى؛ SuperAdmin يحدد الشركة عبر `X-Company-Id` أو `X-Company-Code` حسب وسيط توجيه الشركة.
- `companyId` داخل Query/Body، حيث يظهر، يجب أن يطابق الشركة المختارة؛ هو غير `branchId`، وكلاهما غير `employeeCode`.
- `?` في النوع يعني قيمة nullable، و`= ...` قيمة افتراضية في توقيع الدالة؛ لا يعني ذلك وحده وجود تحقق أعمال كافٍ.
- `route/body inferred` تعني أن ASP.NET يستنتج الربط: المعرف الظاهر بين `{}` من المسار، والكائن المعقد غير الموسوم من JSON body.
- `ذاتي` يعني دعم وصول الموظف المرتبط إلى سجلاته فقط، إضافة إلى وصول الإدارة صاحبة المنح. بقية العمليات تحتاج منحًا إداريًا صريحًا، أو مسؤولًا مخولًا.
- إذن قرار process للتصحيح والإجازة يُحسم من `Approved` بواسطة HrAccessFilter؛ رمز الشاشة والمنح لا يستنتجان من المسار.
- المستهلك المذكور مقصد العقد من الكود، وليس إثبات استعمال شاشة أو عميل منشور. خانة التغيير تربط ما يلزم قبل التكامل؛ لا تُحذف مسارات لغياب شاشة.
- المصدر مع أرقام الأسطر يمثل هذه اللقطة فقط. حالات HTTP المعلنة من السمات لا تشمل بالضرورة جميع أخطاء middleware.
- غالبية الردود `ApiResponse<T>`؛ إعدادات الترحيل المالي تعيد كائنًا مباشرًا، وتنزيل Excel/البنك يعيد ملفًا. أخطاء التفويض قد تكون 401/403 بلا غلاف موحد.
- تسجيل الدخول خدمة مشتركة: `POST /api/Auth/login` مع `CompanyLoginCommand`؛ التوكن في `data.accessToken` وفق `TokenDto`.

## AttendanceController — 9 API

رمز الشاشة: `hr-attendance`.

### `GET /api/hr/attendance/employees/{employeeCode}/effective-shift`

الشفت الفعال بتاريخ العمل.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: string employeeCode`؛ `query: DateOnly workDate` ([FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<EffectiveShiftDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<EffectiveShiftDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetEffectiveShift](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceController.cs:38).

### `GET /api/hr/attendance/employees/{employeeCode}/effective-shift-utc`

معاينة حدود الشفت UTC.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: string employeeCode`؛ `query: DateOnly workDate` ([FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `Task<ActionResult<ApiResponse<TimedShiftDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<TimedShiftDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [PreviewUtc](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceController.cs:48).

### `GET /api/hr/attendance/employees/{employeeCode}/match-punch-shift`

مطابقة لحظة بصمة بالشفت.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: string employeeCode`؛ `query: string instant` ([FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired])؛ `query: string punchType` ([FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `Task<ActionResult<ApiResponse<TimedShiftDto?>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<TimedShiftDto?>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [MatchPunchShift](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceController.cs:55).

### `GET /api/hr/attendance/employees/{employeeCode}/day-classification`

معاينة تصنيف اليوم.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: string employeeCode`؛ `query: DateOnly workDate` ([FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `Task<ActionResult<ApiResponse<AttendanceDayClassificationDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<AttendanceDayClassificationDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [ClassifyDay](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceController.cs:64).

### `POST /api/hr/attendance/employees/{employeeCode}/day-classification`

حفظ التصنيف تحت القفل المالي.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string employeeCode`؛ `query: DateOnly workDate` ([FromQuery, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `Task<ActionResult<ApiResponse<AttendanceDayClassificationDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<AttendanceDayClassificationDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [SaveDayClassification](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceController.cs:71).

### `POST /api/hr/attendance/check-in`

تسجيل دخول GPS.

- الصلاحية: `create`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `body: CheckInRequestDto dto` ([FromBody]).
- المستهلك المقصود: موبايل / ويب حضور.
- التعديل/الإضافة/الإبقاء: تحديث العميل قبل النشر: eventId ثابت، إيصال 202 وRawEventId؛ Data لا تعني نجاح المعالجة المالية.
- الرد: `ApiResponse<AttendancePunchResultDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<AttendancePunchResultDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status202Accepted؛ Status400BadRequest؛ Status409Conflict.
- المصدر: [CheckIn](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceController.cs:85).

### `POST /api/hr/attendance/check-out`

تسجيل خروج GPS.

- الصلاحية: `create`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `body: CheckOutRequestDto dto` ([FromBody]).
- المستهلك المقصود: موبايل / ويب حضور.
- التعديل/الإضافة/الإبقاء: تحديث العميل قبل النشر: eventId ثابت، إيصال 202 وRawEventId؛ Data لا تعني نجاح المعالجة المالية.
- الرد: `ApiResponse<AttendancePunchResultDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<AttendancePunchResultDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status202Accepted؛ Status400BadRequest؛ Status409Conflict.
- المصدر: [CheckOut](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceController.cs:118).

### `GET /api/hr/attendance/today-status`

حالة حضور اليوم.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: string employeeCode` ([FromQuery]).
- المستهلك المقصود: موبايل / ويب حضور.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<TodayAttendanceStatusDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<TodayAttendanceStatusDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status404NotFound.
- المصدر: [GetTodayStatus](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceController.cs:149).

### `GET /api/hr/attendance/nearby-branches`

الفروع الأقرب للإحداثيات.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: decimal latitude` ([FromQuery])؛ `query: decimal longitude` ([FromQuery])؛ `query: long? companyId` ([FromQuery]).
- المستهلك المقصود: موبايل / ويب حضور.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<NearbyBranchDto>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<NearbyBranchDto>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetNearbyBranches](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceController.cs:168).

## AttendanceCorrectionsController — 3 API

رمز الشاشة: `hr-attendance-corrections`.

### `POST /api/hr/attendance-corrections`

طلب تصحيح حضور.

- الصلاحية: `create`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `body: AttendanceCorrectionRequestDto dto` ([FromBody]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: UTC ونسخة يوم و409؛ الرفض المسبب مسموح عند CLOSED والموافقة محمية مالياً.
- الرد: `ApiResponse<AttendanceCorrectionRequest>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<AttendanceCorrectionRequest>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [SubmitCorrection](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceCorrectionsController.cs:39).

### `POST /api/hr/attendance-corrections/{id:long}/process`

اعتماد أو رفض تصحيح الحضور.

- الصلاحية: `approve / reject عند الرفض`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`؛ `body: ProcessAttendanceCorrectionDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: UTC ونسخة يوم و409؛ الرفض المسبب مسموح عند CLOSED والموافقة محمية مالياً.
- الرد: `ApiResponse<AttendanceCorrectionRequest>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<AttendanceCorrectionRequest>>>`.
- حالات HTTP المعلنة: Status409Conflict؛ Status200OK.
- المصدر: [ProcessCorrection](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceCorrectionsController.cs:52).

### `GET /api/hr/attendance-corrections`

قائمة طلبات التصحيح.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: string? employeeCode` ([FromQuery])؛ `query: string? status` ([FromQuery]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: UTC ونسخة يوم و409؛ الرفض المسبب مسموح عند CLOSED والموافقة محمية مالياً.
- الرد: `ApiResponse<IReadOnlyList<AttendanceCorrectionRequest>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<AttendanceCorrectionRequest>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetCorrections](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceCorrectionsController.cs:67).

## AttendanceTimeSettingsController — 2 API

رمز الشاشة: `hr-attendance`.

### `GET /api/hr/attendance/time-settings`

قراءة منطقة توقيت الشركة.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: لا توجد معاملات أعمال.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `Task<ActionResult<ApiResponse<AttendanceTimeSettings?>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<AttendanceTimeSettings?>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Get](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceTimeSettingsController.cs:18).

### `POST /api/hr/attendance/time-settings`

تهيئة منطقة توقيت الشركة مرة واحدة.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: InitializeAttendanceTimeDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `Task<ActionResult<ApiResponse<AttendanceTimeSettings>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<AttendanceTimeSettings>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Initialize](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceTimeSettingsController.cs:22).

## BranchAttendanceLocationsController — 3 API

رمز الشاشة: `hr-attendance`.

### `GET /api/hr/attendance/branches/{branchId:long}/location`

تفاصيل موقع فرع الحضور.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long branchId`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `Task<ActionResult<ApiResponse<BranchAttendanceLocationDto?>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<BranchAttendanceLocationDto?>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Get](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/BranchAttendanceLocationsController.cs:16).

### `POST /api/hr/attendance/branches/{branchId:long}/location`

إنشاء موقع فرع الحضور.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long branchId`؛ `body: WriteBranchAttendanceLocationDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `Task<ActionResult<ApiResponse<BranchAttendanceLocationDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<BranchAttendanceLocationDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Create](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/BranchAttendanceLocationsController.cs:23).

### `PUT /api/hr/attendance/branches/{branchId:long}/location`

تعديل موقع فرع الحضور.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long branchId`؛ `body: WriteBranchAttendanceLocationDto dto` ([FromBody])؛ `header: string version` ([FromHeader(Name = "If-Match")]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `Task<ActionResult<ApiResponse<BranchAttendanceLocationDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<BranchAttendanceLocationDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Update](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/BranchAttendanceLocationsController.cs:26).

## EmployeesController — 15 API

رمز الشاشة: `hr-employees`.

### `GET /api/hr/employees`

قائمة الموظفين والبحث والترقيم.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: string? searchKeyword` ([FromQuery])؛ `query: string? departmentCode` ([FromQuery])؛ `query: string? status` ([FromQuery])؛ `query: long? branchId` ([FromQuery])؛ `query: int pageIndex = 1` ([FromQuery])؛ `query: int pageSize = 20` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<PagedResultDto<EmployeeSummaryDto>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PagedResultDto<EmployeeSummaryDto>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetEmployees](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:41).

### `GET /api/hr/employees/export`

تصدير الموظفين Excel.

- الصلاحية: `export`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: string? searchKeyword` ([FromQuery])؛ `query: string? departmentCode` ([FromQuery])؛ `query: string? status` ([FromQuery])؛ `query: long? branchId` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `FileContentResult`؛ نوع التوقيع: `Task<IActionResult>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [ExportEmployees](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:61).

### `GET /api/hr/employees/export-template`

قالب استيراد الموظفين.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: لا توجد معاملات أعمال.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `FileContentResult`؛ نوع التوقيع: `Task<IActionResult>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [DownloadTemplate](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:78).

### `POST /api/hr/employees/import/validate`

فحص Excel قبل الاستيراد.

- الصلاحية: `import`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `form: IFormFile? file`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<EmployeeImportResultDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<EmployeeImportResultDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status400BadRequest.
- المصدر: [ValidateImport](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:89).

### `POST /api/hr/employees/import`

استيراد الموظفين أو تحديثهم.

- الصلاحية: `import`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `form: IFormFile? file`؛ `query: bool updateExisting = false` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<EmployeeImportResultDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<EmployeeImportResultDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status400BadRequest.
- المصدر: [ImportEmployees](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:113).

### `GET /api/hr/employees/{code}`

تفاصيل الموظف.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<EmployeeDetailsDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<EmployeeDetailsDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetEmployeeByCode](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:143).

### `GET /api/hr/employees/me/user-link`

الموظف المرتبط بالحساب الحالي.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: لا توجد معاملات أعمال.
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `Task<ActionResult<ApiResponse<EmployeeUserLinkDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<EmployeeUserLinkDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [GetMyEmployeeLink](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:158).

### `PUT /api/hr/employees/{code}/user-link`

ربط أو فك ربط الحساب.

- الصلاحية: `link-user`؛ الوصول: **مسؤول فقط**.
- المدخلات: `route/body inferred: string code`؛ `body: LinkEmployeeUserDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `Task<ActionResult<ApiResponse<EmployeeUserLinkDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<EmployeeUserLinkDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [LinkEmployeeUser](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:172).

### `POST /api/hr/employees`

إنشاء موظف.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreateEmployeeDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: بوابة رواتب قبل الحفظ وفق EMP-05/11؛ الملف والمعالون غير مؤرخين حالياً؛ CLOSED أو الاعتماد قد يمنع الكتابة.
- الرد: `ApiResponse<EmployeeDetailsDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<EmployeeDetailsDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [CreateEmployee](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:183).

### `PUT /api/hr/employees/{code}`

تعديل موظف.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`؛ `body: UpdateEmployeeDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: بوابة رواتب قبل الحفظ وفق EMP-05/11؛ الملف والمعالون غير مؤرخين حالياً؛ CLOSED أو الاعتماد قد يمنع الكتابة.
- الرد: `ApiResponse<EmployeeDetailsDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<EmployeeDetailsDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [UpdateEmployee](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:195).

### `DELETE /api/hr/employees/{code}`

تعطيل الموظف وإنهاء حالته.

- الصلاحية: `delete`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: بوابة رواتب قبل الحفظ وفق EMP-05/11؛ الملف والمعالون غير مؤرخين حالياً؛ CLOSED أو الاعتماد قد يمنع الكتابة.
- الرد: `ApiResponse<bool>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<bool>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [DeleteEmployee](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:208).

### `POST /api/hr/employees/{code}/dependents`

إضافة معال.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`؛ `body: CreateDependentDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: بوابة رواتب قبل الحفظ وفق EMP-05/11؛ الملف والمعالون غير مؤرخين حالياً؛ CLOSED أو الاعتماد قد يمنع الكتابة.
- الرد: `ApiResponse<DependentDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<DependentDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [AddDependent](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:220).

### `DELETE /api/hr/employees/dependents/{id:long}`

حذف معال.

- الصلاحية: `delete`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: بوابة رواتب قبل الحفظ وفق EMP-05/11؛ الملف والمعالون غير مؤرخين حالياً؛ CLOSED أو الاعتماد قد يمنع الكتابة.
- الرد: `ApiResponse<bool>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<bool>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [DeleteDependent](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:233).

### `POST /api/hr/employees/{code}/salary-structure`

تعيين تعريف راتب.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`؛ `body: AssignSalaryStructureDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<SalaryStructureDetailsDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<SalaryStructureDetailsDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [AssignSalaryStructure](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:245).

### `GET /api/hr/employees/{code}/salary-structure`

تعريف الراتب النشط بتاريخ معين.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`؛ `query: DateTime? asOfDate` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<SalaryStructureDetailsDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<SalaryStructureDetailsDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetSalaryStructure](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:258).

## LeavesController — 22 API

رمز الشاشة: `hr-leaves`.

### `GET /api/hr/leaves/types`

أنواع الإجازات.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: bool activeOnly = true` ([FromQuery]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<LeaveTypeDto>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<LeaveTypeDto>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetLeaveTypes](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:38).

### `GET /api/hr/leaves/types/{code}`

تفاصيل نوع إجازة.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: string code`.
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveTypeDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveTypeDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status404NotFound.
- المصدر: [GetLeaveTypeByCode](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:50).

### `POST /api/hr/leaves/types`

إنشاء نوع إجازة.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreateLeaveTypeDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveTypeDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveTypeDto>>>`.
- حالات HTTP المعلنة: Status201Created؛ Status400BadRequest.
- المصدر: [CreateLeaveType](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:66).

### `PUT /api/hr/leaves/types/{code}`

تعديل نوع إجازة.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`؛ `body: UpdateLeaveTypeDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveTypeDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveTypeDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status404NotFound.
- المصدر: [UpdateLeaveType](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:79).

### `DELETE /api/hr/leaves/types/{code}`

تعطيل نوع إجازة.

- الصلاحية: `delete`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<bool>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<bool>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status404NotFound.
- المصدر: [DeleteLeaveType](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:93).

### `GET /api/hr/leaves/requests`

قائمة طلبات الإجازة.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: string? employeeCode` ([FromQuery])؛ `query: string? leaveTypeCode` ([FromQuery])؛ `query: string? status` ([FromQuery])؛ `query: DateTime? fromDate` ([FromQuery])؛ `query: DateTime? toDate` ([FromQuery])؛ `query: int pageIndex = 1` ([FromQuery])؛ `query: int pageSize = 20` ([FromQuery]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<PagedResultDto<LeaveRequestDto>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PagedResultDto<LeaveRequestDto>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetLeaveRequests](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:109).

### `POST /api/hr/leaves/requests`

تقديم طلب إجازة.

- الصلاحية: `create`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `body: SubmitLeaveRequestDto dto` ([FromBody]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveRequestDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveRequestDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [SubmitLeaveRequest](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:129).

### `POST /api/hr/leaves/requests/{id:long}/process`

اعتماد أو رفض إجازة.

- الصلاحية: `approve / reject عند الرفض`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`؛ `body: ProcessLeaveRequestDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveRequestDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveRequestDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [ProcessLeaveRequest](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:141).

### `GET /api/hr/leaves/balances/{employeeCode}`

أرصدة إجازات الموظف.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: string employeeCode`؛ `query: int? year` ([FromQuery]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<LeaveBalanceDto>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<LeaveBalanceDto>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetEmployeeBalances](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:154).

### `GET /api/hr/leaves/balances/{employeeCode}/movements`

دفتر حركات رصيد الإجازة.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: string employeeCode`؛ `query: string? leaveTypeCode = null` ([FromQuery])؛ `query: DateTime? cycleStart = null` ([FromQuery])؛ `query: int page = 1` ([FromQuery])؛ `query: int size = 50` ([FromQuery]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveMovementPageDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveMovementPageDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetMovements](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:166).

### `POST /api/hr/leaves/balances/grant`

منح أو تصحيح رصيد إجازة.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: GrantLeaveBalanceDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveBalanceDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveBalanceDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status400BadRequest.
- المصدر: [GrantOrAdjustBalance](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:178).

### `POST /api/hr/leaves/balances/accrue-monthly`

تنفيذ استحقاق شهري يدوي.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: MonthlyLeaveAccrualDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveAccrualResultDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveAccrualResultDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status400BadRequest.
- المصدر: [RunMonthlyAccrual](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:191).

### `POST /api/hr/leaves/balances/yearly-grant`

تنفيذ توزيع سنوي.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: YearlyLeaveAllocationDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveAccrualResultDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveAccrualResultDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status400BadRequest.
- المصدر: [RunYearlyAllocation](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:204).

### `POST /api/hr/leaves/balances/year-end-rollover`

ترحيل الرصيد بين سنتين.

- الصلاحية: `post`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: YearEndRolloverDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveRolloverResultDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveRolloverResultDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status400BadRequest.
- المصدر: [RunYearEndRollover](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:217).

### `POST /api/hr/leaves/balances/year-end-close/draft`

إنشاء مسودة إقفال دورة الإجازة.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: YearEndRolloverDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveYearCloseDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveYearCloseDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [CreateYearEndCloseDraft](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:229).

### `GET /api/hr/leaves/balances/year-end-close/{id:long}`

تفاصيل إقفال دورة الإجازة.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveYearCloseDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveYearCloseDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetYearEndClose](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:241).

### `POST /api/hr/leaves/balances/year-end-close/{id:long}/post`

اعتماد إقفال دورة الإجازة.

- الصلاحية: `post`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveYearCloseDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveYearCloseDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [PostYearEndClose](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:250).

### `POST /api/hr/leaves/balances/year-end-close/{id:long}/unpost`

عكس إقفال دورة الإجازة.

- الصلاحية: `reverse`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeaveYearCloseDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeaveYearCloseDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [UnpostYearEndClose](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:260).

### `GET /api/hr/leaves/policies`

سياسات الإجازات.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: لا توجد معاملات أعمال.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetLeavePolicies](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:270).

### `GET /api/hr/leaves/settings`

قراءة إعدادات الإجازات.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: لا توجد معاملات أعمال.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<HrLeaveSettingsDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<HrLeaveSettingsDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetLeaveSettings](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:280).

### `PUT /api/hr/leaves/settings`

تحديث إعدادات الإجازات.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: UpdateHrLeaveSettingsDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<HrLeaveSettingsDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<HrLeaveSettingsDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status400BadRequest.
- المصدر: [UpdateLeaveSettings](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:290).

### `POST /api/hr/leaves/policies`

حفظ سياسة الإجازة حسب النوع.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreateLeavePolicyDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<LeavePolicyDetailsDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<LeavePolicyDetailsDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status400BadRequest.
- المصدر: [CreateOrUpdateLeavePolicy](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:303).

## LoansController — 6 API

رمز الشاشة: `hr-loans`.

### `POST /api/hr/loans`

طلب قرض وإنشاء أقساطه.

- الصلاحية: `create`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `body: CreateLoanApplicationDto dto` ([FromBody]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<EmployeeLoan>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<EmployeeLoan>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [CreateLoan](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LoansController.cs:39).

### `POST /api/hr/loans/{id:long}/approve`

اعتماد القرض.

- الصلاحية: `approve`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<EmployeeLoan>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<EmployeeLoan>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [ApproveLoan](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LoansController.cs:51).

### `GET /api/hr/loans`

قائمة القروض.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: string? employeeCode` ([FromQuery])؛ `query: string? status` ([FromQuery]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<EmployeeLoan>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<EmployeeLoan>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetLoans](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LoansController.cs:63).

### `POST /api/hr/loans/advances`

طلب سلفة.

- الصلاحية: `create`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `body: CreateAdvanceRequestDto dto` ([FromBody]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<EmployeeAdvance>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<EmployeeAdvance>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [CreateAdvance](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LoansController.cs:75).

### `POST /api/hr/loans/advances/{id:long}/approve`

اعتماد السلفة.

- الصلاحية: `approve`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<EmployeeAdvance>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<EmployeeAdvance>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [ApproveAdvance](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LoansController.cs:87).

### `GET /api/hr/loans/advances`

قائمة السلف.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: string? employeeCode` ([FromQuery])؛ `query: string? targetPayPeriod` ([FromQuery]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<EmployeeAdvance>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<EmployeeAdvance>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetAdvances](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LoansController.cs:99).

## PayrollAdjustmentsController — 8 API

رمز الشاشة: `hr-payroll-adjustments`.

### `POST /api/hr/adjustments`

إنشاء تعديل راتب PENDING.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreatePayrollAdjustmentDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: الجديد PENDING؛ approve وpost يعتمدان إلى APPROVED، unpost يعيده PENDING مع حماية المستهلك/المسير.
- الرد: `ApiResponse<PayrollAdjustment>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PayrollAdjustment>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [CreateAdjustment](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:39).

### `POST /api/hr/adjustments/{id:long}/approve`

اعتماد تعديل.

- الصلاحية: `approve`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: الجديد PENDING؛ approve وpost يعتمدان إلى APPROVED، unpost يعيده PENDING مع حماية المستهلك/المسير.
- الرد: `ApiResponse<PayrollAdjustment>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PayrollAdjustment>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [ApproveAdjustment](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:51).

### `POST /api/hr/adjustments/{id:long}/reject`

رفض تعديل.

- الصلاحية: `reject`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: الجديد PENDING؛ approve وpost يعتمدان إلى APPROVED، unpost يعيده PENDING مع حماية المستهلك/المسير.
- الرد: `ApiResponse<PayrollAdjustment>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PayrollAdjustment>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [RejectAdjustment](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:63).

### `POST /api/hr/adjustments/{id:long}/post`

اعتماد/Post تعديل PENDING إلى مدخل الراتب.

- الصلاحية: `post`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: الجديد PENDING؛ approve وpost يعتمدان إلى APPROVED، unpost يعيده PENDING مع حماية المستهلك/المسير.
- الرد: `Task<ActionResult<ApiResponse<PayrollAdjustment>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PayrollAdjustment>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [PostAdjustment](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:74).

### `POST /api/hr/adjustments/{id:long}/unpost`

عكس ترحيل تعديل مع فحص التوابع.

- الصلاحية: `reverse`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: الجديد PENDING؛ approve وpost يعتمدان إلى APPROVED، unpost يعيده PENDING مع حماية المستهلك/المسير.
- الرد: `Task<ActionResult<ApiResponse<PayrollAdjustment>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PayrollAdjustment>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [UnpostAdjustment](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:82).

### `GET /api/hr/adjustments`

قائمة التعديلات.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId` ([FromQuery])؛ `query: string? employeeCode` ([FromQuery])؛ `query: string? payPeriod` ([FromQuery])؛ `query: string? status` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: الجديد PENDING؛ approve وpost يعتمدان إلى APPROVED، unpost يعيده PENDING مع حماية المستهلك/المسير.
- الرد: `ApiResponse<IReadOnlyList<PayrollAdjustmentDto>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<PayrollAdjustmentDto>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetAdjustments](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:91).

### `GET /api/hr/adjustments/{id:long}`

تفاصيل تعديل.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: الجديد PENDING؛ approve وpost يعتمدان إلى APPROVED، unpost يعيده PENDING مع حماية المستهلك/المسير.
- الرد: `ApiResponse<PayrollAdjustment>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PayrollAdjustment>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetAdjustmentById](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:105).

### `DELETE /api/hr/adjustments/{id:long}`

حذف تعديل.

- الصلاحية: `delete`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: الجديد PENDING؛ approve وpost يعتمدان إلى APPROVED، unpost يعيده PENDING مع حماية المستهلك/المسير.
- الرد: `ApiResponse<bool>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<bool>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [DeleteAdjustment](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:117).

## PayrollController — 11 API

رمز الشاشة: `hr-payroll`.

### `POST /api/hr/payroll/calculate`

حساب مسير راتب جديد لفرع.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreatePayrollRunDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<PayrollCalculationResultDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PayrollCalculationResultDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [CalculatePayroll](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:45).

### `POST /api/hr/payroll/{id:long}/approve`

اعتماد المسير وتسوية الاقتطاعات.

- الصلاحية: `approve`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<PayrollRun>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PayrollRun>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [ApprovePayroll](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:57).

### `POST /api/hr/payroll/{id:long}/cancel`

إلغاء مسير محسوب غير معتمد.

- الصلاحية: `reverse`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`؛ `body: CancelPayrollRunDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إضافة إلغاء المحسوب؛ عكس الاعتماد المالي مهمة مستقلة.
- الرد: `ApiResponse<PayrollCalculationResultDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PayrollCalculationResultDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [CancelPayroll](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:69).

### `POST /api/hr/payroll/{id:long}/post-to-gl`

ترحيل المسير إلى المحاسبة.

- الصلاحية: `post`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<PostGlVoucherResultDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PostGlVoucherResultDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [PostToGl](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:79).

### `POST /api/hr/payroll/{id:long}/reverse-gl`

عكس حالة قيد الرواتب.

- الصلاحية: `reverse`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<PayrollRun>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PayrollRun>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [ReverseGl](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:91).

### `GET /api/hr/payroll/{id:long}`

نتيجة مسير بالمعرف.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<PayrollCalculationResultDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PayrollCalculationResultDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetPayrollRun](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:103).

### `GET /api/hr/payroll/{id:long}/export-bank`

تنزيل ملف البنك.

- الصلاحية: `export`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`؛ `query: string? format` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `FileContentResult`؛ نوع التوقيع: `Task<IActionResult>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [ExportBankFile](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:115).

### `GET /api/hr/payroll/{id:long}/export-bank/preview`

معاينة تعيد بيانات الملف البنكي ومحتواه.

- الصلاحية: `export`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`؛ `query: string? format` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<BankPayrollExportResultDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<BankPayrollExportResultDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [PreviewBankExport](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:127).

### `GET /api/hr/payroll/run-lines/{lineId:long}/explanation`

شرح احتساب بند الموظف.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long lineId`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<PayrollExplanationDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PayrollExplanationDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetCalculationExplanation](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:139).

### `GET /api/hr/payroll/periods`

قائمة فترات الرواتب.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId` ([FromQuery])؛ `query: int? year` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<PayrollPeriod>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<PayrollPeriod>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetPeriods](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:151).

### `POST /api/hr/payroll/periods`

إنشاء فترة رواتب.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: PayrollPeriod period` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<PayrollPeriod>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PayrollPeriod>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status400BadRequest.
- المصدر: [CreatePeriod](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:164).

## PayrollPoliciesController — 8 API

رمز الشاشة: `hr-payroll-policies`.

### `GET /api/hr/payroll-policies/proration`

سياسات تجزئة الراتب.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<ProrationPolicy>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<ProrationPolicy>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetProrationPolicies](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:40).

### `POST /api/hr/payroll-policies/proration`

إضافة سياسة تجزئة.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: ProrationPolicyDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<ProrationPolicy>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ProrationPolicy>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [AddProrationPolicy](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:51).

### `GET /api/hr/payroll-policies/ssc`

سياسات الضمان.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<SSCPolicy>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<SSCPolicy>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetSSCPolicies](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:85).

### `POST /api/hr/payroll-policies/ssc`

إضافة سياسة ضمان.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: SSCPolicyDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<SSCPolicy>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<SSCPolicy>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [AddSSCPolicy](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:96).

### `GET /api/hr/payroll-policies/tax`

سياسات الضريبة.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<TaxPolicy>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<TaxPolicy>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetTaxPolicies](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:126).

### `POST /api/hr/payroll-policies/tax`

إضافة سياسة ضريبة وشرائحها.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: TaxPolicyDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<TaxPolicy>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<TaxPolicy>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [AddTaxPolicy](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:137).

### `GET /api/hr/payroll-policies/deductions`

سياسات الاقتطاع.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<DeductionPolicy>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<DeductionPolicy>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetDeductionPolicies](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:184).

### `POST /api/hr/payroll-policies/deductions`

إضافة سياسة اقتطاع.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: DeductionPolicyDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<DeductionPolicy>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<DeductionPolicy>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [AddDeductionPolicy](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:195).

## PayrollPostingConfigurationsController — 2 API

رمز الشاشة: `hr-payroll-posting`.

### `GET /api/hr/payroll-posting/{branchId:long}`

إعداد حسابات الرواتب للفرع.

- الصلاحية: `view`؛ الوصول: **مسؤول فقط**.
- المدخلات: `route/body inferred: long branchId`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `Task<IActionResult>`؛ نوع التوقيع: `Task<IActionResult>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Get](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPostingConfigurationsController.cs:13).

### `PUT /api/hr/payroll-posting/{branchId:long}`

حفظ حسابات الرواتب للفرع.

- الصلاحية: `configure`؛ الوصول: **مسؤول فقط**.
- المدخلات: `route/body inferred: long branchId`؛ `route/body inferred: PayrollPostingConfiguration configuration`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `Task<IActionResult>`؛ نوع التوقيع: `Task<IActionResult>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Save](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPostingConfigurationsController.cs:17).

## RawAttendanceController — 6 API

رمز الشاشة: `hr-attendance`.

### `POST /api/hr/attendance/raw-events`

حفظ دليل جهاز موثوق دون معالجة مالية.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: DeviceAttendanceDto dto` ([FromBody]).
- المستهلك المقصود: تكامل أجهزة موثوق / HR.
- التعديل/الإضافة/الإبقاء: API جديد؛ معالجة صريحة دون عامل background أو مصادقة جهاز عامة.
- الرد: `Task<ActionResult<ApiResponse<RawAttendanceDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<RawAttendanceDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Ingest](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/RawAttendanceController.cs:22).

### `GET /api/hr/attendance/raw-events/{id:long}`

قراءة دليل خام ضمن نطاق الموظف.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: long id`؛ `query: string employeeCode` ([FromQuery, BindRequired]).
- المستهلك المقصود: موبايل / ويب HR.
- التعديل/الإضافة/الإبقاء: API جديد؛ معالجة صريحة دون عامل background أو مصادقة جهاز عامة.
- الرد: `Task<ActionResult<ApiResponse<RawAttendanceDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<RawAttendanceDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Get](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/RawAttendanceController.cs:35).

### `GET /api/hr/attendance/raw-events/pending`

طابور أدلة الحضور المعلقة.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: int size = 100` ([FromQuery]).
- المستهلك المقصود: ويب HR / معالج مخول صريح.
- التعديل/الإضافة/الإبقاء: API جديد؛ معالجة صريحة دون عامل background أو مصادقة جهاز عامة.
- الرد: `Task<ActionResult<ApiResponse<IReadOnlyList<RawAttendanceDto>>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<RawAttendanceDto>>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Pending](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/RawAttendanceController.cs:43).

### `POST /api/hr/attendance/raw-events/{id:long}/process`

معالجة دليل خام إلى اليوم المالي.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / معالج مخول صريح.
- التعديل/الإضافة/الإبقاء: API جديد؛ معالجة صريحة دون عامل background أو مصادقة جهاز عامة.
- الرد: `Task<ActionResult<ApiResponse<RawProcessingResult>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<RawProcessingResult>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Process](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/RawAttendanceController.cs:51).

### `POST /api/hr/attendance/raw-events/process-batch`

معالجة دفعة أدلة وإظهار فشل كل حدث.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: ProcessRawBatchDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / معالج مخول صريح.
- التعديل/الإضافة/الإبقاء: API جديد؛ معالجة صريحة دون عامل background أو مصادقة جهاز عامة.
- الرد: `Task<ActionResult<ApiResponse<IReadOnlyList<RawProcessingResult>>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<RawProcessingResult>>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Batch](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/RawAttendanceController.cs:55).

### `POST /api/hr/attendance/raw-events/{id:long}/ignore`

تجاهل دليل معلق بسبب موثق.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`؛ `body: IgnoreRawAttendanceDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / معالج مخول صريح.
- التعديل/الإضافة/الإبقاء: API جديد؛ معالجة صريحة دون عامل background أو مصادقة جهاز عامة.
- الرد: `Task<ActionResult<ApiResponse<RawAttendanceDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<RawAttendanceDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Ignore](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/RawAttendanceController.cs:59).

## SalaryComponentsController — 2 API

رمز الشاشة: `hr-salary-components`.

### `GET /api/hr/salary-components`

قائمة مكونات الرواتب.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: لا توجد معاملات أعمال.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<SalaryComponentDto>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<SalaryComponentDto>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetComponents](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/SalaryComponentsController.cs:38).

### `POST /api/hr/salary-components`

إنشاء مكون راتب.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreateSalaryComponentDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<SalaryComponentDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<SalaryComponentDto>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [CreateComponent](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/SalaryComponentsController.cs:48).

## ShiftAssignmentsController — 5 API

رمز الشاشة: `hr-attendance`.

### `GET /api/hr/employees/{employeeCode}/shift-assignments`

قائمة إسنادات الشفت.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: string employeeCode`؛ `query: DateOnly? from = null` ([FromQuery])؛ `query: DateOnly? to = null` ([FromQuery])؛ `query: bool activeOnly = false` ([FromQuery])؛ `query: int page = 1` ([FromQuery])؛ `query: int size = 50` ([FromQuery]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `Task<ActionResult<ApiResponse<ShiftAssignmentPageDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ShiftAssignmentPageDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [List](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftAssignmentsController.cs:17).

### `GET /api/hr/employees/{employeeCode}/shift-assignments/{id:long}`

تفاصيل إسنادات الشفت.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: string employeeCode`؛ `route/body inferred: long id`.
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `Task<ActionResult<ApiResponse<ShiftAssignmentDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ShiftAssignmentDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Get](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftAssignmentsController.cs:23).

### `POST /api/hr/employees/{employeeCode}/shift-assignments`

إنشاء إسنادات الشفت.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string employeeCode`؛ `body: WriteShiftAssignmentDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `ApiResponse<ShiftAssignmentDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ShiftAssignmentDto>>>`.
- حالات HTTP المعلنة: Status201Created؛ Status412PreconditionFailed.
- المصدر: [Create](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftAssignmentsController.cs:33).

### `PUT /api/hr/employees/{employeeCode}/shift-assignments/{id:long}`

تعديل إسنادات الشفت.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string employeeCode`؛ `route/body inferred: long id`؛ `body: WriteShiftAssignmentDto dto` ([FromBody])؛ `header: string version` ([FromHeader(Name = "If-Match")]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `ApiResponse<ShiftAssignmentDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ShiftAssignmentDto>>>`.
- حالات HTTP المعلنة: Status412PreconditionFailed.
- المصدر: [Update](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftAssignmentsController.cs:46).

### `DELETE /api/hr/employees/{employeeCode}/shift-assignments/{id:long}`

حذف إسنادات الشفت.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string employeeCode`؛ `route/body inferred: long id`؛ `header: string version` ([FromHeader(Name = "If-Match")]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `ApiResponse<bool>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<bool>>>`.
- حالات HTTP المعلنة: Status412PreconditionFailed.
- المصدر: [Delete](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftAssignmentsController.cs:61).

## ShiftDaysController — 6 API

رمز الشاشة: `hr-attendance`.

### `GET /api/hr/employees/{employeeCode}/shift-days`

قائمة الجدول اليومي.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: string employeeCode`؛ `query: DateOnly? from = null` ([FromQuery])؛ `query: DateOnly? to = null` ([FromQuery])؛ `query: bool activeOnly = false` ([FromQuery])؛ `query: int page = 1` ([FromQuery])؛ `query: int size = 50` ([FromQuery]).
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `Task<ActionResult<ApiResponse<ShiftDayPageDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ShiftDayPageDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [List](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftDaysController.cs:17).

### `GET /api/hr/employees/{employeeCode}/shift-days/{id:long}`

تفاصيل الجدول اليومي.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: string employeeCode`؛ `route/body inferred: long id`.
- المستهلك المقصود: موبايل / ويب HR ضمن النطاق.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `Task<ActionResult<ApiResponse<ShiftDayDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ShiftDayDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Get](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftDaysController.cs:23).

### `POST /api/hr/employees/{employeeCode}/shift-days`

إنشاء الجدول اليومي.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string employeeCode`؛ `body: WriteShiftDayDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `ApiResponse<ShiftDayDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ShiftDayDto>>>`.
- حالات HTTP المعلنة: Status201Created؛ Status412PreconditionFailed.
- المصدر: [Create](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftDaysController.cs:33).

### `PUT /api/hr/employees/{employeeCode}/shift-days/{id:long}`

تعديل الجدول اليومي.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string employeeCode`؛ `route/body inferred: long id`؛ `body: WriteShiftDayDto dto` ([FromBody])؛ `header: string version` ([FromHeader(Name = "If-Match")]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `ApiResponse<ShiftDayDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ShiftDayDto>>>`.
- حالات HTTP المعلنة: Status412PreconditionFailed.
- المصدر: [Update](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftDaysController.cs:46).

### `POST /api/hr/employees/{employeeCode}/shift-days/generate`

توليد الجدول اليومي مع حماية اليدوي.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string employeeCode`؛ `body: GenerateShiftDaysDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `Task<ActionResult<ApiResponse<ShiftGenerationResult>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ShiftGenerationResult>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Generate](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftDaysController.cs:60).

### `DELETE /api/hr/employees/{employeeCode}/shift-days/{id:long}`

حذف الجدول اليومي.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string employeeCode`؛ `route/body inferred: long id`؛ `header: string version` ([FromHeader(Name = "If-Match")]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `ApiResponse<bool>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<bool>>>`.
- حالات HTTP المعلنة: Status412PreconditionFailed.
- المصدر: [Delete](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftDaysController.cs:65).

## ShiftsController — 5 API

رمز الشاشة: `hr-attendance`.

### `GET /api/hr/shifts`

قائمة تعريفات الشفت.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: bool activeOnly = false` ([FromQuery])؛ `query: string? search = null` ([FromQuery])؛ `query: int page = 1` ([FromQuery])؛ `query: int size = 50` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `Task<ActionResult<ApiResponse<ShiftDefinitionPageDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ShiftDefinitionPageDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [List](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftsController.cs:17).

### `GET /api/hr/shifts/{code}`

تفاصيل تعريفات الشفت.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `Task<ActionResult<ApiResponse<ShiftDefinitionDto>>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ShiftDefinitionDto>>>`.
- حالات HTTP المعلنة: غير مصرح بها في السمات؛ راجع جسم الدالة.
- المصدر: [Get](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftsController.cs:22).

### `POST /api/hr/shifts`

إنشاء تعريفات الشفت.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreateShiftDefinitionDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `ApiResponse<ShiftDefinitionDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ShiftDefinitionDto>>>`.
- حالات HTTP المعلنة: Status201Created.
- المصدر: [Create](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftsController.cs:31).

### `PUT /api/hr/shifts/{code}`

تعديل تعريفات الشفت.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`؛ `body: WriteShiftDefinitionDto dto` ([FromBody])؛ `header: string version` ([FromHeader(Name = "If-Match")]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `ApiResponse<ShiftDefinitionDto>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<ShiftDefinitionDto>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status412PreconditionFailed.
- المصدر: [Update](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftsController.cs:41).

### `DELETE /api/hr/shifts/{code}`

حذف تعريفات الشفت.

- الصلاحية: `configure`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`؛ `header: string version` ([FromHeader(Name = "If-Match")]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: API جديد؛ واجهة الإعداد والإصدارات المؤرخة SET-03/07 مستقلة.
- الرد: `ApiResponse<bool>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<bool>>>`.
- حالات HTTP المعلنة: Status200OK؛ Status412PreconditionFailed.
- المصدر: [Delete](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/ShiftsController.cs:59).

## WorkCalendarsController — 6 API

رمز الشاشة: `hr-work-calendars`.

### `POST /api/hr/work-calendars`

إنشاء تقويم وأيام عمل.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreateWorkCalendarDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<WorkCalendar>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<WorkCalendar>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [CreateCalendar](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/WorkCalendarsController.cs:39).

### `GET /api/hr/work-calendars`

قائمة التقاويم.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<WorkCalendar>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<WorkCalendar>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetCalendars](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/WorkCalendarsController.cs:51).

### `GET /api/hr/work-calendars/{id:long}`

تفاصيل تقويم.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<WorkCalendar>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<WorkCalendar>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetCalendarById](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/WorkCalendarsController.cs:62).

### `POST /api/hr/work-calendars/holidays`

إضافة عطلة.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreatePublicHolidayDto dto` ([FromBody]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<PublicHoliday>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<PublicHoliday>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [CreateHoliday](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/WorkCalendarsController.cs:74).

### `GET /api/hr/work-calendars/holidays`

قائمة العطل.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId` ([FromQuery])؛ `query: int? year` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<IReadOnlyList<PublicHoliday>>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<IReadOnlyList<PublicHoliday>>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [GetHolidays](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/WorkCalendarsController.cs:86).

### `GET /api/hr/work-calendars/working-days-count`

حساب عدد أيام العمل.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId` ([FromQuery])؛ `query: DateTime startDate` ([FromQuery])؛ `query: DateTime endDate` ([FromQuery]).
- المستهلك المقصود: ويب HR / تكامل ERP مخول.
- التعديل/الإضافة/الإبقاء: إبقاء المسار؛ اكتمال دورة الأعمال والصلاحيات والتكامل حسب HR_ROADMAP_AR.md.
- الرد: `ApiResponse<int>`؛ نوع التوقيع: `Task<ActionResult<ApiResponse<int>>>`.
- حالات HTTP المعلنة: Status200OK.
- المصدر: [CountWorkingDays](C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/WorkCalendarsController.cs:98).

## ملاحظات مهمة على العقود

- `DELETE /employees/{code}` تعطيل منطقي، بينما حذف المعال/تعديل الراتب عمليات مختلفة؛ راجع تقرير الفجوات قبل الاعتماد عليها.
- مسار التعديلات هو `/api/hr/adjustments`، بينما رمز صلاحيته `hr-payroll-adjustments`؛ ليسا نفس النص.
- `POST /payroll/calculate` ينشئ مسيرًا جديدًا، وليس PUT لإعادة حساب المسير نفسه.
- `GET /payroll/{id}/export-bank/preview` يعيد `FileBytes` أيضًا حاليًا؛ لا يُعامل كمعاينة خالية من محتوى الملف.
- `GET /payroll-posting/{branchId}` يعيد `Ok(configuration)`، وقد يكون الجسم null عند غياب الإعداد؛ لا يعيد 404 صريحًا حاليًا.
- `POST /leaves/policies` يحفظ حسب نوع الإجازة رغم اسم الدالة CreateOrUpdate؛ صلاحيته الحالية create حتى عند التحديث.
- `GET /employees/export-template` صلاحيته view، بينما فحص الملف واستيراده يحتاجان import.
- إدارة الشفتات والإسنادات واليومي والتوقيت وraw موجودة الآن. لا تظهر واجهة إعداد سياسة الحضور أو تعديل/حذف مكونات الرواتب والتقويم والعطل؛ راجع المهام المفتوحة قبل التكامل.
- بصمة الموبايل الجديدة تعيد 202 لحفظ الدليل أو 400 لرفض محفوظ؛ إعادة EventId بمحتوى مختلف تعيد 409. نجاح الإيصال لا يعني اكتمال اليوم المالي.
- الإضافي خارج النسخة التأسيسية، والاستقبال الموثوق للأجهزة ليس API عاماً بلا مصادقة. لا توجد توصية بإيقاف مسار حالياً؛ كل مسار باقٍ ضمن نطاقه.

## تعريفات أجسام الطلبات

الحقول أدناه من نماذج C# الحالية؛ أسماء JSON المتوقعة عادة camelCase. هذه ليست أمثلة بيانات ولا تضمن التحقق من القيم. الكائنات المتداخلة مذكورة في نفس التعريفات.

### InitializeAttendanceTimeDto

```csharp
public record InitializeAttendanceTimeDto(string TimeZoneId);
```

### DeviceAttendanceDto

```csharp
public record DeviceAttendanceDto(string EmployeeCode, string OccurredAt, string PunchType, string DeviceId, string EventId, string Source = "BIOMETRIC");
```

### ProcessRawBatchDto

```csharp
public record ProcessRawBatchDto(int Size = 100);
```

### IgnoreRawAttendanceDto

```csharp
public record IgnoreRawAttendanceDto(string Reason);
```

### WriteBranchAttendanceLocationDto

```csharp
public record WriteBranchAttendanceLocationDto([property: global::System.Text.Json.Serialization.JsonRequired] decimal? Latitude,
    [property: global::System.Text.Json.Serialization.JsonRequired] decimal? Longitude,
    [property: global::System.Text.Json.Serialization.JsonRequired] decimal RadiusMeters,
    [property: global::System.Text.Json.Serialization.JsonRequired] bool EnforceGeofence);
```

### BranchAttendanceLocationDto

```csharp
public record BranchAttendanceLocationDto(long BranchId, long CompanyId, decimal? Latitude, decimal? Longitude, decimal RadiusMeters, bool EnforceGeofence,
    string Version, string CreationUser, DateTime CreationDate, string? UpdateUser, DateTime? UpdateDate);
```

### EffectiveShiftDto

```csharp
public record EffectiveShiftDto(string EmployeeCode, DateOnly WorkDate, string Status, string Source, string? ShiftCode,
    long? AssignmentId, long? DailyShiftId, DateTime? StartsAtLocal, DateTime? EndsAtLocal,
    int BreakMinutes, decimal ScheduledMinutes, decimal ScheduledHours);
```

### CheckInRequestDto

```csharp
public record CheckInRequestDto(string EmployeeCode,
    [property: global::System.Text.Json.Serialization.JsonRequired] decimal Latitude,
    [property: global::System.Text.Json.Serialization.JsonRequired] decimal Longitude,
    long? BranchId = null,
    decimal? AccuracyMeters = null,
    string? DeviceId = null,
    string Source = "MOBILE_APP",
    [param: global::System.ComponentModel.DataAnnotations.Required] string? EventId = null);
```

### CheckOutRequestDto

```csharp
public record CheckOutRequestDto(string EmployeeCode,
    [property: global::System.Text.Json.Serialization.JsonRequired] decimal Latitude,
    [property: global::System.Text.Json.Serialization.JsonRequired] decimal Longitude,
    long? BranchId = null,
    decimal? AccuracyMeters = null,
    string? DeviceId = null,
    string Source = "MOBILE_APP",
    [param: global::System.ComponentModel.DataAnnotations.Required] string? EventId = null);
```

### AttendancePunchResultDto

```csharp
public record AttendancePunchResultDto(bool Success,
    string EmployeeCode,
    string PunchType,
    DateTime PunchTime,
    long? BranchId,
    string BranchName,
    decimal? Latitude,
    decimal? Longitude,
    decimal DistanceMeters,
    decimal AllowedRadiusMeters,
    bool IsWithinGeofence,
    string GeofenceStatus,
    string Message,
    long? RawEventId = null,
    bool EvidenceOnly = false);
```

### NearbyBranchDto

```csharp
public record NearbyBranchDto(long BranchId,
    string BranchNameLocal,
    string BranchNameEn,
    decimal? Latitude,
    decimal? Longitude,
    decimal GeofenceRadiusMeters,
    decimal DistanceMeters,
    bool IsWithinRange);
```

### TodayAttendanceStatusDto

```csharp
public record TodayAttendanceStatusDto(string EmployeeCode,
    DateTime AttendanceDate,
    string Status,
    DateTime? FirstCheckIn,
    DateTime? LastCheckOut,
    decimal ActualWorkedHours,
    long? CheckInBranchId,
    string? CheckInBranchName,
    bool? IsCheckInWithinGeofence,
    long? CheckOutBranchId,
    string? CheckOutBranchName,
    bool? IsCheckOutWithinGeofence,
    bool IsCurrentlyCheckedIn,
    bool HasGeofenceViolation);
```

### CreateWorkCalendarDto

```csharp
public record CreateWorkCalendarDto(long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsDefault,
    List<WorkCalendarDayDto>? Days);
```

### WorkCalendarDayDto

```csharp
public record WorkCalendarDayDto(int DayOfWeek,
    bool IsWorkingDay,
    decimal StandardWorkingHours,
    string? DefaultShiftCode);
```

### CreatePublicHolidayDto

```csharp
public record CreatePublicHolidayDto(long CompanyId,
    long? BranchId,
    string NameAr,
    string NameEn,
    DateTime HolidayDate,
    bool IsPaid,
    bool IsRecurring,
    string? Description);
```

### RawPunchInputDto

```csharp
public record RawPunchInputDto(string EmployeeCode,
    DateTime PunchTime,
    string PunchType,
    string? DeviceId,
    string? Source);
```

### AttendanceCorrectionRequestDto

```csharp
public record AttendanceCorrectionRequestDto(string EmployeeCode,
    DateTime AttendanceDate,
    DateTime? RequestedCheckIn,
    DateTime? RequestedCheckOut,
    string Reason);
```

### ProcessAttendanceCorrectionDto

```csharp
public record ProcessAttendanceCorrectionDto(bool Approved,
    string? RejectionReason);
```

### DailyAttendanceSummaryDto

```csharp
public record DailyAttendanceSummaryDto(string EmployeeCode,
    string EmployeeName,
    DateTime Date,
    decimal ScheduledHours,
    decimal ActualWorkedHours,
    int LateArrivalMinutes,
    int EarlyLeaveMinutes,
    decimal OvertimeHours,
    string Status,
    bool HasMissingPunch);
```

### AttendancePolicyDto

```csharp
public record AttendancePolicyDto(long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    int GracePeriodMinutes,
    int EarlyLeaveToleranceMinutes,
    int MinMinutesForOvertime,
    bool AutoDeductLateArrival,
    string MissingPunchHandling,
    bool IsDefault);
```

### OvertimeRuleDto

```csharp
public record OvertimeRuleDto(long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string DayType,
    decimal Multiplier,
    int MinimumMinutes,
    int MaximumMinutes,
    string HourlyDivisorFormula,
    bool RequiresApproval);
```

### ProrationPolicyDto

```csharp
public record ProrationPolicyDto(long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string Method,
    bool IsDefault,
    string SalarySegmentAllocation = "UNCONFIGURED");
```

### DeductionPolicyDto

```csharp
public record DeductionPolicyDto(long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal MaxDeductionPercentage,
    decimal MinNetPayGuarantee,
    bool AutoCapAndCarryForward,
    bool AllowNegativeNetPay,
    bool IsDefault);
```

### TaxPolicyDto

```csharp
public record TaxPolicyDto(long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal PersonalExemptionSelf,
    decimal PersonalExemptionDependent,
    decimal NationalContribThreshold,
    decimal NationalContribRate,
    bool IsSscTaxDeductible,
    string CalculationFrequency,
    List<TaxBracketDto> Brackets);
```

### TaxBracketDto

```csharp
public record TaxBracketDto(int BracketOrder,
    decimal LowerLimit,
    decimal? UpperLimit,
    decimal RatePercent,
    string? Description);
```

### SSCPolicyDto

```csharp
public record SSCPolicyDto(long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal EmployeeContribRate,
    decimal EmployerContribRate,
    decimal HighRiskSurchargeRate,
    decimal MonthlyCeilingCap,
    decimal MinimumWageFloor);
```

### CreateLoanApplicationDto

```csharp
public record CreateLoanApplicationDto(string EmployeeCode,
    string LoanType,
    decimal PrincipalAmount,
    int TotalInstallments,
    DateTime StartDate,
    string? Notes);
```

### CreateAdvanceRequestDto

```csharp
public record CreateAdvanceRequestDto(string EmployeeCode,
    decimal AdvanceAmount,
    string TargetPayPeriod,
    string? Reason);
```

### LoanRepaymentScheduleDto

```csharp
public record LoanRepaymentScheduleDto(long Id,
    int InstallmentNo,
    string PayPeriod,
    decimal ScheduledAmount,
    decimal PaidAmount,
    decimal CarriedForwardAmount,
    string Status,
    DateTime? PaidDate);
```

### CreatePayrollAdjustmentDto

```csharp
public record CreatePayrollAdjustmentDto(long CompanyId,
    string EmployeeCode,
    string PayPeriod,
    string ComponentCode,
    string AdjustmentType, // EARNING / DEDUCTION
    decimal Amount,
    string? Description);
```

### PayrollAdjustmentDto

```csharp
public record PayrollAdjustmentDto(long Id,
    long CompanyId,
    string EmployeeCode,
    string EmployeeName,
    string PayPeriod,
    string ComponentCode,
    string ComponentName,
    string AdjustmentType,
    decimal Amount,
    string? Description,
    string Status);
```

### CreatePayrollRunDto

```csharp
public record CreatePayrollRunDto(long CompanyId,
    string PayPeriod,
    long? BranchId);
```

### CancelPayrollRunDto

```csharp
public record CancelPayrollRunDto(string Reason);
```

### PayrollCalculationResultDto

```csharp
public record PayrollCalculationResultDto(long PayrollRunId,
    string PayPeriod,
    int EmployeeCount,
    decimal TotalGross,
    decimal TotalNet,
    decimal TotalEmployeeSsc,
    decimal TotalEmployerSsc,
    decimal TotalIncomeTax,
    decimal TotalNationalContrib,
    decimal TotalOtherDeductions,
    List<PayslipSummaryDto> Payslips);
```

### PayslipSummaryDto

```csharp
public record PayslipSummaryDto(long RunLineId,
    string EmployeeCode,
    string EmployeeName,
    decimal BasicSalary,
    decimal GrossSalary,
    decimal TotalEarnings,
    decimal SscEmployeeContrib,
    decimal IncomeTaxWithheld,
    decimal OtherDeductions,
    decimal TotalDeductions,
    decimal NetPay,
    string PaymentMethod);
```

### PayrollExplanationDto

```csharp
public record PayrollExplanationDto(long RunLineId,
    string EmployeeCode,
    string EmployeeName,
    string PayPeriod,
    decimal BaseSalary,
    decimal ProrationFactor,
    string ProrationMethod,
    decimal ProratedBase,
    decimal OvertimeHours,
    decimal OvertimeEarnings,
    decimal GrossPay,
    decimal SscEligibleSalary,
    decimal SscEmployeeShare,
    decimal SscEmployerShare,
    decimal TaxableGross,
    decimal TaxExemptions,
    decimal IncomeTax,
    decimal NationalSolidarityContrib,
    decimal ScheduledLoanDeduction,
    decimal ActualLoanDeduction,
    decimal CarriedForwardDeduction,
    decimal NetPay,
    string DetailsJson);
```

### PostGlVoucherResultDto

```csharp
public record PostGlVoucherResultDto(long PayrollRunId,
    long JournalVoucherId,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    DateTime PostedAt);
```

### PayrollValidationResultDto

```csharp
public record PayrollValidationResultDto(bool IsValid,
    List<string> Errors,
    List<string> Warnings);
```

### LinkEmployeeUserDto

```csharp
public record LinkEmployeeUserDto(long? UserId);
```

### EmployeeUserLinkDto

```csharp
public record EmployeeUserLinkDto(string EmployeeCode, long? UserId);
```

### CreateEmployeeDto

```csharp
public record CreateEmployeeDto(string EmployeeCode,
    string NameLocal,
    string NameEn,
    string NationalId,
    string Nationality,
    string? PassportNumber,
    DateTime DateOfBirth,
    string Gender,
    string MaritalStatus,
    string? Email,
    string? Phone,
    DateTime HireDate,
    DateTime? ProbationEndDate,
    string EmploymentType,
    string EmploymentStatus,
    string? DepartmentCode,
    string? PositionCode,
    long? BranchId,
    string? ManagerEmployeeCode,
    string? SscNumber,
    int TaxExemptionCount,
    bool IsHighRiskRole,
    string? BankName,
    string? BankAccountNumber,
    string? BankIban);
```

### UpdateEmployeeDto

```csharp
public record UpdateEmployeeDto(string NameLocal,
    string NameEn,
    string NationalId,
    string Nationality,
    string? PassportNumber,
    DateTime DateOfBirth,
    string Gender,
    string MaritalStatus,
    string? Email,
    string? Phone,
    DateTime HireDate,
    DateTime? ProbationEndDate,
    DateTime? TerminationDate,
    string? TerminationReason,
    string EmploymentType,
    string EmploymentStatus,
    string? DepartmentCode,
    string? PositionCode,
    long? BranchId,
    string? ManagerEmployeeCode,
    string? SscNumber,
    int TaxExemptionCount,
    bool IsHighRiskRole,
    string? BankName,
    string? BankAccountNumber,
    string? BankIban,
    bool IsActive);
```

### EmployeeSummaryDto

```csharp
public record EmployeeSummaryDto(string EmployeeCode,
    string NameLocal,
    string NameEn,
    string NationalId,
    string? DepartmentCode,
    string? PositionCode,
    string EmploymentStatus,
    decimal? BasicSalary,
    string? Phone,
    string? Email,
    long? UserId = null);
```

### EmployeeDetailsDto

```csharp
public record EmployeeDetailsDto(string EmployeeCode,
    string NameLocal,
    string NameEn,
    string NationalId,
    string Nationality,
    string? PassportNumber,
    DateTime DateOfBirth,
    string Gender,
    string MaritalStatus,
    string? Email,
    string? Phone,
    DateTime HireDate,
    DateTime? ProbationEndDate,
    DateTime? TerminationDate,
    string? TerminationReason,
    string EmploymentType,
    string EmploymentStatus,
    string? DepartmentCode,
    string? PositionCode,
    long? BranchId,
    string? ManagerEmployeeCode,
    string? SscNumber,
    int TaxExemptionCount,
    bool IsHighRiskRole,
    string? BankName,
    string? BankAccountNumber,
    string? BankIban,
    bool IsActive,
    List<DependentDto> Dependents,
    SalaryStructureDetailsDto? ActiveSalaryStructure,
    long? UserId = null);
```

### CreateDependentDto

```csharp
public record CreateDependentDto(string NameLocal,
    string NameEn,
    string Relationship,
    DateTime DateOfBirth,
    string Gender,
    string? NationalId,
    bool IsTaxExemptionClaimed,
    bool IsMedicalCovered);
```

### DependentDto

```csharp
public record DependentDto(long Id,
    string EmployeeCode,
    string NameLocal,
    string NameEn,
    string Relationship,
    DateTime DateOfBirth,
    string Gender,
    string? NationalId,
    bool IsTaxExemptionClaimed,
    bool IsMedicalCovered,
    bool IsActive);
```

### AssignSalaryStructureDto

```csharp
public record AssignSalaryStructureDto(DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal BasicSalary,
    string CurrencyCode,
    string PaymentMethod,
    List<SalaryStructureLineDto>? Lines);
```

### SalaryStructureLineDto

```csharp
public record SalaryStructureLineDto(string ComponentCode,
    decimal Amount,
    decimal? Percent);
```

### SalaryStructureDetailsDto

```csharp
public record SalaryStructureDetailsDto(long Id,
    string EmployeeCode,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal BasicSalary,
    string CurrencyCode,
    string PaymentMethod,
    bool IsActive,
    List<SalaryStructureLineDetailDto> Lines);
```

### SalaryStructureLineDetailDto

```csharp
public record SalaryStructureLineDetailDto(long Id,
    string ComponentCode,
    string ComponentNameLocal,
    string ComponentNameEn,
    string ComponentType,
    decimal Amount,
    decimal? Percent,
    bool IsActive);
```

### CreateSalaryComponentDto

```csharp
public record CreateSalaryComponentDto(string ComponentCode,
    string NameLocal,
    string NameEn,
    string ComponentType, // BASIC, ALLOWANCE, DEDUCTION, EMPLOYER_CONTRIB
    string CalculationType, // FIXED, PERCENTAGE, FORMULA
    decimal? DefaultAmount,
    decimal? DefaultPercent,
    bool IsTaxable,
    bool IsSscApplicable,
    string? GlAccountCode);
```

### SalaryComponentDto

```csharp
public record SalaryComponentDto(string ComponentCode,
    string NameLocal,
    string NameEn,
    string ComponentType,
    string CalculationType,
    decimal? DefaultAmount,
    decimal? DefaultPercent,
    bool IsTaxable,
    bool IsSscApplicable,
    string? GlAccountCode,
    bool IsActive);
```

### LeaveTypeDto

```csharp
public record LeaveTypeDto(string LeaveTypeCode,
    string NameLocal,
    string NameEn,
    bool IsPaid,
    bool IsStatutory,
    decimal MaxDaysPerYear,
    bool CarryForwardAllowed,
    decimal CarryForwardCapDays,
    bool RequiresDocumentation,
    bool IsActive,
    bool TracksBalance,
    string YearEndExcessAction);
```

### CreateLeaveTypeDto

```csharp
public record CreateLeaveTypeDto(string LeaveTypeCode,
    string NameLocal,
    string NameEn,
    bool IsPaid = true,
    bool IsStatutory = true,
    decimal MaxDaysPerYear = 14m,
    bool CarryForwardAllowed = false,
    decimal CarryForwardCapDays = 0m,
    bool RequiresDocumentation = false,
    bool IsActive = true,
    bool TracksBalance = true,
    string YearEndExcessAction = "EXPIRE");
```

### UpdateLeaveTypeDto

```csharp
public record UpdateLeaveTypeDto(string NameLocal,
    string NameEn,
    bool IsPaid,
    bool IsStatutory,
    decimal MaxDaysPerYear,
    bool CarryForwardAllowed,
    decimal CarryForwardCapDays,
    bool RequiresDocumentation,
    bool IsActive,
    bool? TracksBalance = null,
    string? YearEndExcessAction = null);
```

### SubmitLeaveRequestDto

```csharp
public record SubmitLeaveRequestDto(string EmployeeCode,
    string LeaveTypeCode,
    DateTime StartDate,
    DateTime EndDate,
    decimal DaysRequested,
    string? Reason,
    string? AttachmentFileRef);
```

### ProcessLeaveRequestDto

```csharp
public record ProcessLeaveRequestDto(bool Approved,
    string? RejectionReason);
```

### LeaveRequestDto

```csharp
public record LeaveRequestDto(long Id,
    string EmployeeCode,
    string EmployeeName,
    string LeaveTypeCode,
    string LeaveTypeName,
    DateTime StartDate,
    DateTime EndDate,
    decimal DaysRequested,
    string Status,
    string? Reason,
    string? ApprovedBy,
    DateTime? ApprovalDate,
    string? RejectionReason,
    DateTime CreationDate);
```

### LeaveBalanceDto

```csharp
public record LeaveBalanceDto(string EmployeeCode,
    string LeaveTypeCode,
    string LeaveTypeName,
    int YearNo,
    decimal AccruedDays,
    decimal UsedDays,
    decimal CarriedForwardDays,
    decimal RemainingDays);
```

### HrLeaveSettingsDto

```csharp
public record HrLeaveSettingsDto(int LeaveYearStartMonth,
    int LeaveYearStartDay,
    string BalanceDisplayFormat,
    decimal? StandardWorkdayHours);
```

### UpdateHrLeaveSettingsDto

```csharp
public record UpdateHrLeaveSettingsDto(int LeaveYearStartMonth,
    int LeaveYearStartDay,
    string BalanceDisplayFormat,
    decimal? StandardWorkdayHours);
```

### BankPayrollExportRecordDto

```csharp
public record BankPayrollExportRecordDto(int SequenceNo,
    string EmployeeCode,
    string NationalId,
    string FullName,
    string BankCode,
    string Iban,
    string Currency,
    decimal NetAmount,
    string Remarks);
```

### BankPayrollExportResultDto

```csharp
public record BankPayrollExportResultDto(long PayrollRunId,
    string PayPeriod,
    string Format,
    int TotalRecords,
    decimal TotalAmount,
    byte[] FileBytes,
    string FileName,
    string ContentType);
```

### EmployeeImportErrorDto

```csharp
public record EmployeeImportErrorDto(int RowNumber,
    string? Column,
    string Code,
    string Message);
```

### GrantLeaveBalanceDto

```csharp
public record GrantLeaveBalanceDto(string EmployeeCode,
    string LeaveTypeCode,
    int YearNo,
    decimal Days,
    string AdjustmentType, // OPENING_BALANCE, MANUAL_GRANT, COMPENSATORY, CORRECTION
    string? Reason,
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(200, MinimumLength = 1)]
    string? IdempotencyKey = null);
```

### YearlyLeaveAllocationDto

```csharp
public record YearlyLeaveAllocationDto(int YearNo,
    string? LeaveTypeCode,
    string? DepartmentCode,
    string? EmployeeCode,
    bool ProrateForMidYearHires);
```

### MonthlyLeaveAccrualDto

```csharp
public record MonthlyLeaveAccrualDto(int YearNo,
    int MonthNo,
    string? LeaveTypeCode,
    string? EmployeeCode);
```

### YearEndRolloverDto

```csharp
public record YearEndRolloverDto(int FromYear,
    int ToYear,
    string? LeaveTypeCode);
```

### LeaveYearCloseLineDto

```csharp
public record LeaveYearCloseLineDto(string EmployeeCode,
    string LeaveTypeCode,
    int SourceYear,
    int TargetYear,
    DateTime SourceCycleStartDate,
    DateTime SourceCycleEndDate,
    DateTime TargetCycleStartDate,
    DateTime TargetCycleEndDate,
    decimal UnusedDays,
    decimal CarriedDays,
    decimal ExpiredDays,
    decimal CashoutDays);
```

### LeaveYearCloseDto

```csharp
public record LeaveYearCloseDto(long Id,
    int FromYear,
    int ToYear,
    string LeaveTypeCode,
    string Status,
    string CreationUser,
    DateTime CreationDate,
    string? PostedBy,
    DateTime? PostedDate,
    string? UnpostedBy,
    DateTime? UnpostedDate,
    decimal TotalUnusedDays,
    decimal TotalCarriedDays,
    decimal TotalExpiredDays,
    decimal TotalCashoutDays,
    IReadOnlyList<LeaveYearCloseLineDto> Lines);
```

### LeaveAccrualResultDto

```csharp
public record LeaveAccrualResultDto(int TotalEmployeesProcessed,
    int UpdatedBalancesCount,
    decimal TotalDaysAccrued,
    List<string> Details);
```

### LeaveRolloverResultDto

```csharp
public record LeaveRolloverResultDto(int TotalEmployeesProcessed,
    int RolloverBalancesCount,
    decimal TotalDaysCarriedForward,
    decimal TotalDaysExpired,
    List<string> Details);
```

### LeavePolicyDetailsDto

```csharp
public record LeavePolicyDetailsDto(long Id,
    string PolicyName,
    string LeaveTypeCode,
    string AccrualMethod,
    decimal AccrualRate,
    string ApplicableTo,
    int MinServiceMonths,
    int Tier1YearsThreshold,
    decimal Tier1Days,
    decimal Tier2Days,
    bool IsActive,
    decimal? AnnualEntitlementDays = null,
    string CycleBasis = "CALENDAR_YEAR",
    bool ProrateFirstCycle = false);
```

### CreateLeavePolicyDto

```csharp
public record CreateLeavePolicyDto(string PolicyName,
    string LeaveTypeCode,
    string AccrualMethod,
    decimal AccrualRate,
    string ApplicableTo,
    int MinServiceMonths,
    int Tier1YearsThreshold,
    decimal Tier1Days,
    decimal Tier2Days,
    bool IsActive,
    decimal? AnnualEntitlementDays = null,
    string CycleBasis = "CALENDAR_YEAR",
    bool ProrateFirstCycle = false);
```

### LeaveMovementDto

```csharp
public record LeaveMovementDto(long Id, long BalanceId, string EmployeeCode, string LeaveTypeCode, DateTime CycleStartDate,
    long SequenceNo, string Kind, string Source, string Reason, string Actor, DateTime CreatedDate,
    decimal AccruedDelta, decimal UsedDelta, decimal CarriedDelta, decimal AccruedAfter, decimal UsedAfter, decimal CarriedAfter,
    decimal AvailableAfter, decimal ExpiredDays, decimal TransferredDays, long? ReversesMovementId);
```

### LeaveMovementPageDto

```csharp
public record LeaveMovementPageDto(IReadOnlyList<LeaveMovementDto> Items, int TotalCount, int PageIndex, int PageSize);
```

### WriteShiftAssignmentDto

```csharp
public record WriteShiftAssignmentDto(string ShiftCode, string AssignmentKind, DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo, bool? IsActive, string ShiftVersion);
```

### ShiftAssignmentDto

```csharp
public record ShiftAssignmentDto(long Id, string EmployeeCode, string ShiftCode, string? AssignmentKind,
    string EffectiveKind, bool IsLegacy, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive,
    string Version, string? ShiftVersion, bool? ShiftIsActive, DateTime CreationDate, DateTime? UpdateDate);
```

### ShiftAssignmentPageDto

```csharp
public record ShiftAssignmentPageDto(IReadOnlyList<ShiftAssignmentDto> Items, int TotalCount, int Page, int Size);
```

### WriteShiftDayDto

```csharp
public record WriteShiftDayDto(DateOnly? WorkDate, string? ShiftCode, bool? IsRestDay, bool? IsActive, string? ShiftVersion);
```

### ShiftDayDto

```csharp
public record ShiftDayDto(long Id, string EmployeeCode, DateOnly WorkDate, string? ShiftCode, bool IsRestDay,
    bool IsActive, string Source, string Version, string? ShiftVersion, bool? ShiftIsActive,
    string CreationUser, DateTime CreationDate, string? UpdateUser, DateTime? UpdateDate);
```

### ShiftDayPageDto

```csharp
public record ShiftDayPageDto(IReadOnlyList<ShiftDayDto> Items, int TotalCount, int Page, int Size);
```

### WriteShiftDefinitionDto

```csharp
public record WriteShiftDefinitionDto(string NameLocal, string NameEn, TimeSpan? StartTime, TimeSpan? EndTime,
    int? BreakMinutes, IReadOnlyList<int>? WorkingDays, bool? IsActive);
```

### CreateShiftDefinitionDto

```csharp
public record CreateShiftDefinitionDto(string ShiftCode, WriteShiftDefinitionDto Definition);
```

### ShiftDefinitionDto

```csharp
public record ShiftDefinitionDto(string ShiftCode, string NameLocal, string NameEn, TimeSpan StartTime, TimeSpan EndTime,
    int BreakMinutes, IReadOnlyList<int> WorkingDays, bool IsActive, bool IsInUse, string Version,
    DateTime CreationDate, DateTime? UpdateDate, string WorkingDaysJson, IReadOnlyList<string> ValidationErrors);
```

### ShiftDefinitionPageDto

```csharp
public record ShiftDefinitionPageDto(IReadOnlyList<ShiftDefinitionDto> Items, int TotalCount, int Page, int Size);
```

### PayrollPostingConfiguration — كائن المجال المستعمل مباشرة في الطلب

```csharp
public sealed class PayrollPostingConfiguration {
public long BranchId { get; set; }
    public string SalaryExpense { get; set; } = string.Empty;
    public string EmployerSscExpense { get; set; } = string.Empty;
    public string SalaryPayable { get; set; } = string.Empty;
    public string SscPayable { get; set; } = string.Empty;
    public string TaxPayable { get; set; } = string.Empty;
    public string DeductionsPayable { get; set; } = string.Empty;
    public long CurrencyId { get; set; }
    public int VoucherType { get; set; }
}
```

### PayrollPeriod — كائن المجال المستعمل مباشرة في الطلب

```csharp
public sealed class PayrollPeriod {
public long Id { get; set; }
    public long CompanyId { get; set; }
    public string PeriodCode { get; set; } = string.Empty; // e.g. "2026-09"
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public int Month { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime PayDate { get; set; }
    public string PayrollType { get; set; } = "MONTHLY"; // MONTHLY, BIWEEKLY, SPECIAL
    public string Status { get; set; } = "OPEN"; // OPEN, PROCESSING, CLOSED

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
```
