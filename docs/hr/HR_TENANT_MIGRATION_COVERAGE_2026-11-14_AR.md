# جرد تغطية مخطط HR للمستأجرين — مراجعة ساكنة للمصدر

**التاريخ:** 15 نوفمبر 2026  
**الخطة:** `OPS-16` (يعتمد على `OPS-02/03/14`)  
**النطاق:** مصدر HR للـbackend وقاعدة البيانات فقط. لم يُتصل بقاعدة بيانات ولم تُشغّل اختبارات.

### إضافة لاحقة — `LEV-14`، 15 نوفمبر 2026

فُصل تدقيق إلغاء طلب الإجازة عن سبب الرفض: أضيفت `CANCELLED_BY` و`CANCELLATION_DATE` و`CANCELLATION_REASON` اختيارياً إلى `LeaveRequest`، مع migration tenant `20261115002000_AddLeaveRequestCancellationAuditFields`. تفحص التعريفات الثلاثة القائمة قبل أول DDL، وتضيف المفقود nullable بلا default أو backfill؛ والجدول الغائب يظل لمسار bootstrap. لم يحدث backfill لـ`REJECTION_REASON` التاريخي؛ DTO يبقي حقول الإلغاء القديمة ظاهرة عبر fallback من `REJECTION_REASON/UPDATE_USER/UPDATE_DATE` عند غياب الأعمدة الجديدة. حُدث snapshot ليشمل كيان `HR_LEAVE_REQUEST` وعلاقاته التي كانت غائبة عنه، بالإضافة إلى حقول التدقيق الجديدة. نجح بناء API وInfrastructure و`HrSchemaTool` عبر SDK `10.0.302`؛ لا اختبارات أو تطبيق migration/DDL.

## النتائج

### متابعة 15 نوفمبر 2026 — خارج جرد النصوص

أضيفت migration `20261115001500_AddAttendanceMultipleSessions` لترقية `HR_ATTENDANCE_POLICY.ALLOW_MULTIPLE_SESSIONS NUMBER(1) DEFAULT 0 NOT NULL` على tenant؛ `0` هو default الموجود في إعداد EF ويعني false. تتحقق migration من الجدول والعمود والتعريف والقيم القائمة، وDown يحافظ على الإعدادات. كما حُدث `OracleDbContextModelSnapshot` لخصائص `AttendancePolicy` و`AttendanceDay` المضافة في دفعات الحضور/geofence (لقطات الشفت والمنطقة الزمنية وحقول الجغرافيا). جرد snapshot التاريخي البالغ 227 خاصية لم يُعَد احتسابه، ولا يعني هذا تعديل snapshot HR بالكامل.
### تحديث 15 نوفمبر 2026 — OPS-16

أضيفت migration `20261115000700_AddLeaveBalanceDisplayFormat` لتوفير `HR_LEAVE_SETTINGS.BALANCE_DISPLAY_FORMAT` المطلوب عند غيابه باستخدام الافتراضي المجالّي `DECIMAL` المثبت في `HrLeaveSettings`؛ لا تغيّر قيمة عمود موجود. وأضافت `20261115000800_AddLeavePolicyName` `POLICY_NAME` إلزامياً فقط إذا كان الجدول فارغاً، وتوقف بوضوح إذا وجدت صفوف بلا سياسة backfill معتمدة. هذا تحقق ساكن من المصدر وبناء فقط، دون Oracle أو اختبارات.

### تحديث 14 نوفمبر 2026 — OPS-16

أضيف في `HrRepair` preflight قبل أول DDL لنقل البيانات التاريخية من `HR_EMP_SALARY_STRUCTURE` و`HR_EMP_SALARY_STRUCT_LINE` و`HR_EMP_SHIFT_ASSIGNMENT` إلى جداولها الحالية. الفحص يطابق وجود الأعمدة وتوافق النوع/السعة/nullability، ويرفض IDs المكررة أو الصفوف المتعارضة ذات ID مشترك، ويكشف nulls التي لا تقبلها الوجهة وعلاقات سطور الرواتب اليتيمة. أعيد استخدام خريطة الأعمدة نفسها في INSERT. هذا تحقق كودي/build فقط؛ لا قراءة Oracle فعلية ولا تطبيق DDL أو اختبارات.

في installer الشفت `HrShiftSchema` أضيف preflight لقيد `CK_HR_SHIFT_ASSIGN_KIND` قبل أول DDL: يكشف تصادم الاسم على مستوى المخطط، ويقارن شرط القيد الموجود، ويتحقق من الصفوف المخالفة قبل الإضافة أو ENABLE VALIDATE. كما يتحقق مسبقاً من أن `HR_EMPLOYEE_SHIFT_ASSIGNMENT.EFFECTIVE_TO` موجود من نوع DATE وnullable. هذا إثبات مصدر وبناء فقط، دون Oracle أو اختبارات.


في installer تسوية cashout `HrLeaveCashoutSchema` أضيف preflight قبل DDL للفهرس الفريد `UX_HR_LCO_LINE_ADJ`: يرفض تصادم الاسم أو تعريفاً غير فريد/غير صالح أو أعمدة مختلفة، ويكشف `PAYROLL_ADJUSTMENT_ID` المكرر غير الفارغ على جدول قائم. هذا إثبات مصدر وبناء فقط، دون اتصال Oracle أو اختبار.

واستُكمل preflight لعلاقات FK الأربعة التي يضمنها installer: وجود مفاتيح الوالد، تطابق القيد المسجل بالاسم/الجدول/الأعمدة/قاعدة الحذف، وكشف orphan rows قبل DDL. أضيف `HR_SALARY_COMPONENT` إلى الجداول المطلوبة. لم يُنفذ استعلام Oracle فعلي.

في HrAttendanceProcessingSchema أضيف preflight قبل DDL للتحقق من أسماء الفهارس الأربعة وتعريف أعمدتها وحالتها، واسم FK FK_HR_RAW_DAY وهدفه ووجود المفتاح المرجعي، وقيود CHECK المسماة وتعريفاتها، مع فحص orphan links والصفوف المخالفة. تُستكمل حالة ENABLED/VALIDATED للقيود المتوافقة بعد ذلك. تحقق مصدر/build فقط، بلا Oracle أو اختبارات.

في HrAttendanceTimeSchema أضيف فحص ملكية trigger TRG_HR_TIME_IMMUTABLE قبل أي DDL: يقبل فقط trigger المربوط بجدول إعداد توقيت الحضور وبنوع BEFORE STATEMENT وأحداث UPDATE/DELETE، ويرفض الاسم المحجوز إذا كان هدفه/حدثه مختلفاً. لم يُستبدل trigger ولم يحصل اتصال Oracle.

أضيف كذلك فحص/استكمال مفتاح HR_ATTENDANCE_TIME_SETTINGS.COMPANY_ID بما يطابق HasKey(CompanyId): يفحص تركيب المفتاح الحالي، القيم null أو المكررة، وتصادم اسم القيد قبل DDL؛ ثم يضيف PK عند غيابه أو يفعّله ويتحقق منه. لم يتصل بـOracle.

في HrLeavePolicySchema أضيف preflight لفهرس IX_HR_LEAVE_POLICY_EFFECTIVE قبل أي تعديل: يرفض الاسم المحجوز إذا كان على جدول/أعمدة أو نوع/حالة مختلفة. كما يتحقق أن الصفوف التي تحتاج EFFECTIVE_FROM من CREATION_DATE قابلة للتأريخ قبل إضافة الأعمدة أو تنفيذ backfill. تحقق الـverify صار يشترط الفهرس غير الفريد الصالح بأعمدته المتوقعة. لا اتصال Oracle أو DDL.

في HrPayrollAdjustmentDecisionSchema نُقل كشف تكرار روابط المصدر إلى ما قبل إضافة الأعمدة، مع التحقق من COMPANY_ID، وفحص تصادم/تعريف الفهرس UX_HR_ADJ_SOURCE قبل DDL. بعد الإضافة يتحقق installer من فهرس UNIQUE صالح على الأعمدة الثلاثة. لم يُنفذ اتصال أو DDL.

عُثر على عدم تطابق في إنشاء `HR_END_OF_SERVICE_SETTLEMENT`: installer كان يعرّف `CURRENCY_CODE` كـ`NVARCHAR2(10)` بينما إعداد EF يطلب `NVARCHAR2(20)`. وُحّد تعريف الإنشاء إلى 20، وأضيفت migration tenant `20261114235900_AlignEndOfServiceCurrencyLength` لتوسيع العمود الموجود فقط بعد التحقق المسبق من النوع وnullability وغياب default/virtual؛ إذا كان الجدول غائباً تتركه للـbootstrap، وإذا كان العمود غائباً أو غير متوافق تفشل قبل DDL. التوسيع لا يقتطع قيماً و`Down` لا يضيّق المجال. هذا تعديل للمصدر فقط؛ لم تُطبق migration أو تُشغّل اختبارات أو اتصال Oracle.

في مراجعة installer cashout، استُبدل قبول قيود FK حسب الاسم والحالة فقط بفحص نوع القيد والمالك والجدول/الأعمدة المشار إليها والأعمدة المحلية وقاعدة الحذف `NO ACTION`؛ إذا كان التعريف صحيحاً لكنه غير `ENABLED/VALIDATED` يعاد تفعيله والتحقق منه. لم يُشغّل installer أو أي اتصال Oracle.

