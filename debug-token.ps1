# Debug JWT Token Simple

$baseUrl = "http://localhost:5256/api"
$headers = @{
    "Content-Type" = "application/json"
}

Write-Host "=== JWT TOKEN DEBUG ===" -ForegroundColor Cyan

# Login
$loginData = @{
    email = "admin@bookstore.com"
    password = "Admin123!"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method POST -Headers $headers -Body $loginData
$token = $loginResponse.data.token

Write-Host "Token: $($token.Substring(0, 50))..."

# Parse JWT
$tokenParts = $token.Split('.')
$payload = $tokenParts[1]
while ($payload.Length % 4 -ne 0) { $payload += "=" }
$payload = $payload.Replace('-', '+').Replace('_', '/')

$decodedBytes = [System.Convert]::FromBase64String($payload)
$decodedText = [System.Text.Encoding]::UTF8.GetString($decodedBytes)
$claims = $decodedText | ConvertFrom-Json

Write-Host "JWT Claims:"
$claims.PSObject.Properties | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Value)"
}

# Check NameIdentifier
$nameId = $claims.'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'
Write-Host ""
Write-Host "NameIdentifier: $nameId"

if ($nameId) {
    Write-Host "NameIdentifier found!" -ForegroundColor Green
} else {
    Write-Host "NameIdentifier NOT found!" -ForegroundColor Red
}

Write-Host "DEBUG COMPLETED"

