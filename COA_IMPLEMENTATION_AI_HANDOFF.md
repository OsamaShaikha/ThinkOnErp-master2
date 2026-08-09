# تسليم تنفيذ شجرة الحسابات (AI Handoff)

> آخر تحديث: 2026-08-06
>
> المستودع: `D:\ThinkOnErp`
>
> الحالة: التنفيذ البرمجي مكتمل ويُبنى بنجاح، ملف الإكسل مصحح ومتحقق منه، ولم يُنفّذ DDL أو استيراد بيانات على Oracle حي.

## 1. الغرض من هذا الملف

هذا المستند هو نقطة البداية لأي AI أو مطور سيكمل عمل شجرة الحسابات في ThinkOnERP. يصف بدقة:

- ما تم فحصه وتصحيحه في ملف Excel.
- الكيانات والتكوينات والخدمات والمستودعات التي أُضيفت.
- طريقة حماية العزل بين الشركات `Multi-Tenant`.
- طريقة إنشاء جداول Oracle وتحديث مخططات الشركات.
- نتائج البناء والاختبارات الفعلية.
- ما لم يُنفّذ بعد وما يلزم التحقق منه على بيئة Oracle حقيقية.

يجب اعتبار الكود الحالي مصدر الحقيقة. لا تفترض أن ملفات الملخصات التاريخية الأخرى في جذر المستودع تعكس الحالة الحالية.

---

## 2. ملخص النتيجة الحالية

تم تنفيذ النطاق التالي:

1. فحص ملف `ThinkOn_COA_V2_Renumbered_1.xlsx` وتصحيح خطأين في نوع الحساب.
2. إضافة كيانات `AccountCategory`, `GlAccount`, و`GlAccountBranch`.
3. إضافة تكوينات EF Core المتوافقة مع Oracle.
4. تحديث `OracleDbContext` وتسجيل كل التكوينات و`DbSet` المطلوبة.
5. إضافة DTOs وMapper لخدمة الحسابات والشجرة والاستيراد.
6. إضافة خدمة شجرة الحسابات وقواعد إنشاء الحسابات وتحديد الحسابات القابلة للترحيل.
7. إضافة قارئ XLSX بدون مكتبة خارجية ومحرك تحقق واستيراد للنسخة القياسية ذات 315 حسابًا.
8. إضافة مستودع EF Core مع عزل حسب الشركة والفرع.
9. ربط الخدمة بالسياق الموثوق الذي ينشئه `SchemaRoutingMiddleware`.
10. إضافة DDL متكرر وآمن نسبيًا لإنشاء جداول الحسابات والقيود والفهارس وزرع الفئات الثماني لكل Tenant.
11. إضافة `GlAccountsController` و`CoaImportController` كواجهة HTTP فعلية.
12. إضافة وثيقة Swagger مستقلة باسم `Accounting API` تجمع واجهات المحاسبة الحالية.
13. إضافة 33 اختبارًا مركزًا وتشغيلها بنجاح.
14. بناء مشروع الإنتاج `ThinkOnErp.API` بنجاح مع صفر أخطاء.

الـControllers الجديدة Tenant-scoped، وتظهر داخل Company وAccounting Swagger، وتستخدم `ApiResponse<T>` وسياسة `TenantAdminOnly` للعمليات الإدارية.

---

## 3. فحص وتصحيح ملف Excel

### 3.1 الملفات

- الملف العامل المصحح في جذر المستودع:
  - `D:\ThinkOnErp\ThinkOn_COA_V2_Renumbered_1.xlsx`
- نسخة التسليم المصححة:
  - `D:\ThinkOnErp\outputs\019fd31c-bbcc-76f1-bdbd-ef7f77c623b3\ThinkOn_COA_V2_Renumbered_1_fixed.xlsx`
- SHA-256 للملفين، وهو متطابق:
  - `B7D5EBF5FCA384A29EE95997DBD8E30FF5DE7BA142381953059F6A39B2D03807`

### 3.2 بنية المصنف

- اسم الشيت: `شجرة الحسابات`
- النطاق المستخدم: `A1:O316`
- عدد صفوف البيانات: `315`
- عدد صفوف العناوين: `1`
- لا توجد أخطاء Excel formulas.
- العناوين المعتمدة وعددها 15:

```text
account_code
parent_code
account_name_ar
account_name_en
level
account_type
category_code
normal_balance
financial_statement
is_contra
is_control_account
control_account_type
is_branch_specific
is_clearing
notes
```

### 3.3 الأخطاء التي تم اكتشافها وتصحيحها

| الخلية | كود الحساب | المشكلة | التصحيح |
|---|---:|---|---|
| `F16` | `1121` | كان `DETAIL` رغم وجود الحساب الابن `112101` | تغييره إلى `HEADER` |
| `F106` | `2112` | كان `HEADER` ولا توجد له حسابات أبناء | تغييره إلى `DETAIL` |

بعد التصحيح أصبحت نتيجة خط التحقق:

- 315 حسابًا.
- لا تكرار في `account_code`.
- كل Parent موجود.
- كل مستوى يساوي مستوى الأب + 1.
- كل كود ابن يبدأ بكود الأب.
- كل حساب له أبناء هو `HEADER`.
- كل `HEADER` له ابن واحد على الأقل.
- كل حساب مستوى 5 هو `DETAIL`.
- لا أخطاء في الفئات أو طبيعة الرصيد أو القوائم المالية.
- لا أخطاء في Boolean flags أو Control Account types.
- لا أخطاء formulas.

### 3.4 قرار مهم بخصوص الحسابات العكسية

يوجد 17 حسابًا مع `is_contra = true`. لم يتم عكس `normal_balance` داخل ملف Excel أو الكيان؛ طبيعة الرصيد تبقى الطبيعة الأساسية للفئة، بينما `IsContra` هو العلم المنفصل الذي يحدد العرض/المعالجة العكسية.

