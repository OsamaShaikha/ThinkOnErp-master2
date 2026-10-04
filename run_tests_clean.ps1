$ErrorActionPreference = "Continue"

$baseUrl = "http://178.104.126.99:5000"
$testResults = [System.Collections.Generic.List[PSObject]]::new()

function Execute-Test {
    param(
        [string]$TestId,
        [string]$Category,
        [string]$NameAr,
        [string]$NameEn,
        [string]$Method,
        [string]$Url,
        [hashtable]$Headers,
        [object]$Body,
        [int]$ExpectedStatus
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $statusCode = 0
    $respBodyStr = ""
    $reqJson = ""

    if ($Body) {
        $reqJson = $Body | ConvertTo-Json -Depth 10
    }

    try {
        $params = @{
            Uri = $Url
            Method = $Method
            TimeoutSec = 15
        }
        if ($Headers -and $Headers.Count -gt 0) { $params.Headers = $Headers }
        if ($Body) {
            $params.Body = $reqJson
            $params.ContentType = "application/json; charset=utf-8"
        }

        $res = Invoke-RestMethod @params
        $sw.Stop()
        $statusCode = 200
        $respBodyStr = $res | ConvertTo-Json -Depth 10
        if ($res.statusCode) { $statusCode = [int]$res.statusCode }
    }
    catch {
        $sw.Stop()
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $respBodyStr = $reader.ReadToEnd()
        } else {
            $statusCode = 500
            $respBodyStr = $_.Exception.Message
        }
    }

    $isSuccess = ($statusCode -eq $ExpectedStatus)

    $testObj = [PSCustomObject]@{
        Id = $TestId
        Category = $Category
        NameAr = $NameAr
        NameEn = $NameEn
        Method = $Method
        Url = $Url.Replace($baseUrl, "")
        ExpectedStatus = $ExpectedStatus
        ActualStatus = $statusCode
        DurationMs = $sw.ElapsedMilliseconds
        Passed = $isSuccess
        RequestBody = $reqJson
        ResponseBody = $respBodyStr
    }

    $testResults.Add($testObj)
    $badge = if ($isSuccess) { "[PASS]" } else { "[FAIL]" }
    Write-Host "$badge $TestId - $NameEn ($Method $Url) -> Status: $statusCode ($($sw.ElapsedMilliseconds)ms)"
    return $testObj
}

Write-Host "=========================================================="
Write-Host "Starting ThinkOn ERP Full Live API Test Suite against $baseUrl"
Write-Host "=========================================================="

# 1. SuperAdmin Login
$saBody = @{
    userName = "admin"
    password = "Admin@123"
    language = 2
}
$t1 = Execute-Test -TestId "AUTH-01" -Category "Authentication" `
    -NameAr "SuperAdmin Authentication" `
    -NameEn "SuperAdmin Authentication" `
    -Method "Post" -Url "$baseUrl/api/auth/superadmin/login" `
    -Headers @{} -Body $saBody -ExpectedStatus 200

# 2. Tenant Admin Login
$tenantBody = @{
    userName = "admin"
    password = "Admin@123"
    companyCode = "1122"
    language = 2
}
$t2 = Execute-Test -TestId "AUTH-02" -Category "Authentication" `
    -NameAr "Tenant Company Authentication" `
    -NameEn "Tenant Company Authentication" `
    -Method "Post" -Url "$baseUrl/api/Auth/login" `
    -Headers @{} -Body $tenantBody -ExpectedStatus 200

$tenantToken = ""
try {
    $tenantJson = $t2.ResponseBody | ConvertFrom-Json
    $tenantToken = $tenantJson.data.accessToken
} catch {}

$tenantHeaders = @{
    Authorization = "Bearer $tenantToken"
}

# 3. Create Cash Sales Invoice (Normal Success)
$cashInvoiceBody = @{
    branchId = 1
    docYear = 2026
    docType = 100
    trxType = 1001
    docDate = "2026-09-23T12:00:00Z"
    partyTypeCode = 1
    partyId = 1
    partyName = "Al-Amal Co. - Cash Sales Test"
    fromWarehouseId = 61
    currencyCode = "SAR"
    exchangeRate = 1.0
    paymentMethodCode = 1
    discountAmount = 0.0
    notes = "Automated Live Test - Cash Sales Invoice"
    lines = @(
        @{
            itemId = 162
            itemDescription = "Item With Opening Balance ITM-OB-98083"
            uomCode = 1
            uomFactor = 1.0
            quantityIn = 0.0
            quantityOut = 2.0
            unitPrice = 120.0
            unitCost = 50.0
            discountPercent = 0.0
            discountAmount = 0.0
            taxRate = 15.0
        }
    )
}

