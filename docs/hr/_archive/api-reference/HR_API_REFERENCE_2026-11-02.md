# دليل جميع API الخاصة بـHR — 2026-11-02

تم استخراج **77 نقطة نهاية** من **11 متحكمًا** في الكود الحالي. هذا جرد للكود، وليس إثبات نشر أو نجاح كل طلب على الخادم.

## طريقة القراءة والاستخدام

- المسارات تبدأ من عنوان API الذي تشغله، مثل `{baseUrl}/api/hr/...`؛ لا تفترض عنوان خادم أو منفذًا معينًا.
- أرسل `Authorization: Bearer <accessToken>` وسياق الشركة المعتاد. لا يرسل العميل اسم schema حرًا لاختيار قاعدة أخرى؛ SuperAdmin يحدد الشركة عبر `X-Company-Id` أو `X-Company-Code` حسب وسيط توجيه الشركة.
- `companyId` داخل Query/Body، حيث يظهر، يجب أن يطابق الشركة المختارة؛ هو غير `branchId`، وكلاهما غير `employeeCode`.
- `?` في النوع يعني قيمة nullable، و`= ...` قيمة افتراضية في توقيع الدالة؛ لا يعني ذلك وحده وجود تحقق أعمال كافٍ.
- `route/body inferred` تعني أن ASP.NET يستنتج الربط: المعرف الظاهر بين `{}` من المسار، والكائن المعقد غير الموسوم من JSON body.
- `ذاتي` يعني دعم وصول الموظف المرتبط إلى سجلاته فقط، إضافة إلى وصول الإدارة صاحبة المنح. بقية العمليات تحتاج منحًا إداريًا صريحًا، أو مسؤولًا مخولًا.
- عمليتا `process` للتصحيح والإجازة تستخدمان `approve` عند القبول و`reject` عندما `Approved=false`.
- غالبية الردود `ApiResponse<T>`؛ إعدادات الترحيل المالي تعيد كائنًا مباشرًا، وتنزيل Excel/البنك يعيد ملفًا. أخطاء التفويض قد تكون 401/403 بلا غلاف موحد.
- تسجيل الدخول خدمة مشتركة: `POST /api/Auth/login` مع `CompanyLoginCommand`؛ التوكن في `data.accessToken` وفق `TokenDto`.

## AttendanceController — 4 API

رمز الشاشة: `hr-attendance`.

### `POST /api/hr/attendance/check-in`

تسجيل دخول GPS.

- الصلاحية: `create`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `body: CheckInRequestDto dto`.
- الرد المعلن: `ApiResponse<AttendancePunchResultDto>`.
- المصدر: [CheckIn](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceController.cs:42).

### `POST /api/hr/attendance/check-out`

تسجيل خروج GPS.

- الصلاحية: `create`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `body: CheckOutRequestDto dto`.
- الرد المعلن: `ApiResponse<AttendancePunchResultDto>`.
- المصدر: [CheckOut](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceController.cs:70).

### `GET /api/hr/attendance/today-status`

حالة حضور اليوم.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: string employeeCode`.
- الرد المعلن: `ApiResponse<TodayAttendanceStatusDto>`.
- المصدر: [GetTodayStatus](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceController.cs:98).

### `GET /api/hr/attendance/nearby-branches`

الفروع الأقرب للإحداثيات.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: decimal latitude`؛ `query: decimal longitude`؛ `query: long? companyId`.
- الرد المعلن: `ApiResponse<IReadOnlyList<NearbyBranchDto>>`.
- المصدر: [GetNearbyBranches](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceController.cs:117).

## AttendanceCorrectionsController — 3 API

رمز الشاشة: `hr-attendance-corrections`.

### `POST /api/hr/attendance-corrections`

طلب تصحيح حضور.

- الصلاحية: `create`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `body: AttendanceCorrectionRequestDto dto`.
- الرد المعلن: `ApiResponse<AttendanceCorrectionRequest>`.
- المصدر: [SubmitCorrection](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceCorrectionsController.cs:39).

### `POST /api/hr/attendance-corrections/{id:long}/process`

