# تدقيق أطوال نصوص HR — 12 نوفمبر 2026

## النطاق والطريقة

أُضيف الوضع للقراءة فقط `--audit-string-lengths` إلى `HrSchemaTool`. يبني نموذج EF الفعلي، ثم يقارن كل خاصية نصية لها `HasMaxLength` مع `CHAR_LENGTH` ونوع و`DATA_LENGTH` من `ALL_TAB_COLUMNS` في `DEV_TEMPLATE`. لا يقرأ قيم الموظفين ولا ينفذ DDL أو DML. لا يستعلم عن مخططات العملاء، وخصوصاً `THINKONERP_DEV02`.

## النتيجة والإصلاح على القالب

- جرى فحص 56 خريطة EF و79 جدول HR في `DEV_TEMPLATE`.
- قبل الإصلاح: 165 حد أحرف مطابقاً و195 مختلفاً من أصل 360؛ لا توجد أعمدة نصية معيّنة مفقودة.
- الاختلافات الـ195 كلها كانت EF=ضعف `CHAR_LENGTH`، بينما `DATA_LENGTH` يساوي حد EF. مثال: `HR_PAYROLL_PERIOD.PERIOD_CODE` كان EF=40 بينما Oracle `NVARCHAR2` يقبل 20 محرفاً (`DATA_LENGTH=40`). Oracle ODP.NET يوثّق أن `HasMaxLength(x)` مع Unicode maps إلى `NVARCHAR2(x)` وأن طول `NVARCHAR2` هو طول أحرف، وليس بايتات ([Oracle EF Core data type mapping](https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/EFCoreDataTypeMapping.html)).
- وُوئم حد EF في 193 خاصية مع `CHAR_LENGTH` القائم في سبعة ملفات إعدادات. وللحفاظ على سلوك أكواد العملة القابلة للتهيئة حتى 20 حرفاً، احتُفظ بحد العملة 20 ثم أُخذت نسخة Data Pump ناجحة ذات بصمة محلية قبل توسيع أعمدة `CURRENCY_CODE` الخمسة التي كانت 10 إلى 20 محرفاً: في `HR_EMP_SALARY_STRUCTURE` و`HR_END_OF_SERVICE_SETTLEMENT` و`HR_EXPENSE_CLAIM` و`HR_SALARY_STRUCTURE` و`HR_STATUTORY_RULE`. لم تتغير القيم أو السجلات.
- إعادة الفحص: تطابق 360/360، لا mismatch أو أعمدة نصية مفقودة. بقيت 146 عموداً نصياً في جداول HR بلا خاصية نصية مقابلة في خرائط HR الحالية؛ تحتاج تصنيف ملكيتها قبل أي عمل عليها.

الأعمدة الـ146 غير الممثلة موزعة على 23 جدولاً: `HR_ASSET_ASSIGNMENT`, `HR_CANDIDATE`, `HR_CANDIDATE_APP`, `HR_CANDIDATE_APPLICATION`, `HR_ATTENDANCE_RECORD`, `HR_EMPLOYEE_DOCUMENT`, `HR_EMPLOYMENT_CONTRACT`, `HR_EMPLOYMENT_EVENT`, `HR_EMP_SALARY_STRUCTURE`, `HR_EMP_SALARY_STRUCT_LINE`, `HR_EOS_PROVISION_ACCRUAL`, `HR_EOS_SETTLEMENT`, `HR_EXPENSE_CLAIM`, `HR_EXPENSE_CLAIM_LINE`, `HR_FINAL_SETTLEMENT`, `HR_JOB_GRADE`, `HR_JOB_REQUISITION`, `HR_ONBOARDING_TASK`, `HR_OVERTIME_RECORD`, `HR_PAYROLL_LINE_COMP`, `HR_SALARY_REVISION`, `HR_STATUTORY_RULE`, `HR_EMP_SHIFT_ASSIGNMENT`. لم يُفترض أنها legacy أو active من الاسم؛ يلزم قرار نطاق/ملكية قبل تغيير خرائطها.