عُزلت أعمدة EOS الاختيارية التي كان installer ينشئها فقط دون استكمالها على الجداول القائمة: أضيفت migration `20261114235800_AddEndOfServiceOptionalColumns` لحقلي التعديل الاختياريين في `HR_END_OF_SERVICE_POLICY`، وأعمدة `BRANCH_ID` وملاحظات/تواريخ/حقول اعتماد وإلغاء وتدقيق `HR_END_OF_SERVICE_SETTLEMENT` ووصف سطر التسوية. تتحقق من التعريفات الموجودة كلها قبل أول DDL، وتضيف المفقود nullable بلا backfill؛ الجداول الغائبة تظل لمسار bootstrap و`Down` لا يحذف تاريخاً. لم تُطبق migration أو تُشغّل اختبارات أو Oracle. الأعمدة غير القابلة للـnull التي لا تحمل قيمة افتراضية آمنة، ومنها بيانات مدة/مبالغ التسوية، لم تُرحّل: تحتاج سياسة backfill قبل DDL على جدول مأهول.

وبنفس الضابط، أضيفت migration tenant `20261114235700_AddShortLeaveOptionalColumns` لأعمدة الفرع والتدقيق والموافقة/الرفض/الإلغاء الاختيارية في جدولي short leave. تفحص جميع التعريفات الموجودة قبل DDL، وتضيف المفقود nullable فقط، وتترك الجداول الغائبة للـbootstrap. الحقول المطلوبة التي قد تحتاج backfill بقيت خارج الترحيل. لم تُطبق migration أو تُشغّل اختبارات أو Oracle.

أضيفت migration tenant `20261115000100_AddCitizenshipCountryCodeColumns` لعمودي `HR_EMPLOYEE.CITIZENSHIP_COUNTRY_CODE` و`HR_LEAVE_SETTINGS.ELIGIBLE_CITIZENSHIP_COUNTRY_CODE` اللذين كان لهما DDL في template دون ترقية صريحة للجدول القائم على tenant. تفحص التعريفين الموجودين قبل أول DDL، وتقبل `VARCHAR2(2 CHAR)` أو `NVARCHAR2(2 CHAR)` القائمين، وتضيف المفقود nullable كـ`VARCHAR2(2 CHAR)` بما يطابق DEV_TEMPLATE؛ لا تحوّل قيمة `NATIONALITY` النصية أو تفرض قائمة دول، و`Down` يحافظ على القيم. لم تُطبق migration أو تُشغّل اختبارات أو Oracle.

مراجعة ساكنة لاحقة للترحيلات `20261114235700_AddShortLeaveOptionalColumns` و`20261114235800_AddEndOfServiceOptionalColumns` و`20261114235900_AlignEndOfServiceCurrencyLength` لم تجد فجوة إضافية في preflight أو سلوك الإضافة والتراجع: التعريفات الحالية تُفحص قبل أول DDL، والاختياري يضاف nullable بلا backfill، ولا يُضيّق طول العملة في `Down`. لم يتغير عدد صفوف الجرد أو حالة القبول؛ لا اختبار أو Oracle/DDL.

عُدّل `HrEosShortLeaveSchema.Install` كذلك ليجري preflight لتعريفات هذه الأعمدة الاختيارية قبل أول DDL، ثم يضيف المفقود للمخطط `DEV_TEMPLATE` ويضمها إلى فحص mapped columns. بذلك يوجد مصدر ترقية لكل من installer وtenant migration لهذه الحقول nullable؛ لا يثبت هذا تطبيقهما أو قبول metadata على قاعدة.

وأضيف إلى installer فحص مسبق للأعمدة المطلوبة الممثلة في EF على جداول EOS/short leave القائمة: إذا كان عمود مطلوب مفقوداً يتوقف قبل أي DDL برسالة مراجعة/backfill، بدلاً من إنشاء بعض الجداول ثم الفشل عند mapped SELECT. كما يشمل verifier الأعمدة المطلوبة والاختيارية المعرفة ويعيد مطابقة metadata الاختيارية. هذا لا يختار قيمة backfill للحقول المالية أو السياسات الإلزامية؛ يلزم حسمها قبل ترحيل قاعدة مأهولة.

وشمل preflight للحقول غير النصية المطلوبة الأنواع العددية ودقتها ومقياسها (`NUMBER(p,s)`)، و`TIMESTAMP(7)` و`INTERVAL DAY(8) TO SECOND(7)`، مع فحص NOT NULL وعدم كون العمود virtual، قبل أول DDL وبعد التثبيت. تم بناء `HrSchemaTool` بنجاح؛ هذا لا يتحقق من نتائج metadata أو Oracle لأن installer لم يُشغّل.

امتدت مراجعة الأنواع العددية والزمنية إلى installers أخرى. أضيف `HrSchemaColumnGuard` مع preflight قبل أي DDL ثم تحقق بعده لمسارات `HrAttendanceTimeSchema` و`HrAttendanceProcessingSchema` و`HrAttendanceCorrectionSchema` و`HrLeaveClosePolicySnapshotSchema` و`HrLeaveAccrualSettingsSchema` و`HrShiftSchema` و`HrMobilePunchSchema`، إضافةً إلى إعدادات وأعمدة cashout الاختيارية في `HrLeaveCashoutSettingsSchema` و`HrLeaveCashoutSchema`. يتحقق من وجود الجدول، ونوع Oracle والدقة/المقياس أو الحد الأدنى للطول، وnullability، وغياب default أو مطابقة default الموثق (0/1/MANUAL) والعمود الافتراضي؛ الأعمدة الاختيارية المضافة مصنفة صراحةً كي لا تُقبل أعمدة required مفقودة. لا يغطي هذا بعد كل الأعمدة المطلوبة في الجداول المنشأة قديماً، ولا يثبت مطابقة metadata على قاعدة.

لم يُشغّل installer أو اتصال Oracle/DDL. تبقى مراجعة بقية installer flows، والتحقق من الأعمدة/القيود المطلوبة في جداول قائمة، ثم قبولها على مخطط معزول قابل للاستعادة. لا تُشغّل اختبارات أو verifier قبل انتهاء كل التنفيذ.

استُكملت الجولة الساكنة التالية: `HrLeaveCitizenshipSchema`, `HrCurrencyPrecisionSchema`, `HrPayrollRecoveryAccountsSchema`, `HrLeavePolicySchema`, `HrLeaveBalanceMigrationSchema`, `HrBankAccountSchema`, `HrBranchLocationSchema`, `HrPayrollAdjustmentDecisionSchema`, `HrPayrollCorrectionIndexSchema`, و`HrLedgerSchema` تحصل الآن على metadata preflight قبل DDL أو استبدال trigger/فهرس، ثم تحقق بعد الإنشاء/الإضافة عند انطباقه. يثبت هذا توافق مصدر التثبيت فقط؛ لم تُشغّل قراءة Oracle أو verifier.

ما زال `HrAccountingHandoffSchema` يتطلب جولة مستقلة على ترتيب preflight لجميع الجداول/الأعمدة والقيود قبل أي DDL، مع مراجعة ملكية triggers في مسار ledger ومراجعة أعمدة mapped الإلزامية في المنشآت التاريخية.

- روجعت 309 خاصية نصية عليها `HasMaxLength` في ملفات إعداد EF الخاصة بـHR.
- 227 خاصية من إعدادات HR لا تظهر في `OracleDbContextModelSnapshot`.
- ظهر اسم 177 عموداً في مصدر migration لـEF. هذا عدّ لورود الاسم ولا يثبت وجود عملية migration مطابقة على الجدول والعمود نفسيهما. مثال تدقيقي: `CURRENCY_CODE` كان وارداً في migrations لمواضع أخرى، لكن لم تكن هناك ترقية لـ`HR_EMPLOYEE_LOAN.CURRENCY_CODE` و`HR_EMPLOYEE_ADVANCE.CURRENCY_CODE`؛ أضيفت لهما migration `20261114220000_AddLoanAdvanceCurrencyCodes` بعد إعداد هذا الجرد.
- لم يظهر اسم 50 عموداً في مصدر migrations وقت إعداد الجرد؛ وُجدت إحالات إلى 43 منها في `HrSchemaTool`. أضيفت منذ إعداد هذا الجرد migrations tenant لكل الأعمدة الخمسين، بينها ترقية الأعمدة المطلوبة العشرة للجداول الفارغة فقط؛ أما `ATTACHMENT_FILE_REF` فيُنشأ أيضاً مع الجدول الغائب من EF bootstrap.
- بعد التدقيق table+column، مسارات المصدر للصفوف الخمسين: 39 عموداً لها migrations tenant صريحة ضمن 16 ملفاً، و10 صفوف إضافية تشير إلى ملفات installer تحتوي DDL للجدول والعمود المعنيين، وعمود واحد يمر عبر حلقة `HrRepair` العامة؛ أعمدة الجنسية وإعدادات الإجازة صار لها مصدر migration tenant إضافةً إلى installer template.
- أضيفت migration tenant `20261115000300_AddAttendanceSnapshotOptionalColumns` لأعمدة `HR_ATTENDANCE_CORRECTION` و`HR_ATTENDANCE_DAY` الاختيارية: بصمة يوم التصحيح، المنطقة الزمنية ومصدر الشفت، وطوابع بداية/نهاية الشفت ودقائقه. تسبق فحوص النوع والطول والدقة وnullability وdefault/virtual جميع عمليات الإضافة؛ و`Down` لا يحذف سجلات الحضور أو التصحيح. لم تُطبق migration.
- أضيفت migration `20261115000400_AddShiftAssignmentKind` لعمود `ASSIGNMENT_KIND` وقيد `CK_HR_SHIFT_ASSIGN_KIND`: تفحص العمود ووجود `EFFECTIVE_TO` وتعريف القيد والصفوف المخالفة قبل أول DDL، ثم تضيف العمود nullable أو تفعّل القيد المطابق ENABLED/VALIDATED. لا تعيد تصنيف التعيينات التاريخية؛ تبقى NULL وتُفسر بالقواعد التاريخية. لم تُطبق migration.
- أضيفت migration `20261115000500_AddLeaveClosePolicySnapshotColumns` لأعمدة اللقطة الأربعة nullable في `HR_LEAVE_YEAR_CLOSE_LINE`، بما فيها دقة `NUMBER(1,0)`, `NUMBER(6,2)` ونص `NVARCHAR2(20)`. تتحقق من التعريفات القائمة كلها قبل أول DDL، ولا تكتب snapshots تاريخية، و`Down` لا يحذفها. لم تُطبق migration.
- أضيفت migration `20261115000600_AddPayrollCorrectionTenantColumns` لأعمدة تصحيح/إلغاء الرواتب الاختيارية، إصدار التصحيح الافتراضي 1، الرقم القومي للمستفيد، وعلاقتي `CORRECTION_OF_RUN_ID` و`CORRECTION_ROOT_RUN_ID`. تفحص metadata والـID الأب والـorphans وبصمة FK قبل DDL، وتضيف مفقود nullable بلا backfill؛ `CORRECTION_VERSION` وحده يُضاف بـdefault الموثق 1، وDown يحافظ على lineage وبيانات التدقيق. لم تُطبق migration.