مثال: حساب أصل عكسي يبقى `normal_balance = D` مع `is_contra = true` بدل تحويله إلى `C`.

---

## 4. الملفات البرمجية التي أُضيفت

### 4.1 Domain

```text
src/ThinkOnErp.Domain/Entities/Accounting/AccountCategory.cs
src/ThinkOnErp.Domain/Entities/Accounting/GlAccount.cs
src/ThinkOnErp.Domain/Entities/Accounting/GlAccountBranch.cs
src/ThinkOnErp.Domain/Exceptions/AccountingException.cs
src/ThinkOnErp.Domain/Exceptions/AccountingNotFoundException.cs
src/ThinkOnErp.Domain/Exceptions/AccountingConflictException.cs
src/ThinkOnErp.Domain/Interfaces/Accounting/ICurrentTenantContext.cs
src/ThinkOnErp.Domain/Interfaces/Accounting/IGlAccountRepository.cs
```

### 4.2 Application

```text
src/ThinkOnErp.Application/DTOs/Accounting/CoaImportErrorDto.cs
src/ThinkOnErp.Application/DTOs/Accounting/AccountCategoryDto.cs
src/ThinkOnErp.Application/DTOs/Accounting/CoaImportResultDto.cs
src/ThinkOnErp.Application/DTOs/Accounting/CoaImportRowDto.cs
src/ThinkOnErp.Application/DTOs/Accounting/CoaWorkbookReadResultDto.cs
src/ThinkOnErp.Application/DTOs/Accounting/CreateGlAccountDto.cs
src/ThinkOnErp.Application/DTOs/Accounting/GlAccountDto.cs
src/ThinkOnErp.Application/DTOs/Accounting/GlAccountStatusDto.cs
src/ThinkOnErp.Application/DTOs/Accounting/GlAccountTreeDto.cs
src/ThinkOnErp.Application/DTOs/Accounting/UpdateGlAccountStatusDto.cs
src/ThinkOnErp.Application/DTOs/Accounting/UpdateGlAccountDto.cs
src/ThinkOnErp.Application/Mappings/Accounting/GlAccountMapper.cs
src/ThinkOnErp.Application/Services/Accounting/ICoaExcelImportService.cs
src/ThinkOnErp.Application/Services/Accounting/ICoaWorkbookReader.cs
src/ThinkOnErp.Application/Services/Accounting/IGlAccountService.cs
src/ThinkOnErp.Application/Services/Accounting/CoaExcelImportService.cs
src/ThinkOnErp.Application/Services/Accounting/GlAccountService.cs
```

### 4.3 Infrastructure

```text
src/ThinkOnErp.Infrastructure/Data/Configurations/Accounting/AccountCategoryConfiguration.cs
src/ThinkOnErp.Infrastructure/Data/Configurations/Accounting/GlAccountConfiguration.cs
src/ThinkOnErp.Infrastructure/Data/Configurations/Accounting/GlAccountBranchConfiguration.cs
src/ThinkOnErp.Infrastructure/Repositories/Accounting/GlAccountRepository.cs
src/ThinkOnErp.Infrastructure/Services/Accounting/HttpCurrentTenantContext.cs
src/ThinkOnErp.Infrastructure/Services/Accounting/XlsxCoaWorkbookReader.cs
```

### 4.4 API

```text
src/ThinkOnErp.API/Controllers/GlAccountsController.cs
src/ThinkOnErp.API/Controllers/CoaImportController.cs
src/ThinkOnErp.API/Controllers/AccountCategoriesController.cs
src/ThinkOnErp.API/Swagger/ApiSwaggerDocuments.cs
```

### 4.5 الاختبارات

```text
tests/ThinkOnErp.Infrastructure.Tests/Accounting/CoaExcelImportServiceTests.cs
tests/ThinkOnErp.Infrastructure.Tests/Accounting/AccountCategoriesControllerTests.cs
tests/ThinkOnErp.Infrastructure.Tests/Accounting/CoaImportControllerTests.cs
tests/ThinkOnErp.Infrastructure.Tests/Accounting/GlAccountServiceTests.cs
tests/ThinkOnErp.Infrastructure.Tests/Accounting/GlAccountsControllerTests.cs
tests/ThinkOnErp.Infrastructure.Tests/Accounting/XlsxCoaWorkbookReaderTests.cs
```

---

## 5. الملفات القائمة التي عُدّلت

```text
src/ThinkOnErp.Application/DependencyInjection.cs
src/ThinkOnErp.Infrastructure/Data/OracleDbContext.cs
src/ThinkOnErp.Infrastructure/DependencyInjection.cs
src/ThinkOnErp.Infrastructure/Services/OracleSchemaService.cs
src/ThinkOnErp.API/Program.cs
src/ThinkOnErp.API/Middleware/ExceptionHandlingMiddleware.cs
```

التعديلات الأساسية:

- تسجيل `IGlAccountService` و`ICoaExcelImportService` في Application DI.
- إضافة `DbSet<AccountCategory>`, `DbSet<GlAccount>`, و`DbSet<GlAccountBranch>`.
- تطبيق تكوينات الحسابات داخل `OnModelCreating`.
- تسجيل Repository وTenant Context وقارئ XLSX في Infrastructure DI.
- إضافة جداول الحسابات إلى جداول الـTenant وإضافة مسار DDL/Seed/Upgrade في `OracleSchemaService`.
- إضافة وثيقة Accounting Swagger وقاعدة تصنيف صريحة تحافظ على وثيقتي Company وSuperAdmin.
- تحويل `AccountingNotFoundException` إلى HTTP 404 و`AccountingConflictException` إلى HTTP 409.

---

## 6. نموذج الـDomain

### 6.1 `AccountCategory`

يمثل الفئات المحاسبية الثماني الثابتة، ويحتوي على:

