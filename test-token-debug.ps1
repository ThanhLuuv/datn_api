# Debug JWT Token

$baseUrl = "http://localhost:5256/api"
$headers = @{
    "Content-Type" = "application/json"
}

Write-Host "=== JWT TOKEN DEBUG ===" -ForegroundColor Cyan

# Login
Write-Host "`n1. Login..." -ForegroundColor Green
$loginData = @{
    email = "admin@bookstore.com"
    password = "Admin123!"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method POST -Headers $headers -Body $loginData
if ($loginResponse.success) {
    $token = $loginResponse.data.token
    Write-Host "Login successful!" -ForegroundColor Green
    Write-Host "Token: $($token.Substring(0, 50))..." -ForegroundColor White
    
    # Parse JWT token
    Write-Host "`n2. Parsing JWT Token..." -ForegroundColor Green
    $tokenParts = $token.Split('.')
    if ($tokenParts.Length -eq 3) {
        # Decode payload
        $payload = $tokenParts[1]
        while ($payload.Length % 4 -ne 0) { $payload += "=" }
        $payload = $payload.Replace('-', '+').Replace('_', '/')
        
        $decodedBytes = [System.Convert]::FromBase64String($payload)
        $decodedText = [System.Text.Encoding]::UTF8.GetString($decodedBytes)
        $claims = $decodedText | ConvertFrom-Json
        
        Write-Host "JWT Claims:" -ForegroundColor White
        $claims.PSObject.Properties | ForEach-Object {
            Write-Host "  $($_.Name): $($_.Value)" -ForegroundColor Gray
        }
        
        # Check for NameIdentifier specifically
        $nameIdentifier = $claims.'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'
        Write-Host "`nNameIdentifier claim: $nameIdentifier" -ForegroundColor Yellow
        
        if ($nameIdentifier) {
            Write-Host "✓ NameIdentifier found: $nameIdentifier" -ForegroundColor Green
        } else {
            Write-Host "✗ NameIdentifier NOT found!" -ForegroundColor Red
        }
    }
    
    # Test protected endpoint
    Write-Host "`n3. Testing Protected Endpoint..." -ForegroundColor Green
    $authHeaders = @{
        "Content-Type" = "application/json"
        "Authorization" = "Bearer $token"
    }
    
    try {
        $protectedResponse = Invoke-RestMethod -Uri "$baseUrl/test/protected" -Method GET -Headers $authHeaders
        Write-Host "Protected endpoint works!" -ForegroundColor Green
        Write-Host "Response: $($protectedResponse | ConvertTo-Json)" -ForegroundColor White
    } catch {
        Write-Host "Protected endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    
} else {
    Write-Host "Login failed!" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== DEBUG COMPLETED ===" -ForegroundColor Cyan