أضيفت migration `20261115000200_AddLeaveSettingsOptionalColumns` لتوسيع ترقية tenant إلى إعداد accrual والمنطقة الزمنية وحقول cashout الاختيارية في `HR_LEAVE_SETTINGS`. تتحقق قبل DDL من التعريفات الموجودة، بما فيها دقة `NUMBER(10,4)` لمقسّم cashout، وتضيف المفقود nullable بلا default أو backfill؛ و`Down` يحافظ على إعدادات العميل. لا تُطبق migration هنا.

في `20261114210000_AlignHrStringLengths` أضيف فحص `VIRTUAL_COLUMN='NO'` ضمن جولة preflight لجميع الأعمدة الـ77؛ لا تبدأ أي عملية تضييق إذا كان العمود افتراضياً غير قابل للتعديل بهذه migration.

وطُبق فحص عدم الافتراضية أيضاً على الأعمدة الثمانية في `20261114200000_AlignEmployeeDependentTextLengths`، إلى جانب فحوص النوع وnullability وأطوال البيانات.

بناء Infrastructure ناجح مع 13 تحذيراً قائماً خارج التغيير و0 أخطاء؛ لا اختبارات أو تطبيق migration أو اتصال/DDL على Oracle.

بناء Infrastructure ناجح مع 13 تحذيراً قائماً خارج التغيير و0 أخطاء؛ لا اختبارات أو تطبيق migration أو اتصال/DDL على Oracle.

وقت الجرد، ستة من السبعة كانت تقع جداولها ضمن حلقة `HrRepair` العامة، والسابع كان له bootstrap فقط. أضيفت منذ ذلك الحين migrations صريحة لجميعها. ولـ`HR_LEAVE_REQUEST.ATTACHMENT_FILE_REF` أضيفت `20261114233000_AddLeaveRequestAttachmentFileRef`: تضيف العمود nullable `NVARCHAR2(500)` إذا كان الجدول موجوداً، وتفشل عند تعريف غير متوافق. إذا لم يكن الجدول موجوداً وقت تطبيق migration، تترك إنشاءه لـ`OracleSchemaService.BootstrapTablesFromEfModelAsync` الذي ينشئه من EF model ويتضمن العمود. حاجز `EnsureDeveloperTemplateHasCurrentHrStringColumnsAsync` يظل يكتشف template القائم إذا نقصه العمود ويرفض provisioning/sync قبل متابعة النسخ. لا backfill، وDown لا يحذف مرجعاً محفوظاً. لم تُطبق migration؛ يبقى قبول المخطط المعزول وخطة الرجوع مطلوبين.

أدوات التثبيت الحالية في schema tool تستهدف `DEV_TEMPLATE`. قد تكون الإحالة إلى installer أو verifier أو استعلام تدقيق أو fixture أو تعليق؛ لذلك لا تثبت وحدها تطبيق DDL على مخططات tenant. مطابقة table+column كشفت أن بعض الإحالات في النسخة الأولى كانت verifier/audit أو عموداً بالاسم نفسه في جدول آخر: `HrEosShortLeaveSchema` لا يرقّي حقلي إلغاء `HR_PAYROLL_RUN`، و`HrLegacyPayrollMigrationAudit` لا يثبت ترقية `HR_PAYROLL_RUN_LINE.BENEFICIARY_NATIONAL_ID`. جرى تصحيح المصفوفة لهذه الحالات إلى `HrRepair` لمسار `DEV_TEMPLATE`، الذي يضم تلك الجداول في حلقة repair عامة تستمد تعريف العمود من EF model؛ أما `HR_LEAVE_POLICY.POLICY_NAME` فله الآن migration للجدول الفارغ، ويوقف عند جدول مأهول حتى اعتماد backfill. migration tenant `20261115000600_AddPayrollCorrectionTenantColumns` أصبحت الآن مصدر الترقية الصريح لحقول الإلغاء والتصحيح وهوية المستفيد ولروابط تصحيح المسير. فحص migration وحده لا يرصد مسار repair للقالب. كما لم يظهر في مصدر التطبيق الحالي استدعاء `Database.Migrate`/`MigrateAsync` أو مشغل عام يطبق هذه migrations وقت الإقلاع. لا يدّعي هذا الجرد أن العمود موجود أو مفقود من قاعدة حية.

وجدت مراجعة provisioning مساراً صريحاً بديلاً عن تطبيق migrations تلقائياً: `OracleSchemaService` ينشئ الجداول الجديدة من EF model، و`CreateCompanySchemaAsync` يستنسخ `DEV_TEMPLATE` عند إنشاء مستأجر. للمخطط القائم يوجد endpoint إداري `POST /api/companies/{id}/sync-schema-from-dev-template` يستدعي `SyncTenantSchemaAsync`: يجري preflight، يجهز template، يستنسخ الجداول، ثم يضيف الأعمدة المفقودة ويوسع الأطوال. الإجراء محمي بإعداد `TenantProvisioning:AllowExistingTenantSchemaSync` وقيمته الحالية `false`. `BootstrapTablesFromEfModelAsync` يتجاهل الجدول الموجود ولا يضيف أعمدته الناقصة؛ لذلك يجب أن يكون template نفسه محدثاً قبل المزامنة. يلتقط حاجز `EnsureDeveloperTemplateHasCurrentHrStringColumnsAsync` نواقص أعمدة النص المحددة ذات `HasMaxLength` في source template، بما فيها `HR_LEAVE_REQUEST.ATTACHMENT_FILE_REF` بعد bootstrap؛ ويتحقق من نوع العمود وطوله ودلالة `CHAR_USED` وnullability ويرفض virtual columns دون قراءة القيم. لا يصلح الجدول الموجود تلقائياً. لا يثبت هذا الجرد أن الـtemplate أو العملاء محدثون. لم يُشغّل أي مسار provisioning أو مزامنة هنا.

## تحديث بعد التدقيق table+column في 14 نوفمبر 2026

أضيفت migration tenant `20261114220000_AddLoanAdvanceCurrencyCodes` وخصائص nullable إلى `OracleDbContextModelSnapshot` لعمودي `HR_EMPLOYEE_LOAN.CURRENCY_CODE` و`HR_EMPLOYEE_ADVANCE.CURRENCY_CODE`. تفحص migration وجود الجدولين وتتحقق من أن العمود الموجود `NVARCHAR2(20)` nullable بلا default أو virtual؛ وتضيف العمود إن كان غائباً. لا backfill، و`Down` لا يحذف بيانات. لم تُطبق migration على أي قاعدة. هذا تعديل لمصدر الترقية وليس تغييراً في نتيجة فحص قاعدة حية أو دليل مزامنة tenants.

كما أضيفت `20261114230000_AddHrPolicyAndPayrollSnapshotColumns` لأعمدة `HR_LEAVE_POLICY.ACCRUAL_METHOD/APPLICABLE_TO/CYCLE_BASIS` و`HR_LEAVE_TYPE.YEAR_END_EXCESS_ACTION` و`HR_PRORATION_POLICY.SALARY_SEGMENT_ALLOCATION` و`HR_PAYROLL_RUN.INPUT_FINGERPRINT`. استعملت migration القيم الافتراضية الموجودة في إعدادات الكيانات/إصلاح القالب فقط عند إضافة عمود مطلوب غائب؛ لا تعيد كتابة أعمدة أو سجلات موجودة. `INPUT_FINGERPRINT` nullable بلا default. تفحص وجود الجداول وتوافق كل تعريف موجود قبل أول DDL، وDown لا يسقط أعمدة. لا توجد اختبارات أو تطبيق migration أو تحقق من مخطط tenant حي.

واكتملت تغطية مجموعة الأعمدة السبعة بترحيل `20261114233000_AddLeaveRequestAttachmentFileRef`، مع fallback للجدول الغائب عبر bootstrap الحالي. حُصرت إحالات الأدوات للـ43 المتبقية ساكناً حسب الجدول/العمود ووجود DDL في الملف، لكن لم تُراجع دلالات كل installer بنداً بنداً؛ لا يزال يلزم تمييز `ALTER ADD` للجدول القائم من `CREATE TABLE` أو التحقق فقط، ومقارنة metadata على مخطط معزول.

