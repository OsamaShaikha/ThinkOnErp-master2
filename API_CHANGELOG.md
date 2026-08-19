# ThinkOn ERP - API Changelog & Version History 📜

وثيقة التتبع الدائمة لجميع واجهات برمجة التطبيقات (API Endpoints)، التعديلات، الحقول المضافة، والـ Breaking Changes.

---

## 📅 [2026-08-18] - إصدار المرحلة الثانية: العملاء والموردين ودفتر الأستاذ المساعد (Subledger Engine)

### 1. وحدة العملاء (Customers Module)
* **Controller**: `src/ThinkOnErp.API/Controllers/CustomersController.cs`
* **Base Route**: `/api/accounting/customers`

| Method | Endpoint | Description | Request Body | Response Body |
|---|---|---|---|---|
| `GET` | `/api/accounting/customers` | استرجاع قائمة العملاء مع الفلترة | `PartyFilterDto` (Query: `searchTerm`, `branchId`, `isActive`) | `ApiResponse<IReadOnlyList<CustomerDto>>` |
| `GET` | `/api/accounting/customers/{code}` | جلب بيانات عميل برمز العميل | - | `ApiResponse<CustomerDto>` |
| `POST` | `/api/accounting/customers` | إنشاء عميل جديد | `CreateCustomerDto` | `ApiResponse<CustomerDto>` (201 Created) |
| `PUT` | `/api/accounting/customers/{code}` | تحديث بيانات عميل | `UpdateCustomerDto` | `ApiResponse<CustomerDto>` |
| `PATCH` | `/api/accounting/customers/{code}/status` | تفعيل/تعطيل العميل | Query: `isActive=true/false` | `ApiResponse<bool>` |

---

### 2. وحدة الموردين (Vendors Module)
* **Controller**: `src/ThinkOnErp.API/Controllers/VendorsController.cs`
* **Base Route**: `/api/accounting/vendors`

| Method | Endpoint | Description | Request Body | Response Body |
|---|---|---|---|---|
| `GET` | `/api/accounting/vendors` | استرجاع قائمة الموردين | `PartyFilterDto` (Query) | `ApiResponse<IReadOnlyList<VendorDto>>` |
| `GET` | `/api/accounting/vendors/{code}` | جلب بيانات مورد برمز المورد | - | `ApiResponse<VendorDto>` |
| `POST` | `/api/accounting/vendors` | إنشاء مورد جديد | `CreateVendorDto` | `ApiResponse<VendorDto>` (201 Created) |
| `PUT` | `/api/accounting/vendors/{code}` | تحديث بيانات مورد | `UpdateVendorDto` | `ApiResponse<VendorDto>` |
| `PATCH` | `/api/accounting/vendors/{code}/status` | تفعيل/تعطيل المورد | Query: `isActive=true/false` | `ApiResponse<bool>` |

---

### 3. دفتر الأستاذ المساعد ومطابقة الدفعات والفواتير (Subledger & Cash Application)
* **Controller**: `src/ThinkOnErp.API/Controllers/SubledgerController.cs`
* **Base Route**: `/api/accounting/subledger`

