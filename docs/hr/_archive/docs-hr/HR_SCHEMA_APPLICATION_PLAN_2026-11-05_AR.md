# خطة فرق مخطط HR — OPS-01

تاريخ القراءة الحية: 5 نوفمبر 2026. النطاق `DEV_TEMPLATE` فقط. هذه خطة تطبيق ومراجعة، وليست سجلاً لتطبيق DDL. لا تثبت مزامنة العملاء أو النشر.

**تحديث لاحق في اليوم نفسه:** أُخذت نسخة محلية وطُبق الإصلاح والتحقق منه فعلياً وفق [تقرير OPS-02](HR_DEV_TEMPLATE_APPLIED_2026-11-05_AR.md). الجداول والفروقات أدناه تسجل الحالة قبل التطبيق؛ الاستعادة التجريبية والتعافي بعد توقف DDL يتابعان ضمن OPS-11.

## الأدلة

- `dotnet build scripts/HrSchemaTool/HrSchemaTool.csproj --no-restore -m:1 -v:q`: ناجح، صفر أخطاء مع تحذيرات NU1900/NU1701.
- `dotnet run --project scripts/HrSchemaTool --no-build -- --schema-details`: نجح على Oracle، قراءة metadata وأعداد مجمعة فقط. التفاصيل في `artifacts/hr-audit/dev-template-schema-details.txt`، ونتائج مقارنة الأعمدة في `dev-template-schema.txt` بالمجلد نفسه.
- 37 كيان EF مقابل 59 جدول HR؛ ثلاثة جداول جديدة مفقودة. مطابقة أسماء الأعمدة ونجاح SELECT لا يثبتان مطابقة أطوال كل أعمدة النظام القديمة. التقرير التفصيلي يحفظ الدقة والأطوال والـdefaults والقيود والفهارس للمراجعة.
- القيد القديم الفعلي للأرصدة `UX_HR_LEAVE_BAL` على `(EMPLOYEE_CODE, LEAVE_TYPE_CODE, YEAR_NO)`، ENABLED/VALIDATED. كشف القيد بالاسم المفترض في الأداة كان غير كافٍ؛ أصبح الكشف بتوقيع الأعمدة.
- صفر مجموعات أرصدة مكررة بحسب الموظف/النوع/السنة، وصفر سنة خارج 1..9998، وصفر رصيد يتجاوز مدى NUMBER(8,4). هذه نتائج قراءة وليست تجربة ترحيل.

## الأعمدة القائمة التي تحتاج تغييراً

| الجدول/العمود | التعريف المطلوب | default عند الإصلاح | أثر البيانات القائمة |
|---|---|---|---|
| HR_LEAVE_BALANCE.CYCLE_START_DATE | TIMESTAMP(7) nullable | NULL | ترحيل السجل القديم إلى 1 يناير من YEAR_NO |
| HR_LEAVE_BALANCE.CYCLE_END_DATE | TIMESTAMP(7) nullable | NULL | ترحيل السجل القديم إلى 31 ديسمبر من YEAR_NO |
| HR_LEAVE_BALANCE.ACCRUED_DAYS | NUMBER(8,4) NOT NULL بدلاً من (6,2) | لا تغيير | حفظ القيمة وتوسيع الكسور |
| HR_LEAVE_BALANCE.USED_DAYS | NUMBER(8,4) NOT NULL بدلاً من (6,2) | لا تغيير | حفظ القيمة |
| HR_LEAVE_BALANCE.CARRIED_FORWARD_DAYS | NUMBER(8,4) NOT NULL بدلاً من (6,2) | لا تغيير | حفظ القيمة |
| HR_LEAVE_POLICY.ANNUAL_ENTITLEMENT_DAYS | NUMBER(8,4) nullable | NULL | لا نستنتج استحقاقاً جديداً |
| HR_LEAVE_POLICY.CYCLE_BASIS | NVARCHAR2(40) NOT NULL | CALENDAR_YEAR | إبقاء معنى السياسة القديمة |
| HR_LEAVE_POLICY.PRORATE_FIRST_CYCLE | NUMBER(10) NOT NULL (bool محول) | 0 | لا تشغيل تناسب جديد تلقائياً |
| HR_LEAVE_TYPE.TRACKS_BALANCE | NUMBER(10) NOT NULL (bool محول) | 1 | مراجعة الأنواع السبعة؛ تتبعها الحالي محفوظ |
| HR_LEAVE_TYPE.YEAR_END_EXCESS_ACTION | NVARCHAR2(20) NOT NULL | EXPIRE | لا تنفيذ إقفال/تصفير عند إضافة العمود؛ يلزم اعتماد الشركة قبل أي إقفال |
| HR_PRORATION_POLICY.SALARY_SEGMENT_ALLOCATION | NVARCHAR2(30) NOT NULL | UNCONFIGURED | يمنع انتقال الراتب مع FIXED_30 حتى اختيار الشركة |
| HR_PAYROLL_RUN.INPUT_FINGERPRINT | NVARCHAR2(64) nullable | NULL | المسير القديم بلا بصمة يحتاج إعادة حساب قبل الاعتماد |
| HR_PAYROLL_RUN.CANCELLED_BY | NVARCHAR2(200) nullable | NULL | لا إلغاء لسجلات قديمة |
| HR_PAYROLL_RUN.CANCELLATION_DATE | TIMESTAMP(7) nullable | NULL | لا تغيير حالة |
| HR_PAYROLL_RUN.CANCELLATION_REASON | NVARCHAR2(1000) nullable | NULL | لا اختراع سبب |
| HR_PAYROLL_RUN_LINE.CURRENCY_CODE | NVARCHAR2(30) nullable | NULL | لا نسخ من ملف الموظف الحالي إلى مسير تاريخي |
| HR_PAYROLL_RUN_LINE.BENEFICIARY_NAME | NVARCHAR2(400) nullable | NULL | يلزم إثبات تاريخي قبل تصدير مسير قديم |
| HR_PAYROLL_RUN_LINE.BENEFICIARY_NATIONAL_ID | NVARCHAR2(100) nullable | NULL | يلزم إثبات تاريخي قبل تصدير مسير قديم |

