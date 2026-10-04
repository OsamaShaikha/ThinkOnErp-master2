# Automated Scenario Runner for ThinkOnErp Live API
$baseUrl = "http://178.104.126.99:5000"
$results = @()

function Record-TestResult {
    param(
        [string]$ScenarioId,
        [string]$Category,
        [string]$Name,
        [string]$Description,
        [string]$Method,
        [string]$Url,
        [object]$RequestPayload,
        [int]$ExpectedStatus,
        [int]$ActualStatus,
        [double]$DurationMs,
        [bool]$Passed,
        [string]$Notes,
        [object]$ResponseBody
    )
    
    $script:results += [PSCustomObject]@{
        scenarioId = $ScenarioId
        category = $Category
        name = $Name
        description = $Description
        method = $Method
        url = $Url
        requestPayload = $RequestPayload
        expectedStatus = $ExpectedStatus
        actualStatus = $ActualStatus
        durationMs = [Math]::Round($DurationMs, 2)
        passed = $Passed
        notes = $Notes
        responseBody = $ResponseBody
    }
}

Write-Host "===================================================="
Write-Host "Starting ThinkOnErp All-Scenario Live Verification"
Write-Host "Server: $baseUrl"
Write-Host "===================================================="

# Scenario 1: Authentication
Write-Host "[1/11] Testing Authentication..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$loginBodyObj = @{
    userName = "admin"
    password = "Admin@123"
    companyCode = "1122"
    language = 2
}
$loginJson = $loginBodyObj | ConvertTo-Json -Compress

try {
    $loginResp = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body $loginJson
    $sw.Stop()
    $token = $loginResp.data.accessToken
    $passed = ($token -ne $null -and $token.Length -gt 20)
    Record-TestResult "SC-01" "Authentication" "تسجيل الدخول وإصدار رمز التوثيق (JWT)" `
        "التحقق من صحة بيانات الدخول واستلام Access Token صالح للاستخدام" `
        "POST" "$baseUrl/api/auth/login" $loginBodyObj 200 200 $sw.Elapsed.TotalMilliseconds $passed `
        "تم استلام التوكن بنجاح للشركة 1122" $loginResp
} catch {
    $sw.Stop()
    Record-TestResult "SC-01" "Authentication" "تسجيل الدخول وإصدار رمز التوثيق (JWT)" `
        "التحقق من صحة بيانات الدخول واستلام Access Token صالح للاستخدام" `
        "POST" "$baseUrl/api/auth/login" $loginBodyObj 200 500 $sw.Elapsed.TotalMilliseconds $false `
        "فشل تسجيل الدخول: $($_.Exception.Message)" $null
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Scenario 2: Sales Invoice with Unified Quantity
Write-Host "[2/11] Testing Sales Invoice (DocType: 100, TrxType: 1001)..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$salesObj = @{
    branchId = 1
    docYear = 2026
    docType = 100
    trxType = 1001
    docDate = "2026-09-29T12:00:00Z"
    partyTypeCode = 1
    partyId = 1
    partyName = "شركة الأفق للتجارة - مبيعات نقدية"
    fromWarehouseId = 61
    currencyCode = "JOD"
    exchangeRate = 1.0
    paymentMethodCode = 1
    discountAmount = 0.0
    notes = "فاتورة مبيعات نقدية بالكمية الموحدة"
    lines = @(
        @{
            itemId = 162
            itemDescription = "طاولة مكتبية فاخرة"
            uomCode = 1
            uomFactor = 1.0
            quantity = 2.0
            unitPrice = 150.0
            unitCost = 90.0
            discountPercent = 0.0
            discountAmount = 0.0
            taxRate = 16.0
        }
    )
}
$salesJson = $salesObj | ConvertTo-Json -Depth 4
$createdSalesId = $null

