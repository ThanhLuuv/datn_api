# Test API lấy sách theo nhà xuất bản
$baseUrl = "http://localhost:5000"
$token = $null

Write-Host "TEST API LAY SACH THEO NHA XUAT BAN" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Yellow

# Function to make API calls
function Invoke-ApiTest {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Uri,
        [hashtable]$Headers = @{},
        [string]$Body = $null,
        [string]$ContentType = "application/json"
    )
    
    Write-Host "`nTesting: $Name" -ForegroundColor Cyan
    Write-Host "   $Method $Uri" -ForegroundColor Gray
    
    try {
        $requestParams = @{
            Method = $Method
            Uri = $Uri
            Headers = $Headers
        }
        
        if ($Body) {
            $requestParams.Body = $Body
            $requestParams.ContentType = $ContentType
        }
        
        $response = Invoke-RestMethod @requestParams
        Write-Host "   SUCCESS" -ForegroundColor Green
        if ($response.success -ne $null) {
            Write-Host "   Success: $($response.success)" -ForegroundColor Green
            Write-Host "   Message: $($response.message)" -ForegroundColor White
        }
        return @{ Success = $true; Data = $response }
    }
    catch {
        Write-Host "   FAILED" -ForegroundColor Red
        Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $errorContent = $reader.ReadToEnd()
            Write-Host "   Details: $errorContent" -ForegroundColor Yellow
        }
        return @{ Success = $false; Error = $_.Exception.Message }
    }
}

# ========================================
# 1. DANG NHAP ADMIN
# ========================================
Write-Host "`n1. DANG NHAP ADMIN" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

$adminLoginData = @{
    email = "admin@bookstore.com"
    password = "Admin123!"
} | ConvertTo-Json

$adminLoginResult = Invoke-ApiTest -Name "Login Admin" -Method "POST" -Uri "$baseUrl/api/auth/login" -Body $adminLoginData
if ($adminLoginResult.Success) {
    $token = $adminLoginResult.Data.data.token
    Write-Host "   Admin Token: $($token.Substring(0, 50))..." -ForegroundColor Green
}

$authHeaders = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# ========================================
# 2. LAY DANH SACH NHA XUAT BAN
# ========================================
Write-Host "`n2. LAY DANH SACH NHA XUAT BAN" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

$publishersResult = Invoke-ApiTest -Name "Get Publishers" -Method "GET" -Uri "$baseUrl/api/publisher" -Headers $authHeaders
$publisherId = 1
if ($publishersResult.Success -and $publishersResult.Data.data.publishers.Count -gt 0) {
    $publisherId = $publishersResult.Data.data.publishers[0].publisherId
    Write-Host "   Using Publisher ID: $publisherId" -ForegroundColor Green
}

# ========================================
# 3. TEST API LAY SACH THEO NHA XUAT BAN
# ========================================
Write-Host "`n3. TEST API LAY SACH THEO NHA XUAT BAN" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

# Get books by publisher
Invoke-ApiTest -Name "Get Books by Publisher" -Method "GET" -Uri "$baseUrl/api/book/by-publisher/$publisherId" -Headers $authHeaders

# Get books by publisher with pagination
Invoke-ApiTest -Name "Get Books by Publisher Page 1" -Method "GET" -Uri "$baseUrl/api/book/by-publisher/$publisherId?pageNumber=1&pageSize=5" -Headers $authHeaders

# Get books by publisher with search
Invoke-ApiTest -Name "Get Books by Publisher Search" -Method "GET" -Uri "$baseUrl/api/book/by-publisher/$publisherId?searchTerm=Harry" -Headers $authHeaders

# Test with non-existent publisher
Invoke-ApiTest -Name "Get Books by Non-existent Publisher" -Method "GET" -Uri "$baseUrl/api/book/by-publisher/999" -Headers $authHeaders

# ========================================
# 4. TEST VOI CAC ROLES KHAC
# ========================================
Write-Host "`n4. TEST VOI CAC ROLES KHAC" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

# Login as Customer
$customerLoginData = @{
    email = "customer@bookstore.com"
    password = "Customer123!"
} | ConvertTo-Json

$customerLoginResult = Invoke-ApiTest -Name "Login Customer" -Method "POST" -Uri "$baseUrl/api/auth/login" -Body $customerLoginData
if ($customerLoginResult.Success) {
    $customerToken = $customerLoginResult.Data.data.token
    $customerHeaders = @{
        "Authorization" = "Bearer $customerToken"
        "Content-Type" = "application/json"
    }
    
    Write-Host "`nTesting Customer access:" -ForegroundColor Yellow
    Invoke-ApiTest -Name "Customer - Get Books by Publisher" -Method "GET" -Uri "$baseUrl/api/book/by-publisher/$publisherId" -Headers $customerHeaders
}

# Login as Sales Employee
$salesLoginData = @{
    email = "sales@bookstore.com"
    password = "Sales123!"
} | ConvertTo-Json

$salesLoginResult = Invoke-ApiTest -Name "Login Sales Employee" -Method "POST" -Uri "$baseUrl/api/auth/login" -Body $salesLoginData
if ($salesLoginResult.Success) {
    $salesToken = $salesLoginResult.Data.data.token
    $salesHeaders = @{
        "Authorization" = "Bearer $salesToken"
        "Content-Type" = "application/json"
    }
    
    Write-Host "`nTesting Sales Employee access:" -ForegroundColor Yellow
    Invoke-ApiTest -Name "Sales - Get Books by Publisher" -Method "GET" -Uri "$baseUrl/api/book/by-publisher/$publisherId" -Headers $salesHeaders
}

# ========================================
# SUMMARY
# ========================================
Write-Host "`nTEST SUMMARY" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Yellow
Write-Host "Books by Publisher API test completed!" -ForegroundColor Green
Write-Host "`nAPI tested:" -ForegroundColor White
Write-Host "   • GET /api/book/by-publisher/{publisherId} - Get books by publisher" -ForegroundColor White
Write-Host "   • Pagination support (pageNumber, pageSize)" -ForegroundColor White
Write-Host "   • Search support (searchTerm)" -ForegroundColor White
Write-Host "   • Error handling for non-existent publisher" -ForegroundColor White
Write-Host "   • Role-based access control" -ForegroundColor White
Write-Host "`nSwagger UI: $baseUrl/swagger" -ForegroundColor Yellow
