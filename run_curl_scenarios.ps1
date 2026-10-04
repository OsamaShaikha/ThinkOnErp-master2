$ErrorActionPreference = "Continue"

$baseUrl = "http://178.104.126.99:5000"
$testResults = [System.Collections.Generic.List[PSObject]]::new()

function Execute-Scenario {
    param(
        [string]$ScenarioId,
        [string]$Category,
        [string]$NameEn,
        [string]$DescriptionEn,
        [string]$Method,
        [string]$Endpoint,
        [hashtable]$Headers,
        [object]$Body,
        [int]$ExpectedStatus,
        [scriptblock]$CustomValidator = $null
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
            Uri = "$baseUrl$Endpoint"
            Method = $Method
            TimeoutSec = 25
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

    $isPassed = ($statusCode -eq $ExpectedStatus)
    $validationNotes = "Status code matched expected ($ExpectedStatus)."

    if ($CustomValidator) {
        try {
            $parsedJson = $null
            if ($respBodyStr -and $respBodyStr.Trim().StartsWith("{")) {
                $parsedJson = $respBodyStr | ConvertFrom-Json
            }
            $customCheck = & $CustomValidator $parsedJson $statusCode
            if (-not $customCheck.Passed) {
                $isPassed = $false
                $validationNotes = $customCheck.Message
            } else {
                $validationNotes = "$validationNotes $($customCheck.Message)"
            }
        } catch {
            $isPassed = $false
            $validationNotes = "Custom validation error: $($_.Exception.Message)"
        }
    }

    $testObj = [PSCustomObject]@{
        scenarioId = $ScenarioId
        category = $Category
        nameEn = $NameEn
        descriptionEn = $DescriptionEn
        method = $Method
        endpoint = $Endpoint
        expectedStatus = $ExpectedStatus
        actualStatus = $statusCode
        durationMs = $sw.ElapsedMilliseconds
        passed = $isPassed
        validationNotes = $validationNotes
        requestPayload = if ($reqJson) { ($reqJson | ConvertFrom-Json) } else { $null }
        responseBody = try { $respBodyStr | ConvertFrom-Json } catch { $respBodyStr }
    }

    $testResults.Add($testObj)
    $badge = if ($isPassed) { "[PASS]" } else { "[FAIL]" }
    Write-Host "$badge $ScenarioId - $NameEn ($Method $Endpoint) -> Status: $statusCode ($($sw.ElapsedMilliseconds)ms)"
    return $testObj
}

Write-Host "=========================================================="
Write-Host "Starting ThinkOn ERP Full Scenario Live Verification Suite"
Write-Host "Server: $baseUrl"
Write-Host "=========================================================="

# 1. Login
$loginBody = @{
    userName = "admin"
    password = "Admin@123"
    companyCode = "1122"
    language = 2
}
$sc1 = Execute-Scenario `
    -ScenarioId "SC-01" `
    -Category "Authentication and Security" `
    -NameEn "Tenant User Authentication and JWT Generation" `
    -DescriptionEn "Verify login credentials for company 1122 and retrieve Bearer token" `
    -Method "Post" `
    -Endpoint "/api/auth/login" `
    -Headers @{} `
    -Body $loginBody `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data.accessToken) {
            return @{ Passed = $true; Message = "Token acquired successfully." }
        }
        return @{ Passed = $false; Message = "Access token missing in response." }
    }

$token = $sc1.responseBody.data.accessToken
$authHeaders = @{
    Authorization = "Bearer $token"
}

# 2. Sales Invoice
$salesBody = @{
    branchId = 1
    docYear = 2026
    docType = 100
    trxType = 1001
    docDate = "2026-09-29T12:00:00Z"
    partyTypeCode = 1
    partyId = 1
    partyName = "Al-Ofuq Trading Company"
    fromWarehouseId = 61
    currencyCode = "JOD"
    exchangeRate = 1.0
    paymentMethodCode = 1
    discountAmount = 0.0
    notes = "Sales invoice using unified quantity"
    lines = @(
        @{
            itemId = 162
            itemDescription = "Office Desk"
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
$sc2 = Execute-Scenario `
    -ScenarioId "SC-02" `
    -Category "Warehouse Documents" `
    -NameEn "Sales Invoice with Unified Quantity (Auto-Outbound)" `
    -DescriptionEn "Verify sales invoice creation with automatic QuantityOut routing" `
    -Method "Post" `
    -Endpoint "/api/inventory/documents" `
    -Headers $authHeaders `
    -Body $salesBody `
    -ExpectedStatus 201 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.lines[0].quantityOut -eq 2.0 -and $res.data.lines[0].quantityIn -eq 0) {
            return @{ Passed = $true; Message = "Doc $($res.data.docNo) created. QuantityOut automatically resolved to 2.0." }
        }
        return @{ Passed = $false; Message = "QuantityOut was not resolved properly." }
    }