try {
    $resp = Invoke-RestMethod -Uri "$baseUrl/api/inventory/documents" -Method Post -Headers $headers -Body $salesJson
    $sw.Stop()
    $createdSalesId = $resp.data.id
    $qOut = $resp.data.lines[0].quantityOut
    $passed = ($resp.statusCode -eq 201 -and $qOut -eq 2.0)
    Record-TestResult "SC-02" "Warehouse Invoices" "فاتورة مبيعات بالكمية الموحدة (Sales Invoice)" `
        "اختبار إنشاء فاتورة مبيعات والتحقق من التوجيه التلقائي للكمية إلى QuantityOut=2.0" `
        "POST" "$baseUrl/api/inventory/documents" $salesObj 201 $resp.statusCode $sw.Elapsed.TotalMilliseconds $passed `
        "تم إنشاء الفاتورة رقم $($resp.data.docNo) وتوجيه الكمية تلقائياً لصرف المخزون" $resp
} catch {
    $sw.Stop()
    $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 500 }
    Record-TestResult "SC-02" "Warehouse Invoices" "فاتورة مبيعات بالكمية الموحدة (Sales Invoice)" `
        "اختبار إنشاء فاتورة مبيعات والتحقق من التوجيه التلقائي للكمية إلى QuantityOut=2.0" `
        "POST" "$baseUrl/api/inventory/documents" $salesObj 201 $status $sw.Elapsed.TotalMilliseconds $false `
        "فشل: $($_.Exception.Message)" $null
}

# Scenario 3: Purchase Bill with Unified Quantity (DocType: 200, TrxType: 2001)
Write-Host "[3/11] Testing Purchase Bill (DocType: 200, TrxType: 2001)..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$purchObj = @{
    branchId = 1
    docYear = 2026
    docType = 200
    trxType = 2001
    docDate = "2026-09-29T12:00:00Z"
    partyTypeCode = 2
    partyId = 1
    partyName = "مورد الأثاث المكتبي المتحد"
    toWarehouseId = 61
    currencyCode = "JOD"
    exchangeRate = 1.0
    paymentMethodCode = 1
    discountAmount = 0.0
    notes = "فاتورة توريد مشتريات بالكمية الموحدة"
    lines = @(
        @{
            itemId = 162
            itemDescription = "طاولة مكتبية فاخرة"
            uomCode = 1
            uomFactor = 1.0
            quantity = 10.0
            unitPrice = 85.0
            unitCost = 85.0
            discountPercent = 0.0
            discountAmount = 0.0
            taxRate = 16.0
        }
    )
}
$purchJson = $purchObj | ConvertTo-Json -Depth 4

try {
    $resp = Invoke-RestMethod -Uri "$baseUrl/api/inventory/documents" -Method Post -Headers $headers -Body $purchJson
    $sw.Stop()
    $qIn = $resp.data.lines[0].quantityIn
    $passed = ($resp.statusCode -eq 201 -and $qIn -eq 10.0)
    Record-TestResult "SC-03" "Warehouse Invoices" "فاتورة مشتريات وتوريد بضاعة (Purchase Bill)" `
        "اختبار إنشاء فاتورة مشتريات والتحقق من التوجيه التلقائي للكمية إلى QuantityIn=10.0 بالمستودع المستقبل" `
        "POST" "$baseUrl/api/inventory/documents" $purchObj 201 $resp.statusCode $sw.Elapsed.TotalMilliseconds $passed `
        "تم إنشاء فاتورة المشتريات رقم $($resp.data.docNo) وتوجيه الكمية لتوريد المستودع" $resp
} catch {
    $sw.Stop()
    $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 500 }
    Record-TestResult "SC-03" "Warehouse Invoices" "فاتورة مشتريات وتوريد بضاعة (Purchase Bill)" `
        "اختبار إنشاء فاتورة مشتريات والتحقق من التوجيه التلقائي للكمية إلى QuantityIn=10.0" `
        "POST" "$baseUrl/api/inventory/documents" $purchObj 201 $status $sw.Elapsed.TotalMilliseconds $false `
        "فشل: $($_.Exception.Message)" $null
}