$t3 = Execute-Test -TestId "DOC-01" -Category "Warehouse Documents" `
    -NameAr "Create Cash Sales Invoice" `
    -NameEn "Create Valid Cash Sales Invoice" `
    -Method "Post" -Url "$baseUrl/api/inventory/documents" `
    -Headers $tenantHeaders -Body $cashInvoiceBody -ExpectedStatus 201

$createdCashDocId = 0
try {
    $cDoc = $t3.ResponseBody | ConvertFrom-Json
    $createdCashDocId = $cDoc.data.id
} catch {}

# 4. Create Credit Sales Invoice (Normal Success)
$creditInvoiceBody = @{
    branchId = 1
    docYear = 2026
    docType = 100
    trxType = 1002
    docDate = "2026-09-23T12:00:00Z"
    dueDate = "2026-10-23T12:00:00Z"
    partyTypeCode = 1
    partyId = 1
    partyName = "Al-Amal Co. - Credit Sales Test"
    fromWarehouseId = 61
    currencyCode = "SAR"
    exchangeRate = 1.0
    paymentMethodCode = 1
    discountAmount = 10.0
    notes = "Automated Live Test - Credit Sales Invoice"
    lines = @(
        @{
            itemId = 162
            itemDescription = "Item With Opening Balance ITM-OB-98083"
            uomCode = 1
            uomFactor = 1.0
            quantityIn = 0.0
            quantityOut = 1.0
            unitPrice = 150.0
            unitCost = 50.0
            discountPercent = 0.0
            discountAmount = 0.0
            taxRate = 15.0
        }
    )
}

$t4 = Execute-Test -TestId "DOC-02" -Category "Warehouse Documents" `
    -NameAr "Create Credit Sales Invoice" `
    -NameEn "Create Valid Credit Sales Invoice" `
    -Method "Post" -Url "$baseUrl/api/inventory/documents" `
    -Headers $tenantHeaders -Body $creditInvoiceBody -ExpectedStatus 201

# 5. Create Purchase Bill (Normal Success)
$purchaseBillBody = @{
    branchId = 1
    docYear = 2026
    docType = 200
    trxType = 2001
    docDate = "2026-09-23T12:00:00Z"
    dueDate = "2026-11-20T12:00:00Z"
    partyTypeCode = 2
    partyId = 1
    partyName = "General Supplies Co. Ltd."
    fromWarehouseId = $null
    toWarehouseId = 61
    currencyCode = "SAR"
    exchangeRate = 1.0
    paymentMethodCode = 1
    discountAmount = 0.0
    notes = "Automated Live Test - Purchase Bill Inbound"
    lines = @(
        @{
            itemId = 162
            itemDescription = "Additional Supply ITM-OB-98083"
            uomCode = 1
            uomFactor = 1.0
            quantityIn = 25.0
            quantityOut = 0.0
            unitPrice = 45.0
            unitCost = 45.0
            discountPercent = 0.0
            discountAmount = 0.0
            taxRate = 15.0
            lotNumber = "LOT-SEPT-2026"
        }
    )
}

$t5 = Execute-Test -TestId "DOC-03" -Category "Warehouse Documents" `
    -NameAr "Create Purchase Bill" `
    -NameEn "Create Valid Purchase Bill" `
    -Method "Post" -Url "$baseUrl/api/inventory/documents" `
    -Headers $tenantHeaders -Body $purchaseBillBody -ExpectedStatus 201

# 6. Post Draft Document to GL & Stock Ledger
if ($createdCashDocId -gt 0) {
    $t6 = Execute-Test -TestId "DOC-04" -Category "Warehouse Documents" `
        -NameAr "Post Document to GL and Stock" `
        -NameEn "Post Document to GL and Stock Ledger" `
        -Method "Post" -Url "$baseUrl/api/inventory/documents/1/2026/100/$createdCashDocId/post" `
        -Headers $tenantHeaders -Body $null -ExpectedStatus 200
}

# 7. Document Profitability Analytics View
$t7 = Execute-Test -TestId "DOC-05" -Category "Warehouse Documents" `
    -NameAr "Sales Invoice Profitability Analytics" `
    -NameEn "Retrieve Sales Invoice Profitability Report" `
    -Method "Get" -Url "$baseUrl/api/inventory/documents/profitability-report?branchId=1" `
    -Headers $tenantHeaders -Body $null -ExpectedStatus 200