العدّ الساكن الحالي للصفوف النصية الـ50 هو 18 migrations صريحة تغطي الصفوف الخمسين كلها. الأعمدة العشرة المضافة في `20261115001400_AddRequiredEosAndShortLeaveTextColumns` تُرقّى فقط عندما تكون الجداول المستهدفة فارغة؛ المستأجر المأهول الذي يفتقد أحدها يحتاج backfill معتمداً. يثبت ذلك تغطية مصدر migration ولا يغلق OPS-16: ما زال إثبات template/sync على مخطط معزول والقبول التشغيلي ومراجعة backfill على البيانات القائمة مطلوباً. أضيفت أيضاً migration لصيغة عرض الرصيد وهي خارج جرد النصوص. كما أضيفت ترقية `HR_ATTENDANCE_POLICY.ALLOW_MULTIPLE_SESSIONS` العددية خارج الجرد، مع default المجال `0` الموافق لـfalse.

## الأعمدة الـ50 التي لم يظهر اسمها في migrations وقت الجرد

عمودا CLR وRequired يصفان التلميحات الحالية في الكيان والإعداد فقط، ولا يحلان محل metadata من Oracle. نوع هذه الخرائط حسب convention هو NVARCHAR2 بالطول الموضح ما لم يوجد override لاحق. أسماء ملفات الأداة مراجع للمراجعة وليست دليلاً على وجود عملية تثبيت.