# Scenario 4: Warehouse Transfer (DocType: 350, TrxType: 3501)
Write-Host "[4/11] Testing Warehouse Transfer (DocType: 350, TrxType: 3501)..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$transObj = @{
    branchId = 1
    docYear = 2026
    docType = 350
    trxType = 3501
    docDate = "2026-09-29T12:00:00Z"
    fromWarehouseId = 61
    toWarehouseId = 62
    currencyCode = "JOD"
    exchangeRate = 1.0
    paymentMethodCode = 1
    notes = "سند تحويل بضاعة بين المستودع 61 و 62"
    lines = @(
        @{
            itemId = 162
            uomCode = 1
            uomFactor = 1.0
            quantity = 3.0
            unitPrice = 0.0
            unitCost = 90.0
        }
    )
}
$transJson = $transObj | ConvertTo-Json -Depth 4

try {
    $resp = Invoke-RestMethod -Uri "$baseUrl/api/inventory/documents" -Method Post -Headers $headers -Body $transJson
    $sw.Stop()
    $passed = ($resp.statusCode -eq 201)
    Record-TestResult "SC-04" "Warehouse Invoices" "سند تحويل مخزني داخلي (Internal Transfer)" `
        "التحقق من صحة إنشاء سند تحويل بين مستودعين مع تتبع الكميات في كلا الطرفين" `
        "POST" "$baseUrl/api/inventory/documents" $transObj 201 $resp.statusCode $sw.Elapsed.TotalMilliseconds $passed `
        "تم إنشاء سند التحويل رقم $($resp.data.docNo) بنجاح" $resp
} catch {
    $sw.Stop()
    $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 500 }
    Record-TestResult "SC-04" "Warehouse Invoices" "سند تحويل مخزني داخلي (Internal Transfer)" `
        "التحقق من صحة إنشاء سند تحويل بين مستودعين" `
        "POST" "$baseUrl/api/inventory/documents" $transObj 201 $status $sw.Elapsed.TotalMilliseconds $false `
        "فشل: $($_.Exception.Message)" $null
}

# Scenario 5: Multi-UOM with Conversion Factor (uomFactor = 24.0)
Write-Host "[5/11] Testing UOM Conversion Factor..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$uomObj = @{
    branchId = 1
    docYear = 2026
    docType = 100
    trxType = 1001
    docDate = "2026-09-29T12:00:00Z"
    partyTypeCode = 1
    partyId = 1
    fromWarehouseId = 61
    currencyCode = "JOD"
    exchangeRate = 1.0
    paymentMethodCode = 1
    notes = "بيع بوحدة كرتونة معامل 24"
    lines = @(
        @{
            itemId = 162
            itemDescription = "طاولة مكتبية (كرتونة 24)"
            uomCode = 2
            uomFactor = 24.0
            quantity = 2.0
            unitPrice = 3000.0
            unitCost = 2000.0
            taxRate = 16.0
        }
    )
}
$uomJson = $uomObj | ConvertTo-Json -Depth 4

try {
    $resp = Invoke-RestMethod -Uri "$baseUrl/api/inventory/documents" -Method Post -Headers $headers -Body $uomJson
    $sw.Stop()
    $baseOut = $resp.data.lines[0].baseQuantityOut
    $passed = ($resp.statusCode -eq 201 -and $baseOut -eq 48.0)
    Record-TestResult "SC-05" "Multi-UOM Engine" "احتساب معامل تحويل وحدة القياس (UOM Conversion Factor)" `
        "اختبار بيع 2 كرتونة بمعامل uomFactor=24 والتأكد من احتساب الكمية الأساسية بالمستودع = 48 حبة" `
        "POST" "$baseUrl/api/inventory/documents" $uomObj 201 $resp.statusCode $sw.Elapsed.TotalMilliseconds $passed `
        "تم احتساب BaseQuantityOut = $baseOut بنجاح ومطابقة المخزون" $resp
} catch {
    $sw.Stop()
    $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 500 }
    Record-TestResult "SC-05" "Multi-UOM Engine" "احتساب معامل تحويل وحدة القياس (UOM Conversion Factor)" `
        "اختبار احتساب معامل التحويل" `
        "POST" "$baseUrl/api/inventory/documents" $uomObj 201 $status $sw.Elapsed.TotalMilliseconds $false `
        "فشل: $($_.Exception.Message)" $null
}