# 8. Validation Test: Negative Stock Rejection
$failStockBody = @{
    branchId = 1
    docYear = 2026
    docType = 100
    trxType = 1001
    docDate = "2026-09-23T12:00:00Z"
    partyTypeCode = 1
    partyId = 1
    fromWarehouseId = 61
    currencyCode = "SAR"
    lines = @(
        @{
            itemId = 162
            itemDescription = "Excess Quantity Order"
            uomCode = 1
            quantityIn = 0.0
            quantityOut = 999999.0
            unitPrice = 100.0
            unitCost = 50.0
            taxRate = 15.0
        }
    )
}

$t8 = Execute-Test -TestId "VAL-01" -Category "Business Validations" `
    -NameAr "Reject Insufficient Stock" `
    -NameEn "Reject Insufficient Stock Transaction" `
    -Method "Post" -Url "$baseUrl/api/inventory/documents" `
    -Headers $tenantHeaders -Body $failStockBody -ExpectedStatus 400

# 9. Validation Test: Closed Fiscal Period Rejection
$failPeriodBody = @{
    branchId = 1
    docYear = 2018
    docType = 100
    trxType = 1001
    docDate = "2018-05-15T12:00:00Z"
    partyTypeCode = 1
    partyId = 1
    fromWarehouseId = 61
    currencyCode = "SAR"
    lines = @(
        @{
            itemId = 162
            itemDescription = "Date in closed year"
            uomCode = 1
            quantityIn = 0.0
            quantityOut = 1.0
            unitPrice = 100.0
            taxRate = 15.0
        }
    )
}

$t9 = Execute-Test -TestId "VAL-02" -Category "Business Validations" `
    -NameAr "Reject Closed Fiscal Period" `
    -NameEn "Reject Document in Closed/Missing Fiscal Period" `
    -Method "Post" -Url "$baseUrl/api/inventory/documents" `
    -Headers $tenantHeaders -Body $failPeriodBody -ExpectedStatus 400

# 10. Validation Test: Missing Lines
$failEmptyLinesBody = @{
    branchId = 1
    docYear = 2026
    docType = 100
    trxType = 1001
    docDate = "2026-09-23T12:00:00Z"
    lines = @()
}

$t10 = Execute-Test -TestId "VAL-03" -Category "Business Validations" `
    -NameAr "Reject Document with Empty Lines" `
    -NameEn "Reject Document with Empty Lines" `
    -Method "Post" -Url "$baseUrl/api/inventory/documents" `
    -Headers $tenantHeaders -Body $failEmptyLinesBody -ExpectedStatus 400

# 11. POS Validation: POS Order on Inactive/Closed Shift
$failShiftPosOrder = @{
    branchId = 1
    shiftId = 99999
    tillId = 41
    clientUuid = [guid]::NewGuid().ToString()
    orderType = 2
    status = 1
    lines = @(
        @{
            lineNumber = 1
            itemId = 162
            itemCode = "ITM-OB-98083"
            itemName = "Test Item"
            quantity = 1.0
            uomId = 1
            unitPrice = 25.0
            modifiers = @()
        }
    )
    payments = @()
}

$t11 = Execute-Test -TestId "POS-01" -Category "Point of Sale (POS)" `
    -NameAr "Reject POS Order on Closed Shift" `
    -NameEn "Reject POS Order on Inactive Shift" `
    -Method "Post" -Url "$baseUrl/api/pos/orders" `
    -Headers $tenantHeaders -Body $failShiftPosOrder -ExpectedStatus 400

# 12. POS Validation: Prevent Opening Duplicate Shift on Till with Active Shift
$dupShiftBody = @{
    branchId = 1
    tillId = 41
    cashierUserId = 1
    openingFloat = 150.00
}

$t12 = Execute-Test -TestId "POS-02" -Category "Point of Sale (POS)" `
    -NameAr "Prevent Duplicate Active Shift on Till" `
    -NameEn "Prevent Duplicate Shift on Till with Active Session" `
    -Method "Post" -Url "$baseUrl/api/pos/shifts/open" `
    -Headers $tenantHeaders -Body $dupShiftBody -ExpectedStatus 400

