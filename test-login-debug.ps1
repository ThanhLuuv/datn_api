# Test Login Debug

$baseUrl = "http://localhost:5256/api"
$headers = @{
    "Content-Type" = "application/json"
}

Write-Host "=== LOGIN DEBUG TEST ===" -ForegroundColor Cyan

# 1. Lấy accounts trước
Write-Host "`n1. Getting accounts..." -ForegroundColor Green
$accountsResponse = Invoke-RestMethod -Uri "$baseUrl/test/accounts" -Method GET -Headers $headers
Write-Host "Found $($accountsResponse.count) accounts:"
$accountsResponse.items | ForEach-Object {
    Write-Host "  - ID: $($_.accountId), Email: $($_.email), RoleId: $($_.roleId), Active: $($_.isActive)"
}

# 2. Test login với admin account
Write-Host "`n2. Testing login..." -ForegroundColor Green
$loginData = @{
    email = "admin@bookstore.com"
    password = "Admin123!"
} | ConvertTo-Json

Write-Host "Login request data:"
Write-Host $loginData

try {
    $loginResponse = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method POST -Headers $headers -Body $loginData
    Write-Host "SUCCESS: Login successful!" -ForegroundColor Green
    Write-Host "Response: $($loginResponse | ConvertTo-Json -Depth 3)"
} catch {
    Write-Host "FAILED: Login failed!" -ForegroundColor Red
    Write-Host "Exception: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        Write-Host "Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
        
        try {
            $errorStream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($errorStream)
            $errorBody = $reader.ReadToEnd()
            Write-Host "Error Response: $errorBody" -ForegroundColor Red
        } catch {
            Write-Host "Could not read error response" -ForegroundColor Red
        }
    }
}

Write-Host "`n=== LOGIN TEST COMPLETED ===" -ForegroundColor Cyan