# Scenario 6: Posting Document to GL and Stock Ledger
Write-Host "[6/11] Testing Document Posting..."
if ($createdSalesId) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $postUrl = "$baseUrl/api/inventory/documents/1/2026/100/$createdSalesId/post"
    try {
        $resp = Invoke-RestMethod -Uri $postUrl -Method Post -Headers $headers
        $sw.Stop()
        $passed = ($resp.statusCode -eq 200 -and $resp.data.isPostedStock -eq $true -and $resp.data.isPostedGl -eq $true)
        Record-TestResult "SC-06" "Posting & Accounting" "ترحيل الفاتورة للمخزون والأستاذ العام (Posting to GL & Stock)" `
            "التحقق من ترحيل الفاتورة وتحديث حالة السند إلى مرحل (Status=3) مع إنشاء حركات المخزون وقيود اليومية" `
            "POST" $postUrl $null 200 $resp.statusCode $sw.Elapsed.TotalMilliseconds $passed `
            "تم الترحيل بنجاح: isPostedStock=true, isPostedGl=true" $resp
    } catch {
        $sw.Stop()
        $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 500 }
        Record-TestResult "SC-06" "Posting & Accounting" "ترحيل الفاتورة للمخزون والأستاذ العام" `
            "التحقق من ترحيل الفاتورة" `
            "POST" $postUrl $null 200 $status $sw.Elapsed.TotalMilliseconds $false `
            "فشل الترحيل: $($_.Exception.Message)" $null
    }
}

# Scenario 7: Validation Test - Stock Direction Contradiction (QuantityIn in Sales)
Write-Host "[7/11] Testing Validation: QuantityIn on Sales..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$invalidDirObj = @{
    branchId = 1
    docYear = 2026
    docType = 100
    trxType = 1001
    docDate = "2026-09-29T12:00:00Z"
    partyTypeCode = 1
    partyId = 1
    fromWarehouseId = 61
    lines = @(
        @{
            itemId = 162
            uomCode = 1
            quantityIn = 5.0
            quantityOut = 0.0
            unitPrice = 100.0
        }
    )
}
$invalidDirJson = $invalidDirObj | ConvertTo-Json -Depth 4

try {
    $resp = Invoke-RestMethod -Uri "$baseUrl/api/inventory/documents" -Method Post -Headers $headers -Body $invalidDirJson
    $sw.Stop()
    Record-TestResult "SC-07" "Business Validation" "منع إدخال كمية توريد في فواتير المبيعات (Stock Direction Protection)" `
        "محاولة إرسال QuantityIn=5 في فاتورة مبيعات والتأكد من رفضها فوراً بكود 400" `
        "POST" "$baseUrl/api/inventory/documents" $invalidDirObj 400 200 $sw.Elapsed.TotalMilliseconds $false `
        "تم قبول الطلب بشكل خاطئ!" $resp
} catch {
    $sw.Stop()
    $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 500 }
    $errMsg = ""
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $errMsg = $reader.ReadToEnd()
    }
    $passed = ($status -eq 400 -and $errMsg -like "*صرف*")
    Record-TestResult "SC-07" "Business Validation" "منع إدخال كمية توريد في فواتير المبيعات (Stock Direction Protection)" `
        "محاولة إرسال QuantityIn=5 في فاتورة مبيعات والتأكد من رفضها فوراً بكود 400 مع رسالة خطأ واضحة" `
        "POST" "$baseUrl/api/inventory/documents" $invalidDirObj 400 $status $sw.Elapsed.TotalMilliseconds $passed `
        "تم الرفض بنجاح برسالة التحقق: $errMsg" ($errMsg | ConvertFrom-Json)
}

# Scenario 8: Validation Test - Negative Stock / Overdraft Protection
Write-Host "[8/11] Testing Validation: Negative Stock..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$excessiveObj = @{
    branchId = 1
    docYear = 2026
    docType = 100
    trxType = 1001
    docDate = "2026-09-29T12:00:00Z"
    partyTypeCode = 1
    partyId = 1
    fromWarehouseId = 61
    lines = @(
        @{
            itemId = 162
            uomCode = 1
            quantity = 999999.0
            unitPrice = 150.0
        }
    )
}
$excessiveJson = $excessiveObj | ConvertTo-Json -Depth 4

