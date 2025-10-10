# Simple test for book creation API
Write-Host "Testing Book Creation API..."

# Create test image
$imageBytes = [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==")
[System.IO.File]::WriteAllBytes("test.png", $imageBytes)

# Test data
$body = @{
    isbn = "978-604-1-00003-16"
    title = "Test Book Simple"
    categoryId = 2
    publisherId = 1
    unitPrice = 100000
    publishYear = 2025
    pageCount = 200
    authorIds = "1"
    imageFile = Get-Item "test.png"
}

Write-Host "Sending request to API..."
Write-Host "ISBN: $($body.isbn)"
Write-Host "Title: $($body.title)"

try {
    $response = Invoke-RestMethod -Uri "https://localhost:7001/api/Book" -Method POST -Form $body
    Write-Host "Success! Response:"
    Write-Host ($response | ConvertTo-Json -Depth 3)
}
catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
finally {
    if (Test-Path "test.png") {
        Remove-Item "test.png"
    }
}
