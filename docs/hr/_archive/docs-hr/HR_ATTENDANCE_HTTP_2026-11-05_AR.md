# ATT-08 / BASE-04 — تحقق HTTP محلي فعلي

اختبارات HrAttendanceHttpTests تشغّل Kestrel على loopback بمنفذ عشوائي، وتستعمل HttpClient وJWT بمفتاح اختبار مولد لكل fixture. تعمل مصادقة JwtBearer وMVC/JSON وHrAccessFilter وHrAttendanceService وقراءة الموظف/السياسة/فروع الحضور الفعلية وExceptionHandlingMiddleware. لا أسرار JWT الإنتاج أو توكن حساب حقيقي.

**البدائل الصريحة:** اختيار الشركة adapter اختبار ثابت من claim موثّق التوقيع، وليس SchemaRoutingMiddleware؛ لكل شركة مخزن EF InMemory مستقل، وليس مخططي Oracle. حفظ raw adapter ذو قفل داخل الاختبار، وليس معاملة المستودع/قيد Oracle. محاولة المعالجة المالية بديل يرمي خطأ إقفال اصطناعي؛ حارس CLOSED الحقيقي مختبر منفصلاً على Oracle. لا LocalizedApiResponseFilter أو ForceLogout/توجيه شركات الإنتاج في هذا المضيف المحدود.

## خطأ حقيقي كشفه HTTP

وضع `[property: Required]` على EventId في record يجعل MVC يرمي InvalidOperationException عن metadata على property بدلاً من معامل constructor؛ لذلك كان الطلب الصحيح يرجع 400 قبل الخدمة. تغيّر الهدف إلى `[param: Required]` لـCheckInRequestDto وCheckOutRequestDto. JsonRequired للإحداثيات بقي على property لأنه يخص serialization. لا يكفي Validator.TryValidateObject أو استدعاء controller مباشرة لإثبات هذا العقد.

## الحالات المثبتة

- 401 دون مصادقة أو مع توقيع JWT خاطئ، دون حفظ دليل.
- 403 لموظف آخر/فرع آخر ولمحاولة تغيير الشركة عبر Header، أو استعمال استقبال raw/configure وإعداد موقع الفرع كموظف.
- 400 عند غياب الإحداثيات أو EventId من الطلب؛ صفر صريح مقبول، وCheckOut يمر بنفس تحقق constructor.
- 202 بإيصال EvidenceOnly وRawEventId، ثم نفس Data عند إعادة المحاولة و409 عند تغيير المحتوى.
- بقاء raw معلقاً مع ملاحظة المحاولة عند رفض مالي اصطناعي، و400 لرفض GPS نهائي محفوظ بنفس الإيصال عند الإعادة.
- شركتان لهما نفس EmployeeCode/EventId ومعرف raw لا تتشاركان المخزن؛ قراءة raw بالموظف الخطأ ممنوعة، والمعرف نفسه في مخزن غير موجود يرجع404.

نجح 12 اختبار HTTP، إضافة إلى 12 اختبار عقد مباشر و35 فلتر/نطاق: المجموع الفريد 59 عند `-p:HrAccessMatrixOnly=true`. `-p:HrHttpOnly=true` يشغّل 24 منها (HTTP والعقد المباشر). الأدلة att08-http-diagnostic.log للعيب الأولي، وatt10-api-suite.log للنجاح. تنتهي كل fixtures بإغلاق المضيف والعميل؛ لا خادم باقٍ أو تغيير Oracle لهذه الاختبارات.

أوامر التشغيل: `dotnet test tests/ThinkOnErp.HR.Api.Tests/ThinkOnErp.HR.Api.Tests.csproj --no-restore -m:1 -p:HrAccessMatrixOnly=true`.

تبقى ATT-08 وBASE-04 مفتوحتين لتوجيه الشركات الفعلي على Oracle ضمن طلب HTTP، وتصحيح/ليل/توقيت وGPS على عميل موبايل حقيقي، وكتابة العلاقات والمدير بمنح فعلية والتزامن. نجاح هذا المضيف لا يُعلن نشر التطبيق أو اكتمال تلك الحالات.

تحديث 6 نوفمبر: المجموعة الحالية 66 اختبار عقد/فلتر/HTTP/نطاق، بينها 13 HTTP و12 عقداً و41 فلتر/نطاق. أُضيف رفض قراءة كتالوج التنظيم للموظف الذاتي واختبارات نطاق التنظيم والهوية؛ الـadapters وInMemory ما زالا مستخدمين، ولا يثبت ذلك Oracle HTTP أو توجيه المخططات الإنتاجي.
