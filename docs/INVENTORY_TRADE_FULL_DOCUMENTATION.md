# ThinkOn ERP — التوثيق الشامل الكامل لنظام إدارة المخزون والمستندات التجارية والتجميع (v2.4 Final)

---

## 📑 فهرس المحتويات (Table of Contents)

1. [المقدمة والرؤية المعمارية (Architectural Overview & Multi-Tenancy Design)](#1-المقدمة-والرؤية-المعمارية)
2. [بطاقة الصنف والمجموعات المزدوجة (Item Master & Dual Groups Architecture)](#2-بطاقة-الصنف-والمجموعات-المزدوجة)
3. [الكرت التجميعي ووصفات التركيب (Bill of Materials - BOM)](#3-الكرت-التجميعي-ووصفات-التركيب)
4. [المستودعات والمناطق والرفوف (Warehouses, Zones & Bins Hierarchy)](#4-المستودعات-والمناطق-والرفوف)
5. [المستندات التجارية وحركات المخزون الموحدة (Universal Trade Documents Engine)](#5-المستندات-التجارية-وحركات-المخزون-الموحدة)
6. [الأرصدة الافتتاحية للمستودع والفرع والمادة (Opening Balances)](#6-الأرصدة-الافتتاحية)
7. [محرك التكاليف والمتاح للالتزام والمطابقة المحاسبية (Costing, ATP & GL Reconciliation)](#7-محرك-التكاليف-والمتاح-والمطابقة)
8. [دليل الـ RESTful API والمخططات البرمجية الكاملة (Complete Endpoints Matrix)](#8-دليل-الـ-api-والمخططات)
9. [طبقة التحقق وقاموس الاستجابات (Validation Layer & Response Codes)](#9-طبقة-التحقق-وقاموس-الاستجابات)

---

## 1. المقدمة والرؤية المعمارية

يعتمد نظام **ThinkOn ERP** على بنية متعددة الشركات والمستأجرين (**Multi-Tenant Schema-per-Company Architecture**) في قاعدة بيانات **Oracle Enterprise**:

```
+-----------------------------------------------------------------------------------+
|                            Oracle Database Instance (FREE)                        |
+-----------------------------------------------------------------------------------+
                                         |
     +-----------------------------------+-----------------------------------+
     |                                   |                                   |
     v                                   v                                   v
+-----------------------+     +-----------------------+     +-----------------------+
|  THINKON_ERP (Master) |     | DEV_TEMPLATE (Master) |     | THINKONERP_* (Tenant) |
|                       |     |                       |     |                       |
| - SYS_COMPANY         |     | - Blueprint Schema    |     | - INV_ITEM            |
| - SYS_SUPER_ADMIN     |     | - Contains all 23     |     | - INV_ITEM_GROUP      |
| - SYS_CODE (31)       |     |   tables, sequences   |     | - INV_BOM_HEADER/LINE |
| - SYS_VALIDATION_RULE |     |   and seeds used for  |     | - TRX_DOCUMENT_*      |
| - Global Synonyms     |     |   auto-cloning new    |     | - INV_WAREHOUSE/ZONE  |
| - 0 Inventory Tables  |     |   companies.          |     | - INV_STOCK_LEDGER    |
+-----------------------+     +-----------------------+     +-----------------------+
```

### المبادئ المعمارية الحاكمة:
1. **عزل بيانات الشركات (Schema Isolation):** جداول المخزون والمستندات والفواتير والـ BOM والتوجيه المحاسبي تخص سكيمات الشركات فقط (`THINKONERP_<CODE>`) وسكيمة القالب (`DEV_TEMPLATE`).
2. **السكيمة الرئيسية (`THINKON_ERP`):** مسؤولة فقط عن إدارة النظام والشركات والعملات وقاموس الأكواد (`SYS_CODE`) وقواعد التحقق الديناميكية (`SYS_FIELD_VALIDATION_RULE`).
3. **التحكم بالجلسة (Tenant Interceptor):** عند طلب أي مستخدم، يتم تحديد سكيمة شركته تلقائياً وتوجيه استعلامات EF Core إليها.

---

## 2. بطاقة الصنف والمجموعات المزدوجة

### 2.1 الهيكلية الثنائية للمجموعات (Dual Groups Architecture):
تم تصميم شجرة المجموعات لتدعم تصنيفاً ثنائياً لكل صنف:
* **المجموعة الرئيسية (`Main Group`):** تحدد التصنيف الاستراتيجي وحسابات الأستاذ العام الافتراضية (حساب المخزون الرقابي `GL_CONTROL_ACCOUNT`، حساب التكلفة `GL_COGS_ACCOUNT`، وحساب الإيراد `GL_REVENUE_ACCOUNT`).
* **المجموعة الفرعية (`Sub Group`):** تحدد التصنيف التفصيلي الداخلي.

### 2.2 أنواع الأصناف المدعومة (`ItemType`):
* `Stock`: صنف مخزني ملموس عادي يدخل في الجرد والتكلفة وحسابات المستودع.
* `Kit`: طقم مبيعات تجميعي؛ لا يتم تخزينه ككتلة واحدة بل ينفجر تلقائياً في فواتير المبيعات إلى مواده المكونة.
* `Assembly`: منتج تجميعي/تصنيعي مركب يتم تصنيعه عبر أمر تشغيل وله كرت تجميع وتكلفة مستقلة.
* `Service`: صنف خدمي (مثل: أجور نقل، فك وتركيب، صيانة) لا يؤثر على كميات المخزون.
* `NonStock`: صنف استهلاكي مباشر (مستلزمات مكتبية، ضيافة) يرحل كمصروف فوري.

### 2.3 طرق احتساب التكلفة الأربعة (`CostingMethod`):
1. **المتوسط المرجح المتحرك (`WeightedAverage`):**
   $$\text{New Avg Cost} = \frac{(\text{Current Qty} \times \text{Current Avg}) + (\text{Receipt Qty} \times \text{Receipt Cost})}{\text{Current Qty} + \text{Receipt Qty}}$$
2. **الوارد أولاً صادر أولاً (`Fifo`):** بناء طبقات تكلفة مستقلة في جدول `INV_COST_LAYER` واستهلاك الطبقات الأقدم زمنياً عند الصرف.
3. **التكلفة المحددة بالسيريال/الدفعة (`SpecificId`):** احتساب التكلفة الحقيقية الدقيقة للقطعة بناءً على رقم المسلسل أو الدفعة.
4. **التكلفة المعيارية (`Standard`):** تقييم المخزون بسعر ثابت مع ترحيل الفروقات لحساب انحراف أسعار الشراء (`PPV`).

### 2.4 أنظمة التتبع وتعدد الوحدات والباركود:
* **المسلسلات (`Serial Tracking`):** تتبع مسار كل قطعة برقم تسلسلي فريد من الشراء للبيع.
* **التشغيلات والصلاحية (`Lot & Expiry FEFO`):** تتبع رقم الدفعة وتطبيق سياسة **الوارد أولاً ينتهي أولاً (First-Expired, First-Out)**.
* **وحدات القياس المتعددة (`INV_ITEM_UOM`):** تعريف معاملات تحويل متعددة (مثال: حبة، كرتونة = 24 حبة، طبلية = 1200 حبة).
* **الباركودات الدولية (`INV_ITEM_BARCODE`):** دعم EAN-13, Code-128, QR Code مع ربط الباركود بوحدة قياس معينة.

---

## 3. الكرت التجميعي ووصفات التركيب (BOM)

يمكّن نظام **Bill of Materials** من إنشاء بطاقات تجميع للأصناف المركبة وأطقم المبيعات:

### بنية جدول `INV_BOM_HEADER` و `INV_BOM_LINE`:
* **الرأس (Header):** الصنف الأب (`ParentItemId`)، كمية المخرجات (`OutputQty`)، نوع التجميع (`SalesKit` أو `Manufacturing`)، تكلفة العمالة المباشرة (`LaborCost`)، والتكاليف الإضافية (`OverheadCost`).
* **البنود (Lines):** المادة المكونة (`ComponentItemId`)، الكمية المطلوبة (`Quantity`)، نسبة الهالك المسموح (`ScrapPercent`)، حصة المكون من التكلفة (`CostSharePercent`)، وخيار السماح ببديل (`AllowSubstitute`).

### محرك الانفجار التلقائي (Auto-Explosion Engine):
عند إصدار فاتورة مبيعات تحتوي على صنف من نوع `Kit`، يقوم محرك الترحيل تلقائياً بـ:
1. قراءة كرت التجميع الافتراضي للصنف.
2. التحقق من كفاية الأرصدة اللحظية لكافة المواد المكونة في المستودع المحدد.
3. توليد حركات صرف مخزنية للمكونات بنسبة كمية الأطقم المباعة.
4. حساب تكلفة البضاعة المباعة (`COGS`) تلقائياً وإصدار القيود المحاسبية.

---

## 4. المستودعات والمناطق والرفوف (Warehouses Hierarchy)

يدعم النظام هيكلية مكانية ثلاثية الأبعاد لإدارة المستودعات وسلاسل الإمداد:

```
[ الفرع: Branch ]
       |
       v
[ المستودع: INV_WAREHOUSE ] (كود المستودع، الاسم، النوع: رئيسي/فرعي/ترانزيت/افتراضي)
       |
       v
[ منطقة التخزين: INV_ZONE ] (Storage, Receiving, Staging, Quarantine, Damaged, Returns)
       |
       v
[ الرف والخانة: INV_BIN ] (كود الخانة، أقصى وزن مسموح KG، أقصى حجم مسموح M³)
```

---

## 5. المستندات التجارية وحركات المخزون الموحدة

تم توحيد جميع الحركات التجارية والمخزنية في محرك مستندات فائق الأداء (`TrxDocumentService`):

### 5.1 المفتاح الأساسي المركب الرباعي (Composite PK):
```sql
CONSTRAINT PK_DOC_HDR PRIMARY KEY (BRANCH_ID, DOC_YEAR, DOC_TYPE, ID)
```

### 5.2 جدول الأرقام المعتمدة بدلاً من النصوص (`sysCode` Mapping):

#### أ. نوع الطرف (`PartyTypeCode`):
| الرمز الرقمي | الوصف البرمجي | المعنى التجاري |
| :--- | :--- | :--- |
| **0** | `NONE` | بدون طرف خارجي (حركة مخزنية داخلية) |
| **1** | `CUSTOMER` | عميل / زبون |
| **2** | `VENDOR` | مورد / شركة موردة |
| **3** | `BRANCH` | فرع تابع للشركة |
| **4** | `DEPARTMENT` | قسم أو مركز تكلفة داخلي |

#### ب. طريقة الدفع (`PaymentMethodCode`):
| الرمز الرقمي | الوصف البرمجي | المعنى التجاري |
| :--- | :--- | :--- |
| **1** | `CASH` | نقداً (صندوق النقدية) |
| **2** | `CREDIT` | ذمم مدينة / دائنة (آجل) |
| **3** | `BANK` | حوالة أو دفع بنكي |
| **4** | `CHEQUE` | شيكات برسم التحصيل أو الدفع |
| **5** | `MULTI` | طرق دفع متعددة ومختلطة |

#### ج. حالة المستند (`DocStatusCode`):
| الرمز الرقمي | الوصف البرمجي | المعنى التجاري |
| :--- | :--- | :--- |
| **1** | `DRAFT` | مسودة (قابلة للتعديل والحذف بالكامل) |
| **2** | `POSTED` | مرحل نهائياً (غير قابل للتعديل ومقيد محاسبياً ومخزنياً) |
| **3** | `CANCELLED` | ملغي |

### 5.3 أنواع المستندات الموحدة (`DocType`):
* `100`: فواتير المبيعات (Sales Invoices).
* `150`: مردودات المبيعات (Sales Returns).
* `200`: فواتير المشتريات (Purchase Bills).
* `250`: مردودات المشتريات (Purchase Returns).
* `300`: سندات المخزون إدخال/إخراج/إتلاف (Stock Vouchers).
* `350`: التحويلات المخزنية بين الفروع والمستودعات (Stock Transfers).
* `400`: عروض الأسعار وأوامر البيع (Quotations & Sales Orders).
* `450`: طلبات الشراء وأوامر الشراء (Requisitions & Purchase Orders).

---

## 6. الأرصدة الافتتاحية للمخزون (Opening Balances)

* **إنشاء الدفعة (`INV_OPENING_BATCH`):** تسجيل الأرصدة الافتتاحية مقيدة بالفرع والسنة المالية.
* **البنود التفصيلية (`INV_OPENING_LINE`):** إدخال الصنف، المستودع، الرف، الدفعة، تاريخ الانتهاء، الكمية، وسعر التكلفة الأولي.
* **الترحيل المحاسبي اللحظي:** عند ترحيل الدفعة عبر `POST /api/inventory/opening-balances/{id}/post` يقوم النظام بـ:
  * إنشاء سجلات الرصيد الأولي في `INV_STOCK_BALANCE`.
  * تسجيل حركات الدخول في `INV_STOCK_LEDGER`.
  * إنشاء سند قيد افتتاحي في الأستاذ العام: **من حـ/ المخزون (`114301`) إلى حـ/ رأس المال أو الأرباح المدورة (`311101`)**.

---

## 7. محرك التكاليف والمتاح والمطابقة المحاسبية

### 7.1 سجل المخزون التراكمي (`INV_STOCK_LEDGER` - Append-Only):
سجل غير قابل للتعديل أو الحذف، يحفظ الرصيد التراكمي اللحظي للكمية والقيمة بعد كل حركة، مع رقم القيد المحاسبي المولد (`JOURNAL_ENTRY_ID`).

### 7.2 حاسبة المتاح للالتزام الفوري (ATP Engine):
$$\text{ATP} = \text{OnHand} + \text{OnOrder} - \text{Reserved} - \text{SafetyStock}$$

### 7.3 محرك المطابقة والتسوية مع الأستاذ العام (GL Reconciliation):
يقوم بفحص ومطابقة مجموع قيم أرصدة المستودعات مع أرصدة حسابات مراقبة المخزون في شجرة الحسابات (`GL_CONTROL_ACCOUNT`) واستخراج فروقات الجرد فورياً.

---

## 8. دليل الـ RESTful API والمخططات البرمجية الكاملة

جميع الـ Endpoints مصنفة في Swagger تحت تصنيف **`Inventory`**، وتدعم عمليات الـ CRUD الكاملة:

### 1. المستندات والفواتير التجاريّة (`TrxDocumentsController`):
* `POST /api/documents`: إنشاء فاتورة أو مستند مسودة جديد.
* `GET /api/documents/{branchId}/{docYear}/{docType}/{id}`: استرجاع تفاصيل مستند بمفتاحه الرباعي.
* `GET /api/documents`: استرجاع قائمة المستندات مع التصفية والـ Pagination.
* `PUT /api/documents/{branchId}/{docYear}/{docType}/{id}`: تعديل مستند مسودة.
* `DELETE /api/documents/{branchId}/{docYear}/{docType}/{id}`: حذف مستند مسودة.
* `POST /api/documents/{branchId}/{docYear}/{docType}/{id}/post`: الترحيل المالي والمخزني النهائي.

### 2. بطاقة الأصناف (`InvItemsController`):
* `POST /api/inventory/items`: إنشاء صنف جديد.
* `GET /api/inventory/items`: استرجاع قائمة الأصناف.
* `GET /api/inventory/items/{id}`: استرجاع تفاصيل صنف.
* `PUT /api/inventory/items/{id}`: تعديل بيانات صنف.
* `DELETE /api/inventory/items/{id}`: إلغاء تفعيل صنف.
* `POST /api/inventory/items/{id}/uom`: إضافة وحدة قياس فرعية.
* `DELETE /api/inventory/items/{id}/uom/{uomId}`: حذف وحدة قياس فرعية.
* `POST /api/inventory/items/{id}/barcode`: إضافة باركود دولي.
* `DELETE /api/inventory/items/{id}/barcode/{barcodeId}`: حذف باركود.

### 3. مجموعات الأصناف (`InvItemGroupsController`):
* `POST /api/inventory/groups`: إنشاء مجموعة رئيسية أو فرعية.
* `GET /api/inventory/groups`: استرجاع المجموعات مع الـ Pagination.
* `GET /api/inventory/groups/main`: استرجاع المجموعات الرئيسية فقط.
* `GET /api/inventory/groups/{mainGroupId}/sub`: استرجاع المجموعات الفرعية التابعة لمجموعة رئيسية.
* `GET /api/inventory/groups/{id}`: استرجاع تفاصيل مجموعة.
* `PUT /api/inventory/groups/{id}`: تعديل مجموعة.
* `DELETE /api/inventory/groups/{id}`: حذف/إلغاء تفعيل مجموعة.

### 4. الكرت التجميعي (`InvBomController`):
* `POST /api/inventory/bom`: إنشاء كرت تجميعي.
* `GET /api/inventory/bom`: استرجاع كروت التجميع مع الـ Pagination.
* `GET /api/inventory/bom/{id}`: استرجاع تفاصيل كرت تجميع.
* `GET /api/inventory/bom/item/{parentItemId}/default`: استرجاع الكرت التجميعي الافتراضي لصنف أب.
* `GET /api/inventory/bom/item/{parentItemId}`: استرجاع جميع إصدارات التجميع لصنف أب.
* `PUT /api/inventory/bom/{id}`: تعديل كرت تجميع.
* `DELETE /api/inventory/bom/{id}`: حذف كرت تجميع.

### 5. المستودعات والمناطق والرفوف (`InvWarehousesController`):
* `POST /api/inventory/warehouses`: إنشاء مستودع.
* `GET /api/inventory/warehouses?branchId={branchId}`: استرجاع مستودعات فرع معين.
* `GET /api/inventory/warehouses/{id}`: تفاصيل المستودع بمناطقه ورفوفه.
* `PUT /api/inventory/warehouses/{id}`: تعديل مستودع.
* `DELETE /api/inventory/warehouses/{id}`: حذف مستودع.
* `POST /api/inventory/warehouses/{id}/zones`: إضافة منطقة تخزين.
* `PUT /api/inventory/warehouses/zones/{zoneId}`: تعديل منطقة تخزين.
* `DELETE /api/inventory/warehouses/zones/{zoneId}`: حذف منطقة تخزين.
* `POST /api/inventory/warehouses/zones/{zoneId}/bins`: إضافة رف/خانة.
* `PUT /api/inventory/warehouses/bins/{binId}`: تعديل رف/خانة.
* `DELETE /api/inventory/warehouses/bins/{binId}`: حذف رف/خانة.
* `GET /api/inventory/warehouses/{id}/stock`: استرجاع ملخص أرصدة المستودع.

### 6. الأرصدة الافتتاحية (`InvOpeningBalancesController`):
* `POST /api/inventory/opening-balances`: إنشاء دفعة رصيد افتتاحي.
* `GET /api/inventory/opening-balances?branchId={branchId}`: استرجاع دفعات الأرصدة الافتتاحية.
* `GET /api/inventory/opening-balances/{id}`: تفاصيل دفعة الرصيد الافتتاحي.
* `PUT /api/inventory/opening-balances/{id}`: تعديل دفعة مسودة.
* `DELETE /api/inventory/opening-balances/{id}`: حذف دفعة مسودة.
* `POST /api/inventory/opening-balances/{id}/post`: الترحيل النهائي للمخزون والأستاذ العام.

### 7. حركات المخزون والمتاح والتقييم (`InvStockController`):
* `POST /api/inventory/stock/movements`: تسجيل حركة مخزنية مباشرة وترحيلها.
* `GET /api/inventory/stock/movements/{itemId}`: استرجاع سجل حركات صنف.
* `GET /api/inventory/stock/balances`: استرجاع الأرصدة اللحظية للمستودعات.
* `GET /api/inventory/stock/atp/{itemId}`: حساب الكمية المتاحة للالتزام الفوري.
* `GET /api/inventory/stock/reconciliation`: تشغيل تقرير المطابقة مع حسابات الأستاذ العام.

---

## 9. طبقة التحقق وقاموس الاستجابات

### 9.1 التحقق البرمجي (FluentValidation) وقواعد قاعدة البيانات:
* تم إنشاء 7 فئات تحقق مستقلة تغطي كافة الحقول والشروط المحاسبية.
* تم حقن **36 قاعدة تحقق ديناميكية** في جدول `SYS_FIELD_VALIDATION_RULE` لتمكين تخصيص القواعد على مستوى الدولة أو الشركة.

### 9.2 قاموس الاستجابات المعربة (`SYS_CODE` - `CODE_MGR = 31`):
* تم حقن **50 كود استجابة معرّب ومترجم بالإنجليزية** لضمان توحيد رسائل الـ API بين الـ Backend وتطبيقات الـ Web والموبايل.
