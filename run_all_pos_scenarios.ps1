$ErrorActionPreference = "Continue"

$baseUrl = "http://178.104.126.99:5000"
$posResults = [System.Collections.Generic.List[PSObject]]::new()

function Execute-PosScenario {
    param(
        [string]$ScenarioId,
        [string]$CaseType,
        [string]$NameEn,
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
        if ($Body -is [string]) {
            $reqJson = $Body
        } else {
            $reqJson = $Body | ConvertTo-Json -Depth 10
        }
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
            $validationNotes = "Custom validator error: $($_.Exception.Message)"
        }
    }

    $testObj = [PSCustomObject]@{
        scenarioId = $ScenarioId
        caseType = $CaseType
        nameEn = $NameEn
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

    $posResults.Add($testObj)
    $badge = if ($isPassed) { "[PASS]" } else { "[FAIL]" }
    Write-Host "$badge $ScenarioId - $NameEn ($Method $Endpoint) -> Status: $statusCode ($($sw.ElapsedMilliseconds)ms)"
    return $testObj
}

Write-Host "=========================================================="
Write-Host "Starting ThinkOn ERP Full POS Cases Live Verification"
Write-Host "Server: $baseUrl"
Write-Host "=========================================================="

# 1. Login
$loginBody = @{
    userName = "admin"
    password = "Admin@123"
    companyCode = "1122"
    language = 2
}
$sc1 = Execute-PosScenario `
    -ScenarioId "POS-01" `
    -CaseType "Authentication" `
    -NameEn "Cashier Authentication and JWT Token Retrieval" `
    -Method "Post" `
    -Endpoint "/api/auth/login" `
    -Headers @{} `
    -Body $loginBody `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data.accessToken) {
            return @{ Passed = $true; Message = "Bearer token acquired successfully." }
        }
        return @{ Passed = $false; Message = "Token not found in response." }
    }

$token = $sc1.responseBody.data.accessToken
$authHeaders = @{
    Authorization = "Bearer $token"
}

# 2. Fast Takeaway Cash Order (Completed & Settled)
$takeawayBody = @{
    branchId = 1
    shiftId = 61
    tillId = 41
    orderType = 2
    notes = "Fast direct takeaway cash sale"
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
        }
    )
}
$sc2 = Execute-PosScenario `
    -ScenarioId "POS-02" `
    -CaseType "Takeaway (Direct Cash)" `
    -NameEn "Fast Takeaway Cash Order (Completed and Fully Paid)" `
    -Method "Post" `
    -Endpoint "/api/pos/orders" `
    -Headers $authHeaders `
    -Body $takeawayBody `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.status -eq "Completed" -and $res.data.isPaid -eq $true) {
            return @{ Passed = $true; Message = "Order #$($res.data.orderNumber) created and closed as Completed." }
        }
        return @{ Passed = $false; Message = "Order not completed or unpaid." }
    }

# 3. Dine-In Order with Table 41, Covers and Service Charge
$dineInBody = @{
    branchId = 1
    shiftId = 61
    tillId = 41
    orderType = 1
    tableId = 41
    covers = 4
    serviceChargeAmount = 15.0
    notes = "Dine-in Order on Table 41 with 4 Covers"
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
            amount = 189.0
            currencyCode = "JOD"
        }
    )
}
$sc3 = Execute-PosScenario `
    -ScenarioId "POS-03" `
    -CaseType "Dine-In (Hospitality)" `
    -NameEn "Dine-In Order with Table 41 and Service Charge" `
    -Method "Post" `
    -Endpoint "/api/pos/orders" `
    -Headers $authHeaders `
    -Body $dineInBody `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.orderType -eq "DineIn" -and $res.data.tableId -eq 41) {
            return @{ Passed = $true; Message = "Dine-in Order #$($res.data.orderNumber) settled on Table 41." }
        }
        return @{ Passed = $false; Message = "Dine-in order creation failed." }
    }

# 4. Delivery Order with Delivery Fee & Tip
$deliveryBody = @{
    branchId = 1
    shiftId = 61
    tillId = 41
    customerId = 1
    customerName = "Ahmad Customer"
    orderType = 3
    deliveryFee = 5.0
    tipAmount = 2.0
    notes = "Home delivery to Amman City Center"
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
            amount = 181.0
            currencyCode = "JOD"
        }
    )
}
$sc4 = Execute-PosScenario `
    -ScenarioId "POS-04" `
    -CaseType "Delivery" `
    -NameEn "Home Delivery Order with Delivery Fee and Driver Tip" `
    -Method "Post" `
    -Endpoint "/api/pos/orders" `
    -Headers $authHeaders `
    -Body $deliveryBody `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.orderType -eq "Delivery" -and $res.data.deliveryFee -eq 5.0) {
            return @{ Passed = $true; Message = "Delivery Order #$($res.data.orderNumber) settled with fee and tip." }
        }
        return @{ Passed = $false; Message = "Delivery order failed." }
    }

