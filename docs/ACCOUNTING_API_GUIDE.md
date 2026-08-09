# دليل Accounting API — شجرة الحسابات

> آخر تحديث: 2026-08-09  
> Swagger document: `Accounting API`  
> OpenAPI JSON: `/swagger/accounting/swagger.json`

## 1. المصادقة وتحديد الشركة

كل واجهات شجرة الحسابات تتطلب JWT صالحًا وتعمل داخل مخطط الشركة الذي حدده
`SchemaRoutingMiddleware`.

1. سجل الدخول من `POST /api/Auth/login`.
2. انسخ `data.accessToken`.
3. في `/swagger/index.html` اختر `Accounting API` واضغط `Authorize`.
4. الصق الـtoken دون كتابة كلمة `Bearer`.

مستخدم الشركة لا يرسل معرف الشركة في body أو query؛ الشركة مأخوذة من الـJWT.
عند استخدام SuperAdmin يجب اختيار Tenant واحد عبر إحدى الترويستين:

```http
X-Company-Code: <company-code>
```

أو:

```http
X-Company-Id: <company-id>
```

واجهات الإنشاء والتعديل وتغيير الحالة والحذف تتطلب `TenantAdminOnly`.

## 2. ملخص المسارات

| Method | Route | الصلاحية | الوظيفة |
|---|---|---|---|
| `GET` | `/api/accounting/gl-accounts/tree` | مستخدم موثق | الشجرة الهرمية كاملة |
| `GET` | `/api/accounting/gl-accounts/postable?branchId={id}` | مستخدم موثق | الحسابات القابلة للترحيل للفرع |
| `GET` | `/api/accounting/gl-accounts/{id}` | مستخدم موثق | حساب واحد بالمعرف |
| `POST` | `/api/accounting/gl-accounts` | `TenantAdminOnly` | إنشاء حساب |
| `PUT` | `/api/accounting/gl-accounts/{id}` | `TenantAdminOnly` | تعديل الحقول المسموحة |
| `PATCH` | `/api/accounting/gl-accounts/{id}/status` | `TenantAdminOnly` | تفعيل أو تعطيل الحساب |
| `DELETE` | `/api/accounting/gl-accounts/{id}` | `TenantAdminOnly` | حذف حساب مسموح حذفه |
| `GET` | `/api/accounting/account-categories` | مستخدم موثق | الفئات المحاسبية الثماني |
| `POST` | `/api/accounting/coa-import/validate` | `TenantAdminOnly` | فحص ملف COA دون استيراد |
| `POST` | `/api/accounting/coa-import/import` | `TenantAdminOnly` | استيراد ملف COA |

## 3. جلب حساب واحد

```http
GET /api/accounting/gl-accounts/789
Authorization: Bearer <access-token>
```

استجابة ناجحة:

```json
{
  "success": true,
  "statusCode": 200,
  "message": "GL account retrieved successfully",
  "data": {
    "id": 789,
    "accountCode": "111199",
    "accountNameAr": "الصندوق الرئيسي",
    "accountNameEn": "Main Cash",
    "parentAccountId": 456,
    "categoryId": 1,
    "categoryCode": 1,
    "categoryNameAr": "الأصول",
    "categoryNameEn": "Assets",
    "accountLevel": 5,
    "accountType": "DETAIL",
    "normalBalance": "D",
    "isContra": false,
    "isControlAccount": false,
    "controlAccountType": null,
    "isBranchSpecific": true,
    "isClearing": false,
    "isActive": true,
    "isPostable": true,
    "description": "حساب الصندوق الرئيسي",
    "notes": null,
    "branchIds": [1, 2]
  },
  "errors": null,
  "timestamp": "2026-08-09T10:00:00Z",
  "traceId": "<trace-id>"
}
```

إذا كان المعرف غير موجود داخل الشركة الحالية تكون الاستجابة `404` مع
`GL_ACCOUNT_NOT_FOUND`. حساب شركة أخرى يعامل كأنه غير موجود ولا يكشف بياناته.

## 4. تعديل حساب

```http
PUT /api/accounting/gl-accounts/789
Authorization: Bearer <access-token>
Content-Type: application/json
```

Body:

```json
{
  "accountNameAr": "الصندوق الرئيسي - عمان",
  "accountNameEn": "Main Cash - Amman",
  "isContra": false,
  "isControlAccount": false,
  "controlAccountType": null,
  "isBranchSpecific": true,
  "isClearing": false,
  "description": "حساب الصندوق بعد تحديث البيانات",
  "notes": "تم تحديث الاسم وربط الفروع",
  "branchIds": [1, 2]
}
```

