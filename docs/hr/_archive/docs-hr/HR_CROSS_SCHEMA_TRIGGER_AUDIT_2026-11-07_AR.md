# تدقيق مشغّلات دفتر الإجازات العابرة للمخططات

**التاريخ:** 7 نوفمبر 2026  
**النطاق:** metadata وsource للقراءة فقط على `DEV_TEMPLATE` ومشغّلات أخرى مربوطة بجدولها. لم تُقرأ بيانات موظفين أو أرصدة عميل.  
**المهمة:** `OPS-12`، مانع تحقق لـ`LEV-18`.

## النتيجة

يحتوي `DEV_TEMPLATE.HR_LEAVE_MOVEMENT` على المشغّل المقصود `DEV_TEMPLATE.TR_HR_LEAVE_MOVEMENT_IMM`، وهو مفعّل وصالح ومصدره يرفع `ORA-20071` عند `UPDATE` أو `DELETE`.

إلى جانبه، تظهر ثلاثة مشغّلات مفعّلة مملوكة لمخططات أخرى ومربوطة **بنفس جدول DEV_TEMPLATE**:

| المالك | المشغّل | الحالة/الحدث | خطأ الترجمة |
|---|---|---|---|
| `THINKONERP_1122` | `TR_HR_LEAVE_MOVEMENT_IMM` | ENABLED، UPDATE OR DELETE | `PLS-00103` عند `ALTER`، السطر 2 |
| `THINKONERP_SALONDEMO` | `TR_HR_LEAVE_MOVEMENT_IMM` | ENABLED، UPDATE OR DELETE | `PLS-00103` عند `ALTER`، السطر 2 |
| `THINKONERP_THINKONCO` | `TR_HR_LEAVE_MOVEMENT_IMM` | ENABLED، UPDATE OR DELETE | `PLS-00103` عند `ALTER`، السطر 2 |

مصدر كل نسخة يحوي بعد نهاية جسم trigger عبارة `ALTER TRIGGER "<owner>"."TR_HR_LEAVE_MOVEMENT_IMM" ENABLE`، كأن DDL أُلحقت بنص PL/SQL. عند اختبار DML على جدول القالب، أعاد Oracle `ORA-04098` وسمّى trigger `THINKONERP_SALONDEMO.TR_HR_LEAVE_MOVEMENT_IMM`.

بحث المستودع لم يجد كوداً ينشئ النسخ الخارجية. الأداة الموجودة تنشئ trigger واحداً محدداً باسم `DEV_TEMPLATE.TR_HR_LEAVE_MOVEMENT_IMM` على جدول القالب؛ مصدر النسخ الأخرى يحتاج تتبعاً خارج المستودع.

## الأثر وحدود التغيير

- لا يمكن اعتبار حماية دفتر القالب ناجحة حتى تزول مراجع المشغّلات غير الصالحة أو تُصحح بعد مراجعة المالك.
- صار `--verify-leave-ledger` يفحص قبل DML ويرفض وجود trigger خارجي مفعّل وغير صالح على جدول الدفتر.
- `--schema-details` يخرج owner/table/source/errors لهذه الحالة، من دون DML.
- لا تُنفذ مزامنة مخططات العملاء ضمن هذا التقرير. لم يتغير أي trigger خارجي أو بيانات عميل في هذه الحزمة.
- نسخة `artifacts/hr-audit/backups/20261107184300` تغطي `DEV_TEMPLATE` فقط؛ **لا تغطي** مخططات المالكين الثلاثة، فلا تُستخدم كخطة رجوع لتعديلها.

## خطة إصلاح للمراجعة

1. يراجع مالك كل schema سبب وجود trigger مملوك له على جدول `DEV_TEMPLATE`.
2. يجهز DBA تغييراً موجهاً للنسخ الثلاث فقط: إسقاط النسخ العابرة للمخططات إن كانت غير مقصودة، مع الإبقاء على trigger القالب المملوك لـ`DEV_TEMPLATE`؛ أو إعادة إنشاء نسخة صحيحة فقط إذا أثبت المالك أنها مطلوبة.
3. تؤخذ نسخة مناسبة من metadata لكل owner متأثر قبل DDL، ويُسجل مالك التغيير وخطوات الرجوع.
4. بعد تنفيذ الإصلاح المعتمد، يتحقق `--schema-details` من عدم وجود triggers خارجية مفعلة على جدول DEV_TEMPLATE ومن عدم وجود compile errors.
5. يعاد `--verify-leave-ledger`؛ معيار الحماية هو أن يرفض UPDATE وDELETE بـ`ORA-20071` داخل المعاملة rollback-only، ثم يثبت صفر حركات/إقفالات/تسويات تجريبية باقية.

لا يطلب التقرير أو ينفذ أي تغيير على `THINKONERP_DEV02`.