| Method | Endpoint | Description | Request Body / Query | Response Body |
|---|---|---|---|---|
| `GET` | `/api/accounting/subledger/ar/transactions` | استعلام حركات أستاذ العملاء | `customerCode`, `fromDate`, `toDate`, `onlyOpen` | `ApiResponse<IReadOnlyList<SubledgerTransactionDto>>` |
| `GET` | `/api/accounting/subledger/ap/transactions` | استعلام حركات أستاذ الموردين | `vendorCode`, `fromDate`, `toDate`, `onlyOpen` | `ApiResponse<IReadOnlyList<SubledgerTransactionDto>>` |
| `GET` | `/api/accounting/subledger/ar/open-invoices/{customerCode}` | **فواتير العميل المفتوحة غير المسددة** (لشاشة السداد) | - | `ApiResponse<IReadOnlyList<OpenInvoiceDto>>` |
| `GET` | `/api/accounting/subledger/ap/open-bills/{vendorCode}` | **فواتير المورد المفتوحة غير المسددة** (لشاشة الصرف) | - | `ApiResponse<IReadOnlyList<OpenInvoiceDto>>` |
| `GET` | `/api/accounting/subledger/ar/open-payments/{customerCode}` | سندات قبض العميل المفتوحة | - | `ApiResponse<IReadOnlyList<OpenInvoiceDto>>` |
| `GET` | `/api/accounting/subledger/ap/open-payments/{vendorCode}` | سندات صرف المورد المفتوحة | - | `ApiResponse<IReadOnlyList<OpenInvoiceDto>>` |
| `POST` | `/api/accounting/subledger/ar/apply` | **مطابقة وتسوية سداد عميل يدوياً** مع فاتورة/عدة فواتير | `ApplyCashDto` (`PaymentTransactionId`, `Invoices[]`) | `ApiResponse<List<CashApplicationResultDto>>` |
| `POST` | `/api/accounting/subledger/ap/apply` | **مطابقة وتسوية سداد مورد يدوياً** مع فواتير الشراء | `ApplyCashDto` (`PaymentTransactionId`, `Invoices[]`) | `ApiResponse<List<CashApplicationResultDto>>` |
| `POST` | `/api/accounting/subledger/ar/auto-apply` | **تسوية تلقائية بحسب الأقدمية (FIFO)** لسداد العميل | `AutoApplyCashDto` (`PaymentTransactionId`, `Notes`) | `ApiResponse<List<CashApplicationResultDto>>` |
| `POST` | `/api/accounting/subledger/ap/auto-apply` | **تسوية تلقائية بحسب الأقدمية (FIFO)** لسداد المورد | `AutoApplyCashDto` (`PaymentTransactionId`, `Notes`) | `ApiResponse<List<CashApplicationResultDto>>` |
| `DELETE` | `/api/accounting/subledger/ar/applications/{id}` | **إلغاء مطابقة/تسوية عميل** واستعادة المبالغ المفتوحة | - | `ApiResponse<bool>` |
| `DELETE` | `/api/accounting/subledger/ap/applications/{id}` | **إلغاء مطابقة/تسوية مورد** واستعادة المبالغ المفتوحة | - | `ApiResponse<bool>` |
| `GET` | `/api/accounting/subledger/ar/applications/invoice/{id}` | سجل سدادات فاتورة محددة (أرقام السندات والتواريخ) | - | `ApiResponse<IReadOnlyList<CashApplicationDetailDto>>` |
| `GET` | `/api/accounting/subledger/ar/applications/payment/{id}` | سجل الفواتير المسددة بسند قبض محدد | - | `ApiResponse<IReadOnlyList<CashApplicationDetailDto>>` |
| `GET` | `/api/accounting/subledger/ap/applications/bill/{id}` | سجل سدادات فاتورة شراء محددة | - | `ApiResponse<IReadOnlyList<CashApplicationDetailDto>>` |
| `GET` | `/api/accounting/subledger/ap/applications/payment/{id}` | سجل الفواتير المسددة بسند صرف محدد | - | `ApiResponse<IReadOnlyList<CashApplicationDetailDto>>` |
| `GET` | `/api/accounting/subledger/ar/statement/{customerCode}` | **كشف حساب تفصيلي للعميل** مع الرصيد التراكمي المستمر | `fromDate`, `toDate` | `ApiResponse<StatementOfAccountDto>` |
| `GET` | `/api/accounting/subledger/ap/statement/{vendorCode}` | **كشف حساب تفصيلي للمورد** مع الرصيد التراكمي المستمر | `fromDate`, `toDate` | `ApiResponse<StatementOfAccountDto>` |
| `GET` | `/api/accounting/subledger/ar/aging` | **تقرير أعمار ديون العملاء** (30/60/90/120+ يوم) | `asOfDate` | `ApiResponse<AgingReportDto>` |
| `GET` | `/api/accounting/subledger/ap/aging` | **تقرير أعمار ديون الموردين** | `asOfDate` | `ApiResponse<AgingReportDto>` |
| `GET` | `/api/accounting/subledger/reconciliation/ar` | مطابقة أستاذ العملاء مع حساب الأستاذ العام (GL) | `fiscalYearId` | `ApiResponse<SubledgerReconciliationDto>` |
| `GET` | `/api/accounting/subledger/reconciliation/ap` | مطابقة أستاذ الموردين مع حساب الأستاذ العام (GL) | `fiscalYearId` | `ApiResponse<SubledgerReconciliationDto>` |

---

### 4. الأرصدة التجميعية وميزان المراجعة (Account Balances & Trial Balance)
* **Controller**: `src/ThinkOnErp.API/Controllers/AccountBalancesController.cs`
* **Base Route**: `/api/accounting/balances`

