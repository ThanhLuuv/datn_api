# Test API Cancel Order
# PowerShell script to test the cancel order API

Write-Host "=== TEST CANCEL ORDER API ===" -ForegroundColor Green

# Configuration
$baseUrl = "http://localhost:5256"
$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJzaGlwcGVyMUBleGFtcGxlLmNvbSIsImVtYWlsIjoic2hpcHBlcjFAZXhhbXBsZS5jb20iLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJERUxJVkVSWV9FTVBMT1lFRSIsImp0aSI6ImFjYmUxMTE1LWFhNTYtNDQ2ZS05MDFlLWVhMTM3ZWMyYTUwYyIsImlhdCI6MTc2MTM5MTQ3MiwiaHR0cDovL3NjaGVtYXMueG1sc29hcC5vcmcvd3MvMjAwNS8wNS9pZGVudGl0eS9jbGFpbXMvbmFtZWlkZW50aWZpZXIiOiI1IiwicGVybWlzc2lvbnMiOiJERUxJVkVSWV9NQU5BR0VNRU5UIiwiZXhwIjoxNzYxOTk2MjcyLCJpc3MiOiJCb29rU3RvcmVBcGkiLCJhdWQiOiJCb29rU3RvcmVBcGlVc2VycyJ9.9sXeAK8BhniCkouDRqHpOwigzrSpWQsLhPr02tdBuCU"

# Test data
$orderId = 1  # Thay đổi orderId phù hợp
$cancelRequest = @{
    reason = "Khách hàng yêu cầu hủy đơn"
    note = "Khách hàng không còn nhu cầu mua sản phẩm này"
} | ConvertTo-Json

Write-Host "Testing Cancel Order API..." -ForegroundColor Yellow
Write-Host "Order ID: $orderId" -ForegroundColor Cyan
Write-Host "Request Body: $cancelRequest" -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/order/$orderId/cancel" `
        -Method POST `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Content-Type" = "application/json"
        } `
        -Body $cancelRequest

    Write-Host "=== SUCCESS ===" -ForegroundColor Green
    Write-Host "Response:" -ForegroundColor Yellow
    $response | ConvertTo-Json -Depth 10
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

