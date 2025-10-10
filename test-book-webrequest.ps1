# Test script for creating book with image upload using Invoke-WebRequest
$baseUrl = "https://localhost:7001"
$apiUrl = "$baseUrl/api/Book"

Write-Host "Testing Create Book API with image upload using Invoke-WebRequest..."
Write-Host "API URL: $apiUrl"

# Test data
$isbn = "978-604-1-00003-15"
$title = "Test Book with Image WebRequest"
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
    # Create boundary for multipart form data
    $boundary = [System.Guid]::NewGuid().ToString()
    
    # Read image file as bytes
    $imageBytes = [System.IO.File]::ReadAllBytes($testImagePath)
    $imageBase64 = [System.Convert]::ToBase64String($imageBytes)
    
    # Create multipart form data manually
    $bodyLines = @()
    $bodyLines += "--$boundary"
    $bodyLines += "Content-Disposition: form-data; name=`"isbn`""
    $bodyLines += ""
    $bodyLines += $isbn
    $bodyLines += "--$boundary"
    $bodyLines += "Content-Disposition: form-data; name=`"title`""
    $bodyLines += ""
    $bodyLines += $title
    $bodyLines += "--$boundary"
    $bodyLines += "Content-Disposition: form-data; name=`"categoryId`""
    $bodyLines += ""
    $bodyLines += $categoryId
    $bodyLines += "--$boundary"
    $bodyLines += "Content-Disposition: form-data; name=`"publisherId`""
    $bodyLines += ""
    $bodyLines += $publisherId
    $bodyLines += "--$boundary"
    $bodyLines += "Content-Disposition: form-data; name=`"unitPrice`""
    $bodyLines += ""
    $bodyLines += $unitPrice
    $bodyLines += "--$boundary"
    $bodyLines += "Content-Disposition: form-data; name=`"publishYear`""
    $bodyLines += ""
    $bodyLines += $publishYear
    $bodyLines += "--$boundary"
    $bodyLines += "Content-Disposition: form-data; name=`"pageCount`""
    $bodyLines += ""
    $bodyLines += $pageCount
    $bodyLines += "--$boundary"
    $bodyLines += "Content-Disposition: form-data; name=`"authorIds`""
    $bodyLines += ""
    $bodyLines += $authorIds
    $bodyLines += "--$boundary"
    $bodyLines += "Content-Disposition: form-data; name=`"imageFile`"; filename=`"test-image.png`""
    $bodyLines += "Content-Type: image/png"
    $bodyLines += ""
    $bodyLines += [System.Text.Encoding]::UTF8.GetString($imageBytes)
    $bodyLines += "--$boundary--"
    
    $body = $bodyLines -join "`r`n"
    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($body)
    
    Write-Host "Sending request..."
    Write-Host "ISBN: $isbn"
    Write-Host "Title: $title"
    Write-Host "Image File Size: $($imageBytes.Length) bytes"

    # Make the API call
    $response = Invoke-WebRequest -Uri $apiUrl -Method POST -Body $bodyBytes -ContentType "multipart/form-data; boundary=$boundary" -SkipCertificateCheck
    
    Write-Host "Success! Response:"
    Write-Host $response.Content
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
