# Test API Assign Delivery with Email Notification
# PowerShell script to test the assign delivery API with email notification

Write-Host "=== TEST ASSIGN DELIVERY WITH EMAIL API ===" -ForegroundColor Green

# Configuration
$baseUrl = "http://localhost:5256"
$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJzaGlwcGVyMUBleGFtcGxlLmNvbSIsImVtYWlsIjoic2hpcHBlcjFAZXhhbXBsZS5jb20iLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJERUxJVkVSWV9FTVBMT1lFRSIsImp0aSI6ImFjYmUxMTE1LWFhNTYtNDQ2ZS05MDFlLWVhMTM3ZWMyYTUwYyIsImlhdCI6MTc2MTM5MTQ3MiwiaHR0cDovL3NjaGVtYXMueG1sc29hcC5vcmcvd3MvMjAwNS8wNS9pZGVudGl0eS9jbGFpbXMvbmFtZWlkZW50aWZpZXIiOiI1IiwicGVybWlzc2lvbnMiOiJERUxJVkVSWV9NQU5BR0VNRU5UIiwiZXhwIjoxNzYxOTk2MjcyLCJpc3MiOiJCb29rU3RvcmVBcGkiLCJhdWQiOiJCb29rU3RvcmVBcGlVc2VycyJ9.9sXeAK8BhniCkouDRqHpOwigzrSpWQsLhPr02tdBuCU"

# Test data
$orderId = 1  # Thay đổi orderId phù hợp
$assignRequest = @{
    deliveryEmployeeId = 5  # Thay đổi employeeId phù hợp
    deliveryDate = "2024-01-15T00:00:00Z"
} | ConvertTo-Json

Write-Host "Testing Assign Delivery API with Email Notification..." -ForegroundColor Yellow
Write-Host "Order ID: $orderId" -ForegroundColor Cyan
Write-Host "Request Body: $assignRequest" -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/order/$orderId/assign-delivery" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $assignRequest

    Write-Host "=== SUCCESS ===" -ForegroundColor Green
    Write-Host "Response:" -ForegroundColor Yellow
    $response | ConvertTo-Json -Depth 10
    
    Write-Host "`n=== EMAIL NOTIFICATION ===" -ForegroundColor Cyan
    Write-Host "Email đã được gửi đến nhân viên giao hàng với thông tin:" -ForegroundColor Yellow
    Write-Host "- Mã đơn hàng: #$orderId" -ForegroundColor White
    Write-Host "- Thông tin khách hàng và địa chỉ giao hàng" -ForegroundColor White
    Write-Host "- Ngày giao hàng dự kiến" -ForegroundColor White
    Write-Host "- Tổng tiền và số lượng sản phẩm" -ForegroundColor White
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