# 5. Aggregator External Order (Jahez / Talabat)
$aggregatorBody = @{
    branchId = 1
    shiftId = 61
    tillId = 41
    orderType = 4
    notes = "Talabat Aggregator Order #TAL-9942"
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
            paymentMethod = 10
            amount = 174.0
            transactionReference = "TAL-9942"
            currencyCode = "JOD"
        }
    )
}
$sc5 = Execute-PosScenario `
    -ScenarioId "POS-05" `
    -CaseType "Aggregator Integration" `
    -NameEn "External Aggregator Order (Talabat / Jahez Online Paid)" `
    -Method "Post" `
    -Endpoint "/api/pos/orders" `
    -Headers $authHeaders `
    -Body $aggregatorBody `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.orderType -eq "Aggregator" -and $res.data.isPaid -eq $true) {
            return @{ Passed = $true; Message = "Aggregator order #$($res.data.orderNumber) created and settled via AggregatorPaid." }
        }
        return @{ Passed = $false; Message = "Aggregator order failed." }
    }

# 6. Split Payment (Cash + Card)
$splitBody = @{
    branchId = 1
    shiftId = 61
    tillId = 41
    orderType = 2
    notes = "Split Payment: Cash 100 JOD and Card 74 JOD"
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
            amount = 100.0
            currencyCode = "JOD"
        },
        @{
            paymentMethod = 2
            amount = 74.0
            cardNumberMasked = "****-****-****-4242"
            transactionReference = "TXN-998812"
            authCode = "AUTH-4411"
            currencyCode = "JOD"
        }
    )
}
$sc6 = Execute-PosScenario `
    -ScenarioId "POS-06" `
    -CaseType "Split Payment" `
    -NameEn "Split Tender Payment (Cash 100 + Mada/Card 74)" `
    -Method "Post" `
    -Endpoint "/api/pos/orders" `
    -Headers $authHeaders `
    -Body $splitBody `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.payments.Count -eq 2 -and $res.data.isPaid -eq $true) {
            return @{ Passed = $true; Message = "Split payment successful: 2 tenders recorded totaling $($res.data.paidAmount) JOD." }
        }
        return @{ Passed = $false; Message = "Split payment failed." }
    }

# 7. Create Draft Order and Park it (Hold Order)
$orderToPark = @{
    branchId = 1
    shiftId = 61
    tillId = 41
    orderType = 2
    notes = "Customer waiting in line - order to park"
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
$draftOrderResp = (Execute-PosScenario `
    -ScenarioId "POS-07-PRE" `
    -CaseType "Parked Order" `
    -NameEn "Create Draft Order to be Parked" `
    -Method "Post" `
    -Endpoint "/api/pos/orders" `
    -Headers $authHeaders `
    -Body $orderToPark `
    -ExpectedStatus 200).responseBody

$parkOrderId = $draftOrderResp.data.id

$sc7 = Execute-PosScenario `
    -ScenarioId "POS-07" `
    -CaseType "Parked Order" `
    -NameEn "Park Active Order to Free Cashier Terminal (Hold Order)" `
    -Method "Post" `
    -Endpoint "/api/pos/orders/$parkOrderId/park" `
    -Headers $authHeaders `
    -Body $null `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.status -eq "Parked") {
            return @{ Passed = $true; Message = "Order #$parkOrderId status transitioned to Parked." }
        }
        return @{ Passed = $false; Message = "Order status is not Parked." }
    }

# 8. Query Active & Parked Orders
$sc8 = Execute-PosScenario `
    -ScenarioId "POS-08" `
    -CaseType "Order Management" `
    -NameEn "Query Active and Parked Orders for Branch 1" `
    -Method "Get" `
    -Endpoint "/api/pos/orders/active?branchId=1&status=2" `
    -Headers $authHeaders `
    -Body $null `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -ne $null) {
            return @{ Passed = $true; Message = "Retrieved $($res.data.Count) active/parked orders successfully." }
        }
        return @{ Passed = $false; Message = "Failed to retrieve active orders." }
    }

# 9. Settle / Pay Parked Order
$settleParkedJson = '[{"paymentMethod": 1, "amount": 174.0, "currencyCode": "JOD"}]'
$sc9 = Execute-PosScenario `
    -ScenarioId "POS-09" `
    -CaseType "Parked Order Settle" `
    -NameEn "Resume and Settle Payment on Parked Order #$parkOrderId" `
    -Method "Post" `
    -Endpoint "/api/pos/orders/$parkOrderId/payments" `
    -Headers $authHeaders `
    -Body $settleParkedJson `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.status -eq "Completed" -and $res.data.isPaid -eq $true) {
            return @{ Passed = $true; Message = "Parked order #$parkOrderId successfully settled and completed." }
        }
        return @{ Passed = $false; Message = "Failed to settle parked order." }
    }

