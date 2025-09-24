# Test toan dien tat ca cac API trong he thong
$baseUrl = "http://localhost:5000"
$token = $null

Write-Host "TEST TOAN DIEN TAT CA API - BOOKSTORE SYSTEM" -ForegroundColor Green
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
# 1. HEALTH CHECK APIs
# ========================================
Write-Host "`n1. HEALTH CHECK APIs" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

Invoke-ApiTest -Name "Health Check" -Method "GET" -Uri "$baseUrl/health"
Invoke-ApiTest -Name "Health Ready" -Method "GET" -Uri "$baseUrl/health/ready"
Invoke-ApiTest -Name "Health Live" -Method "GET" -Uri "$baseUrl/health/live"

# ========================================
# 2. AUTHENTICATION APIs
# ========================================
Write-Host "`n2. AUTHENTICATION APIs" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

# Register Admin
$registerData = @{
    email = "admin@bookstore.com"
    password = "Admin123!"
    confirmPassword = "Admin123!"
    roleId = 3
} | ConvertTo-Json

$registerResult = Invoke-ApiTest -Name "Register Admin" -Method "POST" -Uri "$baseUrl/api/auth/register" -Body $registerData

if ($registerResult.Success) {
    $token = $registerResult.Data.data.token
    Write-Host "   Token obtained: $($token.Substring(0, 50))..." -ForegroundColor Green
}

# Register Employee
$employeeData = @{
    email = "employee@bookstore.com"
    password = "Employee123!"
    confirmPassword = "Employee123!"
    roleId = 2
} | ConvertTo-Json

Invoke-ApiTest -Name "Register Employee" -Method "POST" -Uri "$baseUrl/api/auth/register" -Body $employeeData

# Register Customer
$customerData = @{
    email = "customer@bookstore.com"
    password = "Customer123!"
    confirmPassword = "Customer123!"
    roleId = 1
} | ConvertTo-Json

Invoke-ApiTest -Name "Register Customer" -Method "POST" -Uri "$baseUrl/api/auth/register" -Body $customerData

# Login
$loginData = @{
    email = "admin@bookstore.com"
    password = "Admin123!"
} | ConvertTo-Json

$loginResult = Invoke-ApiTest -Name "Login Admin" -Method "POST" -Uri "$baseUrl/api/auth/login" -Body $loginData

if ($loginResult.Success) {
    $token = $loginResult.Data.data.token
    Write-Host "   Login token: $($token.Substring(0, 50))..." -ForegroundColor Green
}

# Set headers for authenticated requests
$authHeaders = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# ========================================
# 3. CATEGORY APIs
# ========================================
Write-Host "`n3. CATEGORY APIs" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

Invoke-ApiTest -Name "Get All Categories" -Method "GET" -Uri "$baseUrl/api/category" -Headers $authHeaders
Invoke-ApiTest -Name "Get Categories Page 1" -Method "GET" -Uri "$baseUrl/api/category?pageNumber=1&pageSize=5" -Headers $authHeaders
Invoke-ApiTest -Name "Get Categories Search" -Method "GET" -Uri "$baseUrl/api/category?searchTerm=tiểu" -Headers $authHeaders

# Get first category ID for testing
$categoriesResult = Invoke-ApiTest -Name "Get Categories for ID" -Method "GET" -Uri "$baseUrl/api/category" -Headers $authHeaders
$categoryId = 1
if ($categoriesResult.Success -and $categoriesResult.Data.data.categories.Count -gt 0) {
    $categoryId = $categoriesResult.Data.data.categories[0].categoryId
}

Invoke-ApiTest -Name "Get Category by ID" -Method "GET" -Uri "$baseUrl/api/category/$categoryId" -Headers $authHeaders

# Create new category
$newCategory = @{
    name = "Test Category $(Get-Date -Format 'HHmmss')"
    description = "Test category created by API test"
} | ConvertTo-Json

$createCategoryResult = Invoke-ApiTest -Name "Create Category" -Method "POST" -Uri "$baseUrl/api/category" -Body $newCategory -Headers $authHeaders

# Update category
if ($createCategoryResult.Success) {
    $newCategoryId = $createCategoryResult.Data.data.categoryId
    $updateCategory = @{
        name = "Updated Test Category $(Get-Date -Format 'HHmmss')"
        description = "Updated test category description"
    } | ConvertTo-Json
    
    Invoke-ApiTest -Name "Update Category" -Method "PUT" -Uri "$baseUrl/api/category/$newCategoryId" -Body $updateCategory -Headers $authHeaders
}

# ========================================
# 4. BOOK APIs
# ========================================
Write-Host "`n4. BOOK APIs" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

Invoke-ApiTest -Name "Get All Books" -Method "GET" -Uri "$baseUrl/api/book" -Headers $authHeaders
Invoke-ApiTest -Name "Get Books Page 1" -Method "GET" -Uri "$baseUrl/api/book?pageNumber=1&pageSize=5" -Headers $authHeaders
Invoke-ApiTest -Name "Get Books Search" -Method "GET" -Uri "$baseUrl/api/book?searchTerm=Harry" -Headers $authHeaders
Invoke-ApiTest -Name "Get Books Filter by Category" -Method "GET" -Uri "$baseUrl/api/book?categoryId=1" -Headers $authHeaders
Invoke-ApiTest -Name "Get Books Filter by Publisher" -Method "GET" -Uri "$baseUrl/api/book?publisherId=1" -Headers $authHeaders