يعيد `200 ApiResponse<GlAccountDto>` ويحتوي `data` على الحالة النهائية للحساب.
هذا مسار `PUT` كامل للحقول القابلة للتعديل؛ أرسل جميع حقول الـbody في كل طلب حتى
لا تعاد القيم المنطقية أو النصوص الاختيارية غير المرسلة إلى قيمها الافتراضية.

### الحقول القابلة للتعديل

- `accountNameAr`: مطلوب، حتى 200 حرف.
- `accountNameEn`: مطلوب، حتى 200 حرف.
- `isContra`.
- `isControlAccount` و`controlAccountType`.
- `isBranchSpecific` و`branchIds`.
- `isClearing`.
- `description`: حتى 1000 حرف.
- `notes`: حتى 2000 حرف.

### الحقول الهيكلية غير القابلة للتعديل

لا يقبل `PUT` الحقول التالية، لأنها تحدد موقع الحساب وبنية الشجرة:

- `accountCode`
- `parentAccountId`
- `categoryId`
- `accountLevel`
- `accountType`
- `normalBalance`
- `isActive`

استخدم `PATCH /api/accounting/gl-accounts/{id}/status` لتغيير `isActive`.

### قواعد التعديل

- Control account يجب أن يكون حساب `DETAIL`.
- عندما يكون `isControlAccount=true` يجب أن يكون النوع `AR` أو `AP` أو `INVENTORY`.
- عندما يكون `isControlAccount=false` يجب أن يكون `controlAccountType=null`.
- `isBranchSpecific` و`isClearing` مسموحان لحسابات `DETAIL` فقط.
- عندما يكون `isBranchSpecific=true` يلزم فرع فعال واحد على الأقل من الشركة الحالية.
- عندما يكون `isBranchSpecific=false` يجب أن تكون `branchIds` فارغة.
- معرفات الفروع يجب أن تكون أرقامًا موجبة؛ التكرار يزال تلقائيًا.
- روابط الفروع غير المطلوبة تصبح غير فعالة، ويمكن إعادة تفعيلها بتضمينها لاحقًا.

## 5. حذف حساب

```http
DELETE /api/accounting/gl-accounts/789
Authorization: Bearer <access-token>
```

عند النجاح يعيد `200 ApiResponse<GlAccountDto>`. قيمة `data` هي لقطة الحساب قبل
حذفه، لتستطيع الواجهة عرض ما تم حذفه أو تسجيله.

الحذف ممنوع في الحالات التالية:

- الحساب جذري: `409 GL_ROOT_ACCOUNT_DELETE_FORBIDDEN`.
- للحساب أبناء: `409 GL_ACCOUNT_HAS_CHILDREN`.
- قاعدة البيانات رفضت الحذف بسبب سجل مرجعي قائم: `409 GL_ACCOUNT_IN_USE`.

تُحذف روابط الحساب في `GL_ACCOUNT_BRANCH` ضمن عملية الحذف نفسها قبل حذف الحساب.
لا يشترط تعطيل الحساب أولًا في التنفيذ الحالي.

### ملاحظة عن الحركات المحاسبية

لا توجد في النسخة الحالية جداول قيود يومية أو دفتر أستاذ مرتبطة بـ`GL_ACCOUNT`؛
لذلك لا يوجد فحص تطبيقي مسبق للحركات بعد. عند إضافة تلك الجداول يجب إنشاء FK إلى
`GL_ACCOUNT` وإضافة فحص صريح للمراجع. أي FK موجود بالفعل ويرفض الحذف يحول إلى
`409 GL_ACCOUNT_IN_USE` بدل إرجاع خطأ خادم غير مفهوم.

## 6. تفعيل وتعطيل حساب

```http
PATCH /api/accounting/gl-accounts/789/status
Authorization: Bearer <access-token>
Content-Type: application/json
```

للتعطيل:

```json
{
  "isActive": false
}
```

لإعادة التفعيل:

```json
{
  "isActive": true
}
```

الحساب `DETAIL` المعطل لا يكون قابلًا للترحيل.

## 7. فئات الحسابات

```http
GET /api/accounting/account-categories
Authorization: Bearer <access-token>
```

شكل كل عنصر:

```json
{
  "id": 1,
  "categoryCode": 1,
  "nameAr": "الأصول",
  "nameEn": "Assets",
  "normalBalance": "D",
  "financialStatement": "BALANCE_SHEET",
  "displayOrder": 1
}
```