القيم الافتراضية أعلاه مصدرها constructors وmapping المستخدمان بواسطة HrRepair، وليست قواعد بلد. لا تُستبدل الإعدادات الموجودة. لا توجد سجلات LeavePolicy في القراءة الحالية. توجد سبعة أرصدة ومسير واحد؛ لا تعاد حساب مبالغها أو اعتمادها أثناء الإصلاح.

## الجداول الجديدة وحقولها

مصدر DDL الكامل القابل للمراجعة: `scripts/HrSchemaTool/HrRepair.cs`. لا DEFAULT في أوامر CREATE لهذه الجداول؛ الحقول المطلوبة يملؤها API، بخلاف ID المولد.

| الجدول | الأعمدة والتعريفات |
|---|---|
| HR_LEAVE_SETTINGS | ID NUMBER(19) PK غير مولد؛ LEAVE_YEAR_START_MONTH وLEAVE_YEAR_START_DAY NUMBER(10) NOT NULL؛ BALANCE_DISPLAY_FORMAT NVARCHAR2(20) NOT NULL؛ STANDARD_WORKDAY_HOURS NUMBER(6,2) nullable؛ CREATION_USER NVARCHAR2(200) وCREATION_DATE TIMESTAMP(7) مطلوبان؛ UPDATE_USER NVARCHAR2(200) وUPDATE_DATE TIMESTAMP(7) nullable |
| HR_LEAVE_YEAR_CLOSE | ID NUMBER(19) identity PK؛ CLOSE_KEY NVARCHAR2(300) UNIQUE NOT NULL؛ FROM_YEAR وTO_YEAR NUMBER(10) NOT NULL؛ LEAVE_TYPE_CODE NVARCHAR2(100) وSTATUS NVARCHAR2(20) وCREATION_USER NVARCHAR2(200) وCREATION_DATE TIMESTAMP(7) مطلوبة؛ POSTED_BY وUNPOSTED_BY NVARCHAR2(200)، POSTED_DATE وUNPOSTED_DATE TIMESTAMP(7) nullable |
| HR_LEAVE_YEAR_CLOSE_LINE | ID NUMBER(19) identity PK؛ CLOSE_ID وSOURCE_BALANCE_ID NUMBER(19) NOT NULL؛ TARGET_BALANCE_ID NUMBER(19) nullable؛ EMPLOYEE_CODE وLEAVE_TYPE_CODE NVARCHAR2(100) NOT NULL؛ SOURCE_YEAR وTARGET_YEAR NUMBER(10) NOT NULL؛ SOURCE_CYCLE_START_DATE وSOURCE_CYCLE_END_DATE TIMESTAMP(7) nullable؛ TARGET_CYCLE_START_DATE وTARGET_CYCLE_END_DATE TIMESTAMP(7) NOT NULL |
| HR_LEAVE_YEAR_CLOSE_LINE — مبالغ الأيام | جميع SOURCE_ACCRUED_BEFORE، SOURCE_USED_DAYS، SOURCE_CARRIED_BEFORE، TARGET_ACCRUED_BEFORE، TARGET_CARRIED_BEFORE، TARGET_USED_BEFORE، UNUSED_DAYS، CARRIED_DAYS، EXPIRED_DAYS، CASHOUT_DAYS: NUMBER(8,4) NOT NULL |

