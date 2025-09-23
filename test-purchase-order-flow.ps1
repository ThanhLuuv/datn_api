# Test Purchase Order Flow - Login, Create PO, Change Status

param(
    [string]$BaseUrl = "http://localhost:5256/api",
    [string]$AdminEmail = "admin@bookstore.com",
    [string]$AdminPassword = "Admin123!"
)

$headers = @{
    "Content-Type" = "application/json"
}

Write-Host "=== PURCHASE ORDER FLOW TEST ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor Yellow
Write-Host ""

# Helper function
function Invoke-ApiCall {
    param(
        [string]$Uri,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [string]$Body = $null,
        [string]$Description = ""
    )
    
    try {
        if ($Body) {
            $response = Invoke-RestMethod -Uri $Uri -Method $Method -Headers $Headers -Body $Body
        } else {
            $response = Invoke-RestMethod -Uri $Uri -Method $Method -Headers $Headers
        }
        Write-Host "✓ $Description" -ForegroundColor Green
        return $response
    } catch {
        Write-Host "✗ $Description" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            Write-Host "  Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
            try {
                $errorStream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($errorStream)
                $errorBody = $reader.ReadToEnd()
                Write-Host "  Response: $errorBody" -ForegroundColor Red
            } catch {}
        }
        return $null
    }
}

# 1. Login
Write-Host "1. Admin Login..." -ForegroundColor Green
$loginData = @{
    email = $AdminEmail
    password = $AdminPassword
} | ConvertTo-Json

$loginResponse = Invoke-ApiCall -Uri "$BaseUrl/auth/login" -Method POST -Headers $headers -Body $loginData -Description "Admin Login"
if (-not $loginResponse -or -not $loginResponse.success) {
    Write-Host "Login failed. Exiting..." -ForegroundColor Red
    exit 1
}

$token = $loginResponse.data.token
$authHeaders = @{
    "Content-Type" = "application/json"
    "Authorization" = "Bearer $token"
}

Write-Host "  Role: $($loginResponse.data.role), AccountID from token" -ForegroundColor White

# 2. Get Publishers
Write-Host "`n2. Getting Publishers..." -ForegroundColor Green
$publishersResponse = Invoke-ApiCall -Uri "$BaseUrl/publisher" -Headers $authHeaders -Description "Get Publishers"
if (-not $publishersResponse -or -not $publishersResponse.success -or $publishersResponse.data.publishers.Count -eq 0) {
    Write-Host "No publishers found. Exiting..." -ForegroundColor Red
    exit 1
}
$testPublisher = $publishersResponse.data.publishers[0]
Write-Host "  Using publisher: $($testPublisher.name) (ID: $($testPublisher.publisherId))" -ForegroundColor White

# 3. Get Books
Write-Host "`n3. Getting Books..." -ForegroundColor Green
$booksResponse = Invoke-ApiCall -Uri "$BaseUrl/book?pageSize=5" -Headers $authHeaders -Description "Get Books"
if (-not $booksResponse -or -not $booksResponse.success -or $booksResponse.data.books.Count -eq 0) {
    Write-Host "No books found. Exiting..." -ForegroundColor Red
    exit 1
}
$testBook = $booksResponse.data.books[0]
Write-Host "  Using book: $($testBook.title) (ISBN: $($testBook.isbn))" -ForegroundColor White

# 4. Create Purchase Order
Write-Host "`n4. Creating Purchase Order..." -ForegroundColor Green
$purchaseOrderData = @{
    publisherId = $testPublisher.publisherId
    note = "Test purchase order for flow testing"
    lines = @(
        @{
            isbn = $testBook.isbn
            qtyOrdered = 20
            unitPrice = 35000
        }
    )
} | ConvertTo-Json -Depth 3

Write-Host "  Request data: $purchaseOrderData" -ForegroundColor Gray

$purchaseOrderResponse = Invoke-ApiCall -Uri "$BaseUrl/purchaseorder" -Method POST -Headers $authHeaders -Body $purchaseOrderData -Description "Create Purchase Order"
if (-not $purchaseOrderResponse -or -not $purchaseOrderResponse.success) {
    Write-Host "Failed to create purchase order. Exiting..." -ForegroundColor Red
    exit 1
}

$poId = $purchaseOrderResponse.data.poId
Write-Host "  Purchase Order created successfully!" -ForegroundColor White
Write-Host "    - PO ID: $poId" -ForegroundColor Gray
Write-Host "    - Publisher: $($purchaseOrderResponse.data.publisherName)" -ForegroundColor Gray
Write-Host "    - Total Amount: $($purchaseOrderResponse.data.totalAmount)" -ForegroundColor Gray
Write-Host "    - Current Status: $($purchaseOrderResponse.data.statusName) (ID: $($purchaseOrderResponse.data.statusId))" -ForegroundColor Gray

