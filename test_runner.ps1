$baseUrl = "http://178.104.126.99:5000"

# 1. Tenant Login
$loginBody = @{ userName = "admin"; password = "Admin@123"; companyCode = "1122"; language = 2 } | ConvertTo-Json
$authRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $authRes.data.accessToken
$headers = @{ Authorization = "Bearer $token"; "X-Company-Id" = "461" }

# 2. SuperAdmin Login
$saBody = @{ userName = "admin"; password = "Admin@123"; language = 2 } | ConvertTo-Json
$saRes = Invoke-RestMethod -Uri "$baseUrl/api/auth/superadmin/login" -Method Post -Body $saBody -ContentType "application/json"
$saToken = $saRes.data.accessToken

# Test Results Collection
$results = @()

function Run-Test {
    param($category, $name, $expectedStatus, [scriptblock]$action)
    try {
        $resp = & $action
        $statusCode = 200
        $data = $resp
        $errMsg = ""
    } catch {
        if ($_.Exception.Response -ne $null) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            $reader = New-Object System.IO.StreamReader $_.Exception.Response.GetResponseStream()
            $errMsg = $reader.ReadToEnd()
        } else {
            $statusCode = 500
            $errMsg = $_.Exception.Message
        }
        $data = $null
    }
    
    $passed = ($statusCode -eq $expectedStatus)
    [PSCustomObject]@{
        Category = $category
        TestName = $name
        ExpectedStatus = $expectedStatus
        ActualStatus = $statusCode
        Passed = $passed
        Details = if ($passed) { "Success" } else { $errMsg }
        RawData = $data
        ErrorBody = $errMsg
    }
}

# --- Stock Balances Query ---
$whId = 61
$itemId = 162

$bInit = Invoke-RestMethod -Uri "$baseUrl/api/inventory/stock/balances?itemId=$itemId&warehouseId=$whId" -Headers $headers -Method Get
$initialStock = $bInit.data[0].onHandQty
Write-Host "Initial Stock of Item $itemId in Warehouse $whId : $initialStock"