$createdSalesDocId = $null
if ($sc2.responseBody.data.id) {
    $createdSalesDocId = $sc2.responseBody.data.id
}

# 3. Purchase Bill (DocType: 200, TrxType: 2001)
$purchBody = @{
    branchId = 1
    docYear = 2026
    docType = 200
    trxType = 2001
    docDate = "2026-09-29T12:00:00Z"
    partyTypeCode = 2
    partyId = 1
    partyName = "United Furniture Supplier"
    toWarehouseId = 61
    currencyCode = "JOD"
    exchangeRate = 1.0
    paymentMethodCode = 1
    discountAmount = 0.0
    notes = "Purchase bill using unified quantity"
    lines = @(
        @{
            itemId = 162
            itemDescription = "Office Desk"
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
$sc3 = Execute-Scenario `
    -ScenarioId "SC-03" `
    -Category "Warehouse Documents" `
    -NameEn "Purchase Bill with Unified Quantity (Auto-Inbound)" `
    -DescriptionEn "Verify purchase bill creation with automatic QuantityIn routing to receiving warehouse" `
    -Method "Post" `
    -Endpoint "/api/inventory/documents" `
    -Headers $authHeaders `
    -Body $purchBody `
    -ExpectedStatus 201 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.lines[0].quantityIn -eq 10.0 -and $res.data.lines[0].quantityOut -eq 0) {
            return @{ Passed = $true; Message = "Purchase Doc $($res.data.docNo) created. QuantityIn auto-resolved to 10.0." }
        }
        return @{ Passed = $false; Message = "QuantityIn was not resolved properly." }
    }

# 4. Warehouse Transfer (DocType: 350, TrxType: 3501, From WH 61 to WH 101)
$transBody = @{
    branchId = 1
    docYear = 2026
    docType = 350
    trxType = 3501
    docDate = "2026-09-29T12:00:00Z"
    fromWarehouseId = 61
    toWarehouseId = 101
    currencyCode = "JOD"
    exchangeRate = 1.0
    paymentMethodCode = 1
    notes = "Warehouse Transfer between WH61 and WH101"
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
$sc4 = Execute-Scenario `
    -ScenarioId "SC-04" `
    -Category "Warehouse Documents" `
    -NameEn "Warehouse Transfer between Wh 61 and 101" `
    -DescriptionEn "Verify transfer document creation between two warehouses" `
    -Method "Post" `
    -Endpoint "/api/inventory/documents" `
    -Headers $authHeaders `
    -Body $transBody `
    -ExpectedStatus 201 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.docNo) {
            return @{ Passed = $true; Message = "Transfer document $($res.data.docNo) created successfully." }
        }
        return @{ Passed = $false; Message = "Transfer document creation failed." }
    }

# 5. Multi-UOM
$uomBody = @{
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
    notes = "Sales with UOM Carton Factor 24"
    lines = @(
        @{
            itemId = 162
            itemDescription = "Office Desk (Box of 24)"
            uomCode = 2
            uomFactor = 24.0
            quantity = 2.0
            unitPrice = 3000.0
            unitCost = 2000.0
            taxRate = 16.0
        }
    )
}
$sc5 = Execute-Scenario `
    -ScenarioId "SC-05" `
    -Category "Multi-UOM Engine" `
    -NameEn "Multi-UOM Factor Verification (Quantity 2 * 24 = 48 Base Qty)" `
    -DescriptionEn "Verify multi-UOM calculation converting 2 cartons to 48 pieces in base stock" `
    -Method "Post" `
    -Endpoint "/api/inventory/documents" `
    -Headers $authHeaders `
    -Body $uomBody `
    -ExpectedStatus 201 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.lines[0].baseQuantityOut -eq 48.0) {
            return @{ Passed = $true; Message = "BaseQuantityOut correctly computed to 48.0 (2 x 24)." }
        }
        return @{ Passed = $false; Message = "BaseQuantityOut mismatch." }
    }

# 6. Posting
$postDocId = if ($createdSalesDocId) { $createdSalesDocId } else { 16 }
$sc6 = Execute-Scenario `
    -ScenarioId "SC-06" `
    -Category "Posting and Accounting" `
    -NameEn "Post Document to GL and Stock Ledger (Lifecycle Status 3)" `
    -DescriptionEn "Verify document posting updating status to Posted (3) with stock and GL entries" `
    -Method "Post" `
    -Endpoint "/api/inventory/documents/1/2026/100/$postDocId/post" `
    -Headers $authHeaders `
    -Body $null `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.isPostedStock -eq $true -and $res.data.isPostedGl -eq $true) {
            return @{ Passed = $true; Message = "Document posted successfully. isPostedStock=true, isPostedGl=true." }
        }
        return @{ Passed = $false; Message = "Posting flag not updated." }
    }

