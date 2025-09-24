# Test Purchase Order Simple Flow

$baseUrl = "http://localhost:5256/api"
$headers = @{
    "Content-Type" = "application/json"
}

Write-Host "=== PURCHASE ORDER TEST ===" -ForegroundColor Cyan

# 1. Login
Write-Host "`n1. Login..." -ForegroundColor Green
$loginData = @{
    email = "admin@bookstore.com"
    password = "Admin123!"
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method POST -Headers $headers -Body $loginData
    if ($loginResponse.success) {
        $token = $loginResponse.data.token
        Write-Host "Login successful! Role: $($loginResponse.data.role)" -ForegroundColor Green
        
        $authHeaders = @{
            "Content-Type" = "application/json"
            "Authorization" = "Bearer $token"
        }
    } else {
        Write-Host "Login failed: $($loginResponse.message)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "Login error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 2. Get Publishers
Write-Host "`n2. Get Publishers..." -ForegroundColor Green
try {
    $publishersResponse = Invoke-RestMethod -Uri "$baseUrl/publisher" -Method GET -Headers $authHeaders
    $testPublisher = $publishersResponse.data.publishers[0]
    Write-Host "Using publisher: $($testPublisher.name) (ID: $($testPublisher.publisherId))" -ForegroundColor Green
} catch {
    Write-Host "Get publishers error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 3. Get Books
Write-Host "`n3. Get Books..." -ForegroundColor Green
try {
    $booksResponse = Invoke-RestMethod -Uri "$baseUrl/book?pageSize=5" -Method GET -Headers $authHeaders
    $testBook = $booksResponse.data.books[0]
    Write-Host "Using book: $($testBook.title) (ISBN: $($testBook.isbn))" -ForegroundColor Green
} catch {
    Write-Host "Get books error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 4. Create Purchase Order
Write-Host "`n4. Create Purchase Order..." -ForegroundColor Green
$purchaseOrderData = @{
    publisherId = $testPublisher.publisherId
    note = "Test purchase order"
    lines = @(
        @{
            isbn = $testBook.isbn
            qtyOrdered = 15
            unitPrice = 30000
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $purchaseOrderResponse = Invoke-RestMethod -Uri "$baseUrl/purchaseorder" -Method POST -Headers $authHeaders -Body $purchaseOrderData
    if ($purchaseOrderResponse.success) {
        $poId = $purchaseOrderResponse.data.poId
        Write-Host "Purchase Order created! ID: $poId" -ForegroundColor Green
        Write-Host "Status: $($purchaseOrderResponse.data.statusName) (ID: $($purchaseOrderResponse.data.statusId))" -ForegroundColor White
        Write-Host "Total: $($purchaseOrderResponse.data.totalAmount)" -ForegroundColor White
    } else {
        Write-Host "Create PO failed: $($purchaseOrderResponse.message)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "Create PO error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        try {
            $errorStream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($errorStream)
            $errorBody = $reader.ReadToEnd()
            Write-Host "Error response: $errorBody" -ForegroundColor Red
        } catch {}
    }
    exit 1
}

# 5. Change Status 1 to 2
Write-Host "`n5. Change Status to Sent..." -ForegroundColor Green
$changeStatusData = @{
    newStatusId = 2
    note = "Changed to Sent status"
} | ConvertTo-Json

try {
    $changeStatusResponse = Invoke-RestMethod -Uri "$baseUrl/purchaseorder/$poId/change-status" -Method POST -Headers $authHeaders -Body $changeStatusData
    if ($changeStatusResponse.success) {
        Write-Host "Status changed successfully!" -ForegroundColor Green
        Write-Host "New Status: $($changeStatusResponse.data.statusName) (ID: $($changeStatusResponse.data.statusId))" -ForegroundColor White
        Write-Host "Order File: $($changeStatusResponse.data.orderFileUrl)" -ForegroundColor White
    } else {
        Write-Host "Change status failed: $($changeStatusResponse.message)" -ForegroundColor Red
    }
} catch {
    Write-Host "Change status error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        try {
            $errorStream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($errorStream)
            $errorBody = $reader.ReadToEnd()
            Write-Host "Error response: $errorBody" -ForegroundColor Red
        } catch {}
    }
}

# 6. Change Status 2 to 3
Write-Host "`n6. Change Status to Confirmed..." -ForegroundColor Green
$changeStatus2Data = @{
    newStatusId = 3
    note = "Changed to Confirmed status"
} | ConvertTo-Json

try {
    $changeStatus2Response = Invoke-RestMethod -Uri "$baseUrl/purchaseorder/$poId/change-status" -Method POST -Headers $authHeaders -Body $changeStatus2Data
    if ($changeStatus2Response.success) {
        Write-Host "Status changed to Confirmed!" -ForegroundColor Green
        Write-Host "Final Status: $($changeStatus2Response.data.statusName) (ID: $($changeStatus2Response.data.statusId))" -ForegroundColor White
    } else {
        Write-Host "Change status failed: $($changeStatus2Response.message)" -ForegroundColor Red
    }
} catch {
    Write-Host "Change status error: $($_.Exception.Message)" -ForegroundColor Red
}

# 7. Get Final PO Details
Write-Host "`n7. Get Final PO Details..." -ForegroundColor Green
try {
    $finalPoResponse = Invoke-RestMethod -Uri "$baseUrl/purchaseorder/$poId" -Method GET -Headers $authHeaders
    if ($finalPoResponse.success) {
        Write-Host "Final PO Details:" -ForegroundColor White
        Write-Host "  Status: $($finalPoResponse.data.statusName)" -ForegroundColor Gray
        Write-Host "  Created By: $($finalPoResponse.data.createdByName)" -ForegroundColor Gray
        Write-Host "  Order File: $($finalPoResponse.data.orderFileUrl)" -ForegroundColor Gray
        Write-Host "  Note: $($finalPoResponse.data.note)" -ForegroundColor Gray
    }
} catch {
    Write-Host "Get final PO error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== TEST COMPLETED ===" -ForegroundColor Cyan
Write-Host "PO ID: $poId" -ForegroundColor Yellow