# --- POS Validations ---
# 1. Closed Shift
$t1 = Run-Test "POS Validation" "1. Prevent order on closed shift (ShiftId=1)" 400 {
    $b = @{
        branchId = 1; shiftId = 1; tillId = 1; orderType = 1;
        lines = @(@{ lineNumber = 1; itemId = $itemId; quantity = 1; unitPrice = 10; uomId = 1 })
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/api/pos/orders" -Headers $headers -Method Post -Body $b -ContentType "application/json"
}
$results += $t1

# 2. Till Mismatch
$t2 = Run-Test "POS Validation" "2. Prevent order on till mismatch (Shift 61 is on Till 41, sending Till 1)" 400 {
    $b = @{
        branchId = 1; shiftId = 61; tillId = 1; orderType = 1;
        lines = @(@{ lineNumber = 1; itemId = $itemId; quantity = 1; unitPrice = 10; uomId = 1 })
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/api/pos/orders" -Headers $headers -Method Post -Body $b -ContentType "application/json"
}
$results += $t2

# 3. Non-Positive Quantity
$t3 = Run-Test "POS Validation" "3. Prevent non-positive quantity (Quantity=0)" 400 {
    $b = @{
        branchId = 1; shiftId = 61; tillId = 41; orderType = 1;
        lines = @(@{ lineNumber = 1; itemId = $itemId; quantity = 0; unitPrice = 10; uomId = 1 })
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/api/pos/orders" -Headers $headers -Method Post -Body $b -ContentType "application/json"
}
$results += $t3

# 4. Invalid Manual Discount
$t4 = Run-Test "POS Validation" "4. Prevent invalid manual discount percent (Discount=150%)" 400 {
    $b = @{
        branchId = 1; shiftId = 61; tillId = 41; orderType = 1; manualDiscountPercent = 150;
        lines = @(@{ lineNumber = 1; itemId = $itemId; quantity = 1; unitPrice = 10; uomId = 1 })
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/api/pos/orders" -Headers $headers -Method Post -Body $b -ContentType "application/json"
}
$results += $t4

# 5. Invalid Payment Amount
$t5 = Run-Test "POS Validation" "5. Prevent negative/zero payment amount (Amount=-5)" 400 {
    $b = @{
        branchId = 1; shiftId = 61; tillId = 41; orderType = 1;
        lines = @(@{ lineNumber = 1; itemId = $itemId; quantity = 1; unitPrice = 10; uomId = 1 });
        payments = @(@{ paymentMethod = 1; amount = -5 })
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/api/pos/orders" -Headers $headers -Method Post -Body $b -ContentType "application/json"
}
$results += $t5

# 6. Negative Stock Forbidden & Insufficient Balance
$t6 = Run-Test "POS Validation" "6. Prevent sale exceeding available balance (Qty=99999 > Stock)" 400 {
    $b = @{
        branchId = 1; shiftId = 61; tillId = 41; orderType = 1;
        lines = @(@{ lineNumber = 1; itemId = $itemId; quantity = 99999; unitPrice = 10; uomId = 1 })
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/api/pos/orders" -Headers $headers -Method Post -Body $b -ContentType "application/json"
}
$results += $t6

# --- POS Order Creation & Real-Time Stock Deduction ---
$stockBeforePos = (Invoke-RestMethod -Uri "$baseUrl/api/inventory/stock/balances?itemId=$itemId&warehouseId=$whId" -Headers $headers -Method Get).data[0].onHandQty

$posOrderPayload = @{
    branchId = 1
    shiftId = 61
    tillId = 41
    orderType = 1
    lines = @(
        @{
            lineNumber = 1
            itemId = $itemId
            quantity = 1
            unitPrice = 10
            uomId = 1
        }
    )
    payments = @(
        @{
            paymentMethod = 1
            amount = 11.5
            tenderedAmount = 15.0
        }
    )
} | ConvertTo-Json -Depth 5

$tPosOrder = Run-Test "POS Order Flow" "7. Create & Pay POS Order with real-time stock deduction" 200 {
    Invoke-RestMethod -Uri "$baseUrl/api/pos/orders" -Headers $headers -Method Post -Body $posOrderPayload -ContentType "application/json"
}
$results += $tPosOrder

$createdPosOrderId = $tPosOrder.RawData.data.id
$createdPosOrderNum = $tPosOrder.RawData.data.orderNumber
$createdPosInvNum = $tPosOrder.RawData.data.invoiceNumber
Write-Host "Created POS Order ID: $createdPosOrderId, Number: $createdPosOrderNum, Inv: $createdPosInvNum"

$stockAfterPos = (Invoke-RestMethod -Uri "$baseUrl/api/inventory/stock/balances?itemId=$itemId&warehouseId=$whId" -Headers $headers -Method Get).data[0].onHandQty
Write-Host "Stock Before POS: $stockBeforePos, Stock After POS: $stockAfterPos (Deducted: $($stockBeforePos - $stockAfterPos))"

# --- POS Refund & Stock Restock ---
$refundPayload = @{
    orderId = $createdPosOrderId
    refundReason = "Customer return - Live Verification Test"
    refundLines = @(
        @{
            orderLineId = 1
            quantityToRefund = 1
        }
    )
} | ConvertTo-Json -Depth 5

$tPosRefund = Run-Test "POS Refund Flow" "8. Refund POS Order & restock inventory" 200 {
    Invoke-RestMethod -Uri "$baseUrl/api/pos/orders/refund" -Headers $headers -Method Post -Body $refundPayload -ContentType "application/json"
}
$results += $tPosRefund

$stockAfterRefund = (Invoke-RestMethod -Uri "$baseUrl/api/inventory/stock/balances?itemId=$itemId&warehouseId=$whId" -Headers $headers -Method Get).data[0].onHandQty
Write-Host "Stock After Refund: $stockAfterRefund (Restocked: $($stockAfterRefund - $stockAfterPos))"

# Output summary table
$results | Select-Object Category, TestName, ExpectedStatus, ActualStatus, Passed | Format-Table -AutoSize