| Code | العربية | English | Balance | Statement |
|---:|---|---|---|---|
| 1 | الأصول | Assets | `D` | `BALANCE_SHEET` |
| 2 | الالتزامات | Liabilities | `C` | `BALANCE_SHEET` |
| 3 | حقوق الملكية | Equity | `C` | `BALANCE_SHEET` |
| 4 | الإيرادات | Revenue | `C` | `INCOME_STATEMENT` |
| 5 | تكلفة المبيعات | Cost of Sales | `D` | `INCOME_STATEMENT` |
| 6 | المصروفات التشغيلية | Operating Expenses | `D` | `INCOME_STATEMENT` |
| 7 | الإيرادات الأخرى | Other Income | `C` | `INCOME_STATEMENT` |
| 8 | المصروفات الأخرى | Other Expenses | `D` | `INCOME_STATEMENT` |

استخدم `id` المرسل من هذه الواجهة كقيمة `categoryId` عند إنشاء الحساب. لا تفترض
في واجهة المستخدم أن `id` يساوي دائمًا `categoryCode` حتى لو كانا متساويين في
بيانات الزرع الحالية.

## 8. أكواد الأخطاء

| HTTP | Error code | المعنى |
|---:|---|---|
| 400 | `GL_INVALID_ID` | المعرف صفر أو سالب |
| 400 | `GL_REQUIRED_FIELD` | اسم عربي أو إنجليزي مطلوب |
| 400 | `GL_FIELD_TOO_LONG` | تجاوز طول الحقل |
| 400 | `GL_INVALID_CONTROL_TYPE` | نوع Control غير صحيح |
| 400 | `GL_UNEXPECTED_CONTROL_TYPE` | Control type مع حساب غير Control |
| 400 | `GL_CONTROL_NOT_DETAIL` | حساب HEADER لا يمكن أن يكون Control |
| 400 | `GL_DETAIL_FLAG_ON_HEADER` | Branch/Clearing مستخدم على HEADER |
| 400 | `GL_INVALID_BRANCH` | معرف فرع غير موجب |
| 400 | `GL_BRANCH_REQUIRED` | Branch-specific بلا فروع |
| 400 | `GL_UNEXPECTED_BRANCH_LINK` | فروع لحساب غير Branch-specific |
| 400 | `GL_BRANCH_NOT_FOUND` | الفرع غير موجود/غير فعال/من شركة أخرى |
| 404 | `GL_ACCOUNT_NOT_FOUND` | الحساب غير موجود في الشركة الحالية |
| 409 | `GL_ROOT_ACCOUNT_DELETE_FORBIDDEN` | محاولة حذف حساب جذري |
| 409 | `GL_ACCOUNT_HAS_CHILDREN` | الحساب له أبناء |
| 409 | `GL_ACCOUNT_IN_USE` | قاعدة البيانات رفضت الحذف لوجود مرجع |
| 401 | — | Token مفقود أو غير صالح |
| 403 | — | المستخدم لا يملك الصلاحية المطلوبة |

مثال Conflict:

```json
{
  "success": false,
  "statusCode": 409,
  "message": "An account with child accounts cannot be deleted.",
  "data": null,
  "errors": ["GL_ACCOUNT_HAS_CHILDREN"],
  "timestamp": "2026-08-09T10:00:00Z",
  "traceId": "<trace-id>"
}
```

## 9. أمثلة cURL

جلب حساب:

```bash
curl -X GET "<base-url>/api/accounting/gl-accounts/789" \
  -H "Authorization: Bearer <access-token>"
```

تعديل حساب:

```bash
curl -X PUT "<base-url>/api/accounting/gl-accounts/789" \
  -H "Authorization: Bearer <access-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "accountNameAr": "الصندوق الرئيسي - عمان",
    "accountNameEn": "Main Cash - Amman",
    "isContra": false,
    "isControlAccount": false,
    "controlAccountType": null,
    "isBranchSpecific": false,
    "isClearing": false,
    "description": "Updated account",
    "notes": null,
    "branchIds": []
  }'
```

حذف حساب:

```bash
curl -X DELETE "<base-url>/api/accounting/gl-accounts/789" \
  -H "Authorization: Bearer <access-token>"
```

جلب الفئات:

```bash
curl -X GET "<base-url>/api/accounting/account-categories" \
  -H "Authorization: Bearer <access-token>"
```