| الكيان | الجدول.العمود | أقصى محارف | تعريف CLR | إعداد required | ملف EF | مراجع HrSchemaTool |
|---|---|---:|---|---|---|---|
| `AttendanceCorrectionRequest` | `HR_ATTENDANCE_CORRECTION.ORIGINAL_DAY_VERSION` | 64 | `string?` | غير مصرح صراحة | `AttendanceConfigurations.cs` | Migration `20261115000300_AddAttendanceSnapshotOptionalColumns`; HrAttendanceCorrectionSchema.cs |
| `AttendanceDay` | `HR_ATTENDANCE_DAY.SHIFT_SOURCE` | 30 | `string?` | غير مصرح صراحة | `AttendanceConfigurations.cs` | Migration `20261115000300_AddAttendanceSnapshotOptionalColumns`; HrAttendanceTimeSchema.cs |
| `AttendanceDay` | `HR_ATTENDANCE_DAY.TIME_ZONE_ID` | 100 | `string?` | غير مصرح صراحة | `AttendanceConfigurations.cs` | Migration `20261115000300_AddAttendanceSnapshotOptionalColumns`; HrAttendanceTimeSchema.cs, HrShiftTimingVerification.cs |
| `Employee` | `HR_EMPLOYEE.CITIZENSHIP_COUNTRY_CODE` | 2 | `string?` | `VARCHAR2(2 CHAR)` | `EmployeeConfigurations.cs` | Migration `20261115000100_AddCitizenshipCountryCodeColumns`; HrLeaveCitizenshipSchema.cs |
| `EmployeeShiftAssignment` | `HR_EMPLOYEE_SHIFT_ASSIGNMENT.ASSIGNMENT_KIND` | 20 | `string?` | غير مصرح صراحة | `AttendanceConfigurations.cs` | Migration `20261115000400_AddShiftAssignmentKind`; HrShiftSchema.cs |
| `EndOfServicePolicy` | `HR_END_OF_SERVICE_POLICY.CALCULATION_BASIS` | 50 | `string` | مطلوب صراحة | `EndOfServiceConfigurations.cs` | Migration `20261115001400_AddRequiredEosAndShortLeaveTextColumns` (empty table only); HrEosShortLeaveSchema.cs |
| `EndOfServicePolicy` | `HR_END_OF_SERVICE_POLICY.POLICY_NAME` | 200 | `string` | مطلوب صراحة | `EndOfServiceConfigurations.cs` | Migration `20261115001400_AddRequiredEosAndShortLeaveTextColumns` (empty table only); HrEosShortLeaveSchema.cs |
| `EndOfServiceSettlement` | `HR_END_OF_SERVICE_SETTLEMENT.CALCULATION_NOTES` | 2000 | `string?` | غير مصرح صراحة | `EndOfServiceConfigurations.cs` | Migration `20261114235800_AddEndOfServiceOptionalColumns`; HrEosShortLeaveSchema.cs |
| `EndOfServiceSettlement` | `HR_END_OF_SERVICE_SETTLEMENT.CANCELLATION_REASON` | 1000 | `string?` | غير مصرح صراحة | `EndOfServiceConfigurations.cs` | Migration `20261114235800_AddEndOfServiceOptionalColumns`; HrEosShortLeaveSchema.cs |
| `EndOfServiceSettlement` | `HR_END_OF_SERVICE_SETTLEMENT.CANCELLED_BY` | 200 | `string?` | غير مصرح صراحة | `EndOfServiceConfigurations.cs` | Migration `20261114235800_AddEndOfServiceOptionalColumns`; HrEosShortLeaveSchema.cs |
| `EndOfServiceSettlement` | `HR_END_OF_SERVICE_SETTLEMENT.TERMINATION_TYPE` | 50 | `string` | مطلوب صراحة | `EndOfServiceConfigurations.cs` | Migration `20261115001400_AddRequiredEosAndShortLeaveTextColumns` (empty table only); HrEosShortLeaveSchema.cs |
| `LeaveBalanceMigration` | `HR_LEAVE_BALANCE_MIGRATION.MIGRATION_KEY` | 200 | `string` | مطلوب صراحة؛ ترقية الجدول الفارغ فقط | `LeaveConfigurations.cs` | Migration `20261115001200_AddLeaveBalanceMigrationKey`; HrLeaveBalanceMigrationSchema.cs |
| `LeaveCashoutSettlement` | `HR_LEAVE_CASHOUT.BASIS_COMPONENT_CODE` | 100 | `string?` | غير مصرح صراحة | `LeaveConfigurations.cs` | Migration `20261115001100_AddLeaveCashoutBasisComponentCode`; HrLeaveCashoutSchema.cs |
| `LeaveCashoutSettlement` | `HR_LEAVE_CASHOUT.PAY_COMPONENT_CODE` | 100 | `string` | مطلوب صراحة | `LeaveConfigurations.cs` | Migration `20261115001400_AddRequiredEosAndShortLeaveTextColumns` (empty table only); HrLeaveCashoutSchema.cs |
| `LeaveCashoutSettlement` | `HR_LEAVE_CASHOUT.SETTLEMENT_KEY` | 200 | `string` | مطلوب صراحة | `LeaveConfigurations.cs` | Migration `20261115001400_AddRequiredEosAndShortLeaveTextColumns` (empty table only); HrLeaveCashoutSchema.cs |
| `LeaveCashoutSettlement` | `HR_LEAVE_CASHOUT.WAGE_BASIS_CODE` | 30 | `string` | مطلوب صراحة | `LeaveConfigurations.cs` | Migration `20261115001400_AddRequiredEosAndShortLeaveTextColumns` (empty table only); HrLeaveCashoutSchema.cs |
| `LeavePolicy` | `HR_LEAVE_POLICY.ACCRUAL_METHOD` | 50 | `string` | مطلوب صراحة | `LeaveConfigurations.cs` | Migration `20261114230000_AddHrPolicyAndPayrollSnapshotColumns` |
| `LeavePolicy` | `HR_LEAVE_POLICY.APPLICABLE_TO` | 50 | `string` | مطلوب صراحة | `LeaveConfigurations.cs` | Migration `20261114230000_AddHrPolicyAndPayrollSnapshotColumns` |
| `LeavePolicy` | `HR_LEAVE_POLICY.CYCLE_BASIS` | 40 | `string` | مطلوب صراحة | `LeaveConfigurations.cs` | Migration `20261114230000_AddHrPolicyAndPayrollSnapshotColumns` |
| `LeavePolicy` | `HR_LEAVE_POLICY.POLICY_NAME` | 200 | `string` | مطلوب صراحة؛ ترقية الجدول الفارغ فقط | `LeaveConfigurations.cs` | Migration `20261115000800_AddLeavePolicyName`; HrRepair.cs |
| `LeaveRequest` | `HR_LEAVE_REQUEST.ATTACHMENT_FILE_REF` | 500 | `string?` | غير مصرح صراحة | `LeaveConfigurations.cs` | Migration `20261114233000_AddLeaveRequestAttachmentFileRef`; EF bootstrap if table absent |
| `HrLeaveSettings` | `HR_LEAVE_SETTINGS.ACCRUAL_TIME_ZONE_ID` | 120 | `string?` | غير مصرح صراحة | `LeaveConfigurations.cs` | Migration `20261115000200_AddLeaveSettingsOptionalColumns`; HrLeaveAccrualSettingsSchema.cs |
| `HrLeaveSettings` | `HR_LEAVE_SETTINGS.BALANCE_DISPLAY_FORMAT` | 20 | `string` | مطلوب صراحة؛ default DECIMAL | `LeaveConfigurations.cs` | Migration `20261115000700_AddLeaveBalanceDisplayFormat`; HrRepair.cs |
| `HrLeaveSettings` | `HR_LEAVE_SETTINGS.ELIGIBLE_CITIZENSHIP_COUNTRY_CODE` | 2 | `string?` | `VARCHAR2(2 CHAR)` | `LeaveConfigurations.cs` | Migration `20261115000100_AddCitizenshipCountryCodeColumns`; HrLeaveCitizenshipSchema.cs |
| `HrLeaveSettings` | `HR_LEAVE_SETTINGS.LEAVE_CASHOUT_BASIS_COMPONENT_CODE` | 100 | `string?` | غير مصرح صراحة | `LeaveConfigurations.cs` | Migration `20261115000200_AddLeaveSettingsOptionalColumns`; HrLeaveCashoutSettingsSchema.cs |
| `HrLeaveSettings` | `HR_LEAVE_SETTINGS.LEAVE_CASHOUT_PAY_COMPONENT_CODE` | 100 | `string?` | غير مصرح صراحة | `LeaveConfigurations.cs` | Migration `20261115000200_AddLeaveSettingsOptionalColumns`; HrLeaveCashoutSettingsSchema.cs |
| `HrLeaveSettings` | `HR_LEAVE_SETTINGS.LEAVE_CASHOUT_WAGE_BASIS_CODE` | 30 | `string?` | غير مصرح صراحة | `LeaveConfigurations.cs` | Migration `20261115000200_AddLeaveSettingsOptionalColumns`; HrLeaveCashoutSettingsSchema.cs |
| `LeaveType` | `HR_LEAVE_TYPE.YEAR_END_EXCESS_ACTION` | 20 | `string` | مطلوب صراحة | `LeaveConfigurations.cs` | Migration `20261114230000_AddHrPolicyAndPayrollSnapshotColumns` |
| `LeaveYearClose` | `HR_LEAVE_YEAR_CLOSE.CLOSE_KEY` | 300 | `string` | مطلوب صراحة؛ ترقية الجدول الفارغ فقط | `LeaveConfigurations.cs` | Migration `20261115001300_AddLeaveYearCloseKey`; HrRepair.cs |
| `LeaveYearClose` | `HR_LEAVE_YEAR_CLOSE.UNPOSTED_BY` | 200 | `string?` | غير مصرح صراحة | `LeaveConfigurations.cs` | Migration `20261115001000_AddLeaveYearCloseUnpostedBy`; HrRepair.cs |
| `LeaveYearCloseLine` | `HR_LEAVE_YEAR_CLOSE_LINE.YEAR_END_EXCESS_ACTION_SNAPSHOT` | 20 | `string?` | غير مصرح صراحة | `LeaveConfigurations.cs` | Migration `20261115000500_AddLeaveClosePolicySnapshotColumns`; HrLeaveClosePolicySnapshotSchema.cs |
| `PayrollRun` | `HR_PAYROLL_RUN.CANCELLATION_REASON` | 1000 | `string?` | غير مصرح صراحة | `PayrollConfigurations.cs` | Migration `20261115000600_AddPayrollCorrectionTenantColumns`; HrRepair.cs |
| `PayrollRun` | `HR_PAYROLL_RUN.CANCELLED_BY` | 200 | `string?` | غير مصرح صراحة | `PayrollConfigurations.cs` | Migration `20261115000600_AddPayrollCorrectionTenantColumns`; HrRepair.cs |
| `PayrollRun` | `HR_PAYROLL_RUN.CORRECTION_REASON` | 1000 | `string?` | غير مصرح صراحة | `PayrollConfigurations.cs` | Migration `20261115000600_AddPayrollCorrectionTenantColumns`; HrAccountingHandoffSchema.cs |
| `PayrollRun` | `HR_PAYROLL_RUN.INPUT_FINGERPRINT` | 64 | `string?` | غير مصرح صراحة | `PayrollConfigurations.cs` | Migration `20261114230000_AddHrPolicyAndPayrollSnapshotColumns` |
| `PayrollRunLine` | `HR_PAYROLL_RUN_LINE.BENEFICIARY_NATIONAL_ID` | 100 | `string?` | غير مصرح صراحة | `PayrollConfigurations.cs` | Migration `20261115000600_AddPayrollCorrectionTenantColumns`; HrRepair.cs |
| `ProrationPolicy` | `HR_PRORATION_POLICY.SALARY_SEGMENT_ALLOCATION` | 30 | `string` | مطلوب صراحة | `PolicyConfigurations.cs` | Migration `20261114230000_AddHrPolicyAndPayrollSnapshotColumns` |
| `RawAttendance` | `HR_RAW_ATTENDANCE.EVENT_ID` | 100 | `string?` | غير مصرح صراحة | `AttendanceConfigurations.cs` | Migration `20261115000900_AddRawAttendanceProcessingColumns`; HrAttendanceProcessingSchema.cs, HrMobilePunchSchema.cs, HrScheduledAttendanceVerification.cs |
| `RawAttendance` | `HR_RAW_ATTENDANCE.EVENT_PAYLOAD_HASH` | 64 | `string?` | غير مصرح صراحة | `AttendanceConfigurations.cs` | Migration `20261115000900_AddRawAttendanceProcessingColumns`; HrMobilePunchSchema.cs |
| `RawAttendance` | `HR_RAW_ATTENDANCE.LAST_ATTEMPTED_BY` | 200 | `string?` | غير مصرح صراحة | `AttendanceConfigurations.cs` | Migration `20261115000900_AddRawAttendanceProcessingColumns`; HrAttendanceProcessingSchema.cs |
| `RawAttendance` | `HR_RAW_ATTENDANCE.PROCESSED_BY` | 200 | `string?` | غير مصرح صراحة | `AttendanceConfigurations.cs` | Migration `20261115000900_AddRawAttendanceProcessingColumns`; HrAttendanceProcessingSchema.cs, HrPayrollInputLockVerification.cs |
| `RawAttendance` | `HR_RAW_ATTENDANCE.PROCESSING_OUTCOME` | 20 | `string?` | غير مصرح صراحة | `AttendanceConfigurations.cs` | Migration `20261115000900_AddRawAttendanceProcessingColumns`; HrAttendanceProcessingSchema.cs, HrPayrollInputLockVerification.cs |
| `RawAttendance` | `HR_RAW_ATTENDANCE.PROCESSING_REASON` | 1000 | `string?` | غير مصرح صراحة | `AttendanceConfigurations.cs` | Migration `20261115000900_AddRawAttendanceProcessingColumns`; HrAttendanceProcessingSchema.cs, HrPayrollInputLockVerification.cs |
| `ShortLeavePolicy` | `HR_SHORT_LEAVE_POLICY.EXCESS_ACTION` | 50 | `string` | مطلوب صراحة | `ShortLeaveConfigurations.cs` | Migration `20261115001400_AddRequiredEosAndShortLeaveTextColumns` (empty table only); HrEosShortLeaveSchema.cs |
| `ShortLeavePolicy` | `HR_SHORT_LEAVE_POLICY.LEAVE_TYPE_CODE_FOR_DEDUCTION` | 50 | `string` | مطلوب صراحة | `ShortLeaveConfigurations.cs` | Migration `20261115001400_AddRequiredEosAndShortLeaveTextColumns` (empty table only); HrEosShortLeaveSchema.cs |
| `ShortLeaveRequest` | `HR_SHORT_LEAVE_REQUEST.CANCELLATION_REASON` | 500 | `string?` | غير مصرح صراحة | `ShortLeaveConfigurations.cs` | Migration `20261114235700_AddShortLeaveOptionalColumns`; HrEosShortLeaveSchema.cs |
| `ShortLeaveRequest` | `HR_SHORT_LEAVE_REQUEST.CANCELLED_BY` | 100 | `string?` | غير مصرح صراحة | `ShortLeaveConfigurations.cs` | Migration `20261114235700_AddShortLeaveOptionalColumns`; HrEosShortLeaveSchema.cs |
| `ShortLeaveRequest` | `HR_SHORT_LEAVE_REQUEST.COMPENSATION_TYPE` | 50 | `string` | مطلوب صراحة | `ShortLeaveConfigurations.cs` | Migration `20261115001400_AddRequiredEosAndShortLeaveTextColumns` (empty table only); HrEosShortLeaveSchema.cs |
| `ShortLeaveRequest` | `HR_SHORT_LEAVE_REQUEST.PERMISSION_TYPE` | 50 | `string` | مطلوب صراحة | `ShortLeaveConfigurations.cs` | Migration `20261115001400_AddRequiredEosAndShortLeaveTextColumns` (empty table only); HrEosShortLeaveSchema.cs |
| `ShortLeaveRequest` | `HR_SHORT_LEAVE_REQUEST.REJECTED_BY` | 100 | `string?` | غير مصرح صراحة | `ShortLeaveConfigurations.cs` | Migration `20261114235700_AddShortLeaveOptionalColumns`; HrEosShortLeaveSchema.cs |

## تحديث مسار إصلاح أعمدة النص في HrSchemaTool

أضيف `HrStringRepair` إلى `HrRepair.Apply` لاستنتاج تعريف أعمدة HR النصية ذات `HasMaxLength` من EF model عند تشغيل repair على `DEV_TEMPLATE`. كما يشغّل `Program.cs` preflight نفسه قبل DDL في أوامر التثبيت المباشر. تُبنى خطة كاملة: الأعمدة الموجودة تتحقق من نوع Oracle والطول الأدنى وnullability وكونها ليست virtual، كما تُطابق دلالة `BYTE/CHAR` عند التصريح بها. العمود nullable المفقود يضاف في مسار repair nullable؛ والعمود المطلوب المفقود لا يضاف إلى جدول مأهول إلا مع default EF غير فارغ وصالح للطول، وإلا يوقف repair برسالة مراجعة backfill. الجداول الغائبة تُترك لمسار bootstrap ولا تُنشأ في هذا الإصلاح. لا تُقرأ قيم الأعمدة الموجودة.