إعدادات API الافتراضية عند عدم وجود سجل: 1 يناير وDECIMAL وStandardWorkdayHours=NULL؛ لا يضيف الإصلاح سجل إعدادات ولا يفترض 8 ساعات. الشركة تعتمد إعدادها قبل التشغيل.

## القيود وترتيب الترحيل

1. إيقاف كتابات HR على نسخة التطوير المستهدفة وأخذ نسخة قابلة للاستعادة، وحفظ التقرير الحالي وتعريفات القيود والأعداد. لا يمكن الاعتماد على ROLLBACK لاسترجاع Oracle DDL.
2. إنشاء الجداول والأعمدة الناقصة وتوسيع دقة الأرصدة. قيود الجداول الجديدة: PK لكل ID، وunique على CLOSE_KEY، وunique `(CLOSE_ID,SOURCE_BALANCE_ID)`، وFK من CLOSE_ID إلى الإقفال مع ON DELETE CASCADE. لا يضيف الإصلاح FK للسجلات المرجعية الأخرى في سطر اللقطة.
3. تعبئة تاريخي الأرصدة اللذين كلاهما NULL بالسنة التقويمية الأصلية، مع الحفاظ على YEAR_NO والمبالغ والمعرفات. لا يعاد تفسيرها حسب دورة شركة جديدة. التاريخ المكتمل الموجود لا يعدل. السنة غير الصالحة أو تاريخ ناقص واحد أو دورة مكررة/معكوسة يوقف الانتقال للمراجعة.
4. إضافة unique `(EMPLOYEE_CODE,LEAVE_TYPE_CODE,CYCLE_START_DATE)` أولاً، ثم إزالة القيود والفهارس الفريدة القديمة المطابقة لأعمدة السنة، مهما كان اسمها. إبقاء القديم يمنع دورتين صحيحتين في نفس السنة. لا تُحذف PK أو FK الموظف/النوع.
5. بقية أداة repair تنسخ IDs القديمة المفقودة فقط لهياكل الراتب والشفتات، وتضبط identity فوق أعلى ID، وتسجل شاشات وعمليات HR دون منح أدوار. يلزم فحص هذه الآثار ضمن OPS-02؛ الأمر ليس DDL فقط. لا تعتمد على جداول النسخ القديمة كتكامل مستمر.

## القبول والرجوع — OPS-02

- تشغيل repair على نسخة قابلة للاستعادة ثم preflight وschema-details؛ صفر جدول/عمود مفقود، والتأكد من دقة الأرصدة وغياب قيد السنة القديم ووجود قيد الدورة الفعال.
- مقارنة المعرفات والمبالغ والأعداد قبل/بعد؛ إعادة repair للتأكد من عدم تكرار نسخ البيانات. إنشاء دورتين بنفس السنة واختبار رفض نفس الدورة المكررة يكون داخل بيانات اختبار معزولة.
- التحقق من rollback عند دورة غير سليمة ومن التعافي بعد توقف DDL في منتصف الخطوات، ثم اختبارات خدمة الإجازات والمسير تحت المخطط الجديد. لا تعني قراءة metadata نجاح هذه الحالات.
- الرجوع بعد DDL يكون باستعادة نسخة التطوير أو خطة عكس مراجعة؛ لا حذف آلي لأعمدة أو جداول تحوي بيانات جديدة. لا يعاد قيد السنة إذا وجدت بالفعل دورتان في السنة نفسها.

OPS-01 مكتملة كنطاق جرد وخطة وتصحيح أداة. تطبيق الإصلاح واختبارات ترحيله وإعادة تشغيله ما زالت OPS-02؛ مزامنة العملاء OPS-03 والنشر مراحل مستقلة.
