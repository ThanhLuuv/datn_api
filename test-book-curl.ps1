# Test script for creating book with image upload using curl
$baseUrl = "https://localhost:7001"
$apiUrl = "$baseUrl/api/Book"

Write-Host "Testing Create Book API with image upload using curl..."
Write-Host "API URL: $apiUrl"

# Test data
$isbn = "978-604-1-00003-14"
$title = "Test Book with Image Curl"
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
    # Build curl command
    $curlCommand = @(
        "curl",
        "-X", "POST",
        "-F", "isbn=$isbn",
        "-F", "title=$title",
        "-F", "categoryId=$categoryId",
        "-F", "publisherId=$publisherId",
        "-F", "unitPrice=$unitPrice",
        "-F", "publishYear=$publishYear",
        "-F", "pageCount=$pageCount",
        "-F", "authorIds=$authorIds",
        "-F", "imageFile=@$testImagePath",
        $apiUrl
    )

    Write-Host "Executing curl command..."
    Write-Host "Command: $($curlCommand -join ' ')"

    # Execute curl command
    $result = & $curlCommand[0] $curlCommand[1..($curlCommand.Length-1)]
    
    Write-Host "Response:"
    Write-Host $result
}
catch {
    Write-Host "Error occurred:"
    Write-Host "Error Message: $($_.Exception.Message)"
}
finally {
    # Clean up test image
    if (Test-Path $testImagePath) {
        Remove-Item $testImagePath
        Write-Host "Test image cleaned up"
    }
}

Write-Host "Test completed."
