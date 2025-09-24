# Test API Publisher
$baseUrl = "http://localhost:5000"
$token = $null

Write-Host "TEST API PUBLISHER" -ForegroundColor Green
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
# 2. TEST API PUBLISHER
# ========================================
Write-Host "`n2. TEST API PUBLISHER" -ForegroundColor Magenta
Write-Host "----------------------------------------" -ForegroundColor Gray

# Get all publishers
Invoke-ApiTest -Name "Get All Publishers" -Method "GET" -Uri "$baseUrl/api/publisher" -Headers $authHeaders

# Get publishers with pagination
Invoke-ApiTest -Name "Get Publishers Page 1" -Method "GET" -Uri "$baseUrl/api/publisher?pageNumber=1&pageSize=5" -Headers $authHeaders

# Get publishers with search
Invoke-ApiTest -Name "Get Publishers Search" -Method "GET" -Uri "$baseUrl/api/publisher?searchTerm=Kim" -Headers $authHeaders

# Get publisher by ID
Invoke-ApiTest -Name "Get Publisher by ID" -Method "GET" -Uri "$baseUrl/api/publisher/1" -Headers $authHeaders

# Create new publisher
$newPublisher = @{
    name = "NXB Test $(Get-Date -Format 'HHmmss')"
    address = "123 Test Street, Test City"
    email = "test$(Get-Date -Format 'HHmmss')@example.com"
    phone = "0123456789"
} | ConvertTo-Json

$createResult = Invoke-ApiTest -Name "Create Publisher" -Method "POST" -Uri "$baseUrl/api/publisher" -Body $newPublisher -Headers $authHeaders

# Update publisher
if ($createResult.Success) {
    $publisherId = $createResult.Data.data.publisherId
    $updatePublisher = @{
        name = "Updated NXB Test $(Get-Date -Format 'HHmmss')"
        address = "456 Updated Street, Updated City"
        email = "updated$(Get-Date -Format 'HHmmss')@example.com"
        phone = "0987654321"
    } | ConvertTo-Json
    
    Invoke-ApiTest -Name "Update Publisher" -Method "PUT" -Uri "$baseUrl/api/publisher/$publisherId" -Body $updatePublisher -Headers $authHeaders
}

# ========================================
# 3. TEST PHAN QUYEN
# ========================================
Write-Host "`n3. TEST PHAN QUYEN" -ForegroundColor Magenta
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
    
    Write-Host "`nTesting Customer permissions:" -ForegroundColor Yellow
    Invoke-ApiTest -Name "Customer - Get Publishers" -Method "GET" -Uri "$baseUrl/api/publisher" -Headers $customerHeaders
    Invoke-ApiTest -Name "Customer - Try Create Publisher (Should Fail)" -Method "POST" -Uri "$baseUrl/api/publisher" -Body $newPublisher -Headers $customerHeaders
}

# ========================================
# SUMMARY
# ========================================
Write-Host "`nTEST SUMMARY" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Yellow
Write-Host "Publisher API test completed!" -ForegroundColor Green
Write-Host "`nAPIs tested:" -ForegroundColor White
Write-Host "   • GET /api/publisher - Get all publishers" -ForegroundColor White
Write-Host "   • GET /api/publisher/{id} - Get publisher by ID" -ForegroundColor White
Write-Host "   • POST /api/publisher - Create publisher (ADMIN only)" -ForegroundColor White
Write-Host "   • PUT /api/publisher/{id} - Update publisher (ADMIN only)" -ForegroundColor White
Write-Host "   • DELETE /api/publisher/{id} - Delete publisher (ADMIN only)" -ForegroundColor White
Write-Host "`nSwagger UI: $baseUrl/swagger" -ForegroundColor Yellow