اعتماد أو رفض تصحيح الحضور.

- الصلاحية: `approve / reject عند الرفض`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`؛ `body: ProcessAttendanceCorrectionDto dto`.
- الرد المعلن: `ApiResponse<AttendanceCorrectionRequest>`.
- المصدر: [ProcessCorrection](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceCorrectionsController.cs:51).

### `GET /api/hr/attendance-corrections`

قائمة طلبات التصحيح.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: string? employeeCode`؛ `query: string? status`.
- الرد المعلن: `ApiResponse<IReadOnlyList<AttendanceCorrectionRequest>>`.
- المصدر: [GetCorrections](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/AttendanceCorrectionsController.cs:64).

## EmployeesController — 15 API

رمز الشاشة: `hr-employees`.

### `GET /api/hr/employees`

قائمة الموظفين والبحث والترقيم.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: string? searchKeyword`؛ `query: string? departmentCode`؛ `query: string? status`؛ `query: long? branchId`؛ `query: int pageIndex = 1`؛ `query: int pageSize = 20`.
- الرد المعلن: `ApiResponse<PagedResultDto<EmployeeSummaryDto>>`.
- المصدر: [GetEmployees](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:41).

### `GET /api/hr/employees/export`

تصدير الموظفين Excel.

- الصلاحية: `export`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: string? searchKeyword`؛ `query: string? departmentCode`؛ `query: string? status`؛ `query: long? branchId`.
- الرد المعلن: `FileContentResult`.
- المصدر: [ExportEmployees](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:61).

### `GET /api/hr/employees/export-template`

قالب استيراد الموظفين.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: لا توجد معاملات أعمال.
- الرد المعلن: `FileContentResult`.
- المصدر: [DownloadTemplate](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:78).

### `POST /api/hr/employees/import/validate`

فحص Excel قبل الاستيراد.

- الصلاحية: `import`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `form: IFormFile? file`.
- الرد المعلن: `ApiResponse<EmployeeImportResultDto>`.
- المصدر: [ValidateImport](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:89).

### `POST /api/hr/employees/import`

استيراد الموظفين أو تحديثهم.

- الصلاحية: `import`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `form: IFormFile? file`؛ `query: bool updateExisting = false`.
- الرد المعلن: `ApiResponse<EmployeeImportResultDto>`.
- المصدر: [ImportEmployees](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:113).

### `GET /api/hr/employees/{code}`

تفاصيل الموظف.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`.
- الرد المعلن: `ApiResponse<EmployeeDetailsDto>`.
- المصدر: [GetEmployeeByCode](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:143).

### `GET /api/hr/employees/me/user-link`

الموظف المرتبط بالحساب الحالي.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: لا توجد معاملات أعمال.
- الرد المعلن: `IActionResult: راجع الملاحظات`.
- المصدر: [GetMyEmployeeLink](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:158).

### `PUT /api/hr/employees/{code}/user-link`

ربط أو فك ربط الحساب.

- الصلاحية: `link-user`؛ الوصول: **مسؤول فقط**.
- المدخلات: `route/body inferred: string code`؛ `body: LinkEmployeeUserDto dto`.
- الرد المعلن: `IActionResult: راجع الملاحظات`.
- المصدر: [LinkEmployeeUser](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:172).

### `POST /api/hr/employees`

إنشاء موظف.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreateEmployeeDto dto`.
- الرد المعلن: `ApiResponse<EmployeeDetailsDto>`.
- المصدر: [CreateEmployee](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:183).

### `PUT /api/hr/employees/{code}`

تعديل موظف.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`؛ `body: UpdateEmployeeDto dto`.
- الرد المعلن: `ApiResponse<EmployeeDetailsDto>`.
- المصدر: [UpdateEmployee](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:195).

### `DELETE /api/hr/employees/{code}`

تعطيل الموظف وإنهاء حالته.

- الصلاحية: `delete`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`.
- الرد المعلن: `ApiResponse<bool>`.
- المصدر: [DeleteEmployee](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:208).

### `POST /api/hr/employees/{code}/dependents`

