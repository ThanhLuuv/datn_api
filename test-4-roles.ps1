# Test he thong voi 4 roles moi
$baseUrl = "http://localhost:5000"
$tokens = @{}

Write-Host "TEST HE THONG VOI 4 ROLES MOI" -ForegroundColor Green
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
# 1. DANG KY CAC TAI KHOAN VOI 4 ROLES
# ========================================
Write-Host "`n1. DANG KY CAC TAI KHOAN VOI 4 ROLES" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

# Register Admin
$adminData = @{
    email = "admin@bookstore.com"
    password = "Admin123!"
    confirmPassword = "Admin123!"
    roleId = 1
} | ConvertTo-Json

$adminResult = Invoke-ApiTest -Name "Register Admin" -Method "POST" -Uri "$baseUrl/api/auth/register" -Body $adminData

# Register Sales Employee
$salesData = @{
    email = "sales@bookstore.com"
    password = "Sales123!"
    confirmPassword = "Sales123!"
    roleId = 2
} | ConvertTo-Json

$salesResult = Invoke-ApiTest -Name "Register Sales Employee" -Method "POST" -Uri "$baseUrl/api/auth/register" -Body $salesData

# Register Delivery Employee
$deliveryData = @{
    email = "delivery@bookstore.com"
    password = "Delivery123!"
    confirmPassword = "Delivery123!"
    roleId = 3
} | ConvertTo-Json

$deliveryResult = Invoke-ApiTest -Name "Register Delivery Employee" -Method "POST" -Uri "$baseUrl/api/auth/register" -Body $deliveryData

# Register Customer
$customerData = @{
    email = "customer@bookstore.com"
    password = "Customer123!"
    confirmPassword = "Customer123!"
    roleId = 4
} | ConvertTo-Json

$customerResult = Invoke-ApiTest -Name "Register Customer" -Method "POST" -Uri "$baseUrl/api/auth/register" -Body $customerData

# ========================================
# 2. DANG NHAP VA LAY TOKEN
# ========================================
Write-Host "`n2. DANG NHAP VA LAY TOKEN" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

# Login Admin
$adminLoginData = @{
    email = "admin@bookstore.com"
    password = "Admin123!"
} | ConvertTo-Json

$adminLoginResult = Invoke-ApiTest -Name "Login Admin" -Method "POST" -Uri "$baseUrl/api/auth/login" -Body $adminLoginData
if ($adminLoginResult.Success) {
    $tokens.Admin = $adminLoginResult.Data.data.token
    Write-Host "   Admin Token: $($tokens.Admin.Substring(0, 50))..." -ForegroundColor Green
}

# Login Sales Employee
$salesLoginData = @{
    email = "sales@bookstore.com"
    password = "Sales123!"
} | ConvertTo-Json

$salesLoginResult = Invoke-ApiTest -Name "Login Sales Employee" -Method "POST" -Uri "$baseUrl/api/auth/login" -Body $salesLoginData
if ($salesLoginResult.Success) {
    $tokens.Sales = $salesLoginResult.Data.data.token
    Write-Host "   Sales Token: $($tokens.Sales.Substring(0, 50))..." -ForegroundColor Green
}

# Login Delivery Employee
$deliveryLoginData = @{
    email = "delivery@bookstore.com"
    password = "Delivery123!"
} | ConvertTo-Json

$deliveryLoginResult = Invoke-ApiTest -Name "Login Delivery Employee" -Method "POST" -Uri "$baseUrl/api/auth/login" -Body $deliveryLoginData
if ($deliveryLoginResult.Success) {
    $tokens.Delivery = $deliveryLoginResult.Data.data.token
    Write-Host "   Delivery Token: $($tokens.Delivery.Substring(0, 50))..." -ForegroundColor Green
}

# Login Customer
$customerLoginData = @{
    email = "customer@bookstore.com"
    password = "Customer123!"
} | ConvertTo-Json

$customerLoginResult = Invoke-ApiTest -Name "Login Customer" -Method "POST" -Uri "$baseUrl/api/auth/login" -Body $customerLoginData
if ($customerLoginResult.Success) {
    $tokens.Customer = $customerLoginResult.Data.data.token
    Write-Host "   Customer Token: $($tokens.Customer.Substring(0, 50))..." -ForegroundColor Green
}

# ========================================
# 3. TEST PHAN QUYEN
# ========================================
Write-Host "`n3. TEST PHAN QUYEN" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