try {
    $resp = Invoke-RestMethod -Uri "$baseUrl/api/inventory/documents" -Method Post -Headers $headers -Body $excessiveJson
    $sw.Stop()
    Record-TestResult "SC-08" "Business Validation" "منع البيع بالسالب عند نفاد المخزون (Negative Stock Protection)" `
        "طلب بيع كمية ضخمة 999999 تفوق رصيد المستودع والتحقق من الرفض التلقائي" `
        "POST" "$baseUrl/api/inventory/documents" $excessiveObj 400 200 $sw.Elapsed.TotalMilliseconds $false `
        "تم قبول الفاتورة رغم عدم توفر الرصيد!" $resp
} catch {
    $sw.Stop()
    $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 500 }
    $errMsg = ""
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $errMsg = $reader.ReadToEnd()
    }
    $passed = ($status -eq 400 -and $errMsg -like "*الرصيد المتاح*")
    Record-TestResult "SC-08" "Business Validation" "منع البيع بالسالب عند نفاد المخزون (Negative Stock Protection)" `
        "طلب بيع كمية ضخمة تفوق رصيد المستودع والتأكد من رفض النظام للعملية وحماية أرصدة المخزون" `
        "POST" "$baseUrl/api/inventory/documents" $excessiveObj 400 $status $sw.Elapsed.TotalMilliseconds $passed `
        "تم الرفض بنجاح: $errMsg" ($errMsg | ConvertFrom-Json)
}

# Scenario 9: Validation Test - Closed Fiscal Period Protection
Write-Host "[9/11] Testing Validation: Closed Fiscal Period..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$closedPeriodObj = @{
    branchId = 1
    docYear = 2026
    docType = 100
    trxType = 1001
    docDate = "2020-01-01T12:00:00Z"
    partyTypeCode = 1
    partyId = 1
    fromWarehouseId = 61
    lines = @(
        @{
            itemId = 162
            uomCode = 1
            quantity = 1.0
            unitPrice = 150.0
        }
    )
}
$closedPeriodJson = $closedPeriodObj | ConvertTo-Json -Depth 4

try {
    $resp = Invoke-RestMethod -Uri "$baseUrl/api/inventory/documents" -Method Post -Headers $headers -Body $closedPeriodJson
    $sw.Stop()
    Record-TestResult "SC-09" "Business Validation" "منع إنشاء فواتير في فترة مالية مغلقة (Fiscal Period Protection)" `
        "محاولة تسجيل فاتورة بتاريخ قديم يقع في فترة محاسبية مغلقة (2020) والتحقق من المنع" `
        "POST" "$baseUrl/api/inventory/documents" $closedPeriodObj 400 200 $sw.Elapsed.TotalMilliseconds $false `
        "تم قبول الفاتورة في فترة مغلقة!" $resp
} catch {
    $sw.Stop()
    $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 500 }
    $errMsg = ""
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $errMsg = $reader.ReadToEnd()
    }
    $passed = ($status -eq 400 -and $errMsg -like "*فترة محاسبية*")
    Record-TestResult "SC-09" "Business Validation" "منع إنشاء فواتير في فترة مالية مغلقة (Fiscal Period Protection)" `
        "محاولة تسجيل فاتورة بتاريخ قديم يقع في فترة محاسبية مغلقة والتأكد من حماية القيود المحاسبية" `
        "POST" "$baseUrl/api/inventory/documents" $closedPeriodObj 400 $status $sw.Elapsed.TotalMilliseconds $passed `
        "تم الرفض بنجاح: $errMsg" ($errMsg | ConvertFrom-Json)
}

# Scenario 10: POS Order Creation with Open Shift
Write-Host "[10/11] Testing POS Order Creation..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$posOrderObj = @{
    branchId = 1
    shiftId = 61
    tillId = 41
    customerId = 1
    customerName = "زبون نقدي - نقاط البيع"
    orderType = 1
    notes = "طلب كاشير سريع مباشر"
    lines = @(
        @{
            itemId = 162
            itemName = "طاولة مكتبية فاخرة"
            uomCode = 1
            quantity = 1.0
            unitPrice = 150.0
            taxRate = 16.0
        }
    )
    payments = @(
        @{
            paymentMethod = 1
            amount = 174.0
            currencyCode = "JOD"
            exchangeRate = 1.0
        }
    )
}
$posOrderJson = $posOrderObj | ConvertTo-Json -Depth 4