إضافة معال.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`؛ `body: CreateDependentDto dto`.
- الرد المعلن: `ApiResponse<DependentDto>`.
- المصدر: [AddDependent](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:220).

### `DELETE /api/hr/employees/dependents/{id:long}`

حذف معال.

- الصلاحية: `delete`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- الرد المعلن: `ApiResponse<bool>`.
- المصدر: [DeleteDependent](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:233).

### `POST /api/hr/employees/{code}/salary-structure`

تعيين تعريف راتب.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`؛ `body: AssignSalaryStructureDto dto`.
- الرد المعلن: `ApiResponse<SalaryStructureDetailsDto>`.
- المصدر: [AssignSalaryStructure](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:245).

### `GET /api/hr/employees/{code}/salary-structure`

تعريف الراتب النشط بتاريخ معين.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`؛ `query: DateTime? asOfDate`.
- الرد المعلن: `ApiResponse<SalaryStructureDetailsDto>`.
- المصدر: [GetSalaryStructure](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/EmployeesController.cs:258).

## LeavesController — 15 API

رمز الشاشة: `hr-leaves`.

### `GET /api/hr/leaves/types`

أنواع الإجازات.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: bool activeOnly = true`.
- الرد المعلن: `ApiResponse<IReadOnlyList<LeaveTypeDto>>`.
- المصدر: [GetLeaveTypes](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:38).

### `GET /api/hr/leaves/types/{code}`

تفاصيل نوع إجازة.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: string code`.
- الرد المعلن: `ApiResponse<LeaveTypeDto>`.
- المصدر: [GetLeaveTypeByCode](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:50).

### `POST /api/hr/leaves/types`

إنشاء نوع إجازة.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreateLeaveTypeDto dto`.
- الرد المعلن: `ApiResponse<LeaveTypeDto>`.
- المصدر: [CreateLeaveType](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:66).

### `PUT /api/hr/leaves/types/{code}`

تعديل نوع إجازة.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`؛ `body: UpdateLeaveTypeDto dto`.
- الرد المعلن: `ApiResponse<LeaveTypeDto>`.
- المصدر: [UpdateLeaveType](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:79).

### `DELETE /api/hr/leaves/types/{code}`

تعطيل نوع إجازة.

- الصلاحية: `delete`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: string code`.
- الرد المعلن: `ApiResponse<bool>`.
- المصدر: [DeleteLeaveType](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:93).

### `GET /api/hr/leaves/requests`

قائمة طلبات الإجازة.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: string? employeeCode`؛ `query: string? leaveTypeCode`؛ `query: string? status`؛ `query: DateTime? fromDate`؛ `query: DateTime? toDate`؛ `query: int pageIndex = 1`؛ `query: int pageSize = 20`.
- الرد المعلن: `ApiResponse<PagedResultDto<LeaveRequestDto>>`.
- المصدر: [GetLeaveRequests](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:109).

### `POST /api/hr/leaves/requests`

تقديم طلب إجازة.

- الصلاحية: `create`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `body: SubmitLeaveRequestDto dto`.
- الرد المعلن: `ApiResponse<LeaveRequestDto>`.
- المصدر: [SubmitLeaveRequest](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:129).

### `POST /api/hr/leaves/requests/{id:long}/process`

اعتماد أو رفض إجازة.

- الصلاحية: `approve / reject عند الرفض`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`؛ `body: ProcessLeaveRequestDto dto`.
- الرد المعلن: `ApiResponse<LeaveRequestDto>`.
- المصدر: [ProcessLeaveRequest](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:141).

### `GET /api/hr/leaves/balances/{employeeCode}`

أرصدة إجازات الموظف.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `route/body inferred: string employeeCode`؛ `query: int? year`.
- الرد المعلن: `ApiResponse<IReadOnlyList<LeaveBalanceDto>>`.
- المصدر: [GetEmployeeBalances](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:154).

### `POST /api/hr/leaves/balances/grant`

