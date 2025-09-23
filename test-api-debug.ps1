# Test API Debug - Kiểm tra lỗi 401 khi tạo đơn hàng

$baseUrl = "http://localhost:5256/api"
$headers = @{
    "Content-Type" = "application/json"
}

Write-Host "=== BOOKSTORE API DEBUG TEST ===" -ForegroundColor Cyan

# 1. Lấy danh sách accounts
Write-Host "" 
Write-Host "1. Getting accounts..." -ForegroundColor Green
$accountsResponse = Invoke-RestMethod -Uri "$baseUrl/test/accounts" -Method GET -Headers $headers
Write-Host "Found $($accountsResponse.count) accounts"
$testAccount = $accountsResponse.items | Where-Object { $_.isActive -eq $true } | Select-Object -First 1
Write-Host "Using account: $($testAccount.email)"

# 2. Lấy danh sách employees  
Write-Host ""
Write-Host "2. Getting employees..." -ForegroundColor Green
$employeesResponse = Invoke-RestMethod -Uri "$baseUrl/test/employees" -Method GET -Headers $headers
Write-Host "Found $($employeesResponse.count) employees"

# 3. Đăng nhập
Write-Host ""
Write-Host "3. Login..." -ForegroundColor Green
$loginData = @{
    email = $testAccount.email
    password = "Admin123!"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method POST -Headers $headers -Body $loginData
if ($loginResponse.success) {
    $token = $loginResponse.data.token
    Write-Host "Login successful! Role: $($loginResponse.data.role)"
    
    $authHeaders = @{
        "Content-Type" = "application/json"
        "Authorization" = "Bearer $token"
    }
} else {
    Write-Host "Login failed: $($loginResponse.message)" -ForegroundColor Red
    exit 1
}

# 4. Test protected endpoint
Write-Host ""
Write-Host "4. Testing protected endpoint..." -ForegroundColor Green
try {
    $protectedResponse = Invoke-RestMethod -Uri "$baseUrl/test/protected" -Method GET -Headers $authHeaders
    Write-Host "Protected endpoint works! Email: $($protectedResponse.email), Role: $($protectedResponse.role)"
} catch {
    Write-Host "Protected endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
}

# 5. Lấy publishers
Write-Host ""
Write-Host "5. Getting publishers..." -ForegroundColor Green
$publishersResponse = Invoke-RestMethod -Uri "$baseUrl/publisher" -Method GET -Headers $authHeaders
$testPublisher = $publishersResponse.data.publishers[0]
Write-Host "Using publisher: $($testPublisher.name) (ID: $($testPublisher.publisherId))"

# 6. Lấy books
Write-Host ""
Write-Host "6. Getting books..." -ForegroundColor Green
$booksResponse = Invoke-RestMethod -Uri "$baseUrl/book?pageSize=5" -Method GET -Headers $authHeaders
$testBook = $booksResponse.data.books[0]
Write-Host "Using book: $($testBook.title) (ISBN: $($testBook.isbn))"

# 7. Tạo đơn đặt mua - MAIN DEBUG
Write-Host ""
Write-Host "7. Creating purchase order (MAIN DEBUG)..." -ForegroundColor Green
$purchaseOrderData = @{
    publisherId = $testPublisher.publisherId
    note = "Test purchase order"
    lines = @(
        @{
            isbn = $testBook.isbn
            qtyOrdered = 10
            unitPrice = 25000
        }
    )
} | ConvertTo-Json -Depth 3

Write-Host "Request data:"
Write-Host $purchaseOrderData
Write-Host "Authorization header: Bearer $($token.Substring(0, 50))..."

try {
    $purchaseOrderResponse = Invoke-RestMethod -Uri "$baseUrl/purchaseorder" -Method POST -Headers $authHeaders -Body $purchaseOrderData
    if ($purchaseOrderResponse.success) {
        Write-Host "SUCCESS: Purchase order created! ID: $($purchaseOrderResponse.data.poId)" -ForegroundColor Green
    } else {
        Write-Host "FAILED: Purchase order failed: $($purchaseOrderResponse.message)" -ForegroundColor Red
        Write-Host "Errors: $($purchaseOrderResponse.errors -join ', ')" -ForegroundColor Red
    }
} catch {
    Write-Host "EXCEPTION: Purchase order request failed!" -ForegroundColor Red
    Write-Host "Exception: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
    
    # Try to read error response
    try {
        $errorStream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($errorStream)
        $errorBody = $reader.ReadToEnd()
        Write-Host "Response Body: $errorBody" -ForegroundColor Red
    } catch {
        Write-Host "Could not read error response" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== TEST COMPLETED ===" -ForegroundColor Cyan