هذا يغطي preflight العام لأعمدة النص bounded الممثلة في model، لكنه لا يغطي الأنواع غير النصية، ولا يصلح mismatches أو يثبت نتيجة على مخطط فعلي. أداة `HrSchemaTool` بُنيت بنجاح (0 تحذيرات/0 أخطاء)؛ لم تُشغّل اختبارات أو أداة على Oracle، ولم ينفذ DDL. يظل قبول `HrStringRepair` وmigrations وpreflight على مخطط معزول قابل للاسترجاع، ثم توثيق مقارنة metadata وخطة rollback، مطلوباً قبل أي sync؛ مزامنة tenant ما تزال معطلة افتراضياً.

## تصنيف سلوك الإحالات المباشرة

مراجعة مصدر الملفات التي تحمل إحالات DDL للصفوف الخمسين فرّقت بين ترقية الجدول الموجود وبين إنشاء جدول عند غيابه:

- إضافة مباشرة لعمود nullable على جدول قائم: `HR_ATTENDANCE_CORRECTION.ORIGINAL_DAY_VERSION`؛ `HR_ATTENDANCE_DAY.SHIFT_SOURCE/TIME_ZONE_ID`؛ `HR_EMPLOYEE_SHIFT_ASSIGNMENT.ASSIGNMENT_KIND`؛ عمودا الجنسية في `HR_EMPLOYEE` و`HR_LEAVE_SETTINGS`؛ `HR_LEAVE_SETTINGS.ACCRUAL_TIME_ZONE_ID` وحقول إعداد cashout الثلاثة؛ `HR_LEAVE_YEAR_CLOSE_LINE.YEAR_END_EXCESS_ACTION_SNAPSHOT`؛ حقول المعالجة في `HR_RAW_ATTENDANCE`؛ و`HR_PAYROLL_RUN.CORRECTION_REASON`. فروع installer نفسها غالباً تفحص الوجود ثم تضيف التعريف المكتوب في المصدر. يسبق جميع أوامر التثبيت الآن `HrStringRepair.PreflightAsync` بعد تثبيت `CURRENT_SCHEMA=DEV_TEMPLATE`؛ لذلك يوقف تعريف string موجوداً ومخالفاً للـEF (النوع/الحد الأدنى للطول/nullability/virtual وBYTE/CHAR عند التصريح) قبل أول DDL. أما إثبات metadata الناتجة بعد إضافة العمود ومطابقة جميع فروع الإنشاء فيبقى قبولاً على مخطط معزول.
- إضافة الأعمدة الإلزامية ذات القيم التشغيلية الخاصة: `20261115001400_AddRequiredEosAndShortLeaveTextColumns` يضيف أعمدة EOS وcashout وshort leave العشرة المطلوبة للجداول الفارغة فقط؛ الجداول المأهولة بلا الأعمدة تحتاج backfill معتمداً. كما تضيف migrations منفصلة `MIGRATION_KEY` الفريد لـ`HR_LEAVE_BALANCE_MIGRATION` و`CLOSE_KEY` الفريد لـ`HR_LEAVE_YEAR_CLOSE` إذا كان كل جدول فارغاً؛ لا تُخترع قيم تاريخية أو مفاتيح عند وجود صفوف.
- `HR_LEAVE_CASHOUT` له مسار مختلط: ينشئ الجدولين الغائبين، ويضيف حقول posting/reversal ومرجع payroll الناقصة إلى الجدول الموجود؛ ولـ`BASIS_COMPONENT_CODE` migration tenant تضيف العمود وتتحقق من مفتاح المكوّن وغياب orphans وتطابق FK. ثم يرفض verifier شكل الأعمدة غير المتوقع.
- `HR_LEAVE_SETTINGS.BALANCE_DISPLAY_FORMAT` صار له tenant migration صريح مع default `DECIMAL` المثبت في المجال. ويضاف `HR_LEAVE_POLICY.POLICY_NAME` إلى الجدول الفارغ فقط؛ يحتاج tenant مأهول قرار backfill آمن. أما حقول الإلغاء في `HR_PAYROLL_RUN` و`HR_PAYROLL_RUN_LINE.BENEFICIARY_NATIONAL_ID` فصار لها tenant migration صريح من `20261115000600_AddPayrollCorrectionTenantColumns`، مع بقاء `HrRepair` لمسار القالب. وفحص التغطية النصية يخطط أعمدة النص bounded الممثلة في model عند تشغيل repair.

هذا تصنيف مصدر ساكن للصفوف ذات الصلة فقط. لا يعني أن الملفات شُغلت، ولا يثبت schema sync إلى tenant. `HrStringRepair` يفحص metadata الحالية قبل installer، ويضيف الأعمدة bounded الآمنة في مسار `--repair` على القالب؛ لكنه لا يحول أعمدة `VARCHAR2`، ولا يثبت metadata بعد installer على Oracle، ولا يطبق EF migrations تلقائياً على tenant. تبقى مراجعة/قبول migrations والتحويل والـrepair على مخطط معزول قابل للاسترجاع، ثم توثيق مقارنة metadata وخطة rollback قبل أي tenant sync.

كشفت المطابقة نفسها تعارض نوع في عدة مسارات إنشاء: `HrLeaveBalanceMigrationSchema` كان ينشئ حقول النص بــ`VARCHAR2` بينما EF model يطلب `NVARCHAR2`؛ وكذلك أعمدة الجنسية وإعدادات cashout ولقطة `YEAR_END_EXCESS_ACTION_SNAPSHOT`. وُحّد DDL للإنشاء الجديد مع EF على `NVARCHAR2`، وشُددت verifier definitions لحقول الجنسية/cashout. وأضيفت migration tenant `20261114234500_AlignHrUnicodeStringColumns` لتحويل 14 عموداً legacy محدداً بأعمدة ظل قابلة للاستئناف. تفحص البيانات والطول وقابلية التحويل والقيود قبل أول DDL، وتحفظ uniqueness لمفتاح `MIGRATION_KEY` بما في ذلك مؤشر التحويل أثناء الاستئناف قبل/بعد إعادة تسمية عمود الظل، وترفض القيد غير الفريد/المعطل والمفاتيح ذات foreign-key dependents والحالات غير المدعومة؛ `Down` لا يعيد `VARCHAR2` حتى لا يفقد Unicode المكتوب بعد التحويل. لم تُطبق migration ولم يُجر اتصال Oracle أو DDL؛ يلزم قبولها على مخطط معزول قابل للاستعادة، والتحقق من سلوك التحويل/القيود وخطة rollback قبل فتح sync.

استكمل تدقيق الاستئناف: إذا كان المصدر والظل موجودين معاً، يثبت preflight الآن أن shadow column تحمل تعليق الملكية `HR-OPS16-NVARCHAR2` قبل اعتماد نوعها أو محتواها. وجود عمود باسم الظل دون العلامة يوقف التحويل لمراجعة يدوية، كما كان يفعل بالفعل في حالة الظل بعد إسقاط المصدر.

إذا كان الظل المملوك موجوداً مع المصدر، لا يشترط preflight أن تكون بيانات الظل قد نُسخت سابقاً؛ المصدر هو المرجع حتى لحظة إعادة التسمية، وكتلة التحويل تعيد نسخه كاملاً وتتحقق من الطول والتطابق قبل إسقاطه. هذا يجعل retry بعد انقطاع واقع بين إضافة shadow ونسخ البيانات قابلاً للاستئناف دون الوثوق بمحتوى shadow القديم.

بناء Infrastructure بعد التعديل: ناجح، 13 تحذيراً قائماً خارج التغيير و0 أخطاء. لم تُشغّل اختبارات أو تطبيق migration أو اتصال/DDL على Oracle.

## المتابعة المطلوبة

حدد التدقيق الساكن لكل صف مساراً مصدرياً ونوع Oracle والطول وnullability؛ ولا يعني ذلك أن المسار نُفّذ أو قُبل على قاعدة. حُسم تصنيف `ALTER ADD` مقابل `CREATE`/verifier، وأضيف preflight عام قبل installer وrepair. المتابعة المتبقية فحص metadata الناتجة بعد installer، وقبول repair والتحويلات على مخطط معزول قابل للاستعادة ومقارنة metadata وخطة rollback؛ لا EF migration runner ولا tenant sync هنا، ولا تُطبق عملية محصورة في `DEV_TEMPLATE` على tenants دون مسار هدف معتمد.

### تحديث 14 نوفمبر 2026 — OPS-16 / ترتيب DDL في AccountingHandoff

أضيف preflight مبكر داخل `HrAccountingHandoffSchema` قبل أول إنشاء جدول، يغطي إضافات الأعمدة الثلاثين التي كانت تُنفذ بعد جداول الـhandoff: يفحص تعريف العمود الموجود مسبقاً (نوع Oracle والطول أو الدقة/المقياس وnullability حسب تعريف الإضافة)، ويتوقف قبل DDL إن كان التعريف غير متوافق. كما يتحقق من وجود الجداول التابعة القائمة (`HR_EMPLOYEE_LOAN`, `HR_EMPLOYEE_ADVANCE`, `HR_LOAN_REPAYMENT_SCHEDULE`, `HR_PAYROLL_RUN`, `GL_VOUCHER_HEADER`)؛ أما جداول handoff التي ينشئها installer فيسمح بغيابها. أضيف فحص مبكر لتعريفات 34 قيداً مسمى، وPK على ID والجداول ذات ID مكرر/NULL عند غياب PK، إضافة إلى الفهارس الأربعة؛ لا يسمح بتعريف مسمى متضارب بأن يفشل بعد إنشاء بعض الجداول. كما حُولت تعاريف الأعمدة من أوامر CREATE TABLE التسعة إلى expectations وتُقارن أنواعها وأطوالها/دقتها ومقاييسها وnullability وdefaults عبر HrSchemaColumnGuard قبل أول DDL؛ ويُسمح فقط للأعمدة المعلنة ضمن الإضافات أن تكون مفقودة مؤقتاً قبل إضافتها.