منح أو تصحيح رصيد إجازة.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: GrantLeaveBalanceDto dto`.
- الرد المعلن: `ApiResponse<LeaveBalanceDto>`.
- المصدر: [GrantOrAdjustBalance](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:167).

### `POST /api/hr/leaves/balances/accrue-monthly`

تنفيذ استحقاق شهري يدوي.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: MonthlyLeaveAccrualDto dto`.
- الرد المعلن: `ApiResponse<LeaveAccrualResultDto>`.
- المصدر: [RunMonthlyAccrual](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:180).

### `POST /api/hr/leaves/balances/yearly-grant`

تنفيذ توزيع سنوي.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: YearlyLeaveAllocationDto dto`.
- الرد المعلن: `ApiResponse<LeaveAccrualResultDto>`.
- المصدر: [RunYearlyAllocation](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:193).

### `POST /api/hr/leaves/balances/year-end-rollover`

ترحيل الرصيد بين سنتين.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: YearEndRolloverDto dto`.
- الرد المعلن: `ApiResponse<LeaveRolloverResultDto>`.
- المصدر: [RunYearEndRollover](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:206).

### `GET /api/hr/leaves/policies`

سياسات الإجازات.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: لا توجد معاملات أعمال.
- الرد المعلن: `ApiResponse<IReadOnlyList<LeavePolicyDetailsDto>>`.
- المصدر: [GetLeavePolicies](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:218).

### `POST /api/hr/leaves/policies`

حفظ سياسة الإجازة حسب النوع.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreateLeavePolicyDto dto`.
- الرد المعلن: `ApiResponse<LeavePolicyDetailsDto>`.
- المصدر: [CreateOrUpdateLeavePolicy](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LeavesController.cs:229).

## LoansController — 6 API

رمز الشاشة: `hr-loans`.

### `POST /api/hr/loans`

طلب قرض وإنشاء أقساطه.

- الصلاحية: `create`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `body: CreateLoanApplicationDto dto`.
- الرد المعلن: `ApiResponse<EmployeeLoan>`.
- المصدر: [CreateLoan](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LoansController.cs:39).

### `POST /api/hr/loans/{id:long}/approve`

اعتماد القرض.

- الصلاحية: `approve`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- الرد المعلن: `ApiResponse<EmployeeLoan>`.
- المصدر: [ApproveLoan](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LoansController.cs:51).

### `GET /api/hr/loans`

قائمة القروض.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: string? employeeCode`؛ `query: string? status`.
- الرد المعلن: `ApiResponse<IReadOnlyList<EmployeeLoan>>`.
- المصدر: [GetLoans](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LoansController.cs:63).

### `POST /api/hr/loans/advances`

طلب سلفة.

- الصلاحية: `create`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `body: CreateAdvanceRequestDto dto`.
- الرد المعلن: `ApiResponse<EmployeeAdvance>`.
- المصدر: [CreateAdvance](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LoansController.cs:75).

### `POST /api/hr/loans/advances/{id:long}/approve`

اعتماد السلفة.

- الصلاحية: `approve`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- الرد المعلن: `ApiResponse<EmployeeAdvance>`.
- المصدر: [ApproveAdvance](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LoansController.cs:87).

### `GET /api/hr/loans/advances`

قائمة السلف.

- الصلاحية: `view`؛ الوصول: **إدارة أو خدمة ذاتية مقيدة**.
- المدخلات: `query: string? employeeCode`؛ `query: string? targetPayPeriod`.
- الرد المعلن: `ApiResponse<IReadOnlyList<EmployeeAdvance>>`.
- المصدر: [GetAdvances](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/LoansController.cs:99).

## PayrollAdjustmentsController — 6 API

رمز الشاشة: `hr-payroll-adjustments`.

### `POST /api/hr/adjustments`