## تصنيف مراجع المصدر

- **ثلاثة جداول نسخ legacy موثقة:** `HR_EMP_SALARY_STRUCTURE`, `HR_EMP_SALARY_STRUCT_LINE`, `HR_EMP_SHIFT_ASSIGNMENT` هي مصادر نسخ صريحة في `HrRepair.cs` إلى الجداول النشطة؛ تبقى للاحتفاظ التاريخي، ولا تُستخدم كجداول تشغيل حية.
- **مرجع حماية فقط:** `HR_JOB_REQUISITION` يظهر باستعلام حماية حذف الإدارة/المسمى، لكن لا يوجد له كيان EF أو مسار توظيف ظاهر في المصدر.
- **19 جدولاً بلا تطابق مصدر مباشر في `src/`, `scripts/`, `tests/`:** `HR_ASSET_ASSIGNMENT`, `HR_ATTENDANCE_RECORD`, `HR_CANDIDATE`, `HR_CANDIDATE_APP`, `HR_CANDIDATE_APPLICATION`, `HR_EMPLOYEE_DOCUMENT`, `HR_EMPLOYMENT_CONTRACT`, `HR_EMPLOYMENT_EVENT`, `HR_EOS_PROVISION_ACCRUAL`, `HR_EOS_SETTLEMENT`, `HR_EXPENSE_CLAIM`, `HR_EXPENSE_CLAIM_LINE`, `HR_FINAL_SETTLEMENT`, `HR_JOB_GRADE`, `HR_ONBOARDING_TASK`, `HR_OVERTIME_RECORD`, `HR_PAYROLL_LINE_COMP`, `HR_SALARY_REVISION`, `HR_STATUTORY_RULE`. هذا جرد نصي فقط؛ لا يثبت أن الجداول legacy أو أن ميزاتها خارج الخطة.

## القرار والخطوات التالية

اكتملت مواءمة خرائط EF والمخطط الحي لـ`DEV_TEMPLATE` للخصائص النصية المعيّنة. المتبقي مراجعة الأعمدة الـ146 غير المُمَثلة، ثم فحص مخططات العملاء وفق OPS-03 وخطة تطبيق وrollback بعد preflight؛ هذا التقرير لا يصرح بمزامنة أي عميل، ولم يُستعلم عن `THINKONERP_DEV02` في هذه الدفعة.

## التحقق

- `dotnet build scripts/HrSchemaTool --no-restore -m:1`: نجح، 0 أخطاء؛ 15 تحذيراً سابقاً في مشروع Infrastructure.
- `--backup-dev-template`: Data Pump اكتمل، وحُفظ dump/log وmanifest محلياً تحت `artifacts/hr-audit/backups/20261112091921`.
- `--widen-currency-code-columns`: أعاد التحقق من الأعمدة الخمسة بعد التوسيع إلى 20؛ خاص بـ`DEV_TEMPLATE`.
- إعادة تشغيل `--widen-currency-code-columns`: نجحت idempotently مع `changed 0 column(s)`.
- `--audit-string-lengths`: نجح على `DEV_TEMPLATE` للقراءة فقط؛ `MATCH=360`, `MISMATCH=0`, `MISSING=0`; حُفظ التفصيل في `artifacts/hr-audit/dev-template-hr-string-lengths.txt`.
- استُخرجت 23 علامة `UNMAPPED_TABLE` آلياً: لا يوجد جدول EF مطابق بين خرائط HR الحالية؛ تطابق عددها مع جرد المصدر. لا يحكم هذا وحده على كون الجدول مطلوباً أو متروكاً.
- `--verify-salary-metadata`: نجح lookup للعملة والطريقة، round-trip بمبلغ ثلاثة أرقام عشرية داخل معاملة rollback، والتحقق من بقاء سجلات الراتب الأصلية كما هي.
