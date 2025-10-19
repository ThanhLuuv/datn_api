# Test script for Return API with status management
# Run this after applying return-status-migration.sql

$baseUrl = "http://localhost:5000"
$token = "YOUR_JWT_TOKEN_HERE"  # Replace with actual token

Write-Host "=== TESTING RETURN API WITH STATUS MANAGEMENT ===" -ForegroundColor Green

# Headers
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Test 1: Get all returns
Write-Host "`n1. Getting all returns..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/return" -Method GET -Headers $headers
    Write-Host "Success: Found $($response.data.returns.Count) returns" -ForegroundColor Green
    foreach ($return in $response.data.returns) {
        Write-Host "  - Return ID: $($return.returnId), Status: $($return.statusText), Amount: $($return.totalAmount)" -ForegroundColor Cyan
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Get returns by status (PENDING)
Write-Host "`n2. Getting pending returns..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/return?status=0" -Method GET -Headers $headers
    Write-Host "Success: Found $($response.data.returns.Count) pending returns" -ForegroundColor Green
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Get specific return by ID
Write-Host "`n3. Getting return by ID (1)..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/return/1" -Method GET -Headers $headers
    Write-Host "Success: Return ID $($response.data.returnId)" -ForegroundColor Green
    Write-Host "  Status: $($response.data.statusText)" -ForegroundColor Cyan
    Write-Host "  Reason: $($response.data.reason)" -ForegroundColor Cyan
    Write-Host "  Total Amount: $($response.data.totalAmount)" -ForegroundColor Cyan
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Update return status to APPROVED
Write-Host "`n4. Updating return status to APPROVED..." -ForegroundColor Yellow
try {
    $body = @{
        status = 1
        notes = "Đã duyệt phiếu trả và hoàn tiền cho khách hàng"
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "$baseUrl/api/return/1/status" -Method PUT -Headers $headers -Body $body
    Write-Host "Success: Updated return status to $($response.data.statusText)" -ForegroundColor Green
    Write-Host "  Processed by: $($response.data.processedByEmployeeName)" -ForegroundColor Cyan
    Write-Host "  Processed at: $($response.data.processedAt)" -ForegroundColor Cyan
    Write-Host "  Notes: $($response.data.notes)" -ForegroundColor Cyan
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 5: Update return status to REJECTED
Write-Host "`n5. Updating return status to REJECTED..." -ForegroundColor Yellow
try {
    $body = @{
        status = 2
        notes = "Từ chối vì không đủ điều kiện trả hàng"
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "$baseUrl/api/return/3/status" -Method PUT -Headers $headers -Body $body
    Write-Host "Success: Updated return status to $($response.data.statusText)" -ForegroundColor Green
    Write-Host "  Notes: $($response.data.notes)" -ForegroundColor Cyan
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 6: Update return status to PROCESSED
Write-Host "`n6. Updating return status to PROCESSED..." -ForegroundColor Yellow
try {
    $body = @{
        status = 3
        notes = "Đã hoàn thành xử lý phiếu trả"
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "$baseUrl/api/return/2/status" -Method PUT -Headers $headers -Body $body
    Write-Host "Success: Updated return status to $($response.data.statusText)" -ForegroundColor Green
    Write-Host "  Notes: $($response.data.notes)" -ForegroundColor Cyan
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 7: Get returns by different statuses
Write-Host "`n7. Getting returns by different statuses..." -ForegroundColor Yellow

$statuses = @(
    @{value=0; name="PENDING"},
    @{value=1; name="APPROVED"},
    @{value=2; name="REJECTED"},
    @{value=3; name="PROCESSED"}
)

foreach ($status in $statuses) {
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/api/return?status=$($status.value)" -Method GET -Headers $headers
        Write-Host "  $($status.name): $($response.data.returns.Count) returns" -ForegroundColor Cyan
    } catch {
        Write-Host "  $($status.name): Error - $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n=== TEST COMPLETED ===" -ForegroundColor Green
Write-Host "`nAPI Endpoints tested:" -ForegroundColor Yellow
Write-Host "  GET /api/return - Get all returns" -ForegroundColor White
Write-Host "  GET /api/return?status={0|1|2|3} - Get returns by status" -ForegroundColor White
Write-Host "  GET /api/return/{id} - Get return by ID" -ForegroundColor White
Write-Host "  PUT /api/return/{id}/status - Update return status" -ForegroundColor White
Write-Host "`nStatus values:" -ForegroundColor Yellow
Write-Host "  0 = PENDING (Chờ xử lý)" -ForegroundColor White
Write-Host "  1 = APPROVED (Đã duyệt)" -ForegroundColor White
Write-Host "  2 = REJECTED (Từ chối)" -ForegroundColor White
Write-Host "  3 = PROCESSED (Đã xử lý)" -ForegroundColor White