إنشاء تعديل راتب بحالة APPROVED حاليًا.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreatePayrollAdjustmentDto dto`.
- الرد المعلن: `ApiResponse<PayrollAdjustment>`.
- المصدر: [CreateAdjustment](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:39).

### `POST /api/hr/adjustments/{id:long}/approve`

اعتماد تعديل.

- الصلاحية: `approve`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- الرد المعلن: `ApiResponse<PayrollAdjustment>`.
- المصدر: [ApproveAdjustment](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:51).

### `POST /api/hr/adjustments/{id:long}/reject`

رفض تعديل.

- الصلاحية: `reject`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- الرد المعلن: `ApiResponse<PayrollAdjustment>`.
- المصدر: [RejectAdjustment](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:63).

### `GET /api/hr/adjustments`

قائمة التعديلات.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId`؛ `query: string? employeeCode`؛ `query: string? payPeriod`؛ `query: string? status`.
- الرد المعلن: `ApiResponse<IReadOnlyList<PayrollAdjustmentDto>>`.
- المصدر: [GetAdjustments](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:75).

### `GET /api/hr/adjustments/{id:long}`

تفاصيل تعديل.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- الرد المعلن: `ApiResponse<PayrollAdjustment>`.
- المصدر: [GetAdjustmentById](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:89).

### `DELETE /api/hr/adjustments/{id:long}`

حذف تعديل.

- الصلاحية: `delete`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- الرد المعلن: `ApiResponse<bool>`.
- المصدر: [DeleteAdjustment](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollAdjustmentsController.cs:101).

## PayrollController — 10 API

رمز الشاشة: `hr-payroll`.

### `POST /api/hr/payroll/calculate`

حساب مسير راتب جديد لفرع.

- الصلاحية: `edit`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreatePayrollRunDto dto`.
- الرد المعلن: `ApiResponse<PayrollCalculationResultDto>`.
- المصدر: [CalculatePayroll](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:45).

### `POST /api/hr/payroll/{id:long}/approve`

اعتماد المسير وتسوية الاقتطاعات.

- الصلاحية: `approve`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- الرد المعلن: `ApiResponse<PayrollRun>`.
- المصدر: [ApprovePayroll](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:57).

### `POST /api/hr/payroll/{id:long}/post-to-gl`

ترحيل المسير إلى المحاسبة.

- الصلاحية: `post`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- الرد المعلن: `ApiResponse<PostGlVoucherResultDto>`.
- المصدر: [PostToGl](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:69).

### `POST /api/hr/payroll/{id:long}/reverse-gl`

عكس حالة قيد الرواتب.

- الصلاحية: `reverse`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- الرد المعلن: `ApiResponse<PayrollRun>`.
- المصدر: [ReverseGl](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:81).

### `GET /api/hr/payroll/{id:long}`

نتيجة مسير بالمعرف.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- الرد المعلن: `ApiResponse<PayrollCalculationResultDto>`.
- المصدر: [GetPayrollRun](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:93).

### `GET /api/hr/payroll/{id:long}/export-bank`

تنزيل ملف البنك.

- الصلاحية: `export`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`؛ `query: string? format`.
- الرد المعلن: `FileContentResult`.
- المصدر: [ExportBankFile](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:105).

### `GET /api/hr/payroll/{id:long}/export-bank/preview`

معاينة تعيد بيانات الملف البنكي ومحتواه.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`؛ `query: string? format`.
- الرد المعلن: `ApiResponse<BankPayrollExportResultDto>`.
- المصدر: [PreviewBankExport](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:117).

### `GET /api/hr/payroll/run-lines/{lineId:long}/explanation`

شرح احتساب بند الموظف.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long lineId`.
- الرد المعلن: `ApiResponse<PayrollExplanationDto>`.
- المصدر: [GetCalculationExplanation](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:129).

### `GET /api/hr/payroll/periods`

قائمة فترات الرواتب.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId`؛ `query: int? year`.
- الرد المعلن: `ApiResponse<IReadOnlyList<PayrollPeriod>>`.
- المصدر: [GetPeriods](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:141).

### `POST /api/hr/payroll/periods`

إنشاء فترة رواتب.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: PayrollPeriod period`.
- الرد المعلن: `ApiResponse<PayrollPeriod>`.
- المصدر: [CreatePeriod](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollController.cs:154).

## PayrollPoliciesController — 8 API

رمز الشاشة: `hr-payroll-policies`.

### `GET /api/hr/payroll-policies/proration`