# Test Admin - co the lam tat ca
if ($tokens.Admin) {
    $adminHeaders = @{
        "Authorization" = "Bearer $($tokens.Admin)"
        "Content-Type" = "application/json"
    }
    
    Write-Host "`nTesting Admin permissions:" -ForegroundColor Yellow
    Invoke-ApiTest -Name "Admin - Get Categories" -Method "GET" -Uri "$baseUrl/api/category" -Headers $adminHeaders
    Invoke-ApiTest -Name "Admin - Get Books" -Method "GET" -Uri "$baseUrl/api/book" -Headers $adminHeaders
    Invoke-ApiTest -Name "Admin - Get Purchase Orders" -Method "GET" -Uri "$baseUrl/api/purchaseorder" -Headers $adminHeaders
    Invoke-ApiTest -Name "Admin - Get Goods Receipts" -Method "GET" -Uri "$baseUrl/api/goodsreceipt" -Headers $adminHeaders
    Invoke-ApiTest -Name "Admin - Test Admin Only" -Method "GET" -Uri "$baseUrl/api/test/admin-only" -Headers $adminHeaders
}

# Test Sales Employee - co the quan ly ban hang
if ($tokens.Sales) {
    $salesHeaders = @{
        "Authorization" = "Bearer $($tokens.Sales)"
        "Content-Type" = "application/json"
    }
    
    Write-Host "`nTesting Sales Employee permissions:" -ForegroundColor Yellow
    Invoke-ApiTest -Name "Sales - Get Categories" -Method "GET" -Uri "$baseUrl/api/category" -Headers $salesHeaders
    Invoke-ApiTest -Name "Sales - Get Books" -Method "GET" -Uri "$baseUrl/api/book" -Headers $salesHeaders
    Invoke-ApiTest -Name "Sales - Get Purchase Orders" -Method "GET" -Uri "$baseUrl/api/purchaseorder" -Headers $salesHeaders
    Invoke-ApiTest -Name "Sales - Test Sales Only" -Method "GET" -Uri "$baseUrl/api/test/sales-only" -Headers $salesHeaders
    Invoke-ApiTest -Name "Sales - Test Staff Only" -Method "GET" -Uri "$baseUrl/api/test/staff-only" -Headers $salesHeaders
}

# Test Delivery Employee - co the quan ly giao hang
if ($tokens.Delivery) {
    $deliveryHeaders = @{
        "Authorization" = "Bearer $($tokens.Delivery)"
        "Content-Type" = "application/json"
    }
    
    Write-Host "`nTesting Delivery Employee permissions:" -ForegroundColor Yellow
    Invoke-ApiTest -Name "Delivery - Get Categories" -Method "GET" -Uri "$baseUrl/api/category" -Headers $deliveryHeaders
    Invoke-ApiTest -Name "Delivery - Get Books" -Method "GET" -Uri "$baseUrl/api/book" -Headers $deliveryHeaders
    Invoke-ApiTest -Name "Delivery - Get Purchase Orders" -Method "GET" -Uri "$baseUrl/api/purchaseorder" -Headers $deliveryHeaders
    Invoke-ApiTest -Name "Delivery - Get Goods Receipts" -Method "GET" -Uri "$baseUrl/api/goodsreceipt" -Headers $deliveryHeaders
    Invoke-ApiTest -Name "Delivery - Test Delivery Only" -Method "GET" -Uri "$baseUrl/api/test/delivery-only" -Headers $deliveryHeaders
    Invoke-ApiTest -Name "Delivery - Test Staff Only" -Method "GET" -Uri "$baseUrl/api/test/staff-only" -Headers $deliveryHeaders
}

# Test Customer - chi co the xem
if ($tokens.Customer) {
    $customerHeaders = @{
        "Authorization" = "Bearer $($tokens.Customer)"
        "Content-Type" = "application/json"
    }
    
    Write-Host "`nTesting Customer permissions:" -ForegroundColor Yellow
    Invoke-ApiTest -Name "Customer - Get Categories" -Method "GET" -Uri "$baseUrl/api/category" -Headers $customerHeaders
    Invoke-ApiTest -Name "Customer - Get Books" -Method "GET" -Uri "$baseUrl/api/book" -Headers $customerHeaders
    Invoke-ApiTest -Name "Customer - Try Get Purchase Orders (Should Fail)" -Method "GET" -Uri "$baseUrl/api/purchaseorder" -Headers $customerHeaders
    Invoke-ApiTest -Name "Customer - Try Get Goods Receipts (Should Fail)" -Method "GET" -Uri "$baseUrl/api/goodsreceipt" -Headers $customerHeaders
}

# ========================================
# SUMMARY
# ========================================
Write-Host "`nTEST SUMMARY" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Yellow
Write-Host "4 Roles system test completed!" -ForegroundColor Green
Write-Host "`nRoles tested:" -ForegroundColor White
Write-Host "   • ADMIN (RoleId: 1) - Full access" -ForegroundColor White
Write-Host "   • SALES_EMPLOYEE (RoleId: 2) - Sales management" -ForegroundColor White
Write-Host "   • DELIVERY_EMPLOYEE (RoleId: 3) - Delivery management" -ForegroundColor White
Write-Host "   • CUSTOMER (RoleId: 4) - Read-only access" -ForegroundColor White
Write-Host "`nSwagger UI: $baseUrl/swagger" -ForegroundColor Yellow