- `Id`
- `CategoryCode`
- `NameAr`
- `NameEn`
- `NormalBalance`
- `FinancialStatement`
- `DisplayOrder`
- Navigation إلى `Accounts`

### 6.2 `GlAccount`

يحتوي على:

- `Id`
- `CompanyId`
- `AccountCode`
- `AccountNameAr`
- `AccountNameEn`
- `ParentAccountId`
- `CategoryId`
- `AccountLevel`
- `AccountType`
- `NormalBalance`
- `IsContra`
- `IsControlAccount`
- `ControlAccountType`
- `IsBranchSpecific`
- `IsClearing`
- `IsActive`
- `Description`
- `Notes`

والعلاقات:

- `ParentAccount`
- `ChildrenAccounts`
- `Category`
- `BranchLinks`

الخاصية `IsPostable` محسوبة في الذاكرة وليست عمودًا:

```csharp
public bool IsPostable => AccountType == "DETAIL" && IsActive;
```

### 6.3 `GlAccountBranch`

جدول وسيط بالمفتاح المركب:

```text
(GlAccountId, BranchId)
```

ويحتوي على `IsActive` وعلاقتين إلى `GlAccount` و`SysBranch`.

---

## 7. تكوينات EF Core وOracle

### 7.1 `ACCOUNT_CATEGORY`

- `Id`: `NUMBER(19)`, لا يولد تلقائيًا.
- `CATEGORY_CODE`: `NUMBER(2)`, فريد، بين 1 و8.
- النصوص: `NVARCHAR2`.
- `NORMAL_BALANCE`: `D` أو `C`.
- `FINANCIAL_STATEMENT`: `BALANCE_SHEET` أو `INCOME_STATEMENT`.
- مستبعد من EF migrations عبر `ExcludeFromMigrations()` لأنه Tenant-local ويُنشأ عبر provisioning.

### 7.2 `GL_ACCOUNT`

- `Id`: `NUMBER(19)` وIdentity.
- Boolean values: `NUMBER(1)`.
- Unique constraint منطقي على `(COMPANY_ID, ACCOUNT_CODE)`.
- Self-reference من `PARENT_ACCOUNT_ID` إلى `Id` مع `DeleteBehavior.Restrict`.
- FK من `CATEGORY_ID` إلى `ACCOUNT_CATEGORY`.
- `IsPostable` مستبعد من EF mapping.

الفهارس:

```text
UX_GL_ACCOUNT_COMP_CODE (COMPANY_ID, ACCOUNT_CODE) UNIQUE
IX_GL_ACCOUNT_PARENT (PARENT_ACCOUNT_ID)
IX_GL_ACCOUNT_COMP_CTRL (COMPANY_ID, IS_CONTROL_ACCOUNT)
IX_GL_ACCOUNT_CATEGORY (CATEGORY_ID)
```

### 7.3 `GL_ACCOUNT_BRANCH`

- Composite PK: `(GL_ACCOUNT_ID, BRANCH_ID)`.
- FK إلى `GL_ACCOUNT`.
- FK إلى `SYS_BRANCH`.
- `IS_ACTIVE` كـ`NUMBER(1)`.
- فهرس `IX_GL_ACC_BRANCH_BRANCH` على `BRANCH_ID`.

---

## 8. قواعد العمل في `GlAccountService`

الواجهة:

```csharp
Task<IReadOnlyList<GlAccountTreeDto>> GetTreeAsync(...);
Task<IReadOnlyList<GlAccountDto>> GetPostableAccountsAsync(long branchId, ...);
Task<GlAccountDto> CreateAccountAsync(CreateGlAccountDto request, ...);
Task UpdateAccountStatusAsync(long accountId, bool isActive, ...);
```

### 8.1 `GetTreeAsync`

- يأخذ `CompanyId` من `ICurrentTenantContext` فقط.
- يستدعي Repository مرة واحدة للحصول على قائمة الحسابات المسطحة.
- يبني Dictionary حسب `Id` ثم يربط الأبناء بالآباء في الذاكرة.
- يرتب الجذور والأبناء باستخدام `AccountCode` بترتيب Ordinal.

### 8.2 `GetPostableAccountsAsync`

- يرفض `branchId <= 0`.
- يتحقق أن الفرع نشط ويتبع الشركة الحالية.
- يعيد فقط الحسابات:
  - التابعة للشركة الحالية.
  - النشطة.
  - من نوع `DETAIL`.
  - العامة، أو المرتبطة بالفرع المطلوب برابط نشط.

### 8.3 `CreateAccountAsync`

القواعد المطبقة:

- لا يقبل `CompanyId` من DTO؛ الشركة تأتي من Tenant context الموثوق.
- الكود أرقام ASCII فقط.
- النوع `HEADER` أو `DETAIL` فقط.
- الرصيد `D` أو `C` فقط.
- الكود غير مكرر داخل الشركة.
- الأب، إن وجد، يجب أن يكون من نفس الشركة ومن نوع `HEADER`.
- الفئة يجب أن تطابق فئة الأب.
- كود الابن يجب أن يبدأ بكود الأب ويكون أطول منه.
- الحد الأقصى خمسة مستويات.
- طول الكود:
  - المستويات 1 إلى 4: طول الكود يساوي رقم المستوى.
  - المستوى 5: طول الكود 6 خانات.
- كود الحساب الجذر يجب أن يساوي رقم الفئة.
- المستوى 5 يجب أن يكون `DETAIL`.
- `NormalBalance` يجب أن يطابق الرصيد الأساسي للفئة؛ تستخدم `IsContra` للعكس.
- Control account يجب أن يكون `DETAIL` ونوعه أحد `AR`, `AP`, `INVENTORY`.
- لا يسمح بـ`ControlAccountType` إذا كان `IsControlAccount = false`.
- `IsBranchSpecific` و`IsClearing` مسموحان فقط للحسابات `DETAIL`.
- الحساب الخاص بفروع يحتاج فرعًا واحدًا على الأقل.
- لا يسمح بـBranch links لحساب غير خاص بالفروع.
- كل فرع مطلوب يجب أن يكون نشطًا ويتبع الشركة الحالية.
- الحساب الجديد يبدأ `IsActive = true`.