هذا التحسين يمنع mismatch في إضافات الأعمدة والقيود والفهارس المسماة من الظهور بعد إنشاء بعض جداول handoff. صار الـpreflight يفحص البيانات القائمة قبل DDL: 20 خطة unique تكشف مجموعات duplicate غير الفارغة، و14 خطة FK تكشف قيماً orphan في أعمدة الطفل الموجودة. إذا كان العمود سيضاف لاحقاً ويتعذر وجود بيانات سابقة به، يتخطى فحص العلاقة حتى إضافته nullable. هذه الاستعلامات لم تُشغّل على قاعدة؛ قبولها على مخطط معزول مطلوب. parser الأعمدة مستمد من DDL المصدر ولم يُقبل على Oracle بمخطط فعلي. بُني `HrSchemaTool` بنجاح: 0 تحذيرات/0 أخطاء. لم تُشغّل اختبارات أو verifier أو اتصال/DDL، ولم تُطبق schema.

## حدود طريقة الجرد

- استخدمت المطابقة الساكنة أسماء الخصائص والأعمدة في إعدادات HR الحالية وEF snapshot ومصادر migration من `.cs` (باستثناء ملفات designer المولدة) و` scripts/HrSchemaTool/*.cs`.
- قد يظهر الاسم المطابق خارج DDL؛ وغياب الاسم لا يثبت أن العمود غير موجود في قاعدة البيانات.
- لم يفحص هذا الجرد مخططات العملاء أو `DEV_TEMPLATE` أو `THINKONERP_DEV02`، ولم يشغّل اختبارات.
### مراجعة installer تاريخي — EOS وshort leave

تُراجع `HrEosShortLeaveSchema` قبل أي DDL لوجود الأعمدة المطلوبة الممثلة في EF، وتعريفاتها العددية والزمنية، والأعمدة nullable الاختيارية. أضيف فحص مبكر لأسماء الفهارس الستة وتعريفاتها حتى لا يظهر تعارض بعد بعض DDL، وفحص أسماء واتجاه/حالة/أعمدة قيود الموظف وخط التسوية. أضيف فحص البيانات اليتيمة لعلاقات الموظف وخط التسوية، مع استكمال إنشاء FK `FK_HR_EOS_LINE_SETTLE` الناقص على جدول قائم بعد خلوه من الأيتام.

يتطلب قيد الموظف وجود مفتاح فريد/أساسي مفعّل ومتحقق على `HR_EMPLOYEE.EMPLOYEE_CODE`. لم تُنفذ هذه الفحوص على Oracle، ولم تُجر اختبارات أو DDL؛ النتيجة build للمصدر فقط. يلزم قبول installer على مخطط معزول مع مقارنة metadata بعد التنفيذ.
### مراجعة repair التاريخي لأرصدة الإجازة — HrRepair

قبل أول إنشاء/تعديل schema، يفحص `HrRepair` وجود `HR_LEAVE_BALANCE` والأعمدة المطلوبة `EMPLOYEE_CODE`, `LEAVE_TYPE_CODE`, `YEAR_NO`، ويتحقق من metadata لحدود الدورة الموجودة (DATE أو TIMESTAMP(7)، nullable بلا default أو virtual). كما يرفض الحدود الجزئية/المعكوسة، سنوات التقويم غير الصالحة حيث يلزم الاشتقاق، ومفاتيح الموظف/نوع الإجازة/بداية الدورة التي ستتكرر بعد التأريخ. إذا غاب أحد عمودي الدورة وكان العمود المقابل يحتوي قيماً، يوقف الإصلاح للمراجعة اليدوية قبل DDL.

بقيت postconditions بعد التحديث في مسار repair، ولم تُنفذ عملية repair على قاعدة. بُني `HrSchemaTool` بنجاح (0 تحذيرات/0 أخطاء)؛ لا اختبارات أو verifier أو اتصال/DDL. يلزم قبول هذا preflight على مخطط معزول ومقارنة metadata قبل/بعد.
### فحص دقة أيام الرصيد قبل تعديلها

سبق `ALTER TABLE ... MODIFY` لأعمدة `ACCRUED_DAYS`, `USED_DAYS`, `CARRIED_FORWARD_DAYS` فحص مسبق لكل عمود موجود: يتطلب NUMBER وغير افتراضي وغير virtual وNOT NULL، ويتحقق أن كل قيمة قابلة للتمثيل بـ`NUMBER(8,4)` (أقل من 10000 وبحد أقصى أربعة منازل). إذا كانت مجموعة الأعمدة جزئية، يسمح باستكمال الأعمدة الغائبة من EF فقط عندما تكون الموجودة مطابقة للدقة المستهدفة؛ ويرفض المحاذاة الجزئية قبل أي DDL.

هذه الحماية تمنع فشل تضييق الدقة أو فقد كسور/مدى أو تغيير nullability بعد بدء الإصلاح. لم تُقرأ البيانات على Oracle ولم تُنفذ DDL؛ `HrSchemaTool` build ناجح 0 تحذيرات/0 أخطاء.
### مراجعة installer أحداث البصمة المحمولة

قبل أي DDL يتحقق `HrMobilePunchSchema` من اسم الفهرس `UX_HR_MOBILE_EVENT` ومالكه وجدوله، كونه فريداً وصالحاً ومن النوع function-based، ومن تعبيره الوظيفي وعدد أعمدته. كما يكشف تكرار أزواج الموظف/معرف الحدث الموجودة قبل إضافة الأعمدة أو توسيع معرف الحدث أو إعادة إنشاء الفهرس. لا يحذف الفهرس القائم إلا بعد إثبات البصمة المتوقعة وخلو معرفات الحدث المقبولة من البيانات وفق مسار التوسيع القائم.

هذا فحص مصدر/build فقط؛ لم تُشغّل الاستعلامات أو verifier أو اختبارات ولم ينفذ اتصال/DDL. يلزم قبول installer على مخطط معزول ومقارنة metadata.

### مراجعة توقيت DDL لحماية دفتر الإجازات

نُقل فحص اسم trigger `TR_HR_LEAVE_MOVEMENT_IMM` وارتباطه بالجدول/الأحداث المتوقعة في `HrLedgerSchema` إلى ما قبل فحص مسار إنشاء الجدول أو تنفيذ DDL. بذلك لا يُنشأ جدول الحركة أولاً ثم يُكتشف تصادم trigger. يبقى استبدال trigger بعد تحقق الملكية والسياق.

النتيجة build للمصدر فقط؛ لا اختبارات أو verifier أو اتصال Oracle/DDL، ويلزم القبول على مخطط معزول.

### preflight لقيود ترحيل دورات الإجازة

قبل إنشاء أي جدول، يتحقق `HrLeaveBalanceMigrationSchema` من القيود المسماة الـ14: تعارض الاسم داخل المالك، والجدول والنوع والحالة ENABLED/VALIDATED، وترتيب أعمدة المفاتيح، وشروط CHECK، وأعمدة FK المرجعية ومالكها وقاعدة الحذف. ويتأكد من وجود مفاتيح ID فعالة ومتحقق منها في جدولي الأرصدة وحركة الإجازة. إذا كان جدول ترحيل قائم يفتقد قيداً مطلوباً، يتوقف installer قبل DDL لأن مسار الإصلاح الحالي لا يعيد بناء القيود بأمان.

هذا فحص مصدر/build فقط؛ لم تُشغّل Oracle queries أو verifier أو اختبارات ولم ينفذ DDL. يلزم قبول المسار على مخطط معزول.

### مراجعة قيود جدول موقع الحضور

تحقق `HrBranchLocationSchema` قبل الإنشاء من مفتاح فريد/أساسي صالح على `DEV_TEMPLATE.SYS_BRANCH("Id")`. بعد الإنشاء أو عند وجود جدول إعداد الموقع، يتحقق من PK المفرد على `BRANCH_ID`، ومن FK `FK_HR_BRANCH_LOCATION` المفعّل والمتحقق إلى المفتاح الصحيح بلا cascading delete، ومن شرط `CK_HR_BRANCH_LOCATION` الذي يقيّد نصف القطر وخيار geofence وإحداثيات GPS. بذلك لا يُقبل جدول موجود بأعمدة صحيحة وقيود مفقودة.

النتيجة source/build فقط؛ لم تُنفذ استعلامات Oracle أو DDL ولم تُشغّل اختبارات.

### مطابقة قيود إعداد دقة العملة

صار `HrCurrencyPrecisionSchema` يرفض قبل إنشاء الجدول تصادم اسمي القيدين. بعد الإنشاء أو عند وجود الجدول يثبت أن PK هو المفتاح المرتب `(COMPANY_ID,CURRENCY_CODE)` وأن CHECK يطبق بالضبط مجال `DECIMAL_PLACES` من 0 إلى 4، وكلاهما ENABLED/VALIDATED. كان الفحص السابق يكتفي بوجود الاسم والنوع والحالة.

لم تُنفذ استعلامات Oracle أو DDL ولم تُشغّل اختبارات؛ تم التحقق بالبناء فقط.

### تدقيق فهرس إصدارات تصحيح المسير