| Method | Endpoint | Description | Query Parameters | Response Body |
|---|---|---|---|---|
| `GET` | `/api/accounting/balances` | استرجاع الأرصدة التجميعية المسبقة | `accountCode`, `branchId`, `fiscalYearId`, `fiscalPeriodId` | `ApiResponse<IReadOnlyList<GlAccountBalanceDto>>` |
| `GET` | `/api/accounting/balances/trial-balance` | **ميزان المراجعة اللحظي (Trial Balance Report)** | `branchId`, `fiscalYearId`, `fiscalPeriodId` | `ApiResponse<TrialBalanceReportDto>` |
| `POST` | `/api/accounting/balances/recalculate` | إعادة احتساب الأرصدة التجميعية من قيود اليومية | `fiscalYearId`, `branchId` | `ApiResponse<bool>` |

---

### 5. السنوات المالية (Fiscal Years)
* **Controller**: `src/ThinkOnErp.API/Controllers/FiscalYearController.cs`
* **Breaking Change Notice**:
  * ❌ **محذوف**: `GET /api/fiscal-years/company/{companyId}` (تمت إزالة ربط السنة المالية بالشركة على مستوى الـ Entity).
  * ➕ **مضاف**: `GET /api/fiscal-years/branch/{branchId}` (جلب السنوات المالية المرتبطة بالفرع مباشرة).

---

### 6. قيود اليومية (GL Vouchers)
* **Controller**: `src/ThinkOnErp.API/Controllers/GlVouchersController.cs`
* **Behavior Change**:
  * عند ترحيل القيد (`POST /api/accounting/vouchers/{id}/post`)، يتم الآن تلقائياً:
    1. تحديث جدول الأرصدة المجمعة `GL_ACCOUNT_BALANCE`.
    2. توليد قيود أستاذ مساعد (`AR_SUBLEDGER_TRANSACTION` أو `AP_SUBLEDGER_TRANSACTION`) لأي سطر يحتوي على `PartyType` و `PartyCode`.

---

### 7. سندات القبض (Receipt Vouchers)
* **Controller**: `src/ThinkOnErp.API/Controllers/ReceiptsController.cs`
* **Base Route**: `/api/accounting/receipts`

| Method | Endpoint | Description | Request Body / Query | Response Body |
|---|---|---|---|---|
| `GET` | `/api/accounting/receipts` | استرجاع قائمة سندات القبض | `branchId`, `fromDate`, `toDate`, `customerCode` | `ApiResponse<IReadOnlyList<ReceiptVoucherDto>>` |
| `GET` | `/api/accounting/receipts/{id}` | جلب بيانات سند قبض محدد مع الفواتير المسددة | - | `ApiResponse<ReceiptVoucherDto>` |
| `POST` | `/api/accounting/receipts` | **إنشاء وترحيل سند قبض** مع إمكانية التسوية الفورية مع فواتير العميل | `CreateReceiptVoucherDto` | `ApiResponse<ReceiptVoucherDto>` (201 Created) |
| `PUT` | `/api/accounting/receipts/{id}` | **تعديل سند قبض** (للمسودات قبل الترحيل) | `UpdateReceiptVoucherDto` | `ApiResponse<ReceiptVoucherDto>` |
| `DELETE` | `/api/accounting/receipts/{id}` | **حذف/إلغاء سند قبض** (مع فك تسويات الفواتير وعكس القيد آلياً) | - | `ApiResponse<bool>` |

---

### 8. سندات الصرف (Payment Vouchers)
* **Controller**: `src/ThinkOnErp.API/Controllers/PaymentsController.cs`
* **Base Route**: `/api/accounting/payments`

| Method | Endpoint | Description | Request Body / Query | Response Body |
|---|---|---|---|---|
| `GET` | `/api/accounting/payments` | استرجاع قائمة سندات الصرف | `branchId`, `fromDate`, `toDate`, `vendorCode` | `ApiResponse<IReadOnlyList<PaymentVoucherDto>>` |
| `GET` | `/api/accounting/payments/{id}` | جلب بيانات سند صرف محدد مع فواتير المورد المسددة | - | `ApiResponse<PaymentVoucherDto>` |
| `POST` | `/api/accounting/payments` | **إنشاء وترحيل سند صرف** مع إمكانية التسوية الفورية مع فواتير المورد | `CreatePaymentVoucherDto` | `ApiResponse<PaymentVoucherDto>` (201 Created) |
| `PUT` | `/api/accounting/payments/{id}` | **تعديل سند صرف** (للمسودات قبل الترحيل) | `UpdatePaymentVoucherDto` | `ApiResponse<PaymentVoucherDto>` |
| `DELETE` | `/api/accounting/payments/{id}` | **حذف/إلغاء سند صرف** (مع فك تسويات الفواتير وعكس القيد آلياً) | - | `ApiResponse<bool>` |

