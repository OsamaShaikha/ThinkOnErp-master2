# دليل تكامل محرك الضرائب للواجهات الأمامية (Frontend Tax Engine Integration Guide)
**نظام ThinkOnErp — التوثيق الشامل لمطوري الواجهات الأمامية (Frontend & Mobile Developers)**

---

## فهرس المحتويات (Table of Contents)
1. [نظرة عامة وهيكلية المحرك (Architectural Overview)](#1-نظرة-عامة-وهيكلية-المحرك)
2. [معايير الاتصال والمصادقة (Authentication & Request Headers)](#2-معايير-الاتصال-والمصادقة)
3. [كيف يعمل محرك احتساب الضرائب؟ (Core Tax Calculation Logic)](#3-كيف-يعمل-محرك-احتساب-الضرائب)
   - الأسعار الشاملة مقابل غير الشاملة (Inclusive vs. Exclusive)
   - معالجة الخصومات والعملات الأجنبية
   - قيود اليومية المحاسبية المقترحة (Suggested GL Journal)
4. [مرجع واجهات برمجة التطبيقات (API Reference)](#4-مرجع-واجهات-برمجة-التطبيقات)
   - [أ. محرك الاحتساب والفوترة اللحظية (Tax Calculation Engine)](#أ-محرك-الاحتساب-والفوترة-اللحظية)
   - [ب. الفوترة الإلكترونية ورمز الاستجابة السريع (ZATCA E-Invoicing QR)](#ب-الفوترة-الإلكترونية-ورمز-الاستجابة-السريع)
   - [ج. الإقرارات الضريبية الرسمية لعدة دول (Multi-Country VAT Returns)](#ج-الإقرارات-الضريبية-الرسمية-لعدة-دول)
   - [د. إدارة البيانات الرئيسية للضرائب (Categories, Rates, Groups CRUD)](#د-إدارة-البيانات-الرئيسية-للضرائب)
   - [هـ. محرك ضرائب نقاط البيع والتقارير (POS Tax & Reports)](#هـ-محرك-ضرائب-نقاط-البيع-والتقارير)
   - [و. سياسات وشرائح ضريبة الدخل على الرواتب (HR Payroll Income Tax)](#و-سياسات-وشرائح-ضريبة-الدخل-على-الرواتب)
5. [نماذج البيانات من جانب العميل (TypeScript Interfaces)](#5-نماذج-البيانات-من-جانب-العميل-typescript-interfaces)
6. [إرشادات وتوصيات تجربة المستخدم في الواجهات (Frontend UX & Best Practices)](#6-إرشادات-وتوصيات-تجربة-المستخدم-في-الواجهات)

---

## 1. نظرة عامة وهيكلية المحرك

محرك الضرائب في **ThinkOnErp** هو محرك مالي موحد، تم تصميمه ليعالج كافة المتطلبات الضريبية المعقدة في الشرق الأوسط والعالم:
* **حسابات دقيقة للضرائب:** دعم الضرائب المضافة، الصفرية، المعفاة، وتلك الخارجة عن النطاق، والضرائب المركبة.
* **دعم الفاتورة الشاملة وغير الشاملة:** التبديل بنقرة زر واحدة بين الأسعار الشاملة للضريبة وغير الشاملة مع الحفاظ على الدقة الرياضية حتى 3 خانات عشرية.
* **الامتثال للفوترة الإلكترونية (ZATCA):** توليد باركود الفاتورة المشفر بصيغة TLV Base64 المعتمدة للمرحلة الأولى والثانية لهيئة الزكاة والضريبة والجمارك.
* **إقرارات ضريبية رسمية لـ 5 دول وأنظمة:** (السعودية ZATCA، الأردن ISTD، مصر ETA، بريطانيا والاتحاد الأوروبي HMRC/EU، وإقرار عالمي مرن).
* **تكامل كامل مع نقاط البيع (POS):** سرعة فائقة وإمكانية العمل مع سلة المشتريات وطباعة الإيصالات وتقارير ضريبة المخرجات.
* **تكامل مع الرواتب (HR Payroll):** احتساب ضريبة كسب العمل / الدخل التصاعدية للموظفين بنظام الشرائح والإعفاءات.

---

## 2. معايير الاتصال والمصادقة

### صيغة الترويسات (HTTP Headers)
يجب إرسال هذه الترويسات مع جميع الطلبات:
```http
Authorization: Bearer <JWT_ACCESS_TOKEN>
Content-Type: application/json
Accept: application/json
```

### بنية الاستجابة الموحدة (ApiResponse Envelope)
ترجع جميع الـ APIs كائن استجابة موحد بالشكل التالي:
```json
{
  "success": true,
  "message": "Operation completed successfully.",
  "responseCode": "SYS001",
  "data": { ... },
  "errors": [],
  "statusCode": 200,
  "timestamp": "2026-11-04T10:30:00Z"
}
```

---

## 3. كيف يعمل محرك احتساب الضرائب؟

### أ. السعر غير الشامل للضريبة (Exclusive Tax - الأكثر شيوعاً في B2B):
عندما يحدد المستخدم أن السعر لا يشمل الضريبة (`isInclusive: false`):
1. **المبلغ الإجمالي للبند (Gross Amount):**
   $$\text{Gross} = \text{Quantity} \times \text{UnitPrice}$$
2. **الصافي الخاضع للضريبة (Net Base):**
   $$\text{NetBase} = \text{Gross} - \text{DiscountAmount}$$
3. **مبلغ الضريبة (Tax Amount):**
   $$\text{TaxAmount} = \text{NetBase} \times \left(\frac{\text{RatePercent}}{100}\right)$$
4. **إجمالي السطر (Line Total):**
   $$\text{LineTotal} = \text{NetBase} + \text{TaxAmount}$$

---

### ب. السعر الشامل للضريبة (Inclusive Tax - الأكثر شيوعاً في نقاط البيع والتجزئة B2C):
عندما تكون الأسعار المدخلة من الكاشير أو المستخدم شاملة للضريبة (`isInclusive: true`):
1. **المبلغ الإجمالي بعد الخصم:**
   $$\text{NetGross} = (\text{Quantity} \times \text{UnitPrice}) - \text{DiscountAmount}$$
2. **فك واستخراج الصافي قبل الضريبة (Net Base):**
   $$\text{NetBase} = \frac{\text{NetGross}}{1 + \left(\frac{\text{RatePercent}}{100}\right)}$$
3. **مبلغ الضريبة المفكوك:**
   $$\text{TaxAmount} = \text{NetGross} - \text{NetBase}$$
4. **إجمالي السطر:**
   $$\text{LineTotal} = \text{NetGross}$$

---

### ج. تعدد العملات وأسعار الصرف (Multi-Currency Support)
إذا كانت الفاتورة بعملة أجنبية (مثل USD) والعملة المحلية للشركة هي SAR:
* يرسل الفرونت إند حقل `exchangeRate: 3.75`.
* يقوم المحرك بحساب كل سطر والإجماليات بالعملتين (الأصلية والمحلية):
  * `LocalNetBaseAmount = NetBaseAmount * exchangeRate`
  * `LocalTaxAmount = TaxAmount * exchangeRate`
  * `LocalGrandTotalAmount = LineTotalAmount * exchangeRate`

---

### د. اقتراح القيود المحاسبية الآلية (Suggested GL Journal Entries)
يرجع المحرك دائماً مصفوفة `suggestedGlLines` متوازنة (المدين = الدائن) يمكن استخدامها لمعاينة القيد المحاسبي في واجهة المستخدم قبل الحفظ:
* **في فواتير المبيعات (`isSales: true`):**
  * مدين: حساب العميل / المدينون (`112101`) بإجمالي الفاتورة شاملة الضريبة.
  * دائن: حساب الإيرادات (`410101`) بصافي المبيعات قبل الضريبة.
  * دائن: حساب ضريبة القيمة المضافة المستحقة / المخرجات (`213101`) بمبلغ الضريبة.
* **في فواتير المشتريات (`isSales: false`):**
  * مدين: حساب المشتريات أو المصروفات (`510101`) بالصافي.
  * مدين: حساب ضريبة القيمة المضافة المدخلات / القابلة للاسترداد (`113101`) بمبلغ الضريبة.
  * دائن: حساب المورد / الدائنون (`211101`) بالإجمالي المستحق.

---

## 4. مرجع واجهات برمجة التطبيقات (API Reference)

### أ. محرك الاحتساب والفوترة اللحظية

#### 1. احتساب ضرائب الفاتورة
* **المسار:** `POST /api/accounting/tax/calculate` (أو لنقاط البيع: `POST /api/pos/tax/calculate`)
* **الاستخدام:** يتم استدعاؤه عند تعديل الأصناف، الكميات، الأسعار، أو تغيير حالة "السعر شامل الضريبة".

##### كائن الطلب (Request Body):
```json
{
  "isSales": true,
  "isInclusive": false,
  "exchangeRate": 1.0,
  "lines": [
    {
      "lineNumber": 1,
      "itemCode": "ITM-001",
      "itemName": "لابتوب أعمال Dell",
      "quantity": 2,
      "unitPrice": 3000.0,
      "discountAmount": 200.0,
      "taxRateCode": "VAT_15"
    },
    {
      "lineNumber": 2,
      "itemCode": "ITM-002",
      "itemName": "خدمات دعم فني دولية",
      "quantity": 1,
      "unitPrice": 500.0,
      "discountAmount": 0.0,
      "taxRateCode": "VAT_0"
    }
  ]
}
```

##### كائن الاستجابة (Response Body):
```json
{
  "success": true,
  "data": {
    "subtotalAmount": 6300.000,
    "totalDiscountAmount": 200.000,
    "totalTaxAmount": 870.000,
    "grandTotalAmount": 7170.000,
    "localSubtotalAmount": 6300.000,
    "localTotalTaxAmount": 870.000,
    "localGrandTotalAmount": 7170.000,
    "lines": [
      {
        "lineNumber": 1,
        "itemCode": "ITM-001",
        "quantity": 2.0,
        "unitPrice": 3000.0,
        "grossAmount": 6000.000,
        "discountAmount": 200.000,
        "netBaseAmount": 5800.000,
        "taxPercent": 15.0,
        "taxAmount": 870.000,
        "lineTotalAmount": 6670.000,
        "localNetBaseAmount": 5800.000,
        "localTaxAmount": 870.000,
        "localLineTotalAmount": 6670.000,
        "taxRateCode": "VAT_15",
        "taxRateNameLocal": "ضريبة القيمة المضافة 15%",
        "glAccountCode": "213101"
      },
      {
        "lineNumber": 2,
        "itemCode": "ITM-002",
        "quantity": 1.0,
        "unitPrice": 500.0,
        "grossAmount": 500.000,
        "discountAmount": 0.000,
        "netBaseAmount": 500.000,
        "taxPercent": 0.0,
        "taxAmount": 0.000,
        "lineTotalAmount": 500.000,
        "localNetBaseAmount": 500.000,
        "localTaxAmount": 0.000,
        "localLineTotalAmount": 500.000,
        "taxRateCode": "VAT_0",
        "taxRateNameLocal": "ضريبة بنسبة الصفر 0%",
        "glAccountCode": null
      }
    ],
    "taxSummary": [
      {
        "taxRateCode": "VAT_0",
        "taxRateNameLocal": "ضريبة بنسبة الصفر 0%",
        "ratePercent": 0.0,
        "totalBaseAmount": 500.000,
        "totalTaxAmount": 0.000,
        "localTotalBaseAmount": 500.000,
        "localTotalTaxAmount": 0.000,
        "glAccountCode": null
      },
      {
        "taxRateCode": "VAT_15",
        "taxRateNameLocal": "ضريبة القيمة المضافة 15%",
        "ratePercent": 15.0,
        "totalBaseAmount": 5800.000,
        "totalTaxAmount": 870.000,
        "localTotalBaseAmount": 5800.000,
        "localTotalTaxAmount": 870.000,
        "glAccountCode": "213101"
      }
    ],
    "suggestedGlLines": [
      {
        "accountCode": "410101",
        "accountNameLocal": "إيرادات المبيعات العامة",
        "debit": 0.0,
        "credit": 6300.0,
        "localDebit": 0.0,
        "localCredit": 6300.0,
        "description": "صافي مبيعات خاضعة للضريبة"
      },
      {
        "accountCode": "213101",
        "accountNameLocal": "حساب ضريبة القيمة المضافة المستحقة (المخرجات)",
        "debit": 0.0,
        "credit": 870.0,
        "localDebit": 0.0,
        "localCredit": 870.0,
        "description": "ضريبة القيمة المضافة المستحقة على المبيعات"
      },
      {
        "accountCode": "112101",
        "accountNameLocal": "حساب العملاء / المدينون التجاريون",
        "debit": 7170.0,
        "credit": 0.0,
        "localDebit": 7170.0,
        "localCredit": 0.0,
        "description": "إجمالي الفاتورة المستحق على العميل"
      }
    ]
  }
}
```

---

### ب. الفوترة الإلكترونية ورمز الاستجابة السريع (ZATCA E-Invoicing QR)

#### 2. توليد رمز الفاتورة الإلكترونية
* **المسار:** `POST /api/accounting/tax/zatca-qr` (أو لنقاط البيع: `POST /api/pos/tax/zatca-qr`)
* **الاستخدام:** عند حفظ أو طباعة الفاتورة الضريبية المبسطة أو إيصال الكاشير.

##### كائن الطلب (Request Body):
```json
{
  "sellerName": "شركة ثينك أون للتقنية والتجارة",
  "taxRegistrationNumber": "310123456700003",
  "invoiceTimestamp": "2026-11-04T12:00:00Z",
  "invoiceTotalWithVat": 7170.00,
  "vatTotal": 870.00
}
```

##### كائن الاستجابة (Response Body):
```json
{
  "success": true,
  "data": {
    "qrBase64": "AQ3YtNix2YPYqSDYqtmH...[Base64 String]...",
    "sellerName": "شركة ثينك أون للتقنية والتجارة",
    "taxRegistrationNumber": "310123456700003",
    "invoiceTimestamp": "2026-11-04T12:00:00Z",
    "invoiceTotalWithVat": 7170.00,
    "vatTotal": 870.00
  }
}
```

##### طريقة العرض في الواجهة (Frontend Implementation):
في React أو Angular أو Vue، يتم استخدام مكتبة الباركود (مثل `qrcode.react`) لتصيير القيمة المرجعة مباشرة:
```tsx
import QRCode from 'qrcode.react';

// تزويد المكون بنص الـ qrBase64 مباشرة:
<QRCode value={response.data.qrBase64} size={160} level="M" />
```

---

### ج. الإقرارات الضريبية الرسمية لعدة دول (Multi-Country VAT Returns)

#### 3. استعراض قوالب الإقرارات المتاحة
* **المسار:** `GET /api/accounting/tax/declaration/templates`
* **الاستخدام:** لتعبئة قائمة منسدلة (Dropdown) تتيح للمحاسب اختيار الدولة / النظام الضريبي لإقراره.
* **الاستجابة:** قائمة بالكائنات تحتوي على:
  * `templateCode`: الكود البرمجي للقالب (مثل `GCC_ZATCA_16`, `JO_SALES_TAX`, `EG_VAT_FORM_10`).
  * `countryCode`: رمز الدولة (`SA`, `JO`, `EG`, `GB`, `GLOBAL`).
  * `flagEmoji`: علم الدولة (`🇸🇦`, `🇯🇴`, `🇪🇬`, `🇬🇧`, `🌐`).
  * `titleAr` / `titleEn`: عنوان الإقرار الرسمي.
  * `taxAuthorityNameLocal`: اسم الهيئة المسؤولة (مثل: هيئة الزكاة والضريبة والجمارك، دائرة ضريبة الدخل والمبيعات الأردنية).
  * `sections`: مصفوفة الأقسام والخانات الرسمية (Boxes).

#### 4. جلب تفاصيل وبنود قالب محدد
* **المسار:** `GET /api/accounting/tax/declaration/templates/{templateCode}`
* **مثال:** `GET /api/accounting/tax/declaration/templates/JO_SALES_TAX`

#### 5. توليد تقرير الإقرار الضريبي الرسمي
* **المسار:** `GET /api/accounting/tax/vat-declaration` (أو `GET /api/accounting/tax/declaration`)
* **معاملات الاستعلام (Query Parameters):**
  * `fromDate` (مطلوب): تاريخ البداية (مثل: `2026-07-01`).
  * `toDate` (مطلوب): تاريخ النهاية (مثل: `2026-09-30`).
  * `branchId` (اختياري): رقم الفرع، أو تركه فارغاً لجميع الفروع.
  * `templateCode` (اختياري): كود القالب المطلوب (الافتراضي هو `GCC_ZATCA_16`).

##### نموذج استجابة الإقرار الضريبي:
```json
{
  "success": true,
  "data": {
    "templateCode": "GCC_ZATCA_16",
    "countryCode": "SA",
    "templateTitleAr": "إقرار ضريبة القيمة المضافة - هيئة الزكاة والضريبة والجمارك",
    "currencyCode": "SAR",
    "fromDate": "2026-07-01T00:00:00",
    "toDate": "2026-09-30T23:59:59",
    "salesBoxes": [
      {
        "boxNumber": 1,
        "boxKey": "STANDARD_RATED_SALES",
        "titleAr": "المبيعات الخاضعة للنسبة الأساسية (15%)",
        "baseAmount": 150000.00,
        "adjustmentAmount": 0.00,
        "vatAmount": 22500.00
      },
      {
        "boxNumber": 4,
        "boxKey": "ZERO_RATED_SALES",
        "titleAr": "المبيعات الخاضعة لنسبة الصفر",
        "baseAmount": 20000.00,
        "adjustmentAmount": 0.00,
        "vatAmount": 0.00
      }
    ],
    "totalSalesBaseAmount": 170000.00,
    "totalOutputVatAmount": 22500.00,
    "purchaseBoxes": [
      {
        "boxNumber": 7,
        "boxKey": "STANDARD_RATED_DOMESTIC_PURCHASES",
        "titleAr": "المشتريات المحلية الخاضعة للنسبة الأساسية (15%)",
        "baseAmount": 80000.00,
        "adjustmentAmount": 0.00,
        "vatAmount": 12000.00
      }
    ],
    "totalPurchasesBaseAmount": 80000.00,
    "totalInputVatAmount": 12000.00,
    "netVatDueAmount": 10500.00,
    "netVatStatus": "مستحق السداد للهيئة"
  }
}
```

---

### د. إدارة البيانات الرئيسية للضرائب

#### فئات الضرائب (Tax Categories)
| الطريقة | المسار | الوظيفة |
| :--- | :--- | :--- |
| `GET` | `/api/accounting/tax/categories` | جلب جميع الفئات (`?includeInactive=false`) |
| `GET` | `/api/accounting/tax/categories/{id}` | جلب فئة برقمها التعريفي |
| `POST` | `/api/accounting/tax/categories` | إنشاء فئة جديدة |
| `PUT` | `/api/accounting/tax/categories/{id}` | تحديث فئة |
| `DELETE` | `/api/accounting/tax/categories/{id}` | حذف فئة |

##### كائن إنشاء فئة ضريبية (`CreateTaxCategoryDto`):
```json
{
  "categoryCode": "VAT",
  "nameLocal": "ضريبة القيمة المضافة",
  "nameEn": "Value Added Tax",
  "description": "فئة ضريبة القيمة المضافة العامة",
  "displayOrder": 1
}
```

---

#### نسب الضرائب (Tax Rates)
| الطريقة | المسار | الوظيفة |
| :--- | :--- | :--- |
| `GET` | `/api/accounting/tax/rates` | جلب كافة نسب الضرائب المعرفة بالنظام |
| `GET` | `/api/accounting/tax/rates/{id}` | جلب نسبة بالـ ID |
| `GET` | `/api/accounting/tax/rates/code/{code}` | جلب نسبة بالكود (مثل `VAT_15`) |
| `POST` | `/api/accounting/tax/rates` | إنشاء نسبة ضريبية جديدة |
| `PUT` | `/api/accounting/tax/rates/{id}` | تحديث نسبة ضريبية |
| `DELETE` | `/api/accounting/tax/rates/{id}` | حذف نسبة ضريبية |

##### كائن إنشاء نسبة ضريبية (`CreateTaxRateDto`):
```json
{
  "taxRateCode": "VAT_15",
  "taxCategoryId": 1,
  "nameLocal": "ضريبة القيمة المضافة 15%",
  "nameEn": "VAT Standard 15%",
  "ratePercent": 15.0,
  "rateType": "PERCENTAGE",
  "salesTaxGlAccountCode": "213101",
  "purchaseTaxGlAccountCode": "113101",
  "isExempt": false,
  "isZeroRated": false,
  "exemptionReasonCode": null,
  "displayOrder": 1,
  "description": "النسبة الأساسية المعيارية للضريبة"
}
```

---

#### مجموعات الضرائب (Tax Groups)
تُستخدم لتطبيق أكثر من ضريبة على نفس الصنف بتسلسل معين (مثل: ضريبة مبيعات 16% + ضريبة بلدية 2%).
| الطريقة | المسار | الوظيفة |
| :--- | :--- | :--- |
| `GET` | `/api/accounting/tax/groups` | جلب جميع المجموعات الضريبية |
| `GET` | `/api/accounting/tax/groups/{id}` | جلب تفاصيل مجموعة وبنودها |
| `POST` | `/api/accounting/tax/groups` | إنشاء مجموعة ضريبية وتحديد نسبها |
| `DELETE` | `/api/accounting/tax/groups/{id}` | حذف مجموعة ضريبية |

##### كائن إنشاء مجموعة ضريبية (`CreateTaxGroupDto`):
```json
{
  "groupCode": "GRP_MEALS",
  "nameLocal": "ضرائب قطاع المطاعم والمأكولات",
  "nameEn": "Restaurant & Dining Taxes",
  "description": "تطبيق ضريبة القيمة المضافة مع رسوم البلدية",
  "items": [
    {
      "taxRateId": 1,
      "applicationOrder": 1,
      "isCompound": false
    },
    {
      "taxRateId": 4,
      "applicationOrder": 2,
      "isCompound": true
    }
  ]
}
```

---

### هـ. محرك ضرائب نقاط البيع والتقارير (POS Tax & Reports)

* **محرك الـ POS المخصص:**
  تعمل نفس العمليات السابقة تحت مسار `/api/pos/tax/...` لتسهيل استدعائها من تطبيقات الكاشير المعزولة وشاشات الـ POS.
* **تقرير ملخص الضرائب لنقاط البيع:**
  * **المسار:** `GET /api/pos/reports/tax-summary?branchId=1&fromDate=2026-11-01&toDate=2026-11-04`
  * **الوظيفة:** يعرض للكاشير ومدير المعرض تحليلاً شاملاً لضرائب المبيعات المحصلة خلال اليوم أو الوردية.
* **تصدير تقرير الضرائب كملف PDF مباشر:**
  * **المسار:** `GET /api/pos/reports/tax-summary/pdf?branchId=1&fromDate=2026-11-01&toDate=2026-11-04`
  * **الترويسة المرجعة:** `Content-Type: application/pdf`
  * **الاستخدام في الفرونت إند:** فتح الملف في نافذة جديدة أو تحميله كـ `Blob`:
    ```typescript
    window.open(`${API_BASE_URL}/api/pos/reports/tax-summary/pdf?branchId=${bId}`, '_blank');
    ```

---

### و. سياسات وشرائح ضريبة الدخل على الرواتب (HR Payroll Income Tax)

> المسار الأساسي: `/api/hr/payroll-policies` | الصلاحيات: `hr-payroll-policies`

#### 6. استرجاع سياسات ضريبة الدخل
* **المسار:** `GET /api/hr/payroll-policies/tax?companyId={companyId}`
* **الاستخدام:** شاشات إعدادات مسير الرواتب والموارد البشرية لعرض سلم الضريبة الحالي.

#### 7. إنشاء سياسة ضريبة دخل وشرائح تصاعدية
* **المسار:** `POST /api/hr/payroll-policies/tax`

##### كائن الطلب (Request Body):
```json
{
  "companyId": 1,
  "code": "JO_INCOME_TAX_2026",
  "nameAr": "ضريبة الدخل الأردنية - قانون 2026",
  "nameEn": "Jordan Income Tax Law 2026",
  "effectiveFrom": "2026-01-01",
  "effectiveTo": null,
  "personalExemptionSelf": 9000.0,
  "personalExemptionDependent": 9000.0,
  "nationalContribThreshold": 200000.0,
  "nationalContribRate": 1.0,
  "isSscTaxDeductible": true,
  "calculationFrequency": "MONTHLY",
  "brackets": [
    {
      "bracketOrder": 1,
      "lowerLimit": 0.0,
      "upperLimit": 5000.0,
      "ratePercent": 5.0,
      "description": "الشريحة الأولى 5%"
    },
    {
      "bracketOrder": 2,
      "lowerLimit": 5000.01,
      "upperLimit": 10000.0,
      "ratePercent": 10.0,
      "description": "الشريحة الثانية 10%"
    },
    {
      "bracketOrder": 3,
      "lowerLimit": 10000.01,
      "upperLimit": 15000.0,
      "ratePercent": 15.0,
      "description": "الشريحة الثالثة 15%"
    },
    {
      "bracketOrder": 4,
      "lowerLimit": 15000.01,
      "upperLimit": 20000.0,
      "ratePercent": 20.0,
      "description": "الشريحة الرابعة 20%"
    },
    {
      "bracketOrder": 5,
      "lowerLimit": 20000.01,
      "upperLimit": null,
      "ratePercent": 25.0,
      "description": "الشريحة الخامسة وما فوق 25%"
    }
  ]
}
```

---

## 5. نماذج البيانات من جانب العميل (TypeScript Interfaces)

يمكن لمطور الواجهة الأمامية نسخ هذه التعريفات مباشرة إلى ملف Types داخل مشروع Angular / React / Vue (`tax.models.ts`):

```typescript
// ==========================================
// Tax Engine Calculation Types
// ==========================================

export interface TaxCalculationRequest {
  isSales: boolean;
  isInclusive: boolean;
  exchangeRate: number;
  lines: TaxCalculationLineRequest[];
}

export interface TaxCalculationLineRequest {
  lineNumber: number;
  itemCode?: string;
  itemName?: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  taxRateCode?: string;
  taxRateId?: number;
}

export interface TaxCalculationResult {
  subtotalAmount: number;
  totalDiscountAmount: number;
  totalTaxAmount: number;
  grandTotalAmount: number;
  localSubtotalAmount: number;
  localTotalTaxAmount: number;
  localGrandTotalAmount: number;
  lines: TaxCalculationLineResult[];
  taxSummary: TaxSummaryItem[];
  suggestedGlLines: SuggestedGlJournalLine[];
}

export interface TaxCalculationLineResult {
  lineNumber: number;
  itemCode?: string;
  quantity: number;
  unitPrice: number;
  grossAmount: number;
  discountAmount: number;
  netBaseAmount: number;
  taxPercent: number;
  taxAmount: number;
  lineTotalAmount: number;
  localNetBaseAmount: number;
  localTaxAmount: number;
  localLineTotalAmount: number;
  taxRateCode: string;
  taxRateNameLocal: string;
  glAccountCode?: string;
}

export interface TaxSummaryItem {
  taxRateCode: string;
  taxRateNameLocal: string;
  ratePercent: number;
  totalBaseAmount: number;
  totalTaxAmount: number;
  localTotalBaseAmount: number;
  localTotalTaxAmount: number;
  glAccountCode?: string;
}

export interface SuggestedGlJournalLine {
  accountCode: string;
  accountNameLocal: string;
  debit: number;
  credit: number;
  localDebit: number;
  localCredit: number;
  description: string;
}

// ==========================================
// ZATCA E-Invoicing QR Code Types
// ==========================================

export interface ZatcaQrRequest {
  sellerName: string;
  taxRegistrationNumber: string;
  invoiceTimestamp: string; // ISO 8601 string
  invoiceTotalWithVat: number;
  vatTotal: number;
}

export interface ZatcaQrResult {
  qrBase64: string;
  sellerName: string;
  taxRegistrationNumber: string;
  invoiceTimestamp: string;
  invoiceTotalWithVat: number;
  vatTotal: number;
}

// ==========================================
// Tax Declaration & VAT Return Types
// ==========================================

export interface TaxDeclarationTemplate {
  templateCode: string;
  countryCode: string;
  countryNameLocal: string;
  countryNameEn: string;
  flagEmoji: string;
  titleAr: string;
  titleEn: string;
  taxAuthorityNameLocal: string;
  taxAuthorityNameEn: string;
  defaultCurrencyCode: string;
  filingFrequency: string;
  sections: TaxDeclarationTemplateSection[];
}

export interface TaxDeclarationTemplateSection {
  sectionKey: string;
  titleAr: string;
  titleEn: string;
  boxes: TaxDeclarationTemplateBox[];
}

export interface TaxDeclarationTemplateBox {
  boxNumber: number;
  boxKey: string;
  titleAr: string;
  titleEn: string;
  isCalculated: boolean;
  calculationFormula?: string;
}

export interface VatDeclarationResult {
  templateCode: string;
  countryCode: string;
  templateTitleAr: string;
  templateTitleEn: string;
  taxAuthorityNameLocal: string;
  currencyCode: string;
  branchId?: number;
  branchNameLocal: string;
  fromDate: string;
  toDate: string;
  generatedAt: string;
  salesBoxes: VatBoxItem[];
  totalSalesBaseAmount: number;
  totalOutputVatAmount: number;
  purchaseBoxes: VatBoxItem[];
  totalPurchasesBaseAmount: number;
  totalInputVatAmount: number;
  netVatDueAmount: number;
  netVatStatus: string;
}

export interface VatBoxItem {
  boxNumber: number;
  boxKey: string;
  titleAr: string;
  titleEn: string;
  baseAmount: number;
  adjustmentAmount: number;
  vatAmount: number;
}

// ==========================================
// Tax Master Data Types
// ==========================================

export interface TaxRate {
  id: number;
  taxRateCode: string;
  taxCategoryId: number;
  categoryCode: string;
  categoryNameLocal: string;
  nameLocal: string;
  nameEn: string;
  ratePercent: number;
  rateType: string;
  salesTaxGlAccountCode?: string;
  purchaseTaxGlAccountCode?: string;
  isExempt: boolean;
  isZeroRated: boolean;
  exemptionReasonCode?: string;
  isActive: boolean;
}
```

---

## 6. إرشادات وتوصيات تجربة المستخدم في الواجهات

1. **التحميل المسبق والتخزين المؤقت (Caching Tax Rates):**
   * عند فتح شاشة الفواتير أو شاشة الكاشير، قم باستدعاء `GET /api/accounting/tax/rates` مرة واحدة وتخزينها في حالة التطبيق (Redux / Pinia / NgRx).
   * املأ قائمة اختيار الضريبة في كل سطر من الفاتورة بهذه النسب.

2. **الاستدعاء اللحظي مع الـ Debounce:**
   * عند إدخال المستخدم للكميات والأسعار، استخدم `debounceTime(250ms)` لاستدعاء `POST /calculate`.
   * في نقاط البيع (POS Offline / Standalone): يمكن تطبيق المعادلة الحسابية محلياً (الموضحة في القسم 3) لإظهار الإجماليات فوراً دون انتظار استجابة الشبكة، مع مزامنتها مع الـ API عند الحفظ.

3. **التبديل بين "السعر شامل الضريبة" و "غير شامل":**
   * وضع مفتاح تبديل واضح أعلى الفاتورة (Switch: "الأسعار شاملة ضريبة القيمة المضافة").
   * عند تبديل المفتاح، يتم إعادة إرسال الطلب مع `isInclusive: true/false` وإعادة تحديث حقول الصافي والضريبة فوراً دون تغيير السعر الأساسي الذي أدخله المستخدم.

4. **طباعة إيصالات نقاط البيع والـ ZATCA QR:**
   * استخدم `ZatcaQrResult.qrBase64` لرسم كود الاستجابة السريع في أسفل الفاتورة بحجم لا يقل عن `120x120px` لضمان سهولة مسحه عبر أجهزة وتطبيقات هيئة الزكاة والضريبة والجمارك وتطبيقات المستهلكين.

5. **شاشة الإقرار الضريبي:**
   * توفير فلتر للتاريخ (سنة وربع سنوي أو شهر).
   * عرض بطاقة بارزة أعلى الصفحة لـ `netVatDueAmount` بلون أخضر إذا كانت مستردة ولون أحمر/برتقالي إذا كانت مستحقة السداد.
   * عرض جدولين منفصلين لخانات المبيعات (ضريبة المخرجات) وخانات المشتريات (ضريبة المدخلات) مطابقة تماماً لشاشات الهيئات الرسمية.