### 8.4 `UpdateAccountStatusAsync`

- يتحقق من ID موجب.
- يقرأ الحساب داخل الشركة الحالية فقط.
- يحدّث `IsActive` دون أي Cascade تلقائي إلى الأبناء أو روابط الفروع.

---

## 9. محرك قراءة واستيراد Excel

### 9.1 `XlsxCoaWorkbookReader`

تم تنفيذه باستخدام:

- `System.IO.Compression.ZipArchive`
- `System.Xml.Linq`

لا توجد حزمة Excel خارجية جديدة.

يدعم:

- اختيار الشيت العربي `شجرة الحسابات`، أو أول شيت كـfallback.
- Shared strings.
- Inline strings وRich text runs.
- الخلايا الرقمية والنصية وBoolean values.
- إعادة stream إلى الموضع صفر إذا كان Seekable.
- التحقق من جميع العناوين الـ15.
- اكتشاف Header مفقود أو مكرر أو غير متوقع.
- اكتشاف Excel error cells.
- تحويل `level` إلى Integer.
- قبول Boolean representations التالية:
  - `true/false`
  - `1/0`
  - `y/n`
  - `yes/no`
- إرجاع أخطاء منظمة بدل رمي استثناء لمعظم أخطاء تنسيق المصنف.

### 9.2 `CoaExcelImportService.ValidateAsync`

التحقق يفرض:

- العدد بالضبط 315 حسابًا.
- كود مطلوب، رقمي وفريد.
- المستويات من 1 إلى 5.
- أطوال الأكواد وفق قاعدة المستويات.
- الجذر دون أب، وغير الجذر له أب.
- الأب موجود ومستواه أقل بواحد ونوعه `HEADER`.
- Prefix وفئة الابن متطابقان مع الأب.
- الحساب الذي له أبناء `HEADER`.
- لا يسمح بـ`HEADER` فارغ داخل النسخة القياسية المستوردة.
- المستوى الخامس `DETAIL`.
- الفئة معروفة وتطابق أول رقم من الكود.
- طبيعة الرصيد والقائمة المالية تطابقان تعريف الفئة.
- Control flags صحيحة.
- Branch-specific وClearing على `DETAIL` فقط.
- أطوال الأسماء والملاحظات ضمن حدود أعمدة Oracle.

### 9.3 تعريف الفئات داخل محرك الاستيراد

| Excel category | Code | Balance | Statement |
|---|---:|---|---|
| `ASSETS` | 1 | D | BALANCE_SHEET |
| `LIABILITIES` | 2 | C | BALANCE_SHEET |
| `EQUITY` | 3 | C | BALANCE_SHEET |
| `REVENUE` | 4 | C | INCOME_STATEMENT |
| `COGS` | 5 | D | INCOME_STATEMENT |
| `OPEX` | 6 | D | INCOME_STATEMENT |
| `OTHER_INCOME` | 7 | C | INCOME_STATEMENT |
| `OTHER_EXPENSE` | 8 | D | INCOME_STATEMENT |

### 9.4 `ImportAsync`

التدفق:

1. قراءة المصنف والتحقق الكامل.
2. أخذ `CompanyId` من Tenant context.
3. التحقق من `defaultBranchId` وأنه يتبع الشركة الحالية.
4. رفض الاستيراد إذا كانت الشركة تحتوي مسبقًا على أي حسابات.
5. التحقق من وجود الفئات الثماني في مخطط الـTenant وتوافقها.
6. إنشاء 315 كيانًا في الذاكرة.
7. ربط `ParentAccount` بين الكيانات الجديدة.
8. إنشاء رابط مع `defaultBranchId` لكل حساب `IsBranchSpecific`.
9. تنفيذ `AddRange` و`SaveChanges` داخل Transaction واحدة.

مهم:

- الاستيراد القياسي يعمل فقط على شجرة فارغة، وليس Merge/Update.
- لا يتم ضبط `Category` navigation عند الإنشاء؛ يتم ضبط `CategoryId` فقط لتجنب محاولة EF إدراج Category detached مرة أخرى.
- علاقات الآباء بين الحسابات الجديدة تُضبط عبر Navigation ويقوم EF بتوليد المفاتيح وربط FKs.
- لا يوجد استيراد تلقائي عند ترقية جميع الشركات؛ هذا قرار مقصود لتجنب تعديل بيانات المحاسبة دون أمر صريح.

---

## 10. المستودع والوصول للبيانات

`GlAccountRepository` يطبق:

- `GetAllAsync(companyId)` مع `AsNoTracking`, Category وBranchLinks.
- `GetPostableAsync(companyId, branchId)` مع الفلاتر داخل SQL.
- `GetByIdAsync(companyId, accountId)` ككيان tracked للتعديل.
- `AccountCodeExistsAsync` داخل الشركة.
- `HasAccountsAsync` داخل الشركة.
- `GetCategoriesAsync` من المخطط الحالي؛ لا يحتاج CompanyId لأن الجدول Tenant-local.
- `BranchBelongsToCompanyAsync` ويتطلب الفرع نشطًا.
- `AddAsync` و`SaveChangesAsync`.
- `ImportAsync` داخل Transaction.

كل استعلام حسابات حساس للـTenant يفلتر بـ`CompanyId`، إضافة إلى عزل Oracle schema نفسه.

---

## 11. حماية الـMulti-Tenant

### 11.1 مصدر `CompanyId`

`HttpCurrentTenantContext` لا يقرأ CompanyId من DTO ولا يستخدم claim كـfallback.

يقرأ فقط:

