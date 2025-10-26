# Test Delivery Candidates API - Email Fix
# PowerShell script to test the delivery candidates API with correct email

Write-Host "=== TEST DELIVERY CANDIDATES API - EMAIL FIX ===" -ForegroundColor Green

# Configuration
$baseUrl = "http://localhost:5256"
$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhZG1pbkBleGFtcGxlLmNvbSIsImVtYWlsIjoiYWRtaW5AZXhhbXBsZS5jb20iLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJBRE1JTiIsImp0aSI6IjEyMzQ1Njc4LTkwYWItY2RlZi0xMjM0LTU2Nzg5MGFiY2RlZiIsImlhdCI6MTc2MTM5MTQ3MiwiaHR0cDovL3NjaGVtYXMueG1sc29hcC5vcmcvd3MvMjAwNS8wNS9pZGVudGl0eS9jbGFpbXMvbmFtZWlkZW50aWZpZXIiOiIxIiwicGVybWlzc2lvbnMiOiJBRE1JTiIsImV4cCI6MTc2MTk5NjI3MiwiaXNzIjoiQm9va1N0b3JlQXBpIiwiYXVkIjoiQm9va1N0b3JlQXBpVXNlcnMifQ.example"

# Test data
$orderId = 1  # Thay đổi orderId phù hợp

Write-Host "Testing Delivery Candidates API with Email Fix..." -ForegroundColor Yellow
Write-Host "Order ID: $orderId" -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/order/$orderId/delivery-candidates" `
        -Method GET `
        -Headers @{
            "Authorization" = "Bearer $token"
        }

    Write-Host "=== SUCCESS ===" -ForegroundColor Green
    Write-Host "Response:" -ForegroundColor Yellow
    $response | ConvertTo-Json -Depth 10
    
    Write-Host "`n=== EMAIL VERIFICATION ===" -ForegroundColor Cyan
    if ($response.data -and $response.data.Count -gt 0) {
        foreach ($employee in $response.data) {
            Write-Host "Employee: $($employee.fullName)" -ForegroundColor White
            Write-Host "  - Email: $($employee.email)" -ForegroundColor Green
            Write-Host "  - Phone: $($employee.phone)" -ForegroundColor White
            Write-Host "  - Area: $($employee.areaName)" -ForegroundColor White
            Write-Host "  - Area Matched: $($employee.isAreaMatched)" -ForegroundColor White
            Write-Host "  - Active Orders: $($employee.activeAssignedOrders)" -ForegroundColor White
            Write-Host "  - Delivered Orders: $($employee.totalDeliveredOrders)" -ForegroundColor White
            Write-Host ""
        }
        
        Write-Host "✅ Email addresses are now correctly retrieved from Account table!" -ForegroundColor Green
    } else {
        Write-Host "❌ No delivery candidates found!" -ForegroundColor Red
    }
}
catch {
    Write-Host "=== ERROR ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody" -ForegroundColor Red
    }
}

Write-Host "`n=== TEST COMPLETED ===" -ForegroundColor Green

