# Test API voi token
$baseUrl = "http://localhost:5000"

Write-Host "Test API BookStore voi token..." -ForegroundColor Green

# Step 1: Register va lay token
Write-Host "Step 1: Register va lay token..." -ForegroundColor Cyan
try {
    $registerData = @{
        email = "admin@bookstore.com"
        password = "Admin123!"
        confirmPassword = "Admin123!"
        roleId = 3
    } | ConvertTo-Json

    $registerResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/register" -Method POST -Body $registerData -ContentType "application/json"
    
    if ($registerResponse.success) {
        $token = $registerResponse.data.token
        Write-Host "Token: $token" -ForegroundColor Green
        
        # Set headers cho cac request sau
        $headers = @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        }
        
        # Test Categories
        Write-Host "`nStep 2: Test Categories..." -ForegroundColor Cyan
        try {
            $categoriesResponse = Invoke-RestMethod -Uri "$baseUrl/api/category" -Method GET -Headers $headers
            Write-Host "Categories Success: $($categoriesResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Green
        } catch {
            Write-Host "Categories Error: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        # Test Books
        Write-Host "`nStep 3: Test Books..." -ForegroundColor Cyan
        try {
            $booksResponse = Invoke-RestMethod -Uri "$baseUrl/api/book" -Method GET -Headers $headers
            Write-Host "Books Success: $($booksResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Green
        } catch {
            Write-Host "Books Error: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        # Test Create Category
        Write-Host "`nStep 4: Test Create Category..." -ForegroundColor Cyan
        try {
            $newCategory = @{
                name = "Test Category $(Get-Date -Format 'HHmmss')"
                description = "Test category description"
            } | ConvertTo-Json
            
            $createResponse = Invoke-RestMethod -Uri "$baseUrl/api/category" -Method POST -Body $newCategory -Headers $headers
            Write-Host "Create Category Success: $($createResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Green
        } catch {
            Write-Host "Create Category Error: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        # Test Purchase Orders
        Write-Host "`nStep 5: Test Purchase Orders..." -ForegroundColor Cyan
        try {
            $poResponse = Invoke-RestMethod -Uri "$baseUrl/api/purchaseorder" -Method GET -Headers $headers
            Write-Host "Purchase Orders Success: $($poResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Green
        } catch {
            Write-Host "Purchase Orders Error: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        # Test Goods Receipts
        Write-Host "`nStep 6: Test Goods Receipts..." -ForegroundColor Cyan
        try {
            $grResponse = Invoke-RestMethod -Uri "$baseUrl/api/goodsreceipt" -Method GET -Headers $headers
            Write-Host "Goods Receipts Success: $($grResponse | ConvertTo-Json -Depth 3)" -ForegroundColor Green
        } catch {
            Write-Host "Goods Receipts Error: $($_.Exception.Message)" -ForegroundColor Red
        }
        
    } else {
        Write-Host "Register Failed: $($registerResponse.message)" -ForegroundColor Red
    }
} catch {
    Write-Host "Register Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nTest completed!" -ForegroundColor Green
Write-Host "Truy cap Swagger UI tai: $baseUrl/swagger" -ForegroundColor Yellow
