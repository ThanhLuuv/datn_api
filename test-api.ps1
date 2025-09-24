# BookStore API Test Script
# PowerShell script để test các API endpoints

param(
    [string]$BaseUrl = "https://localhost:7000",
    [string]$Email = "admin@bookstore.com",
    [string]$Password = "Admin123!"
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Bắt đầu test các API của BookStore..." -ForegroundColor Green
Write-Host "Base URL: $BaseUrl" -ForegroundColor Yellow
Write-Host ""

# Function to make HTTP requests
function Invoke-ApiRequest {
    param(
        [string]$Method,
        [string]$Uri,
        [hashtable]$Headers = @{},
        [string]$Body = $null,
        [string]$ContentType = "application/json"
    )
    
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
        return @{
            Success = $true
            Data = $response
        }
    }
    catch {
        return @{
            Success = $false
            Error = $_.Exception.Message
            StatusCode = $_.Exception.Response.StatusCode.value__
        }
    }
}

# Test 1: Health Check
Write-Host "🔍 Test Health Check..." -ForegroundColor Cyan
$healthResult = Invoke-ApiRequest -Method "GET" -Uri "$BaseUrl/health"
if ($healthResult.Success) {
    Write-Host "✅ Health Check: OK" -ForegroundColor Green
} else {
    Write-Host "❌ Health Check: Failed - $($healthResult.Error)" -ForegroundColor Red
}

# Test 2: Authentication
Write-Host "`n🔐 Test Authentication..." -ForegroundColor Cyan

# Register
$registerData = @{
    email = $Email
    password = $Password
    confirmPassword = $Password
    roleId = 3
} | ConvertTo-Json

$registerResult = Invoke-ApiRequest -Method "POST" -Uri "$BaseUrl/api/auth/register" -Body $registerData
if ($registerResult.Success) {
    Write-Host "✅ Register: OK" -ForegroundColor Green
    $authToken = $registerResult.Data.data.token
} else {
    Write-Host "❌ Register: Failed - $($registerResult.Error)" -ForegroundColor Red
    # Try login instead
    $loginData = @{
        email = $Email
        password = $Password
    } | ConvertTo-Json
    
    $loginResult = Invoke-ApiRequest -Method "POST" -Uri "$BaseUrl/api/auth/login" -Body $loginData
    if ($loginResult.Success) {
        Write-Host "✅ Login: OK" -ForegroundColor Green
        $authToken = $loginResult.Data.data.token
    } else {
        Write-Host "❌ Login: Failed - $($loginResult.Error)" -ForegroundColor Red
        Write-Host "⚠️  Không thể lấy token, bỏ qua các test cần authentication" -ForegroundColor Yellow
        $authToken = $null
    }
}

# Set authorization header
$headers = @{}
if ($authToken) {
    $headers["Authorization"] = "Bearer $authToken"
}

# Test 3: Categories
Write-Host "`n📚 Test Categories..." -ForegroundColor Cyan

# Get Categories
$categoriesResult = Invoke-ApiRequest -Method "GET" -Uri "$BaseUrl/api/category" -Headers $headers
if ($categoriesResult.Success) {
    Write-Host "✅ Get Categories: OK" -ForegroundColor Green
} else {
    Write-Host "❌ Get Categories: Failed - $($categoriesResult.Error)" -ForegroundColor Red
}

# Create Category
$categoryData = @{
    name = "Test Category $(Get-Date -Format 'yyyyMMddHHmmss')"
    description = "Test category description"
} | ConvertTo-Json

$createCategoryResult = Invoke-ApiRequest -Method "POST" -Uri "$BaseUrl/api/category" -Headers $headers -Body $categoryData
if ($createCategoryResult.Success) {
    Write-Host "✅ Create Category: OK" -ForegroundColor Green
    $categoryId = $createCategoryResult.Data.data.categoryId
} else {
    Write-Host "❌ Create Category: Failed - $($createCategoryResult.Error)" -ForegroundColor Red
    $categoryId = 1  # Use default
}

# Test 4: Books
Write-Host "`n📖 Test Books..." -ForegroundColor Cyan

# Get Books
$booksResult = Invoke-ApiRequest -Method "GET" -Uri "$BaseUrl/api/book" -Headers $headers
if ($booksResult.Success) {
    Write-Host "✅ Get Books: OK" -ForegroundColor Green
} else {
    Write-Host "❌ Get Books: Failed - $($booksResult.Error)" -ForegroundColor Red
}

# Create Author
$authorData = @{
    firstName = "Test"
    lastName = "Author $(Get-Date -Format 'HHmmss')"
    gender = 0
    email = "testauthor$(Get-Date -Format 'HHmmss')@example.com"
} | ConvertTo-Json

$authorResult = Invoke-ApiRequest -Method "POST" -Uri "$BaseUrl/api/book/authors" -Headers $headers -Body $authorData
if ($authorResult.Success) {
    Write-Host "✅ Create Author: OK" -ForegroundColor Green
    $authorId = $authorResult.Data.data.authorId
} else {
    Write-Host "❌ Create Author: Failed - $($authorResult.Error)" -ForegroundColor Red
    $authorId = 1  # Use default
}

# Test 5: Purchase Orders
Write-Host "`n🛒 Test Purchase Orders..." -ForegroundColor Cyan

# Get Purchase Orders
$poResult = Invoke-ApiRequest -Method "GET" -Uri "$BaseUrl/api/purchaseorder" -Headers $headers
if ($poResult.Success) {
    Write-Host "✅ Get Purchase Orders: OK" -ForegroundColor Green
} else {
    Write-Host "❌ Get Purchase Orders: Failed - $($poResult.Error)" -ForegroundColor Red
}

# Test 6: Goods Receipts
Write-Host "`n📦 Test Goods Receipts..." -ForegroundColor Cyan

# Get Goods Receipts
$grResult = Invoke-ApiRequest -Method "GET" -Uri "$BaseUrl/api/goodsreceipt" -Headers $headers
if ($grResult.Success) {
    Write-Host "✅ Get Goods Receipts: OK" -ForegroundColor Green
} else {
    Write-Host "❌ Get Goods Receipts: Failed - $($grResult.Error)" -ForegroundColor Red
}

# Get Available Purchase Orders
$availablePOResult = Invoke-ApiRequest -Method "GET" -Uri "$BaseUrl/api/goodsreceipt/available-purchase-orders" -Headers $headers
if ($availablePOResult.Success) {
    Write-Host "✅ Get Available Purchase Orders: OK" -ForegroundColor Green
} else {
    Write-Host "❌ Get Available Purchase Orders: Failed - $($availablePOResult.Error)" -ForegroundColor Red
}

Write-Host "`n✅ Hoàn thành test các API!" -ForegroundColor Green
Write-Host "`n📝 Lưu ý:" -ForegroundColor Yellow
Write-Host "- Một số test có thể fail do thiếu dữ liệu seed (publisher, category, book)" -ForegroundColor Yellow
Write-Host "- Để test đầy đủ, cần tạo dữ liệu mẫu trước" -ForegroundColor Yellow
Write-Host "- Sử dụng file test-api.http để test chi tiết từng API" -ForegroundColor Yellow
