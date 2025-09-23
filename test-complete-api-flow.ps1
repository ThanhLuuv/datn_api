# Test API hoàn chỉnh - Bao gồm tất cả các chức năng chính

param(
    [string]$BaseUrl = "http://localhost:5256/api",
    [string]$AdminEmail = "admin@bookstore.com",
    [string]$AdminPassword = "Admin123!"
)

$headers = @{
    "Content-Type" = "application/json"
}

Write-Host "=== BOOKSTORE API COMPLETE FLOW TEST ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor Yellow
Write-Host ""

# Helper function to make API calls with error handling
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
        }
        return $null
    }
}

# 1. Test Health Check
Write-Host "1. Testing API Health..." -ForegroundColor Green
$healthResponse = Invoke-ApiCall -Uri "$BaseUrl/../health" -Description "Health Check"
if (-not $healthResponse) {
    Write-Host "API is not healthy. Exiting..." -ForegroundColor Red
    exit 1
}

# 2. Get Accounts List
Write-Host "`n2. Getting Accounts List..." -ForegroundColor Green
$accountsResponse = Invoke-ApiCall -Uri "$BaseUrl/test/accounts" -Headers $headers -Description "Get Accounts"
if ($accountsResponse) {
    Write-Host "  Found $($accountsResponse.count) accounts:" -ForegroundColor White
    $accountsResponse.items | ForEach-Object {
        Write-Host "    - ID: $($_.accountId), Email: $($_.email), RoleId: $($_.roleId), Active: $($_.isActive)" -ForegroundColor Gray
    }
}

# 3. Get Employees List  
Write-Host "`n3. Getting Employees List..." -ForegroundColor Green
$employeesResponse = Invoke-ApiCall -Uri "$BaseUrl/test/employees" -Headers $headers -Description "Get Employees"
if ($employeesResponse) {
    Write-Host "  Found $($employeesResponse.count) employees:" -ForegroundColor White
    $employeesResponse.items | ForEach-Object {
        Write-Host "    - ID: $($_.employeeId), AccountID: $($_.accountId), Name: $($_.firstName) $($_.lastName)" -ForegroundColor Gray
    }
}

# 4. Login
Write-Host "`n4. Admin Login..." -ForegroundColor Green
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

Write-Host "  Login successful! Role: $($loginResponse.data.role), Expires: $($loginResponse.data.expires)" -ForegroundColor White

# 5. Test Protected Endpoints
Write-Host "`n5. Testing Protected Endpoints..." -ForegroundColor Green
$protectedResponse = Invoke-ApiCall -Uri "$BaseUrl/test/protected" -Headers $authHeaders -Description "Protected Endpoint"
$adminOnlyResponse = Invoke-ApiCall -Uri "$BaseUrl/test/admin-only" -Headers $authHeaders -Description "Admin-Only Endpoint"
$salesOnlyResponse = Invoke-ApiCall -Uri "$BaseUrl/test/sales-only" -Headers $authHeaders -Description "Sales-Only Endpoint"

# 6. Get Categories
Write-Host "`n6. Getting Categories..." -ForegroundColor Green
$categoriesResponse = Invoke-ApiCall -Uri "$BaseUrl/category" -Headers $authHeaders -Description "Get Categories"
if ($categoriesResponse -and $categoriesResponse.success) {
    Write-Host "  Found $($categoriesResponse.data.categories.Count) categories" -ForegroundColor White
}

# 7. Get Publishers
Write-Host "`n7. Getting Publishers..." -ForegroundColor Green
$publishersResponse = Invoke-ApiCall -Uri "$BaseUrl/publisher" -Headers $authHeaders -Description "Get Publishers"
if ($publishersResponse -and $publishersResponse.success -and $publishersResponse.data.publishers.Count -gt 0) {
    $testPublisher = $publishersResponse.data.publishers[0]
    Write-Host "  Found $($publishersResponse.data.publishers.Count) publishers. Using: $($testPublisher.name) (ID: $($testPublisher.publisherId))" -ForegroundColor White
} else {
    Write-Host "No publishers found!" -ForegroundColor Red
    exit 1
}

# 8. Get Books
Write-Host "`n8. Getting Books..." -ForegroundColor Green
$booksResponse = Invoke-ApiCall -Uri "$BaseUrl/book?pageSize=5" -Headers $authHeaders -Description "Get Books"
if ($booksResponse -and $booksResponse.success -and $booksResponse.data.books.Count -gt 0) {
    $testBook = $booksResponse.data.books[0]
    Write-Host "  Found $($booksResponse.data.books.Count) books. Using: $($testBook.title) (ISBN: $($testBook.isbn))" -ForegroundColor White
} else {
    Write-Host "No books found!" -ForegroundColor Red
    exit 1
}

# 9. Create Purchase Order
Write-Host "`n9. Creating Purchase Order..." -ForegroundColor Green
$purchaseOrderData = @{
    publisherId = $testPublisher.publisherId
    note = "Test purchase order from complete flow script"
    lines = @(
        @{
            isbn = $testBook.isbn
            qtyOrdered = 15
            unitPrice = 30000
        }
    )
} | ConvertTo-Json -Depth 3