---

### 9. سجل الشيكات الآجلة (Post-Dated Cheques - PDC Register)
* **Controller**: `src/ThinkOnErp.API/Controllers/PdcController.cs`
* **Base Route**: `/api/accounting/pdc`

| Method | Endpoint | Description | Request Body / Query | Response Body |
|---|---|---|---|---|
| `GET` | `/api/accounting/pdc` | استعلام وفلترة الشيكات الآجلة | `branchId`, `chequeType`, `status`, `partyCode`, `fromDueDate`, `toDueDate` | `ApiResponse<IReadOnlyList<PdcRegisterDto>>` |
| `GET` | `/api/accounting/pdc/{id}` | جلب تفاصيل شيك محدد مع القيود والتواريخ | - | `ApiResponse<PdcRegisterDto>` |
| `POST` | `/api/accounting/pdc` | **تسجيل شيك آجل جديد** (وارد أو صادر) | `CreatePdcDto` | `ApiResponse<PdcRegisterDto>` (201 Created) |
| `POST` | `/api/accounting/pdc/{id}/deposit` | **إيداع الشيك في البنك للتحصيل** | `DepositPdcDto` (`DepositBankAccountCode`, `DepositDate`) | `ApiResponse<PdcRegisterDto>` |
| `POST` | `/api/accounting/pdc/{id}/clear` | **تحصيل الشيك** وتوليد قيد الإيداع البنكي آلياً في الـ GL | `ClearPdcDto` (`DepositBankAccountCode`, `ClearedDate`) | `ApiResponse<PdcRegisterDto>` |
| `POST` | `/api/accounting/pdc/{id}/bounce` | **إثبات ارتداد الشيك** وعكس القيد وإعادة فتح مديونية العميل ورسوم البنك | `BouncePdcDto` (`BounceReason`, `BankCharges`, `BouncedDate`) | `ApiResponse<PdcRegisterDto>` |
| `POST` | `/api/accounting/pdc/{id}/cancel` | **إلغاء أو إرجاع الشيك** | Query: `reason` | `ApiResponse<PdcRegisterDto>` |
| `GET` | `/api/accounting/pdc/upcoming-maturities` | **تنبيهات استحقاقات الشيكات القادمة** (خلال 7 و 30 يوماً) | `branchId`, `daysAhead` (default: 30) | `ApiResponse<UpcomingMaturitySummaryDto>` |

---

### 10. التقارير والقوائم المالية الختامية (Core Financial Reports Hub)
* **Controller**: `src/ThinkOnErp.API/Controllers/FinancialReportsController.cs`
* **Base Route**: `/api/accounting/reports`

| Method | Endpoint | Description | Query Parameters | Response Body |
|---|---|---|---|---|
| `GET` | `/api/accounting/reports/gl-statement` | **كشف الحساب العام** لأي حساب في الدليل مع الرصيد التراكمي والحركات | `accountCode`, `branchId`, `fromDate`, `toDate`, `costCenterCode` | `ApiResponse<GlStatementDto>` |
| `GET` | `/api/accounting/reports/income-statement` | **قائمة الدخل (الأرباح والخسائر)** مع تصنيف الإيرادات، تكلفة المبيعات، مجمل الربح، والمصاريف التشغيلية وصافي الدخل | `branchId`, `fiscalYearId`, `fromDate`, `toDate` | `ApiResponse<IncomeStatementDto>` |
| `GET` | `/api/accounting/reports/balance-sheet` | **الميزانية العمومية (قائمة المركز المالي)** مع الأصول والالتزامات وحقوق الملكية وأرباح الفترة وفحص التوازن الآلي | `branchId`, `fiscalYearId`, `asOfDate` | `ApiResponse<BalanceSheetDto>` |
| `GET` | `/api/accounting/reports/customer-statement/{customerCode}` | **كشف حساب العميل** التفصيلي مع الرصيد الافتتاحي والتراكمي | `fromDate`, `toDate` | `ApiResponse<StatementOfAccountDto>` |
| `GET` | `/api/accounting/reports/vendor-statement/{vendorCode}` | **كشف حساب المورد** التفصيلي مع الرصيد الافتتاحي والتراكمي | `fromDate`, `toDate` | `ApiResponse<StatementOfAccountDto>` |
| `GET` | `/api/accounting/reports/ar-aging` | **تقرير أعمار ديون العملاء** بفترات الاستحقاق (30/60/90/120+) | `asOfDate` | `ApiResponse<AgingReportDto>` |
| `GET` | `/api/accounting/reports/ap-aging` | **تقرير أعمار ديون الموردين** بفترات الاستحقاق (30/60/90/120+) | `asOfDate` | `ApiResponse<AgingReportDto>` |
| `GET` | `/api/accounting/balances/trial-balance` | **ميزان المراجعة** بالمجاميع والأرصدة مع التحقق الآلي من التوازن | `branchId`, `fiscalYearId`, `fiscalPeriodId` | `ApiResponse<TrialBalanceReportDto>` |

