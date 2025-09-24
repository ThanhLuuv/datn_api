# Test API đầy đủ - Debug lỗi 401 khi tạo đơn hàng
# Bao gồm: lấy danh sách account, employee, đăng nhập, tạo đơn hàng

$baseUrl = "https://localhost:7138/api"
$headers = @{
    "Content-Type" = "application/json"
}

Write-Host "=== BOOKSTORE API DEBUG TEST ===" -ForegroundColor Cyan
Write-Host "Base URL: $baseUrl" -ForegroundColor Yellow
Write-Host ""

# 1. Test kết nối API
Write-Host "1. Testing API connection..." -ForegroundColor Green
try {
    $healthResponse = Invoke-RestMethod -Uri "$baseUrl/../health" -Method GET
    Write-Host "✓ API is healthy" -ForegroundColor Green
} catch {
    Write-Host "✗ API connection failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 2. Lấy danh sách accounts
Write-Host "`n2. Getting accounts list..." -ForegroundColor Green
try {
    $accountsResponse = Invoke-RestMethod -Uri "$baseUrl/test/accounts" -Method GET -Headers $headers
    Write-Host "✓ Found $($accountsResponse.count) accounts:" -ForegroundColor Green
    $accountsResponse.items | ForEach-Object {
        Write-Host "  - ID: $($_.accountId), Email: $($_.email), RoleId: $($_.roleId), Active: $($_.isActive)" -ForegroundColor White
    }
    
    # Chọn account đầu tiên để test
    $testAccount = $accountsResponse.items | Where-Object { $_.isActive -eq $true } | Select-Object -First 1
    if (-not $testAccount) {
        Write-Host "✗ No active account found!" -ForegroundColor Red
        exit 1
    }
    Write-Host "  → Using account: $($testAccount.email) (ID: $($testAccount.accountId))" -ForegroundColor Yellow
} catch {
    Write-Host "✗ Failed to get accounts: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 3. Lấy danh sách employees
Write-Host "`n3. Getting employees list..." -ForegroundColor Green
try {
    $employeesResponse = Invoke-RestMethod -Uri "$baseUrl/test/employees" -Method GET -Headers $headers
    Write-Host "✓ Found $($employeesResponse.count) employees:" -ForegroundColor Green
    $employeesResponse.items | ForEach-Object {
        Write-Host "  - ID: $($_.employeeId), AccountID: $($_.accountId), Name: $($_.firstName) $($_.lastName), Email: $($_.email)" -ForegroundColor White
    }
} catch {
    Write-Host "✗ Failed to get employees: $($_.Exception.Message)" -ForegroundColor Red
}

# 4. Đăng nhập để lấy token
Write-Host "`n4. Login to get JWT token..." -ForegroundColor Green
$loginData = @{
    email = $testAccount.email
    password = "123456"  # Default password từ seed data
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method POST -Headers $headers -Body $loginData
    if ($loginResponse.success) {
        $token = $loginResponse.data.token
        Write-Host "✓ Login successful!" -ForegroundColor Green
        Write-Host "  - Email: $($loginResponse.data.email)" -ForegroundColor White
        Write-Host "  - Role: $($loginResponse.data.role)" -ForegroundColor White
        Write-Host "  - Token: $($token.Substring(0, 50))..." -ForegroundColor White
        Write-Host "  - Expires: $($loginResponse.data.expires)" -ForegroundColor White
        
        # Cập nhật headers với token
        $authHeaders = @{
            "Content-Type" = "application/json"
            "Authorization" = "Bearer $token"
        }
    } else {
        Write-Host "✗ Login failed: $($loginResponse.message)" -ForegroundColor Red
        Write-Host "Errors: $($loginResponse.errors -join ', ')" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Login request failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $errorDetails = $_.Exception.Response | ConvertFrom-Json
        Write-Host "Error details: $($errorDetails | ConvertTo-Json -Depth 3)" -ForegroundColor Red
    }
    exit 1
}

# 5. Test protected endpoint với token
Write-Host "`n5. Testing protected endpoint with token..." -ForegroundColor Green
try {
    $protectedResponse = Invoke-RestMethod -Uri "$baseUrl/test/protected" -Method GET -Headers $authHeaders
    Write-Host "✓ Protected endpoint works!" -ForegroundColor Green
    Write-Host "  - Message: $($protectedResponse.message)" -ForegroundColor White
    Write-Host "  - Email: $($protectedResponse.email)" -ForegroundColor White
    Write-Host "  - Role: $($protectedResponse.role)" -ForegroundColor White
} catch {
    Write-Host "✗ Protected endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

# 6. Lấy danh sách publishers để tạo đơn hàng
Write-Host "`n6. Getting publishers for purchase order..." -ForegroundColor Green
try {
    $publishersResponse = Invoke-RestMethod -Uri "$baseUrl/publisher" -Method GET -Headers $authHeaders
    if ($publishersResponse.success -and $publishersResponse.data.publishers.Count -gt 0) {
        $testPublisher = $publishersResponse.data.publishers[0]
        Write-Host "✓ Found publishers, using: $($testPublisher.name) (ID: $($testPublisher.publisherId))" -ForegroundColor Green
    } else {
        Write-Host "✗ No publishers found!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Failed to get publishers: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 7. Lấy danh sách books để tạo đơn hàng
Write-Host "`n7. Getting books for purchase order..." -ForegroundColor Green
try {
    $booksResponse = Invoke-RestMethod -Uri "$baseUrl/book?pageSize=5" -Method GET -Headers $authHeaders
    if ($booksResponse.success -and $booksResponse.data.books.Count -gt 0) {
        $testBook = $booksResponse.data.books[0]
        Write-Host "✓ Found books, using: $($testBook.title) (ISBN: $($testBook.isbn))" -ForegroundColor Green
    } else {
        Write-Host "✗ No books found!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Failed to get books: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 8. Tạo đơn đặt mua - DEBUG CHÍNH
Write-Host "`n8. Creating purchase order (MAIN DEBUG)..." -ForegroundColor Green
$purchaseOrderData = @{
    publisherId = $testPublisher.publisherId
    note = "Test purchase order from debug script"
    lines = @(
        @{
            isbn = $testBook.isbn
            qtyOrdered = 10
            unitPrice = 25000
        }
    )
} | ConvertTo-Json -Depth 3

Write-Host "Request data:" -ForegroundColor Yellow
Write-Host $purchaseOrderData -ForegroundColor White
Write-Host ""
Write-Host "Headers:" -ForegroundColor Yellow
$authHeaders.GetEnumerator() | ForEach-Object {
    if ($_.Key -eq "Authorization") {
        Write-Host "  $($_.Key): Bearer $($token.Substring(0, 50))..." -ForegroundColor White
    } else {
        Write-Host "  $($_.Key): $($_.Value)" -ForegroundColor White
    }
}
Write-Host ""

try {
    $purchaseOrderResponse = Invoke-RestMethod -Uri "$baseUrl/purchaseorder" -Method POST -Headers $authHeaders -Body $purchaseOrderData
    if ($purchaseOrderResponse.success) {
        Write-Host "✓ Purchase order created successfully!" -ForegroundColor Green
        Write-Host "  - PO ID: $($purchaseOrderResponse.data.poId)" -ForegroundColor White
        Write-Host "  - Publisher: $($purchaseOrderResponse.data.publisherName)" -ForegroundColor White
        Write-Host "  - Total Amount: $($purchaseOrderResponse.data.totalAmount)" -ForegroundColor White
        Write-Host "  - Total Quantity: $($purchaseOrderResponse.data.totalQuantity)" -ForegroundColor White
        Write-Host "  - Created By: $($purchaseOrderResponse.data.createdByName)" -ForegroundColor White
        Write-Host "  - Status: $($purchaseOrderResponse.data.statusName)" -ForegroundColor White
    } else {
        Write-Host "✗ Purchase order creation failed!" -ForegroundColor Red
        Write-Host "  - Message: $($purchaseOrderResponse.message)" -ForegroundColor Red
        Write-Host "  - Errors: $($purchaseOrderResponse.errors -join ', ')" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ Purchase order request failed!" -ForegroundColor Red
    Write-Host "  - Exception: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        Write-Host "  - Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
        Write-Host "  - Status Description: $($_.Exception.Response.StatusDescription)" -ForegroundColor Red
        
        try {
            $errorStream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($errorStream)
            $errorBody = $reader.ReadToEnd()
            Write-Host "  - Response Body: $errorBody" -ForegroundColor Red
        } catch {
            Write-Host "  - Could not read error response body" -ForegroundColor Red
        }
    }
}

# 9. Test JWT token validation
Write-Host "`n9. Testing JWT token details..." -ForegroundColor Green
try {
    # Parse JWT token để xem claims
    $tokenParts = $token.Split('.')
    if ($tokenParts.Length -eq 3) {
        # Decode payload (base64url)
        $payload = $tokenParts[1]
        # Add padding if needed
        while ($payload.Length % 4 -ne 0) {
            $payload += "="
        }
        # Replace URL-safe characters
        $payload = $payload.Replace('-', '+').Replace('_', '/')
        
        $decodedBytes = [System.Convert]::FromBase64String($payload)
        $decodedText = [System.Text.Encoding]::UTF8.GetString($decodedBytes)
        $claims = $decodedText | ConvertFrom-Json
        
        Write-Host "✓ JWT Claims:" -ForegroundColor Green
        Write-Host "  - Subject (sub): $($claims.sub)" -ForegroundColor White
        Write-Host "  - Email: $($claims.email)" -ForegroundColor White
        Write-Host "  - Role: $($claims.'http://schemas.microsoft.com/ws/2008/06/identity/claims/role')" -ForegroundColor White
        Write-Host "  - NameIdentifier: $($claims.'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier')" -ForegroundColor White
        Write-Host "  - Issued At: $($claims.iat)" -ForegroundColor White
        Write-Host "  - Expires: $($claims.exp)" -ForegroundColor White
        Write-Host "  - JTI: $($claims.jti)" -ForegroundColor White
    }
} catch {
    Write-Host "✗ Failed to parse JWT token: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== TEST COMPLETED ===" -ForegroundColor Cyan
Write-Host "Review the results above to identify the 401 error cause." -ForegroundColor Yellow

