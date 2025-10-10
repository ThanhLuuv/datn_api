# Test script for creating book with image upload
$baseUrl = "https://localhost:7001"
$apiUrl = "$baseUrl/api/Book"

Write-Host "Testing Create Book API with image upload..."
Write-Host "API URL: $apiUrl"

# Test data
$isbn = "978-604-1-00003-13"
$title = "Test Book with Image"
$categoryId = 2
$publisherId = 1
$unitPrice = 100000
$publishYear = 2025
$pageCount = 200
$authorIds = "1"

# Create a test image file (1x1 pixel PNG)
$testImagePath = "test-image.png"
$imageBytes = [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==")
[System.IO.File]::WriteAllBytes($testImagePath, $imageBytes)

Write-Host "Test image created: $testImagePath"

try {
    # Create form data
    $form = @{
        isbn = $isbn
        title = $title
        categoryId = $categoryId
        publisherId = $publisherId
        unitPrice = $unitPrice
        publishYear = $publishYear
        pageCount = $pageCount
        authorIds = $authorIds
        imageFile = Get-Item $testImagePath
    }

    Write-Host "Sending request with form data..."
    Write-Host "ISBN: $isbn"
    Write-Host "Title: $title"
    Write-Host "Image File: $($form.imageFile.FullName)"

    # Make the API call
    $response = Invoke-RestMethod -Uri $apiUrl -Method POST -Form $form -ContentType "multipart/form-data"
    
    Write-Host "Success! Book created:"
    Write-Host "ISBN: $($response.data.isbn)"
    Write-Host "Title: $($response.data.title)"
    Write-Host "Image URL: $($response.data.imageUrl)"
    Write-Host "Response: $($response | ConvertTo-Json -Depth 3)"
}
catch {
    Write-Host "Error occurred:"
    Write-Host "Status Code: $($_.Exception.Response.StatusCode.value__)"
    Write-Host "Error Message: $($_.Exception.Message)"
    
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
finally {
    # Clean up test image
    if (Test-Path $testImagePath) {
        Remove-Item $testImagePath
        Write-Host "Test image cleaned up"
    }
}

Write-Host "Test completed."