```text
HttpContext.Items[TenantRequestContext.HttpContextItemKey]
```

وهو السياق الموثوق الذي يضعه `SchemaRoutingMiddleware` بعد التحقق من الشركة والمخطط.

إذا لم يوجد HTTP context أو لم يحل الـMiddleware Tenant صحيحًا، تفشل العملية برسالة واضحة.

### 11.2 أثر ذلك على الأعمال الخلفية

الخدمات الحالية مصممة لتُستدعى داخل طلب HTTP مرّ عبر `SchemaRoutingMiddleware`. إذا احتاج Background Service إلى استخدامها، فلا تعتمد مباشرة على `HttpCurrentTenantContext`؛ يجب توفير Tenant context صريح وآمن أو إنشاء abstraction مناسب للعمل الخلفي.

### 11.3 الجداول

`ACCOUNT_CATEGORY`, `GL_ACCOUNT`, و`GL_ACCOUNT_BRANCH` مصنفة Tenant-only داخل `OracleSchemaService`، وليست جداول مركزية.

---

## 12. تهيئة Oracle لكل Tenant

أُضيفت الدالة التالية داخل `OracleSchemaService`:

```csharp
EnsureAccountingSchemaAsync(OracleConnection connection, string schemaName)
```

وتقوم بـ:

1. التحقق الصارم من Oracle schema identifier.
2. إنشاء الجداول الثلاثة إذا لم تكن موجودة.
3. تطبيق Default values على Boolean flags.
4. زرع/تحديث الفئات الثماني باستخدام parameterized `MERGE` داخل Transaction.
5. إضافة PK/Unique/FK/Check constraints بطريقة قابلة لإعادة التشغيل.
6. إضافة الفهارس المطلوبة.

تم استدعاؤها في المسارات التالية:

- إنشاء Tenant جديد بعد Clone من developer schema.
- تهيئة `DEV_TEMPLATE` بعد EF bootstrap.
- مزامنة Tenant واحد.
- ترقية/مزامنة جميع Tenant schemas القائمة.

كما أُضيف `GL_ACCOUNT` إلى قائمة Reset identity في مخطط الشركة الجديدة.

### 12.1 الفئات المزروعة

| Id/Code | العربية | English | Balance | Statement |
|---:|---|---|---|---|
| 1 | الأصول | Assets | D | BALANCE_SHEET |
| 2 | الالتزامات | Liabilities | C | BALANCE_SHEET |
| 3 | حقوق الملكية | Equity | C | BALANCE_SHEET |
| 4 | الإيرادات | Revenue | C | INCOME_STATEMENT |
| 5 | تكلفة المبيعات | Cost of Sales | D | INCOME_STATEMENT |
| 6 | المصروفات التشغيلية | Operating Expenses | D | INCOME_STATEMENT |
| 7 | الإيرادات الأخرى | Other Income | C | INCOME_STATEMENT |
| 8 | المصروفات الأخرى | Other Expenses | D | INCOME_STATEMENT |

### 12.2 حدود التحقق الحالية

تمت مراجعة DDL ومطابقته مع EF configuration وتم بناء المشروع، لكن لم يتم الاتصال بقاعدة Oracle فعلية خلال هذه المهمة. لذلك يجب وصف التحقق الحالي بأنه `compile/static verification` فقط، وليس Oracle integration verification.

---

## 13. Dependency Injection

### Application

```csharp
services.AddScoped<IGlAccountService, GlAccountService>();
services.AddScoped<ICoaExcelImportService, CoaExcelImportService>();
```

### Infrastructure

```csharp
services.AddScoped<IGlAccountRepository, GlAccountRepository>();
services.AddScoped<ICurrentTenantContext, HttpCurrentTenantContext>();
services.AddScoped<ICoaWorkbookReader, XlsxCoaWorkbookReader>();
```

`IHttpContextAccessor` مسجل مسبقًا في `ThinkOnErp.API/Program.cs`.

---

## 13.1 واجهة HTTP API

### `GlAccountsController`

Base route:

```text
/api/accounting/gl-accounts
```

| Method | Route | الصلاحية | الوظيفة |
|---|---|---|---|
| `GET` | `/tree` | مستخدم موثق + Tenant محدد | إرجاع الشجرة الهرمية كاملة |
| `GET` | `/postable?branchId={id}` | مستخدم موثق + Tenant محدد | الحسابات التفصيلية النشطة القابلة للترحيل للفرع |
| `GET` | `/{id}` | مستخدم موثق + Tenant محدد | جلب حساب واحد |
| `POST` | `/` | `TenantAdminOnly` | إنشاء حساب جديد |
| `PUT` | `/{id}` | `TenantAdminOnly` | تعديل الحقول غير الهيكلية |
| `PATCH` | `/{id}/status` | `TenantAdminOnly` | تفعيل أو تعطيل حساب |
| `DELETE` | `/{id}` | `TenantAdminOnly` | حذف حساب غير جذري بلا أبناء أو مراجع DB |

الحقول القابلة للتعديل عبر `PUT`: الاسمان، Contra/Control/Branch/Clearing flags،
`ControlAccountType`، الوصف، الملاحظات، وروابط الفروع. تبقى `AccountCode`,
`ParentAccountId`, `CategoryId`, `AccountLevel`, `AccountType`, `NormalBalance`
و`IsActive` غير قابلة للتعديل عبر هذا المسار.

الحذف يعيد لقطة `GlAccountDto` قبل الحذف، ويمنع الجذور والحسابات ذات الأبناء.
تُحذف روابط الفروع قبل الحساب. خطأ Oracle `ORA-02292` يتحول إلى
`409 GL_ACCOUNT_IN_USE`؛ بقية أخطاء قاعدة البيانات لا تُخفى كـConflict.

Body تغيير الحالة:

```json
{
  "isActive": true
}
```

`isActive` nullable في DTO حتى لا يتحول غياب الحقل خطأً إلى `false` وتعطيل الحساب.