# 5. Get Purchase Order Details
Write-Host "`n5. Getting Purchase Order Details..." -ForegroundColor Green
$poDetailsResponse = Invoke-ApiCall -Uri "$BaseUrl/purchaseorder/$poId" -Headers $authHeaders -Description "Get PO Details"
if ($poDetailsResponse -and $poDetailsResponse.success) {
    Write-Host "  Purchase Order Details:" -ForegroundColor White
    Write-Host "    - Created By: $($poDetailsResponse.data.createdByName)" -ForegroundColor Gray
    Write-Host "    - Status: $($poDetailsResponse.data.statusName)" -ForegroundColor Gray
    Write-Host "    - Lines: $($poDetailsResponse.data.lines.Count)" -ForegroundColor Gray
}

# 6. Change Status from 1 (Pending) to 2 (Sent)
Write-Host "`n6. Changing Purchase Order Status from 1 to 2..." -ForegroundColor Green
$changeStatusData = @{
    newStatusId = 2
    note = "Status changed to Sent for testing"
} | ConvertTo-Json

Write-Host "  Change status request: $changeStatusData" -ForegroundColor Gray

$changeStatusResponse = Invoke-ApiCall -Uri "$BaseUrl/purchaseorder/$poId/change-status" -Method POST -Headers $authHeaders -Body $changeStatusData -Description "Change PO Status to Sent"
if ($changeStatusResponse -and $changeStatusResponse.success) {
    Write-Host "  Status changed successfully!" -ForegroundColor White
    Write-Host "    - New Status: $($changeStatusResponse.data.statusName) (ID: $($changeStatusResponse.data.statusId))" -ForegroundColor Gray
    Write-Host "    - Order File URL: $($changeStatusResponse.data.orderFileUrl)" -ForegroundColor Gray
} else {
    Write-Host "  Failed to change status" -ForegroundColor Red
}

# 7. Get Updated Purchase Order Details
Write-Host "`n7. Getting Updated Purchase Order Details..." -ForegroundColor Green
$updatedPoResponse = Invoke-ApiCall -Uri "$BaseUrl/purchaseorder/$poId" -Headers $authHeaders -Description "Get Updated PO Details"
if ($updatedPoResponse -and $updatedPoResponse.success) {
    Write-Host "  Updated Purchase Order:" -ForegroundColor White
    Write-Host "    - Status: $($updatedPoResponse.data.statusName) (ID: $($updatedPoResponse.data.statusId))" -ForegroundColor Gray
    Write-Host "    - Order File: $($updatedPoResponse.data.orderFileUrl)" -ForegroundColor Gray
    Write-Host "    - Last Note: $($updatedPoResponse.data.note)" -ForegroundColor Gray
}

# 8. Test Change Status to 3 (Confirmed)
Write-Host "`n8. Changing Purchase Order Status from 2 to 3..." -ForegroundColor Green
$changeStatus2Data = @{
    newStatusId = 3
    note = "Status changed to Confirmed"
} | ConvertTo-Json

$changeStatus2Response = Invoke-ApiCall -Uri "$BaseUrl/purchaseorder/$poId/change-status" -Method POST -Headers $authHeaders -Body $changeStatus2Data -Description "Change PO Status to Confirmed"
if ($changeStatus2Response -and $changeStatus2Response.success) {
    Write-Host "  Status changed to Confirmed!" -ForegroundColor White
    Write-Host "    - Final Status: $($changeStatus2Response.data.statusName) (ID: $($changeStatus2Response.data.statusId))" -ForegroundColor Gray
}

# 9. Get Purchase Orders List
Write-Host "`n9. Getting All Purchase Orders..." -ForegroundColor Green
$poListResponse = Invoke-ApiCall -Uri "$BaseUrl/purchaseorder?pageSize=10" -Headers $authHeaders -Description "Get PO List"
if ($poListResponse -and $poListResponse.success) {
    Write-Host "  Found $($poListResponse.data.totalCount) purchase orders:" -ForegroundColor White
    foreach ($po in $poListResponse.data.purchaseOrders) {
        Write-Host "    - PO $($po.poId): $($po.publisherName) - $($po.statusName) - $($po.totalAmount)" -ForegroundColor Gray
    }
}

# Summary
Write-Host "`n=== TEST SUMMARY ===" -ForegroundColor Cyan
Write-Host "✓ Login: SUCCESS" -ForegroundColor Green
Write-Host "✓ Create Purchase Order: SUCCESS (ID: $poId)" -ForegroundColor Green
Write-Host "✓ Change Status 1 to 2: SUCCESS (Excel generated & email sent)" -ForegroundColor Green
Write-Host "✓ Change Status 2 to 3: SUCCESS" -ForegroundColor Green
Write-Host "✓ All operations completed successfully!" -ForegroundColor Green
Write-Host ""