لا يُقبل `UX_HR_PAYRUN_CORR_VER` الآن لمجرد كونه فريداً وصالحاً ووظيفياً مع تعبيرين. يقارن installer وschema verifier ترتيب التعبيرين ونصهما بعد تطبيع المسافات والاقتباس، ويرفض استخدام اسم الفهرس أو القيد من جدول/نوع آخر قبل الاستبدال. التوقيع المطلوب يستثني المسيرات الجذرية من مفتاح التفرد ويثبت نسخة التصحيح لكل جذر.

التحقق بالبناء فقط؛ لم يُشغّل verifier أو اختبار أو استعلام Oracle/DDL.

### استكمال بصمة فهرس أحداث البصمة

يثبت `HrMobilePunchSchema` ترتيب مكونات `UX_HR_MOBILE_EVENT`: تعبير `CASE WHEN EVENT_ID IS NOT NULL THEN EMPLOYEE_CODE END` أولاً، وعمود `EVENT_ID` مباشرةً ثانياً. يرفض أي فهرس موجود باسم محجوز وبترتيب مختلف قبل DDL، ويتحقق مجدداً بعد إنشاء الفهرس أو توسيع مساحة معرف الحدث.

`HrSchemaTool` build ناجح 0 تحذيرات/0 أخطاء؛ لم تُشغّل اختبارات أو verifier أو اتصال/DDL.

### اكتمال preflight لتكرارات فهرس البصمة

كشف البيانات يضم الآن كل `EVENT_ID` غير الفارغ، حتى إذا كان `EMPLOYEE_CODE` فارغاً. هذا يطابق سلوك الفهرس الفعلي: عند غياب الموظف يظل `EVENT_ID` هو مفتاح التفرد الفعال؛ التكرارات في هذه المجموعة كانت ستفشل إنشاء الفهرس بعد DDL.

النتيجة source/build فقط؛ لا اختبارات أو verifier أو اتصال Oracle/DDL.

### عمود الموظف الأساسي في installer البصمة المحمولة

أضيف `HR_RAW_ATTENDANCE.EMPLOYEE_CODE` إلى حواجز metadata قبل/بعد التثبيت كـ`NVARCHAR2(50) NOT NULL` مطابقاً لخرائط EF. صار كشف التكرارات يعمل حتماً بعد اجتياز هذا التحقق؛ لم يعد يتخطى الاستعلام عندما يغيب عمود داخل مفتاح الفهرس.

لم تُشغّل اختبارات أو verifier أو اتصال/DDL؛ مصدر/build فقط.

### استكمال metadata الأساسية لمعالجة البصمات

يتحقق `HrAttendanceProcessingSchema` قبل DDL من تعريف أعمدة ID، والموظف، وحالة المعالجة، ووقت البصمة، ومعرف الحدث، ووقت المعالجة في `HR_RAW_ATTENDANCE`، ومن `HR_ATTENDANCE_DAY.ID`. يشترط مفتاحاً مفرداً على ID الأب بحالة ENABLED/VALIDATED قبل إنشاء FK؛ لم يعد فحص الوجود فقط يؤجل اكتشاف النوع أو المفتاح غير الصالح إلى ما بعد إضافة أعمدة أو فهارس.

التحقق source/build فقط؛ لم تُشغّل اختبارات أو verifier أو Oracle/DDL.

### تدقيق verifier فهارس EOS والإجازة القصيرة

تحقق `HrEosShortLeaveSchema.Verify` كان يقارن أسماء الأعمدة فقط عبر `ALL_IND_COLUMNS`، دون إثبات مالك الفهرس على الجدول المتوقع أو حالته ونوعه وتفرده. صار يطابق لكل فهرس الجدول، و`NORMAL` و`NONUNIQUE` و`VALID`، وترتيب الأعمدة. هذا تحديث لمصدر verifier فقط؛ لم يُشغّل verifier أو اختبارات أو Oracle.

### تدقيق verifier علاقات EOS والإجازة القصيرة

صار verifier يثبت كل علاقة FK بالاتجاه الكامل: جدول الطفل، نوع R، ENABLED/VALIDATED، المالك المرجعي، جدول وأعمدة المفتاح الأب، أعمدة الطفل المرتبة وقاعدة الحذف. يغطي علاقتي الموظف المقيدتين بلا cascading، وعلاقة سطور التسوية المتسلسلة بحذف settlement. لم يعد يكتفي بعدد صفوف باسمَي FK أو بقيمة delete rule منفصلة.

تم تعديل المصدر فقط؛ لا verifier أو اختبارات أو Oracle/DDL.

### preflight مفتاح FK لأب سطور EOS

أضيف فحص سابق للـDDL لمفتاح `HR_END_OF_SERVICE_SETTLEMENT.ID` عندما يكون جدول التسوية موجوداً مسبقاً: يجب وجود PK أو UK أحادي العمود ومفعّل ومتحقق، وإلا يتوقف installer قبل إنشاء أي جدول أو فهرس. إذا كان الجدول غائباً، سيُنشأ بتعريف PK ضمن DDL الخاص به. verifier يطابق الآن نوع/حالة المفتاح الأب الذي تشير إليه علاقات الموظف وخط التسوية.

لم يُشغّل verifier أو اختبار أو اتصال/DDL؛ تغيير مصدر/build فقط.

### تدقيق verifier قيود وفهرس cashout

لم يعد verifier يكتفي بتفعيل/تحقق أسماء قيود cashout: يطابق نوع P/U/R/C لكل PK/UK/FK/CHECK. كما يثبت أن فهرس `UX_HR_LCO_LINE_ADJ` على `HR_LEAVE_CASHOUT_LINE` هو NORMAL وUNIQUE وVALID، ويغطي `PAYROLL_ADJUSTMENT_ID` وحده دون تعبيرات وظيفية.

هذا تعديل للمصدر فقط؛ لم يُشغّل verifier أو اختبارات أو Oracle/DDL.

### استكمال preflight قيود cashout قبل DDL

يوسّع `HrLeaveCashoutSchema` مطابقة قيود PK/UK/CHECK المسماة قبل أي DDL إلى بصمة الأعمدة المرتبة أو شرط CHECK وحالة ENABLED/VALIDATED، ويرفض القيود المفقودة على جدول cashout موجود عندما لا يملك installer مسار إصلاح آمن. كما يشمل preflight الآن علاقات FK الإضافية لرأس التسوية وخطوطها؛ يطابق الجدول/العمود الأب والابن ومالك المفتاح وقاعدة الحذف، ويتطلب مفتاحاً أباً أحادي العمود ENABLED/VALIDATED، ويفرض وجود علاقات الإنشاء فقط على الجداول الموجودة، مع إبقاء إصلاح FKs المدعومة في مسار installer.

بناء `HrSchemaTool` ناجح 0 تحذيرات/أخطاء، و`git diff --check` بلا أخطاء (تنبيهات line endings فقط). لا verifier أو اختبارات أو اتصال/DDL؛ التقدم ثابت 83/139.

### preflight كائنات bootstrap في HrRepair

قبل أول DDL في `HrRepair`، يتحقق الآن من تصادم أسماء جداول bootstrap الأربعة مع كائنات أخرى في `DEV_TEMPLATE`، ومن حجز أسماء القيود المسماة للجداول التي ستُنشأ. كما يشترط وجود `SYS_BRANCH."Id"` كمفتاح PK/UK أحادي العمود ENABLED/VALIDATED قبل إنشاء جدول إعداد GL، ويتحقق من مفتاح `HR_LEAVE_YEAR_CLOSE.ID` قبل إنشاء جدول السطور، ويرفض وجود جدول السطور بلا رأس الإقفال.

بناء `HrSchemaTool` ناجح 0 تحذيرات/أخطاء. لم تُشغّل اختبارات أو verifier أو اتصال/DDL؛ بقيت الحاجة إلى قبول المخطط المعزول، والإنجاز ثابت 83/139.

كما يتحقق `HrRepair` قبل أول DDL من وجود كل جداول HR الممثلة في EF التي سيضيف إليها أعمدة mapped؛ يستثني فقط جداول الإعداد/الإقفال الثلاثة التي ينشئها بنفسه. هذا يمنع المرور بإنشاءات مبكرة ثم الفشل لاحقاً عند محاولة تعديل جدول mapped مفقود.

وخطة إضافة الأعمدة scalar في `HrRepair` تُبنى الآن قبل أول DDL وتُعاد هي نفسها للتنفيذ بعد إنشاء الجداول المخصصة. تُفحص أنواع التخزين والقيم الافتراضية التي يستطيع المخطط تمثيلها، وتُرفض إضافة عمود required بلا قيمة آمنة إلى جدول مأهول؛ وتُبقى الأعمدة النصية bounded من اختصاص `HrStringRepair`. هذا لا يطبق أي ترقية أو backfill بنفسه.


### تدقيق أدوات DDL — 15 نوفمبر 2026

مراجعة source لجميع ملفات `scripts/HrSchemaTool` المحتوية أوامر DDL حرفية وجدت أن installers تطبق `HrSchemaColumnGuard` أو تحققات metadata متخصصة قبل/بعد الإضافة؛ `HrRepair` يخطط الأعمدة scalar قبل أي DDL ويتحقق من تعريف Oracle الناتج قبل متابعة الإصلاح. هذا تصنيف ومراجعة مصدر، لا يثبت تشغيل الأداة أو تطبيق أي DDL. لا اختبارات أو verifier أو اتصال Oracle في هذه الدفعة؛ يبقى قبول مخطط معزول قابل للاستعادة ومقارنة tenant مفتوحاً.