### `AccountCategoriesController`

```text
GET /api/accounting/account-categories
```

يعيد `ApiResponse<List<AccountCategoryDto>>` للفئات الثماني بالترتيب، ويستخدم
`id` الناتج كـ`categoryId` عند إنشاء الحساب. المسار Tenant-scoped ويتطلب مستخدمًا
موثقًا، ولا يتطلب صلاحية إدارية لأنه للقراءة فقط.

الدليل العملي الكامل والأمثلة وأكواد الأخطاء موجودة في:

```text
docs/ACCOUNTING_API_GUIDE.md
```

### `CoaImportController`

Base route:

```text
/api/accounting/coa-import
```

| Method | Route | Content type | الصلاحية | الوظيفة |
|---|---|---|---|---|
| `POST` | `/validate` | `multipart/form-data` | `TenantAdminOnly` | فحص الملف دون تعديل البيانات |
| `POST` | `/import` | `multipart/form-data` | `TenantAdminOnly` | استيراد 315 حسابًا إلى شجرة فارغة |

حقول `validate`:

```text
file: XLSX file
```

حقول `import`:

```text
file: XLSX file
defaultBranchId: positive Int64
```

حماية الرفع:

- الملف مطلوب وغير فارغ.
- الامتداد `.xlsx` فقط.
- الحد الأقصى 10MB.
- `RequestSizeLimit` و`RequestFormLimits` مطبقان.
- لا يتم الوثوق بـContent-Type وحده؛ قارئ XLSX يتحقق من ZIP/XML داخليًا.
- اسم الملف المستخدم في السجل يمر عبر `Path.GetFileName`.

دلالات الاستجابة:

- `validate` يعيد HTTP 200 حتى إذا كانت `Data.IsValid = false`؛ نجاح الطلب يعني أن عملية الفحص اكتملت، وتبقى أخطاء الصفوف والأعمدة داخل `Data.Errors`.
- `import` يعيد 201 عند النجاح.
- `import` يعيد 400 عند أخطاء الملف/الفرع/التحقق.
- `import` يعيد 409 عند `COA_ALREADY_INITIALIZED`.
- استجابة الاستيراد المرفوض تحفظ `CoaImportResultDto` داخل `ApiResponse.Data` ولا تفقد الأخطاء المنظمة.

### Swagger

أضيفت وثيقة مستقلة باسم `Accounting API` ويمكن فتحها من قائمة المستندات في:

```text
/swagger/index.html
```

وملف OpenAPI المباشر هو:

```text
/swagger/accounting/swagger.json
```

تحتوي وثيقة Accounting على Controllers التالية فقط:

- `GlAccountsController`: CRUD شجرة الحسابات والحسابات القابلة للترحيل والحالة.
- `AccountCategoriesController`: الفئات المحاسبية الثماني.
- `CoaImportController`: فحص واستيراد ملف COA.
- `FiscalYearController`: إدارة السنوات المالية.
- `CurrencyController`: البيانات المرجعية للعملات؛ تبقى محمية بـ`SuperAdminOnly` وتظهر كذلك في SuperAdmin Swagger.

تبقى واجهات `GlAccounts`, `CoaImport`, و`FiscalYear` ظاهرة أيضًا داخل Company Swagger للتوافق مع المستهلكين الحاليين. لا تغيّر وثيقة Swagger أي صلاحية تشغيلية.

تقوم `TenantCompanyHeaderOperationFilter` بإظهار ترويستي `X-Company-Code` و`X-Company-Id` لمسارات `TenantScoped`. لا تظهران لواجهات العملات لأنها مركزية وليست Tenant-scoped.

---

## 14. الاختبارات التي أُضيفت ونتائجها

تم تشغيل الاختبارات الـ33 التالية بصورة مركزة، ونجحت كلها:

1. `ReadAsync_InlineStringWorksheet_MapsEveryImportField`
2. `ValidateAsync_WhenAccountCountIsNot315_ReturnsCountError`
3. `ValidateAsync_WhenParentDoesNotExist_ReturnsParentError`
4. `ValidateAsync_WhenReaderReportsBadHeader_PropagatesHeaderError`
5. `GetTreeAsync_BuildsSortedHierarchyForCurrentTenant`
6. `GetPostableAccountsAsync_UsesTenantAndBranchScope`
7. `CreateAccountAsync_AssignsCurrentTenantAndValidatedParent`
8. `GetTree_WhenServiceReturnsTree_ReturnsSuccessfulEnvelope`
9. `Create_WhenRequestIsValid_Returns201AndForwardsRequest`
10. `UpdateStatus_WhenIsActiveIsMissing_Returns400WithoutCallingService`
11. `Controller_UsesTenantScopeAndTenantAdminPolicyForMutations`
12. `Validate_WhenWorkbookHasValidationErrors_Returns200WithStructuredResult`
13. `Import_WhenWorkbookIsValid_Returns201`
14. `Import_WhenChartAlreadyExists_Returns409AndPreservesErrors`
15. `Validate_WhenExtensionIsNotXlsx_Returns400WithoutCallingService`
16. `Validate_WhenFileIsMissing_Returns400WithoutCallingService`
17. `Validate_WhenFileExceeds10Mb_Returns400WithoutCallingService`
18. `Import_WhenDefaultBranchIdIsInvalid_Returns400WithoutCallingService`
19. `Controller_UsesTenantScopeAndTenantAdminPolicy`
20. `SwaggerGeneration_AccountingDocumentContainsOnlyAccountingRoutes`
21. `GetAccountAsync_UsesCurrentTenantAndMapsAccount`
22. `GetCategoriesAsync_MapsRepositoryCategories`
23. `UpdateAccountAsync_UpdatesEditableFieldsAndSynchronizesBranches`
24. `DeleteAccountAsync_WhenAccountHasChildren_ThrowsConflict`
25. `DeleteAccountAsync_WhenLeafAccountExists_DeletesAndReturnsSnapshot`
26. `GetById_WhenAccountExists_ReturnsSuccessfulEnvelope`
27. `Update_WhenRequestIsValid_ReturnsUpdatedAccount`
28. `Delete_WhenServiceDeletesAccount_ReturnsDeletedSnapshot`
29. `GetAll_WhenCategoriesExist_ReturnsSuccessfulEnvelope`
30. `Controller_UsesAuthenticatedTenantScope`
31. `GetAccountAsync_WhenAccountDoesNotExist_ThrowsNotFound`
32. `DeleteAccountAsync_WhenAccountIsRoot_ThrowsConflict`
33. `Update_WhenRequestIsMissing_Returns400WithoutCallingService`