سياسات تجزئة الراتب.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId`.
- الرد المعلن: `ApiResponse<IReadOnlyList<ProrationPolicy>>`.
- المصدر: [GetProrationPolicies](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:40).

### `POST /api/hr/payroll-policies/proration`

إضافة سياسة تجزئة.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: ProrationPolicyDto dto`.
- الرد المعلن: `ApiResponse<ProrationPolicy>`.
- المصدر: [AddProrationPolicy](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:51).

### `GET /api/hr/payroll-policies/ssc`

سياسات الضمان.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId`.
- الرد المعلن: `ApiResponse<IReadOnlyList<SSCPolicy>>`.
- المصدر: [GetSSCPolicies](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:78).

### `POST /api/hr/payroll-policies/ssc`

إضافة سياسة ضمان.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: SSCPolicyDto dto`.
- الرد المعلن: `ApiResponse<SSCPolicy>`.
- المصدر: [AddSSCPolicy](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:89).

### `GET /api/hr/payroll-policies/tax`

سياسات الضريبة.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId`.
- الرد المعلن: `ApiResponse<IReadOnlyList<TaxPolicy>>`.
- المصدر: [GetTaxPolicies](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:119).

### `POST /api/hr/payroll-policies/tax`

إضافة سياسة ضريبة وشرائحها.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: TaxPolicyDto dto`.
- الرد المعلن: `ApiResponse<TaxPolicy>`.
- المصدر: [AddTaxPolicy](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:130).

### `GET /api/hr/payroll-policies/deductions`

سياسات الاقتطاع.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId`.
- الرد المعلن: `ApiResponse<IReadOnlyList<DeductionPolicy>>`.
- المصدر: [GetDeductionPolicies](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:177).

### `POST /api/hr/payroll-policies/deductions`

إضافة سياسة اقتطاع.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: DeductionPolicyDto dto`.
- الرد المعلن: `ApiResponse<DeductionPolicy>`.
- المصدر: [AddDeductionPolicy](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPoliciesController.cs:188).

## PayrollPostingConfigurationsController — 2 API

رمز الشاشة: `hr-payroll-posting`.

### `GET /api/hr/payroll-posting/{branchId:long}`

إعداد حسابات الرواتب للفرع.

- الصلاحية: `view`؛ الوصول: **مسؤول فقط**.
- المدخلات: `route/body inferred: long branchId`.
- الرد المعلن: `IActionResult: راجع الملاحظات`.
- المصدر: [Get](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPostingConfigurationsController.cs:13).

### `PUT /api/hr/payroll-posting/{branchId:long}`

حفظ حسابات الرواتب للفرع.

- الصلاحية: `configure`؛ الوصول: **مسؤول فقط**.
- المدخلات: `route/body inferred: long branchId`؛ `route/body inferred: PayrollPostingConfiguration configuration`.
- الرد المعلن: `IActionResult: راجع الملاحظات`.
- المصدر: [Save](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/PayrollPostingConfigurationsController.cs:17).

## SalaryComponentsController — 2 API

رمز الشاشة: `hr-salary-components`.

### `GET /api/hr/salary-components`

قائمة مكونات الرواتب.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: لا توجد معاملات أعمال.
- الرد المعلن: `ApiResponse<IReadOnlyList<SalaryComponentDto>>`.
- المصدر: [GetComponents](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/SalaryComponentsController.cs:38).

### `POST /api/hr/salary-components`

إنشاء مكون راتب.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreateSalaryComponentDto dto`.
- الرد المعلن: `ApiResponse<SalaryComponentDto>`.
- المصدر: [CreateComponent](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/SalaryComponentsController.cs:48).

## WorkCalendarsController — 6 API

رمز الشاشة: `hr-work-calendars`.

### `POST /api/hr/work-calendars`

إنشاء تقويم وأيام عمل.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreateWorkCalendarDto dto`.
- الرد المعلن: `ApiResponse<WorkCalendar>`.
- المصدر: [CreateCalendar](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/WorkCalendarsController.cs:39).

### `GET /api/hr/work-calendars`

