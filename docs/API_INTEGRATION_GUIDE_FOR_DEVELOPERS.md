# دليل التكامل الشامل مع واجهات برمجة التطبيقات (API Integration Guide)
**نظام ThinkOnErp — الدليل المرجعي الكامل لمطوري الواجهات الأمامية وتطبيقات الموبايل (Frontend & Mobile Developers)**

---

## فهرس المحتويات (Table of Contents)
1. [المعمارية الأساسية للاتصال (Core Architecture & Base URLs)](#1-المعمارية-الأساسية-للاتصال)
2. [دورة حياة جلسة العمل والمصادقة (Authentication & Session Lifecycle)](#2-دورة-حياة-جلسة-العمل-والمصادقة)
   - تسجيل الدخول للمستخدم العادي (Company Login)
   - تسجيل دخول المشرف العام (SuperAdmin Login)
   - التجديد الصامت للرمز (Silent Refresh Token)
   - تسجيل الخروج (Logout)
3. [الترويسات ومعايير تعدد المستأجرين (Headers & Multi-Tenancy)](#3-الترويسات-ومعايير-تعدد-المستأجرين)
4. [هيكل الاستجابة الموحد (Unified ApiResponse Envelope)](#4-هيكل-الاستجابة-الموحد)
   - استجابة النجاح (Success Response)
   - استجابة الأخطاء والتحقق (Error & Validation Response)
5. [التحقق من الصلاحيات وتوجيه الشاشات (Permissions & Screen Guards)](#5-التحقق-من-الصلاحيات-وتوجيه-الشاشات)
   - جلب الشاشات المسموحة للمستخدم (`/my-screens`)
   - فحص الصلاحيات اللحظي (`/check`)
6. [كود تطبيقي جاهز: معترض الطلبات (Axios / Fetch Interceptor)](#6-كود-تطبيقي-جاهز-معترض-الطلبات)
7. [التعامل مع عمليات البيانات المشتركة (Standard CRUD Pattern)](#7-التعامل-مع-عمليات-البيانات-المشتركة)
8. [التعامل مع تنزيل الملفات والتقارير (PDF & Excel Exports)](#8-التعامل-مع-تنزيل-الملفات-والتقارير)
9. [معالجة الأخطاء وربطها بنماذج الواجهة (Error Handling & Form Binding)](#9-معالجة-الأخطاء-وربطها-بنماذج-الواجهة)

---

## 1. المعمارية الأساسية للاتصال

يعتمد نظام **ThinkOnErp** على معمارية **RESTful APIs** مبنية بتقنية `.NET 9` مع دعم قاعدة بيانات **Oracle Multi-Tenant Schema Isolation**.
* **نوع البروتوكول:** `HTTPS` (أو `HTTP` في بيئة التطوير المحلية).
* **تنسيق تبادل البيانات:** `application/json` (UTF-8).
* **منافذ وبيئات العمل:**
  * التطوير المحلي (Local Development): `http://localhost:5000` أو `https://localhost:5001`.
  * الإنتاج (Production): يتم ضبطها عبر متغير البيئة `VITE_API_BASE_URL` أو `process.env.API_URL`.
* **مستندات Swagger المباشرة:**
  * توثيق المنشآت والشركات: `/swagger/company/swagger.json`
  * توثيق المشرف العام: `/swagger/superadmin/swagger.json`

---

## 2. دورة حياة جلسة العمل والمصادقة

### أ. تسجيل دخول مستخدم المنشأة (Company Login)
* **المسار:** `POST /api/auth/login`
* **نوع الوصول:** عام (لا يتطلب Authorization Header).
* **المدخلات (JSON Body):**
```json
{
  "userName": "admin",
  "password": "Password123!",
  "companyCode": "COMP01",
  "language": 1
}
```
> **ملاحظة اللغات:** `1` = العربية (الافتراضية)، `2` = الإنجليزية، `3` = الفرنسية، `4` = الإسبانية، `5` = التركية، `6` = الألمانية.

* **الاستجابة الناجحة (`200 OK`):**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Login successful.",
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsIn...",
    "expiresAt": "2026-11-04T16:00:00Z",
    "tokenType": "Bearer",
    "refreshToken": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "refreshTokenExpiresAt": "2026-11-11T16:00:00Z",
    "companyCode": "COMP01",
    "companySchema": "TENANT_COMP01",
    "language": 1
  },
  "errors": null,
  "timestamp": "2026-11-04T14:00:00Z",
  "traceId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d"
}
```

---

### ب. تسجيل دخول المشرف العام (SuperAdmin Login)
* **المسار:** `POST /api/auth/super-admin/login`
* **المدخلات (JSON Body):**
```json
{
  "userName": "superadmin",
  "password": "SuperSecurePassword!"
}
```

---

### ج. التجديد الصامت للرمز (Silent Refresh Token)
عند انتهاء صلاحية الـ `accessToken` (يرجع الخادم `401 Unauthorized`)، يجب ألا يُطرد المستخدم لشاشة الدخول؛ بل تقوم الواجهة الأمامية بالنداء الصامت لـ Refresh Token:
* **المسار:** `POST /api/auth/refresh-token`
* **المدخلات:**
```json
{
  "refreshToken": "7c9e6679-7425-40de-944b-e07fc1f90ae7"
}
```
* **الاستجابة:** تُرجع كائن `TokenDto` جديد يحوي `accessToken` و `refreshToken` جديدين.

---

### د. تسجيل الخروج الآمن (Logout)
* **المسار:** `POST /api/auth/logout`
* **الترويسة:** `Authorization: Bearer <accessToken>`
* **الوظيفة:** إبطال جلسة العمل وحذف الـ Refresh Token من الخادم ومسح الذاكرة المحلية في المتصفح.

---

## 3. الترويسات ومعايير تعدد المستأجرين (Headers & Multi-Tenancy)

يتم إرسال هذه الترويسات إجبارياً في جميع الطلبات المحمية:

```http
Authorization: Bearer <ACCESS_TOKEN>
Content-Type: application/json
Accept: application/json
Accept-Language: ar
```

### حالة المستخدم العادي (Company User):
* لا يحتاج لإرسال ترويسة المنشأة؛ فكود المنشأة ومخطط قاعدة البيانات (`Tenant Schema`) مشفر ومدمج داخل ادعاءات الـ JWT (`Claims`) ويتم توجيه الاستعلام تلقائياً إلى قاعدة بيانات الشركة الصحيحة.

### حالة المشرف العام (SuperAdmin Mode):
* عندما يدخل الـ SuperAdmin لشاشات منشأة محددة (مثل القيود المحاسبية، المخازن، أو الضرائب الخاصة بشركة `COMP01`)، يجب على الواجهة الأمامية إرسال إحدى الترويستين التاليتين لتحديد المستأجر المراد قراءة بياناته:
```http
X-Company-Code: COMP01
```
أو
```http
X-Company-Id: 105
```

---

## 4. هيكل الاستجابة الموحد (Unified ApiResponse Envelope)

جميع مسارات النظام تُرجع البيانات ضمن غلاف برمجي موحد:

### كائن الواجهة في TypeScript:
```typescript
export interface ApiResponse<T> {
  success: boolean;
  statusCode: number;
  message: string;
  data: T | null;
  errors: string[] | null;
  timestamp: string;
  traceId: string;
}
```

### أ. مثال استجابة النجاح (Success - `200 OK` / `201 Created`):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Data retrieved successfully.",
  "data": {
    "id": 15,
    "code": "CUST-001",
    "nameAr": "شركة الأفق للاستيراد"
  },
  "errors": null,
  "timestamp": "2026-11-04T14:15:30Z",
  "traceId": "c4d32a87-e21b-4b10-8b15-e0f3168d1234"
}
```

### ب. مثال استجابة الفشل أو أخطاء الإدخال (Failure - `400 Bad Request`):
```json
{
  "success": false,
  "statusCode": 400,
  "message": "Validation failed for the requested operation.",
  "data": null,
  "errors": [
    "حقل رمز العميل مطلوب ولا يمكن تركه فارغاً.",
    "الرقم الضريبي يجب أن يتكون من 15 رقماً ويبدأ وينتهي بالرقم 3."
  ],
  "timestamp": "2026-11-04T14:15:35Z",
  "traceId": "e5b81a74-f32a-4c22-9c23-f1e4279d5678"
}
```

---

## 5. التحقق من الصلاحيات وتوجيه الشاشات (Permissions & Screen Guards)

يوفر النظام نظام صلاحيات ديناميكي على مستوى الشاشات والوظائف (`V` = عرض، `I` = إضافة، `U` = تعديل، `D` = حذف):

### أ. جلب قائمة الشاشات المسموحة للمستخدم لبناء القائمة الجانبية (Sidebar):
* **المسار:** `GET /api/permissions/my-screens`
* **الاستخدام:** يتم استدعاؤه فور نجاح تسجيل الدخول لبناء شجرة القوائم وإخفاء الأقسام غير المصرح بها.

### ب. فحص صلاحية إجراء معين برمجياً قبل تنفيذه (Button / Route Guard):
* **المسار:** `GET /api/permissions/check?screenCode={screenCode}&featureCode={featureCode}`
* **أمثلة:**
  * `GET /api/permissions/check?screenCode=TAX_RATES&featureCode=I` (فحص صلاحية إضافة نسبة ضريبة).
  * `GET /api/permissions/check?screenCode=GL_VOUCHERS&featureCode=D` (فحص صلاحية حذف قيد محاسبي).
* **الاستجابة:**
```json
{
  "success": true,
  "statusCode": 200,
  "data": {
    "allowed": true
  }
}
```

---

## 6. كود تطبيقي جاهز: معترض الطلبات (Axios Client With Refresh Interceptor)

يمكن لمطور الواجهة الأمامية استخدام هذا الكود الجاهز (`apiClient.ts`) للتعامل تلقائياً مع الترويسات، وتجديد الـ Token، ومعالجة الأخطاء:

```typescript
import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  },
});

// 1. معترض الطلبات: إرفاق التوكن ولغة الجلسة وترويسة الشركة
apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = localStorage.getItem('accessToken');
  const language = localStorage.getItem('language') || '1'; // 1 = Arabic
  const superAdminCompanyCode = localStorage.getItem('selectedCompanyCode');

  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`);
  }

  config.headers.set('Accept-Language', language === '1' ? 'ar' : 'en');

  // إذا كان المستخدم SuperAdmin ويتصفح شركة محددة
  if (superAdminCompanyCode) {
    config.headers.set('X-Company-Code', superAdminCompanyCode);
  }

  return config;
});

// 2. معترض الاستجابات: معالجة انتهاء التوكن 401 والتجديد التلقائي
let isRefreshing = false;
let failedQueue: Array<{ resolve: (token: string) => void; reject: (err: any) => void }> = [];

const processQueue = (error: any, token: string | null = null) => {
  failedQueue.forEach((promise) => {
    if (token) promise.resolve(token);
    else promise.reject(error);
  });
  failedQueue = [];
};

apiClient.interceptors.response.use(
  (response) => response.data, // إرجاع كائن ApiResponse مباشرة
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    if (error.response?.status === 401 && !originalRequest._retry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        }).then((newToken) => {
          originalRequest.headers.set('Authorization', `Bearer ${newToken}`);
          return apiClient(originalRequest);
        });
      }

      originalRequest._retry = true;
      isRefreshing = true;

      const refreshToken = localStorage.getItem('refreshToken');
      if (!refreshToken) {
        // لا يوجد رمز تجديد، توجيه لشاشة الدخول
        localStorage.clear();
        window.location.href = '/login';
        return Promise.reject(error);
      }

      try {
        const refreshResponse: any = await axios.post(`${API_BASE_URL}/api/auth/refresh-token`, {
          refreshToken,
        });

        const newTokens = refreshResponse.data.data;
        localStorage.setItem('accessToken', newTokens.accessToken);
        localStorage.setItem('refreshToken', newTokens.refreshToken);

        processQueue(null, newTokens.accessToken);
        originalRequest.headers.set('Authorization', `Bearer ${newTokens.accessToken}`);
        return apiClient(originalRequest);
      } catch (refreshErr) {
        processQueue(refreshErr, null);
        localStorage.clear();
        window.location.href = '/login';
        return Promise.reject(refreshErr);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error.response?.data || error);
  }
);
```

---

## 7. التعامل مع عمليات البيانات المشتركة (Standard CRUD Pattern)

تتبع جميع الكنترولرات في **ThinkOnErp** معيار RESTful ثابتاً:

| العملية | الطريقة (Method) | المسار (Route) | معامل الطلب / Body | كود الحالة المتوقع |
| :--- | :--- | :--- | :--- | :--- |
| **قراءة القائمة** | `GET` | `/api/{resource}` | `?page=1&pageSize=20&search=keyword` | `200 OK` |
| **قراءة عنصر واحد** | `GET` | `/api/{resource}/{id}` | المعرف الرقمي في المسار | `200 OK` أو `404` |
| **إضافة جديد** | `POST` | `/api/{resource}` | كائن `CreateDto` في الـ Body | `201 Created` |
| **تعديل عنصر** | `PUT` | `/api/{resource}/{id}` | كائن `UpdateDto` في الـ Body | `200 OK` |
| **حذف عنصر** | `DELETE` | `/api/{resource}/{id}` | المعرف الرقمي في المسار | `200 OK` |

---

## 8. التعامل مع تنزيل الملفات والتقارير (PDF & Excel Exports)

عند طلب تقرير أو ملف PDF (مثل تقرير ملخص الضرائب `/api/pos/reports/tax-summary/pdf` أو ميزان المراجعة):
1. يجب تحديد `responseType: 'blob'` في مكتبة الاتصال.
2. تحويل الـ Blob إلى رابط URL محلي وفتحه أو تنزيله.

### كود تطبيقي لتنزيل الملفات في React / Vue / Angular:
```typescript
export async function downloadReportPdf(endpointUrl: string, fileName: string) {
  const token = localStorage.getItem('accessToken');
  const response = await fetch(endpointUrl, {
    method: 'GET',
    headers: {
      'Authorization': `Bearer ${token}`,
    },
  });

  if (!response.ok) {
    throw new Error('فشل تنزيل ملف التقرير');
  }

  const blob = await response.blob();
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  window.URL.revokeObjectURL(url);
  document.body.removeChild(a);
}
```

---

## 9. معالجة الأخطاء وربطها بنماذج الواجهة (Error Handling & Form Binding)

عند حدوث خطأ في التحقق من صحة المدخلات (`Validation Failure`)، يرجع الخادم كود `400 Bad Request` مع مصفوفة `errors`:
```json
{
  "success": false,
  "statusCode": 400,
  "message": "VALIDATION_FAILED",
  "errors": [
    "اسم الصنف بالعربية مطلوب.",
    "سعر البيع يجب أن يكون أكبر من صفر."
  ]
}
```

### أفضل ممارسة للواجهة:
1. إظهار رسالة التنبيه العامة `response.message` عبر Toast أو Alert أحمر أعلى الشاشة.
2. تكرار مصفوفة `response.errors` وعرضها كنقاط تحذيرية أسفل النموذج.
3. إذا كان الخطأ `403 Forbidden`، يتم إظهار تنبيه: *"عذراً، ليس لديك الصلاحية الكافية للقيام بهذا الإجراء"*.
4. إذا كان الخطأ `404 Not Found`، يتم توجيه المستخدم لصفحة "العنصر غير موجود".
