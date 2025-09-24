# Test API don gian
$baseUrl = "http://localhost:5000"

Write-Host "Test API BookStore..." -ForegroundColor Green

# Test 1: Health Check
Write-Host "Test Health Check..." -ForegroundColor Cyan
try {
    $healthResponse = Invoke-RestMethod -Uri "$baseUrl/health" -Method GET
    Write-Host "Health Check: $healthResponse" -ForegroundColor Green
} catch {
    Write-Host "Health Check Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Swagger UI
Write-Host "Test Swagger UI..." -ForegroundColor Cyan
try {
    $swaggerResponse = Invoke-WebRequest -Uri "$baseUrl/swagger" -Method GET
    if ($swaggerResponse.StatusCode -eq 200) {
        Write-Host "Swagger UI: OK" -ForegroundColor Green
    } else {
        Write-Host "Swagger UI Failed: $($swaggerResponse.StatusCode)" -ForegroundColor Red
    }
} catch {
    Write-Host "Swagger UI Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Register
Write-Host "Test Register..." -ForegroundColor Cyan
try {
    $registerData = @{
        email = "test@bookstore.com"
        password = "Test123!"
        confirmPassword = "Test123!"
        roleId = 3
    } | ConvertTo-Json

    $registerResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/register" -Method POST -Body $registerData -ContentType "application/json"
    Write-Host "Register Success: $($registerResponse | ConvertTo-Json)" -ForegroundColor Green
} catch {
    Write-Host "Register Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $errorContent = $reader.ReadToEnd()
        Write-Host "Error details: $errorContent" -ForegroundColor Yellow
    }
}

# Test 4: Categories
Write-Host "Test Categories..." -ForegroundColor Cyan
try {
    $categoriesResponse = Invoke-RestMethod -Uri "$baseUrl/api/category" -Method GET
    Write-Host "Categories Success: $($categoriesResponse | ConvertTo-Json)" -ForegroundColor Green
} catch {
    Write-Host "Categories Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "Categories: Requires authentication (expected)" -ForegroundColor Yellow
    }
}

Write-Host "Test completed!" -ForegroundColor Green
Write-Host "Truy cap Swagger UI tai: $baseUrl/swagger" -ForegroundColor Yellow