# 7. Validation: QuantityIn on Sales
$invalidDirBody = @{
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
$sc7 = Execute-Scenario `
    -ScenarioId "SC-07" `
    -Category "Business Validation" `
    -NameEn "Stock Direction Contradiction Rejection (QuantityIn on Sales)" `
    -DescriptionEn "Verify rejection of QuantityIn on sales documents ensuring stock integrity" `
    -Method "Post" `
    -Endpoint "/api/inventory/documents" `
    -Headers $authHeaders `
    -Body $invalidDirBody `
    -ExpectedStatus 400 `
    -CustomValidator {
        param($res, $status)
        if ($status -eq 400) {
            return @{ Passed = $true; Message = "Successfully rejected: $($res.message)" }
        }
        return @{ Passed = $false; Message = "Did not return expected 400 status." }
    }

# 8. Validation: Negative Stock
$excessiveBody = @{
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
$sc8 = Execute-Scenario `
    -ScenarioId "SC-08" `
    -Category "Business Validation" `
    -NameEn "Negative Stock / Overdraft Prevention Rejection" `
    -DescriptionEn "Verify automatic rejection when requested quantity exceeds available warehouse balance" `
    -Method "Post" `
    -Endpoint "/api/inventory/documents" `
    -Headers $authHeaders `
    -Body $excessiveBody `
    -ExpectedStatus 400 `
    -CustomValidator {
        param($res, $status)
        if ($status -eq 400) {
            return @{ Passed = $true; Message = "Successfully rejected: $($res.message)" }
        }
        return @{ Passed = $false; Message = "Did not return expected 400 status." }
    }

# 9. Validation: Closed Fiscal Period
$closedPeriodBody = @{
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
$sc9 = Execute-Scenario `
    -ScenarioId "SC-09" `
    -Category "Business Validation" `
    -NameEn "Closed Fiscal Period Rejection (Year 2020)" `
    -DescriptionEn "Verify automatic rejection when document date falls in closed accounting period" `
    -Method "Post" `
    -Endpoint "/api/inventory/documents" `
    -Headers $authHeaders `
    -Body $closedPeriodBody `
    -ExpectedStatus 400 `
    -CustomValidator {
        param($res, $status)
        if ($status -eq 400) {
            return @{ Passed = $true; Message = "Successfully rejected: $($res.message)" }
        }
        return @{ Passed = $false; Message = "Did not return expected 400 status." }
    }

# 10. POS Direct Order
$posOrderBody = @{
    branchId = 1
    shiftId = 61
    tillId = 41
    customerId = 1
    customerName = "Cash Customer"
    orderType = 1
    notes = "Fast direct cashier POS order"
    lines = @(
        @{
            lineNumber = 1
            itemId = 162
            itemCode = "ITM-OB-98083"
            itemName = "Office Desk"
            uomId = 1
            quantity = 1.0
            unitPrice = 150.0
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
$sc10 = Execute-Scenario `
    -ScenarioId "SC-10" `
    -Category "Point of Sale (POS)" `
    -NameEn "POS Cash Order Creation and Full Payment on Active Shift" `
    -DescriptionEn "Verify POS order creation and full cash payment settlement on open cashier shift" `
    -Method "Post" `
    -Endpoint "/api/pos/orders" `
    -Headers $authHeaders `
    -Body $posOrderBody `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.orderNumber) {
            return @{ Passed = $true; Message = "POS Order #$($res.data.orderNumber) created and paid successfully." }
        }
        return @{ Passed = $false; Message = "POS order creation failed." }
    }

# 11. POS Closed Shift
$posClosedBody = @{
    branchId = 1
    shiftId = 1
    tillId = 1
    lines = @(
        @{
            lineNumber = 1
            itemId = 162
            itemCode = "ITM-OB-98083"
            itemName = "Office Desk"
            uomId = 1
            quantity = 1.0
            unitPrice = 150.0
        }
    )
}
$sc11 = Execute-Scenario `
    -ScenarioId "SC-11" `
    -Category "Point of Sale (POS)" `
    -NameEn "POS Closed Shift Rejection (Shift #1 AuditedAndClosed)" `
    -DescriptionEn "Verify automatic rejection when trying to create POS order on closed shift" `
    -Method "Post" `
    -Endpoint "/api/pos/orders" `
    -Headers $authHeaders `
    -Body $posClosedBody `
    -ExpectedStatus 400 `
    -CustomValidator {
        param($res, $status)
        if ($status -eq 400) {
            return @{ Passed = $true; Message = "Successfully rejected: $($res.message)" }
        }
        return @{ Passed = $false; Message = "Did not return expected 400 status." }
    }

Write-Host "=========================================================="
$passCount = ($testResults | Where-Object { $_.passed -eq $true }).Count
Write-Host "Verification Complete! Passed: $passCount / $($testResults.Count)"

$jsonOutput = $testResults | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText("d:\ThinkOnErp\scenario_test_results.json", $jsonOutput, [System.Text.Encoding]::UTF8)
Write-Host "Results saved to d:\ThinkOnErp\scenario_test_results.json"