# Get first book ISBN for testing
$booksResult = Invoke-ApiTest -Name "Get Books for ISBN" -Method "GET" -Uri "$baseUrl/api/book" -Headers $authHeaders
$bookIsbn = "978-604-1-00001-1"
if ($booksResult.Success -and $booksResult.Data.data.books.Count -gt 0) {
    $bookIsbn = $booksResult.Data.data.books[0].isbn
}

Invoke-ApiTest -Name "Get Book by ISBN" -Method "GET" -Uri "$baseUrl/api/book/$bookIsbn" -Headers $authHeaders

# Author APIs
Invoke-ApiTest -Name "Get All Authors" -Method "GET" -Uri "$baseUrl/api/book/authors" -Headers $authHeaders

# Create new author
$newAuthor = @{
    firstName = "Test"
    lastName = "Author $(Get-Date -Format 'HHmmss')"
    gender = 0
    email = "testauthor$(Get-Date -Format 'HHmmss')@example.com"
} | ConvertTo-Json

$createAuthorResult = Invoke-ApiTest -Name "Create Author" -Method "POST" -Uri "$baseUrl/api/book/authors" -Body $newAuthor -Headers $authHeaders

# ========================================
# 5. PURCHASE ORDER APIs
# ========================================
Write-Host "`n5. PURCHASE ORDER APIs" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

Invoke-ApiTest -Name "Get All Purchase Orders" -Method "GET" -Uri "$baseUrl/api/purchaseorder" -Headers $authHeaders
Invoke-ApiTest -Name "Get Purchase Orders Page 1" -Method "GET" -Uri "$baseUrl/api/purchaseorder?pageNumber=1&pageSize=5" -Headers $authHeaders
Invoke-ApiTest -Name "Get Purchase Orders Search" -Method "GET" -Uri "$baseUrl/api/purchaseorder?searchTerm=test" -Headers $authHeaders

# Create new purchase order
$newPurchaseOrder = @{
    publisherId = 1
    note = "Test purchase order created by API test"
    lines = @(
        @{
            isbn = "978-604-1-00001-1"
            qtyOrdered = 10
            unitPrice = 45.00
        },
        @{
            isbn = "978-604-1-00002-2"
            qtyOrdered = 5
            unitPrice = 35.00
        }
    )
} | ConvertTo-Json

$createPOResult = Invoke-ApiTest -Name "Create Purchase Order" -Method "POST" -Uri "$baseUrl/api/purchaseorder" -Body $newPurchaseOrder -Headers $authHeaders

# ========================================
# 6. GOODS RECEIPT APIs
# ========================================
Write-Host "`n6. GOODS RECEIPT APIs" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

Invoke-ApiTest -Name "Get All Goods Receipts" -Method "GET" -Uri "$baseUrl/api/goodsreceipt" -Headers $authHeaders
Invoke-ApiTest -Name "Get Goods Receipts Page 1" -Method "GET" -Uri "$baseUrl/api/goodsreceipt?pageNumber=1&pageSize=5" -Headers $authHeaders
Invoke-ApiTest -Name "Get Available Purchase Orders" -Method "GET" -Uri "$baseUrl/api/goodsreceipt/available-purchase-orders" -Headers $authHeaders

# Create new goods receipt (if we have a purchase order)
if ($createPOResult.Success) {
    $poId = $createPOResult.Data.data.poId
    $newGoodsReceipt = @{
        poId = $poId
        note = "Test goods receipt created by API test"
        lines = @(
            @{
                qtyReceived = 8
                unitCost = 42.00
            },
            @{
                qtyReceived = 4
                unitCost = 32.00
            }
        )
    } | ConvertTo-Json
    
    Invoke-ApiTest -Name "Create Goods Receipt" -Method "POST" -Uri "$baseUrl/api/goodsreceipt" -Body $newGoodsReceipt -Headers $authHeaders
}

# ========================================
# 7. TEST CONTROLLER APIs
# ========================================
Write-Host "`n7. TEST CONTROLLER APIs" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

Invoke-ApiTest -Name "Test Controller - Get" -Method "GET" -Uri "$baseUrl/api/test" -Headers $authHeaders
Invoke-ApiTest -Name "Test Controller - Post" -Method "POST" -Uri "$baseUrl/api/test" -Headers $authHeaders

# ========================================
# 8. SWAGGER UI
# ========================================
Write-Host "`n8. SWAGGER UI" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

Invoke-ApiTest -Name "Swagger UI" -Method "GET" -Uri "$baseUrl/swagger"

# ========================================
# SUMMARY
# ========================================
Write-Host "`nTEST SUMMARY" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Yellow
Write-Host "All API tests completed!" -ForegroundColor Green
Write-Host "Swagger UI: $baseUrl/swagger" -ForegroundColor Yellow
Write-Host "Admin Token: $($token.Substring(0, 50))..." -ForegroundColor Yellow
Write-Host "`nTested APIs:" -ForegroundColor White
Write-Host "   • Health Check APIs (3 endpoints)" -ForegroundColor White
Write-Host "   • Authentication APIs (4 endpoints)" -ForegroundColor White
Write-Host "   • Category APIs (6 endpoints)" -ForegroundColor White
Write-Host "   • Book APIs (8 endpoints)" -ForegroundColor White
Write-Host "   • Purchase Order APIs (4 endpoints)" -ForegroundColor White
Write-Host "   • Goods Receipt APIs (4 endpoints)" -ForegroundColor White
Write-Host "   • Test Controller APIs (2 endpoints)" -ForegroundColor White
Write-Host "   • Swagger UI (1 endpoint)" -ForegroundColor White
Write-Host "`nTotal: 32+ API endpoints tested!" -ForegroundColor Green