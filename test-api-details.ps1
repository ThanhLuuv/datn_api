# Test API để lấy thông tin chi tiết cho tài liệu
$baseUrl = "http://localhost:5000"

Write-Host "Testing APIs for Documentation..." -ForegroundColor Green

# Login để lấy token
$loginData = @{
    email = "admin@bookstore.com"
    password = "Admin123!"
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Method POST -Uri "$baseUrl/api/auth/login" -Body $loginData -ContentType "application/json"
    $token = $loginResponse.data.token
    Write-Host "Token obtained: $($token.Substring(0, 50))..." -ForegroundColor Green
} catch {
    Write-Host "Login failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$authHeaders = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Test một số API để lấy response examples
Write-Host "`nTesting APIs for response examples..." -ForegroundColor Yellow

# Test Categories
try {
    $categoriesResponse = Invoke-RestMethod -Method GET -Uri "$baseUrl/api/category" -Headers $authHeaders
    Write-Host "Categories API working" -ForegroundColor Green
} catch {
    Write-Host "Categories API failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test Books
try {
    $booksResponse = Invoke-RestMethod -Method GET -Uri "$baseUrl/api/book" -Headers $authHeaders
    Write-Host "Books API working" -ForegroundColor Green
} catch {
    Write-Host "Books API failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test Authors
try {
    $authorsResponse = Invoke-RestMethod -Method GET -Uri "$baseUrl/api/book/authors" -Headers $authHeaders
    Write-Host "Authors API working" -ForegroundColor Green
} catch {
    Write-Host "Authors API failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nAPI testing completed!" -ForegroundColor Green