# 10. Void Line with Supervisor Approval
$orderForVoid = @{
    branchId = 1
    shiftId = 61
    tillId = 41
    orderType = 2
    notes = "Order with 2 lines to test line void"
    lines = @(
        @{
            lineNumber = 1
            itemId = 162
            itemCode = "ITM-OB-98083"
            itemName = "Office Desk"
            uomId = 1
            quantity = 1.0
            unitPrice = 150.0
        },
        @{
            lineNumber = 2
            itemId = 162
            itemCode = "ITM-OB-98083"
            itemName = "Office Chair"
            uomId = 1
            quantity = 2.0
            unitPrice = 50.0
        }
    )
}
$voidOrderResp = (Execute-PosScenario `
    -ScenarioId "POS-10-PRE" `
    -CaseType "Void Line" `
    -NameEn "Create Order with Multiple Lines for Void Test" `
    -Method "Post" `
    -Endpoint "/api/pos/orders" `
    -Headers $authHeaders `
    -Body $orderForVoid `
    -ExpectedStatus 200).responseBody

$voidOrderId = $voidOrderResp.data.id
$voidLineId = $voidOrderResp.data.lines[0].id

$voidLineBody = @{
    orderId = $voidOrderId
    orderLineId = $voidLineId
    reason = "Customer cancelled this item"
    approvedBy = "shift_supervisor"
}
$sc10 = Execute-PosScenario `
    -ScenarioId "POS-10" `
    -CaseType "Void Line" `
    -NameEn "Void Order Line with Supervisor Approval Tracking" `
    -Method "Post" `
    -Endpoint "/api/pos/orders/void-line" `
    -Headers $authHeaders `
    -Body $voidLineBody `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.lines[0].isVoided -eq $true) {
            return @{ Passed = $true; Message = "Line #$voidLineId marked as Voided and totals recalculated to $($res.data.totalAmount) JOD." }
        }
        return @{ Passed = $false; Message = "Void line failed." }
    }

# 11. Refund Completed Order (Return / Credit Note)
$orderToRefundId = $sc2.responseBody.data.id
$orderToRefundLineId = $sc2.responseBody.data.lines[0].id

$refundBody = @{
    orderId = $orderToRefundId
    refundReason = "Damaged item returned by customer"
    approvedBy = "manager_admin"
    returnToInventory = $true
    refundLines = @(
        @{
            orderLineId = $orderToRefundLineId
            quantityToRefund = 1.0
        }
    )
    refundPayments = @(
        @{
            paymentMethod = 1
            amount = 174.0
        }
    )
}
$sc11 = Execute-PosScenario `
    -ScenarioId "POS-11" `
    -CaseType "Refund and Returns" `
    -NameEn "Issue Refund and Return Inventory (Credit Note Order)" `
    -Method "Post" `
    -Endpoint "/api/pos/orders/refund" `
    -Headers $authHeaders `
    -Body $refundBody `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -and $res.data.status -eq "Refunded" -and $res.data.isRefund -eq $true) {
            return @{ Passed = $true; Message = "Refund Order #$($res.data.orderNumber) generated with negative balance ($($res.data.totalAmount) JOD)." }
        }
        return @{ Passed = $false; Message = "Refund order failed." }
    }

# 12. Delete / Cancel Unpaid Order
$orderToDeleteId = $voidOrderId
$sc12 = Execute-PosScenario `
    -ScenarioId "POS-12" `
    -CaseType "Order Cancellation" `
    -NameEn "Delete Unpaid Draft Order #$orderToDeleteId" `
    -Method "Delete" `
    -Endpoint "/api/pos/orders/$orderToDeleteId" `
    -Headers $authHeaders `
    -Body $null `
    -ExpectedStatus 200 `
    -CustomValidator {
        param($res, $status)
        if ($res.data -eq $true) {
            return @{ Passed = $true; Message = "Unpaid order #$orderToDeleteId deleted successfully." }
        }
        return @{ Passed = $false; Message = "Order deletion failed." }
    }

Write-Host "=========================================================="
# Filter out the PRE steps from final metric count
$finalScenarios = $posResults | Where-Object { $_.scenarioId -notlike "*-PRE" }
$passCount = ($finalScenarios | Where-Object { $_.passed -eq $true }).Count
Write-Host "POS Verification Complete! Passed: $passCount / $($finalScenarios.Count)"

$jsonOutput = $finalScenarios | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText("d:\ThinkOnErp\pos_scenarios_results.json", $jsonOutput, [System.Text.Encoding]::UTF8)
Write-Host "Results saved to d:\ThinkOnErp\pos_scenarios_results.json"