---

### 11. محرك وقواعد الترحيل التلقائي (Automatic Posting Rules Engine)
* **Controller**: `src/ThinkOnErp.API/Controllers/PostingRulesController.cs`
* **Base Route**: `/api/accounting/posting-rules`

| Method | Endpoint | Description | Request Body / Query | Response Body |
|---|---|---|---|---|
| `GET` | `/api/accounting/posting-rules` | استرجاع كافة قواعد الترحيل | `module`, `branchId` | `ApiResponse<IReadOnlyList<PostingRuleDto>>` |
| `GET` | `/api/accounting/posting-rules/{id}` | جلب تفاصيل قاعدة ترحيل محددة | - | `ApiResponse<PostingRuleDto>` |
| `POST` | `/api/accounting/posting-rules` | **إنشاء قاعدة ترحيل تلقائي جديدة** | `CreatePostingRuleDto` | `ApiResponse<PostingRuleDto>` (201 Created) |
| `PUT` | `/api/accounting/posting-rules/{id}` | **تعديل وتخصيص حسابات قاعدة ترحيل** | `UpdatePostingRuleDto` | `ApiResponse<PostingRuleDto>` |
| `DELETE` | `/api/accounting/posting-rules/{id}` | **حذف قاعدة ترحيل** | - | `ApiResponse<bool>` |
| `POST` | `/api/accounting/posting-rules/seed-defaults` | **زرع القواعد القياسية الافتراضية للشركة/الفرع** | `branchId` (optional) | `ApiResponse<bool>` |
| `POST` | `/api/accounting/posting-rules/post-event` | **محرك الترحيل الآلي**: تنفيذ الترحيل التلقائي لأي حركة تشغيلية وتوليد وترحيل القيد في الـ GL | `AutomaticPostingEventRequest` | `ApiResponse<GlVoucherHeaderDto>` |

---

### 12. الحسابات البنكية والخزائن والتسويات البنكية (Banking & Bank Reconciliation)
* **Controllers**:
  * `BankAccountsController` (`/api/accounting/bank-accounts`)
  * `CashRegistersController` (`/api/accounting/cash-registers`)
  * `BankReconciliationController` (`/api/accounting/bank-reconciliations`)

#### أ. الحسابات البنكية (`/api/accounting/bank-accounts`)
| Method | Endpoint | Description | Request Body / Query | Response Body |
|---|---|---|---|---|
| `GET` | `/api/accounting/bank-accounts` | استرجاع الحسابات البنكية | `branchId`, `activeOnly` | `ApiResponse<IReadOnlyList<BankAccountDto>>` |
| `GET` | `/api/accounting/bank-accounts/{id}` | جلب بيانات حساب بنكي محدد | - | `ApiResponse<BankAccountDto>` |
| `POST` | `/api/accounting/bank-accounts` | **إنشاء حساب بنكي جديد** وربطه بشجرة الحسابات | `CreateBankAccountDto` | `ApiResponse<BankAccountDto>` (201 Created) |
| `PUT` | `/api/accounting/bank-accounts/{id}` | **تعديل بيانات الحساب البنكي** | `UpdateBankAccountDto` | `ApiResponse<BankAccountDto>` |