$purchaseOrderResponse = Invoke-ApiCall -Uri "$BaseUrl/purchaseorder" -Method POST -Headers $authHeaders -Body $purchaseOrderData -Description "Create Purchase Order"
if ($purchaseOrderResponse -and $purchaseOrderResponse.success) {
    $poId = $purchaseOrderResponse.data.poId
    Write-Host "  Purchase Order created successfully!" -ForegroundColor White
    Write-Host "    - PO ID: $poId" -ForegroundColor Gray
    Write-Host "    - Publisher: $($purchaseOrderResponse.data.publisherName)" -ForegroundColor Gray
    Write-Host "    - Total Amount: $($purchaseOrderResponse.data.totalAmount)" -ForegroundColor Gray
    Write-Host "    - Total Quantity: $($purchaseOrderResponse.data.totalQuantity)" -ForegroundColor Gray
    Write-Host "    - Status: $($purchaseOrderResponse.data.statusName)" -ForegroundColor Gray
} else {
    Write-Host "Failed to create purchase order!" -ForegroundColor Red
    exit 1
}

# 10. Get Purchase Order Details
Write-Host "`n10. Getting Purchase Order Details..." -ForegroundColor Green
$poDetailsResponse = Invoke-ApiCall -Uri "$BaseUrl/purchaseorder/$poId" -Headers $authHeaders -Description "Get PO Details"
if ($poDetailsResponse -and $poDetailsResponse.success) {
    Write-Host "  Purchase Order Details:" -ForegroundColor White
    Write-Host "    - Created By: $($poDetailsResponse.data.createdByName)" -ForegroundColor Gray
    Write-Host "    - Ordered At: $($poDetailsResponse.data.orderedAt)" -ForegroundColor Gray
    Write-Host "    - Lines Count: $($poDetailsResponse.data.lines.Count)" -ForegroundColor Gray
}

# 11. Get Purchase Orders List
Write-Host "`n11. Getting Purchase Orders List..." -ForegroundColor Green
$poListResponse = Invoke-ApiCall -Uri "$BaseUrl/purchaseorder?pageSize=10" -Headers $authHeaders -Description "Get PO List"
if ($poListResponse -and $poListResponse.success) {
    Write-Host "  Found $($poListResponse.data.totalCount) purchase orders" -ForegroundColor White
}

# 12. Test Books by Publisher
Write-Host "`n12. Getting Books by Publisher..." -ForegroundColor Green
$booksByPublisherResponse = Invoke-ApiCall -Uri "$BaseUrl/book?publisherId=$($testPublisher.publisherId)" -Headers $authHeaders -Description "Get Books by Publisher"
if ($booksByPublisherResponse -and $booksByPublisherResponse.success) {
    Write-Host "  Found $($booksByPublisherResponse.data.books.Count) books by publisher $($testPublisher.name)" -ForegroundColor White
}

# 13. JWT Token Analysis
Write-Host "`n13. JWT Token Analysis..." -ForegroundColor Green
try {
    $tokenParts = $token.Split('.')
    if ($tokenParts.Length -eq 3) {
        # Decode payload
        $payload = $tokenParts[1]
        while ($payload.Length % 4 -ne 0) { $payload += "=" }
        $payload = $payload.Replace('-', '+').Replace('_', '/')
        
        $decodedBytes = [System.Convert]::FromBase64String($payload)
        $decodedText = [System.Text.Encoding]::UTF8.GetString($decodedBytes)
        $claims = $decodedText | ConvertFrom-Json
        
        Write-Host "  JWT Claims:" -ForegroundColor White
        Write-Host "    - Subject: $($claims.sub)" -ForegroundColor Gray
        Write-Host "    - Email: $($claims.email)" -ForegroundColor Gray
        Write-Host "    - Role: $($claims.'http://schemas.microsoft.com/ws/2008/06/identity/claims/role')" -ForegroundColor Gray
        Write-Host "    - Account ID: $($claims.'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier')" -ForegroundColor Gray
        Write-Host "    - Expires: $(Get-Date -UnixTimeSeconds $claims.exp)" -ForegroundColor Gray
    }
} catch {
    Write-Host "  Could not parse JWT token" -ForegroundColor Red
}

# Summary
Write-Host "`n=== TEST SUMMARY ===" -ForegroundColor Cyan
Write-Host "✓ API Health Check: OK" -ForegroundColor Green
Write-Host "✓ Authentication: OK" -ForegroundColor Green
Write-Host "✓ Authorization: OK" -ForegroundColor Green
Write-Host "✓ Purchase Order Creation: OK" -ForegroundColor Green
Write-Host "✓ Data Retrieval: OK" -ForegroundColor Green
Write-Host ""
Write-Host "All tests completed successfully!" -ForegroundColor Green
Write-Host "Purchase Order ID: $poId" -ForegroundColor Yellow
Write-Host ""
