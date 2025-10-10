# Test script for delivery candidates API
# Run this after applying fix-delivery-employees.sql

$baseUrl = "http://localhost:5256"  # Update port if different
$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhZG1pbkBib29rc3RvcmUuY29tIiwiZW1haWwiOiJhZG1pbkBib29rc3RvcmUuY29tIiwiaHR0cDovL3NjaGVtYXMubWljcm9zb2Z0LmNvbS93cy8yMDA4LzA2L2lkZW50aXR5L2NsYWltcy9yb2xlIjoiQURNSU4iLCJqdGkiOiI2ZWMxOTU5MC1lYzE3LTQ4NjMtYjY2MS1kM2Y3ZDEyMzM5NWUiLCJpYXQiOjE3NTk0OTM0MzAsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL25hbWVpZGVudGlmaWVyIjoiMSIsInBlcm1pc3Npb25zIjoiREVMSVZFUllfTUFOQUdFTUVOVCBSRUFEX0JPT0sgUkVBRF9DQVRFR09SWSBSRUFEX0dPT0RTX1JFQ0VJUFQgUkVBRF9QVVJDSEFTRV9PUkRFUiBTQUxFU19NQU5BR0VNRU5UIFdSSVRFX0JPT0sgV1JJVEVfQ0FURUdPUlkgV1JJVEVfR09PRFNfUkVDRUlQVCBXUklURV9QVVJDSEFTRV9PUkRFUiIsImV4cCI6MTc2MDA5ODIzMCwiaXNzIjoiQm9va1N0b3JlQXBpIiwiYXVkIjoiQm9va1N0b3JlQXBpVXNlcnMifQ.hu4cvijTGxP50Ds7bBZIMw6qyiWituNqXkDgfQPDbaM"

Write-Host "=== TESTING DELIVERY CANDIDATES API ===" -ForegroundColor Green

# Headers
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Test different orders
$testOrders = @(1001, 1002, 1003, 1004, 1005)

foreach ($orderId in $testOrders) {
    Write-Host "`nTesting Order ID: $orderId" -ForegroundColor Yellow
    
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/api/order/$orderId/delivery-candidates" -Method GET -Headers $headers
        
        if ($response.success) {
            Write-Host "Success: Found $($response.data.Count) delivery candidates" -ForegroundColor Green
            
            if ($response.data.Count -gt 0) {
                foreach ($candidate in $response.data) {
                    Write-Host "  - Employee ID: $($candidate.employeeId)" -ForegroundColor Cyan
                    Write-Host "    Name: $($candidate.fullName)" -ForegroundColor Cyan
                    Write-Host "    Phone: $($candidate.phone)" -ForegroundColor Cyan
                    Write-Host "    Email: $($candidate.email)" -ForegroundColor Cyan
                    Write-Host "    Areas: $($candidate.areaName)" -ForegroundColor Cyan
                    Write-Host "    Area Matched: $($candidate.isAreaMatched)" -ForegroundColor Cyan
                    Write-Host "    Active Orders: $($candidate.activeAssignedOrders)" -ForegroundColor Cyan
                    Write-Host "    Total Delivered: $($candidate.totalDeliveredOrders)" -ForegroundColor Cyan
                    Write-Host ""
                }
            } else {
                Write-Host "  No delivery candidates found!" -ForegroundColor Red
            }
        } else {
            Write-Host "Error: $($response.message)" -ForegroundColor Red
        }
    } catch {
        Write-Host "Exception: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n=== TEST COMPLETED ===" -ForegroundColor Green
Write-Host "`nIf no candidates found, run fix-delivery-employees.sql first!" -ForegroundColor Yellow