النتيجة الفعلية:

```text
Passed: 33
Failed: 0
Skipped: 0
```

كما تم تشغيل قارئ الإنتاج على ملف Excel الحقيقي وكانت النتيجة:

```text
Rows = 315
Errors = 0
IsValid = true
First account = 1 / الأصول
Last account = 831101 / Income Tax Expense
```

### مشكلة مشروع الاختبارات الكامل

عند بناء مشروع الاختبارات كاملًا:

```powershell
dotnet build tests\ThinkOnErp.Infrastructure.Tests\ThinkOnErp.Infrastructure.Tests.csproj --no-restore --verbosity minimal
```

فشل بـ312 خطأ تاريخيًا خارج مجلد `Accounting`, مثل:

- مراجع قديمة إلى `AddTraceabilitySystem`.
- مراجع قديمة إلى `OracleDbContext.CreateConnection`.
- خصائص/واجهات قديمة في Audit, Alerts, SLA وStorage tests.
- تعارضات نسخ EF Core في مشروع الاختبارات.

لا تنسب هذا الفشل إلى ميزة شجرة الحسابات، ولا تصلح هذه الاختبارات غير المتعلقة إلا بطلب منفصل.

---

## 15. البناء والتحقق المنفذ

الأمر:

```powershell
dotnet build src\ThinkOnErp.API\ThinkOnErp.API.csproj --no-restore --verbosity minimal
```

النتيجة:

```text
Build succeeded
0 Error(s)
```

التحذيرات الباقية موجودة في المشروع مسبقًا وتشمل:

- Advisories معروفة لـOpenTelemetry وMailKit.
- توافق C5 وTDigest المستعادة من .NET Framework.
- XML documentation warnings في ملفات Domain قديمة.

فحص whitespace/diff:

```powershell
git diff --check
```

لم يظهر أي خطأ whitespace؛ ظهرت فقط رسائل تحويل LF إلى CRLF في بعض الملفات المعدلة.

---

## 16. ما لم يتم تنفيذه

لا تدّع أن النقاط التالية مكتملة:

1. لم يُنفّذ `EnsureAccountingSchemaAsync` على Oracle حي.
2. لم تُنشأ الجداول فعليًا في DEV_TEMPLATE أو Tenant حقيقي ضمن هذه المهمة.
3. لم تُستورد الحسابات الـ315 إلى شركة فعلية.
4. لم يُحدّد `defaultBranchId` تشغيليًا لشركة فعلية.
5. لم تُضف UI لشجرة الحسابات أو شاشة الاستيراد.
6. لم تتم إضافة audit events مخصصة لإنشاء/تعطيل/استيراد الحسابات؛ ما زالت معالجة الاستثناءات العامة تسجل الأخطاء فقط.
7. لم تُختبر حالات التزامن عند محاولتي استيراد متزامنتين على شجرة فارغة.
8. `UpdateAccountStatusAsync` لا يفرض قواعد تعطيل Parent/Children ولا يعمل Cascade.
9. الاستيراد القياسي لا يدعم Merge أو Replace؛ يقبل شجرة فارغة فقط.
10. لم يُشغّل Smoke test للـHTTP endpoints ضد API وOracle حقيقيين؛ التحقق الحالي Controller unit tests + production build.

---

## 17. خطوات التشغيل التالية المقترحة

### 17.1 التحقق على Oracle تجريبي

شغّل أولًا مسار تهيئة Developer schema أو مزامنة Tenant تجريبي، ثم تحقق من:

```sql
SELECT TABLE_NAME
FROM USER_TABLES
WHERE TABLE_NAME IN ('ACCOUNT_CATEGORY', 'GL_ACCOUNT', 'GL_ACCOUNT_BRANCH');

SELECT COUNT(*) FROM ACCOUNT_CATEGORY;

SELECT CONSTRAINT_NAME, CONSTRAINT_TYPE, STATUS
FROM USER_CONSTRAINTS
WHERE TABLE_NAME IN ('ACCOUNT_CATEGORY', 'GL_ACCOUNT', 'GL_ACCOUNT_BRANCH')
ORDER BY TABLE_NAME, CONSTRAINT_NAME;

SELECT INDEX_NAME, TABLE_NAME, UNIQUENESS
FROM USER_INDEXES
WHERE TABLE_NAME IN ('ACCOUNT_CATEGORY', 'GL_ACCOUNT', 'GL_ACCOUNT_BRANCH')
ORDER BY TABLE_NAME, INDEX_NAME;
```

المتوقع: 3 جداول و8 فئات وجميع القيود فعّالة.

### 17.2 الاستيراد التجريبي

داخل طلب شركة موثوق مرّ عبر `SchemaRoutingMiddleware`:

```csharp
var validation = await coaImportService.ValidateAsync(workbookStream, cancellationToken);
if (!validation.IsValid)
{
    // Return validation.Errors to the caller.
}

var import = await coaImportService.ImportAsync(
    workbookStream,
    defaultBranchId,
    cancellationToken);
```