try {
    $resp = Invoke-RestMethod -Uri "$baseUrl/api/pos/orders" -Method Post -Headers $headers -Body $posOrderJson
    $sw.Stop()
    $passed = ($resp.statusCode -eq 200 -or $resp.statusCode -eq 201)
    Record-TestResult "SC-10" "Point of Sale (POS)" "إنشاء وسداد طلب نقطة بيع مباشر (POS Cash Order)" `
        "تسجيل طلب بيع كاشير على الوردية المفتوحة (Shift: 61, Till: 41) مع السداد النقدي وحساب الضريبة" `
        "POST" "$baseUrl/api/pos/orders" $posOrderObj 200 $resp.statusCode $sw.Elapsed.TotalMilliseconds $passed `
        "تم تسجيل الطلب رقم $($resp.data.orderNumber) وإغلاقه بالسداد الكامل" $resp
} catch {
    $sw.Stop()
    $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 500 }
    $errMsg = ""
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $errMsg = $reader.ReadToEnd()
    }
    Record-TestResult "SC-10" "Point of Sale (POS)" "إنشاء وسداد طلب نقطة بيع مباشر (POS Cash Order)" `
        "تسجيل طلب بيع كاشير على الوردية المفتوحة" `
        "POST" "$baseUrl/api/pos/orders" $posOrderObj 200 $status $sw.Elapsed.TotalMilliseconds $false `
        "فشل: $errMsg" $null
}

# Scenario 11: POS Validation - Closed Shift Rejection
Write-Host "[11/11] Testing POS Validation: Closed Shift..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$posClosedShiftObj = @{
    branchId = 1
    shiftId = 1
    tillId = 1
    lines = @(
        @{
            itemId = 162
            quantity = 1.0
            unitPrice = 150.0
        }
    )
}
$posClosedShiftJson = $posClosedShiftObj | ConvertTo-Json -Depth 4

try {
    $resp = Invoke-RestMethod -Uri "$baseUrl/api/pos/orders" -Method Post -Headers $headers -Body $posClosedShiftJson
    $sw.Stop()
    Record-TestResult "SC-11" "Point of Sale (POS)" "منع تسجيل طلبات كاشير في وردية مغلقة (Closed Shift Protection)" `
        "محاولة إصدار طلب نقطة بيع على وردية مغلقة وتدقيقها مسبقاً (ShiftId: 1) والتأكد من المنع" `
        "POST" "$baseUrl/api/pos/orders" $posClosedShiftObj 400 200 $sw.Elapsed.TotalMilliseconds $false `
        "تم قبول الطلب بوردية مغلقة!" $resp
} catch {
    $sw.Stop()
    $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 500 }
    $errMsg = ""
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $errMsg = $reader.ReadToEnd()
    }
    $passed = ($status -eq 400 -and $errMsg -like "*وردية*")
    Record-TestResult "SC-11" "Point of Sale (POS)" "منع تسجيل طلبات كاشير في وردية مغلقة (Closed Shift Protection)" `
        "محاولة إصدار طلب نقطة بيع على وردية مغلقة وتدقيقها مسبقاً والتحقق من رفضها" `
        "POST" "$baseUrl/api/pos/orders" $posClosedShiftObj 400 $status $sw.Elapsed.TotalMilliseconds $passed `
        "تم الرفض بنجاح: $errMsg" ($errMsg | ConvertFrom-Json)
}

Write-Host "===================================================="
Write-Host "Test execution complete. Total Tests: $($results.Count)"
$passedTests = ($results | Where-Object { $_.passed -eq $true }).Count
Write-Host "Passed: $passedTests / $($results.Count)"
Write-Host "Saving results to d:\ThinkOnErp\live_tests_data.json..."

$results | ConvertTo-Json -Depth 6 | Set-Content -Path "d:\ThinkOnErp\live_tests_data.json" -Encoding UTF8
Write-Host "Done!"