قائمة التقاويم.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId`.
- الرد المعلن: `ApiResponse<IReadOnlyList<WorkCalendar>>`.
- المصدر: [GetCalendars](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/WorkCalendarsController.cs:51).

### `GET /api/hr/work-calendars/{id:long}`

تفاصيل تقويم.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `route/body inferred: long id`.
- الرد المعلن: `ApiResponse<WorkCalendar>`.
- المصدر: [GetCalendarById](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/WorkCalendarsController.cs:62).

### `POST /api/hr/work-calendars/holidays`

إضافة عطلة.

- الصلاحية: `create`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `body: CreatePublicHolidayDto dto`.
- الرد المعلن: `ApiResponse<PublicHoliday>`.
- المصدر: [CreateHoliday](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/WorkCalendarsController.cs:74).

### `GET /api/hr/work-calendars/holidays`

قائمة العطل.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId`؛ `query: int? year`.
- الرد المعلن: `ApiResponse<IReadOnlyList<PublicHoliday>>`.
- المصدر: [GetHolidays](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/WorkCalendarsController.cs:86).

### `GET /api/hr/work-calendars/working-days-count`

حساب عدد أيام العمل.

- الصلاحية: `view`؛ الوصول: **صلاحية إدارة للفرع**.
- المدخلات: `query: long companyId`؛ `query: DateTime startDate`؛ `query: DateTime endDate`.
- الرد المعلن: `ApiResponse<int>`.
- المصدر: [CountWorkingDays](/C:/Users/Khaled/ThinkOnErp-master2/src/ThinkOnErp.API/Controllers/Hr/WorkCalendarsController.cs:98).

## ملاحظات مهمة على العقود

- `DELETE /employees/{code}` تعطيل منطقي، بينما حذف المعال/تعديل الراتب عمليات مختلفة؛ راجع تقرير الفجوات قبل الاعتماد عليها.
- مسار التعديلات هو `/api/hr/adjustments`، بينما رمز صلاحيته `hr-payroll-adjustments`؛ ليسا نفس النص.
- `POST /payroll/calculate` ينشئ مسيرًا جديدًا، وليس PUT لإعادة حساب المسير نفسه.
- `GET /payroll/{id}/export-bank/preview` يعيد `FileBytes` أيضًا حاليًا؛ لا يُعامل كمعاينة خالية من محتوى الملف.
- `GET /payroll-posting/{branchId}` يعيد `Ok(configuration)`، وقد يكون الجسم null عند غياب الإعداد؛ لا يعيد 404 صريحًا حاليًا.
- `POST /leaves/policies` يحفظ حسب نوع الإجازة رغم اسم الدالة CreateOrUpdate؛ صلاحيته الحالية create حتى عند التحديث.
- `GET /employees/export-template` صلاحيته view، بينما فحص الملف واستيراده يحتاجان import.
- لم يظهر API تعديل/حذف للمكونات أو سياسات الرواتب أو التقويم والعطل، ولم تظهر إدارة ورديات أو قواعد إضافي أو سياسة حضور عبر متحكمات HR الحالية.

## تعريفات أجسام الطلبات

الحقول أدناه من نماذج C# الحالية؛ أسماء JSON المتوقعة عادة camelCase. هذه ليست أمثلة بيانات ولا تضمن التحقق من القيم. الكائنات المتداخلة مذكورة في نفس التعريفات.

### CheckInRequestDto

```csharp
public record CheckInRequestDto(string EmployeeCode,
    decimal Latitude,
    decimal Longitude,
    long? BranchId = null,
    decimal? AccuracyMeters = null,
    string? DeviceId = null,
    string Source = "MOBILE_APP");
```

### CheckOutRequestDto

```csharp
public record CheckOutRequestDto(string EmployeeCode,
    decimal Latitude,
    decimal Longitude,
    long? BranchId = null,
    decimal? AccuracyMeters = null,
    string? DeviceId = null,
    string Source = "MOBILE_APP");
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
    string Message);
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
    bool IsDefault);
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
    bool IsActive);
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
    bool IsActive = true);
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
    bool IsActive);
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
    string? Reason);
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
    bool IsActive);
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
    bool IsActive);
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