# 13. POS Order: Dine-In with Split Payment (Cash + Card) on Active Shift 61
$activeShiftId = 61
$dineInOrderBody = @{
    branchId = 1
    shiftId = $activeShiftId
    tillId = 41
    clientUuid = [guid]::NewGuid().ToString()
    orderType = 1
    status = 1
    customerId = 1
    customerName = "Faisal Al-Otaibi"
    tableId = $null
    covers = 2
    serviceChargeAmount = 5.0
    deliveryFee = 0.0
    tipAmount = 0.0
    notes = "Live Automated Dine-In Order with Split Payment"
    lines = @(
        @{
            lineNumber = 1
            itemId = 162
            itemCode = "ITM-OB-98083"
            itemName = "Item 162"
            quantity = 2.0
            uomId = 1
            unitPrice = 50.0
            modifiers = @()
        }
    )
    payments = @(
        @{
            paymentMethod = 1
            amount = 60.0
            tenderedAmount = 60.0
            changeAmount = 0.0
        },
        @{
            paymentMethod = 2
            amount = 60.0
            tenderedAmount = 60.0
            changeAmount = 0.0
            cardNumberMasked = "**** **** **** 1122"
            cardType = "MADA"
            transactionReference = "TXN-AUTO-9988"
        }
    )
}

$t13 = Execute-Test -TestId "POS-03" -Category "Point of Sale (POS)" `
    -NameAr "Create Dine-In Order with Split Payment" `
    -NameEn "Create Dine-In POS Order with Split Payment" `
    -Method "Post" -Url "$baseUrl/api/pos/orders" `
    -Headers $tenantHeaders -Body $dineInOrderBody -ExpectedStatus 200

$createdOrderId = 0
try {
    $ordJson = $t13.ResponseBody | ConvertFrom-Json
    $createdOrderId = $ordJson.data.id
} catch {}

# 14. POS Order: Takeaway Quick Sale with Full Cash & Change on Active Shift 61
$takeawayOrderBody = @{
    branchId = 1
    shiftId = $activeShiftId
    tillId = 41
    clientUuid = [guid]::NewGuid().ToString()
    orderType = 2
    status = 1
    customerId = $null
    customerName = "Walk-in Customer"
    covers = 1
    notes = "Live Automated Takeaway Order"
    lines = @(
        @{
            lineNumber = 1
            itemId = 162
            itemCode = "ITM-OB-98083"
            itemName = "Takeaway Item"
            quantity = 1.0
            uomId = 1
            unitPrice = 80.0
            modifiers = @()
        }
    )
    payments = @(
        @{
            paymentMethod = 1
            amount = 92.0
            tenderedAmount = 100.0
            changeAmount = 8.0
        }
    )
}

$t14 = Execute-Test -TestId "POS-04" -Category "Point of Sale (POS)" `
    -NameAr "Create Takeaway Order with Cash Payment" `
    -NameEn "Create Takeaway Order with Full Cash Payment" `
    -Method "Post" -Url "$baseUrl/api/pos/orders" `
    -Headers $tenantHeaders -Body $takeawayOrderBody -ExpectedStatus 200

# 15. Query POS Orders (Verification)
$t15 = Execute-Test -TestId "POS-05" -Category "Point of Sale (POS)" `
    -NameAr "Query POS Orders List" `
    -NameEn "Retrieve Active Orders for Shift" `
    -Method "Get" -Url "$baseUrl/api/pos/orders?branchId=1&shiftId=61&pageIndex=1&pageSize=5" `
    -Headers $tenantHeaders -Body $null -ExpectedStatus 200

# 16. Get POS Order By ID
if ($createdOrderId -gt 0) {
    $t16 = Execute-Test -TestId "POS-06" -Category "Point of Sale (POS)" `
        -NameAr "Retrieve Single Order by ID" `
        -NameEn "Get POS Order Details by Key" `
        -Method "Get" -Url "$baseUrl/api/pos/orders/$createdOrderId" `
        -Headers $tenantHeaders -Body $null -ExpectedStatus 200
}

Write-Host "=========================================================="
Write-Host "Test Suite Completed! Total Tests: $($testResults.Count)"
$passCount = ($testResults | Where-Object { $_.Passed }).Count
$failCount = $testResults.Count - $passCount
Write-Host "Passed: $passCount | Failed: $failCount"
Write-Host "=========================================================="

# Export results to JSON
$jsonResults = $testResults | ConvertTo-Json -Depth 10
$jsonResults | Set-Content -Path "d:\ThinkOnErp\test_results.json" -Encoding UTF8
Write-Host "Results saved to d:\ThinkOnErp\test_results.json"