#### ب. الخزائن والصناديق النقدية (`/api/accounting/cash-registers`)
| Method | Endpoint | Description | Request Body / Query | Response Body |
|---|---|---|---|---|
| `GET` | `/api/accounting/cash-registers` | استرجاع الخزائن وصناديق العهد | `branchId`, `activeOnly` | `ApiResponse<IReadOnlyList<CashRegisterDto>>` |
| `GET` | `/api/accounting/cash-registers/{id}` | جلب بيانات خزينة محددة | - | `ApiResponse<CashRegisterDto>` |
| `POST` | `/api/accounting/cash-registers` | **إنشاء خزينة أو صندوق عهدة جديد** | `CreateCashRegisterDto` | `ApiResponse<CashRegisterDto>` (201 Created) |
| `PUT` | `/api/accounting/cash-registers/{id}` | **تعديل بيانات الخزينة** | `UpdateCashRegisterDto` | `ApiResponse<CashRegisterDto>` |

#### ج. التسويات والمطابقات البنكية (`/api/accounting/bank-reconciliations`)
| Method | Endpoint | Description | Request Body / Query | Response Body |
|---|---|---|---|---|
| `GET` | `/api/accounting/bank-reconciliations` | استرجاع مذكرات التسوية البنكية | `bankAccountId`, `fiscalYearId` | `ApiResponse<IReadOnlyList<BankReconciliationDto>>` |
| `GET` | `/api/accounting/bank-reconciliations/{id}` | جلب تفاصيل مذكرة التسوية مع أسطر كشف الحساب | - | `ApiResponse<BankReconciliationDto>` |
| `POST` | `/api/accounting/bank-reconciliations` | **إنشاء مذكرة تسوية واستيراد كشف حساب البنك** | `CreateBankReconciliationDto` | `ApiResponse<BankReconciliationDto>` (201 Created) |
| `POST` | `/api/accounting/bank-reconciliations/{id}/auto-reconcile` | **المطابقة البنكية الآلية** ومطابقة الحركات مع قيود الـ GL آلياً | - | `ApiResponse<AutoReconcileResultDto>` |
| `GET` | `/api/accounting/bank-reconciliations/{id}/statement-report` | **تقرير مذكرة تسوية البنك الرسمية** مع الشيكات المعلقة والإيداعات بالطريق | - | `ApiResponse<BankReconciliationStatementDto>` |

---

### 13. إقفال الفترات والسنة المالية وتدوير الأرصدة الافتتاحية (Fiscal Period & Year-End Closing)
* **Controller**: `src/ThinkOnErp.API/Controllers/FiscalClosingController.cs`
* **Base Route**: `/api/accounting/closing`

| Method | Endpoint | Description | Request Body / Query | Response Body |
|---|---|---|---|---|
| `GET` | `/api/accounting/closing/periods/{periodId}/validate` | **فحص جاهزية الفترة المالية للإقفال** (التحقق من عدم وجود مسودات أو قيود معلقة) | - | `ApiResponse<PreClosingPeriodValidationDto>` |
| `POST` | `/api/accounting/closing/periods/{periodId}/close` | **إقفال الفترة المالية وقفل ترحيل القيود** | `ExecutePeriodCloseRequest` (`Reason`) | `ApiResponse<bool>` |
| `POST` | `/api/accounting/closing/periods/{periodId}/reopen` | **إعادة فتح الفترة المالية رسمياً** (بترخيص وتوثيق السبب) | `ExecutePeriodReopenRequest` (`Reason`) | `ApiResponse<bool>` |
| `GET` | `/api/accounting/closing/years/{yearId}/validate` | **فحص الجاهزية والتدقيق المحاسبي الشامل للإقفال السنوي** (توازن ميزان المراجعة، إقفال الفترات، القيود المعلقة، صافي الربح المتوقع) | - | `ApiResponse<PreClosingYearValidationDto>` |
| `POST` | `/api/accounting/closing/years/{yearId}/execute` | **تنفيذ الإقفال السنوي**: تصفير الإيرادات والمصروفات، ترحيل صافي الربح/الخسارة للأرباح المبقاة، تدوير الأرصدة وتوليد القيد الافتتاحي للسنة الجديدة وقفل السنة | `ExecuteYearEndCloseRequest` (`RetainedEarningsAccountCode`, `TargetNextFiscalYearId`, `GenerateOpeningVoucher`) | `ApiResponse<YearEndClosingResultDto>` |