ملاحظة: يجب إعادة فتح stream أو التأكد أنه Seekable. القارئ يعيده إلى الموضع صفر تلقائيًا إذا كان Seekable.

بعد الاستيراد تحقق من:

```sql
SELECT COUNT(*) FROM GL_ACCOUNT;
SELECT COUNT(*) FROM GL_ACCOUNT WHERE ACCOUNT_TYPE = 'DETAIL' AND IS_ACTIVE = 1;
SELECT COUNT(*) FROM GL_ACCOUNT_BRANCH;

SELECT ACCOUNT_CODE, COUNT(*)
FROM GL_ACCOUNT
GROUP BY ACCOUNT_CODE
HAVING COUNT(*) > 1;

SELECT child.ACCOUNT_CODE
FROM GL_ACCOUNT child
LEFT JOIN GL_ACCOUNT parent ON parent."Id" = child.PARENT_ACCOUNT_ID
WHERE child.PARENT_ACCOUNT_ID IS NOT NULL
  AND parent."Id" IS NULL;
```

المتوقع: 315 حسابًا، لا تكرار ولا Orphans.

### 17.3 التحقق من API عبر Swagger/Postman

1. سجّل دخول مستخدم شركة، أو استخدم SuperAdmin مع اختيار شركة صريح.
2. افتح `Accounting API` من `/swagger/index.html`.
3. نفّذ `/api/accounting/coa-import/validate` أولًا.
4. راجع `data.isValid` و`data.errors`.
5. نفّذ `/api/accounting/coa-import/import` مع `defaultBranchId` صحيح على Tenant تجريبي فارغ.
6. نفّذ `/api/accounting/gl-accounts/tree` وتحقق من 8 جذور و315 حسابًا إجمالًا.
7. نفّذ `/postable` بفرعين مختلفين للتحقق من Branch-specific filtering.
8. نفّذ `GET /gl-accounts/{id}` ثم `PUT` وتحقق من بقاء الحقول الهيكلية كما هي.
9. اختبر `DELETE` على leaf تجريبي، ثم على root وحساب له أبناء وتحقق من `409`.
10. نفّذ `GET /api/accounting/account-categories` وتحقق من الفئات الثماني.
11. أضف audit events صريحة للاستيراد والإنشاء والتعديل والحذف وتغيير الحالة إذا أصبحت مطلوبة تشغيليًا.

---

## 18. ملاحظات مهمة لأي AI لاحق

1. لا تغيّر تصميم `normal_balance` للحسابات العكسية دون قرار محاسبي صريح؛ `IsContra` منفصل عمدًا.
2. لا تجعل الاستيراد تلقائيًا أثناء Tenant upgrade؛ هذا قد يغيّر بيانات شركات قائمة.
3. لا تثق بـCompanyId أو schema name القادم من DTO أو claim غير متحقق.
4. لا تنقل `ACCOUNT_CATEGORY` إلى المخطط المركزي دون إعادة تصميم FK وعزل البيانات.
5. لا تضف مكتبة Excel جديدة ما لم توجد حاجة لا يغطيها القارئ الحالي.
6. لا تستخدم EF migrations لهذه الجداول قبل حسم استراتيجية tenant migrations؛ التكوينات حاليًا `ExcludeFromMigrations` ويقوم `OracleSchemaService` بالتهيئة.
7. لا توجد حاليًا جداول Journal/Ledger مرتبطة بـ`GL_ACCOUNT`. عند إضافتها يجب ربطها بـFK وإضافة فحص استخدام صريح قبل الحذف؛ لا تدّع أن الحركات مفحوصة حاليًا.
8. ابنِ مشروع الإنتاج دائمًا بعد أي تعديل:

```powershell
dotnet build src\ThinkOnErp.API\ThinkOnErp.API.csproj --no-restore --verbosity minimal
```

8. شغّل اختبارات Accounting بصورة مركزة، ولا تدّع أن كامل Test Suite ناجح.
9. راجع `git status --short` قبل أي تعديل؛ توجد ملفات مستخدم غير متتبعة لا تخص هذه الميزة ويجب الحفاظ عليها.

---

## 19. حالة Git وقت كتابة هذا المستند

- لم يتم إنشاء Commit.
- لم يتم Stage للملفات.
- ملفات الميزة الجديدة ما زالت Untracked، والملفات الأربعة القائمة معدلة في working tree.
- توجد ملفات مستخدم أخرى غير متتبعة، منها أدلة HTML وPDF. لم يتم حذفها أو تعديلها.

الملفات غير المتعلقة التي يجب الحفاظ عليها:

```text
Chart_Of_Accounts_Accountant_Guide.html
ThinkOn_Accounting_MVP_Dev_Spec.pdf
ThinkOn_COA_Backend_Guide.pdf
branch_permissions_architecture_guide.html
chart_of_accounts_architecture.html
```

---

## 20. Definition of Done للمرحلة التالية

تُعد المرحلة التشغيلية مكتملة فقط عند تحقق جميع ما يلي:

- [ ] نجاح تهيئة الجداول والفئات على Oracle تجريبي.
- [ ] نجاح إعادة تشغيل التهيئة دون أخطاء أو تكرار Objects.
- [ ] نجاح استيراد ملف الـ315 حسابًا داخل Tenant تجريبي.
- [ ] التحقق من 315 صفًا، 8 فئات، وعدم وجود duplicate/orphan records.
- [ ] التحقق من Branch-specific accounts للفرع الصحيح.
- [ ] التحقق من `GetTreeAsync` و`GetPostableAccountsAsync` عبر اتصال Oracle فعلي.
- [x] إضافة API وTenant authorization وSwagger exposure.
- [ ] إضافة audit events محاسبية مخصصة إذا كانت مطلوبة.
- [ ] نجاح بناء Production API.
- [ ] نجاح اختبارات Accounting المركزة.
- [ ] توثيق أي تحذيرات أو أخطاء تاريخية منفصلة عن الميزة.
