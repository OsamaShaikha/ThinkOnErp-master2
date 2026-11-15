# تطبيق مخطط HR على DEV_TEMPLATE — OPS-02

بتاريخ 5 نوفمبر 2026، وبناءً على طلب صاحب النظام أخذ نسخة على جهازه ثم المتابعة، أُخذت نسخة Oracle Data Pump كاملة من DEV_TEMPLATE وطُبق إصلاح HR عليه مرتين.

## النسخة المحلية

- المجلد: `artifacts/hr-audit/backups/20261105073951/` داخل المستودع على جهاز المستخدم؛ مستثنى من Git لاحتوائه على بيانات.
- الملف: `dev_template_20261105073951.dmp`، حجمه 8,048,640 بايت، بصيغة Data Pump الأصلية. يشمل بيانات وmetadata المخطط، عند SCN متسق.
- `manifest.json` يحتوي الاسم والحجم وبصمة SHA-256؛ أُعيد حساب بصمة الملف المحلي وطابقت القيمة المسجلة: `0D37A839E57B76FAF4CFDA959C3A62C62C81E71290005592165065C0729B0660`.
- حالة التصدير COMPLETED، ولم يوجد رمز ORA في سجل التصدير المحلي. بقيت ملفات التصدير الأصلية في DATA_PUMP_DIR على الخادم أيضاً.
- الاستعادة تكون بواسطة Oracle Data Pump IMPORT من الملف، ويفضّل إجراء تمرين في مخطط معزول أولاً. لم تُجرّب استعادة فعلية؛ هذا العمل متبقٍّ صراحة ضمن OPS-11. ملف بصمات المقارنة ليس نسخة استعادة.

## ما طُبق فعلياً

- إنشاء HR_LEAVE_SETTINGS وHR_LEAVE_YEAR_CLOSE وHR_LEAVE_YEAR_CLOSE_LINE.
- إضافة تواريخ دورة الأرصدة، وحقول سياسة الإجازات وأنواعها، وحقول بصمة المسير وإلغائه، ولقطة اسم المستفيد وهويته وعملة سطر الراتب، وخيار توزيع مقاطع FIXED_30.
- توسيع دقة الأيام الثلاثة في HR_LEAVE_BALANCE إلى NUMBER(8,4).
- تعبئة تاريخي الأرصدة السبعة القديمة بالسنة التقويمية الأصلية، مع إبقاء المعرفات والقيم المالية وYEAR_NO كما كانت.
- إنشاء UX_HR_LEAVE_BALANCE_CYCLE الفعال والمتحقق منه، وإزالة قيد السنة القديم UX_HR_LEAVE_BAL.
- إعادة ضبط identity للجداول المنسوخة إلى ما بعد أعلى ID. نسخ بيانات الرواتب والشفتات أضاف 0 صفوف في التشغيلين؛ إجمالياتها بقيت 5/10/5.
- سجلت الأداة الشاشات والعمليات الناقصة؛ لم تمنح أدواراً أو حسابات صلاحيات، ولم تضبط حسابات ترحيل أو إعدادات شركات جديدة.

## الأدلة والنتائج

| الدليل المحلي في artifacts/hr-audit | النتيجة |
|---|---|
| migration-baseline-run.log وhr-migration-baseline.json | بصمة متسقة لجميع أعمدة HR الأصلية في 59 جدولاً قبل الإصلاح، دون حفظ قيم الموظفين |
| backup-dev-template-run.log | اكتمال التصدير وتنزيل النسخة إلى الجهاز |
| ops02-repair-first.log | أول تطبيق ناجح؛ لا جداول أو أعمدة mapped مفقودة بعده |
| ops02-preservation-first.log | تطابق كل البيانات الأصلية في 59/59 جدولاً بعد الإصلاح |
| ops02-repair-repeat.log | إعادة تشغيل ناجحة دون تكرار النسخ؛ نجحت قراءة كيانات HR بالنطاق واختبار هوية ذاتية غير مرتبطة |
| ops02-final-verification.log | تطابق 59/59 مرة أخرى؛ قَبِل Oracle دورتين بالسنة نفسها ورفض الدورة المكررة؛ تحقق إلغاء جميع صفوف الاختبار |
| dev-template-schema-details.txt | قيد الدورة ENABLED/VALIDATED، فهرسه VALID، قيد السنة القديم غائب، ودقة الأرصدة الجديدة موجودة |

الأوامر المستعملة بعد بناء الأداة بـ`--no-restore -m:1`:

```powershell
dotnet run --project scripts/HrSchemaTool --no-build -- --migration-baseline
dotnet run --project scripts/HrSchemaTool --no-build -- --backup-dev-template
dotnet run --project scripts/HrSchemaTool --no-build -- --repair
dotnet run --project scripts/HrSchemaTool --no-build -- --migration-verify
dotnet run --project scripts/HrSchemaTool --no-build -- --repair --verify-scopes
dotnet run --project scripts/HrSchemaTool --no-build -- --verify-cycle-key --migration-verify --schema-details
```

OPS-02 مكتملة بنطاق النسخة الاحتياطية والتطبيق ومقارنة البيانات والقيود وإعادة التشغيل. اختبار الاستعادة وتعطيل DDL في منتصف العملية لم يُنفذا (OPS-11). اختبارات التزامن ومسارات HTTP والتشغيل الكامل تظل OPS-04/05/07. لم تُزامَن مخططات عملاء أخرى، ولم يُنشر إصدار التطبيق.
