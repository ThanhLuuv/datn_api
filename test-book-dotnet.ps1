# Test script using .NET HttpClient
Add-Type -AssemblyName System.Net.Http

Write-Host "Testing Book Creation API using .NET HttpClient..."

# Create test image
$imageBytes = [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==")
[System.IO.File]::WriteAllBytes("test.png", $imageBytes)

try {
    $httpClient = New-Object System.Net.Http.HttpClient
    $httpClient.BaseAddress = "https://localhost:7001"
    
    # Disable SSL certificate validation for testing
    $httpClientHandler = New-Object System.Net.Http.HttpClientHandler
    $httpClientHandler.ServerCertificateCustomValidationCallback = {$true}
    $httpClient = New-Object System.Net.Http.HttpClient($httpClientHandler)
    $httpClient.BaseAddress = "https://localhost:7001"
    
    # Create multipart form data
    $multipartContent = New-Object System.Net.Http.MultipartFormDataContent
    
    # Add form fields
    $multipartContent.Add([System.Net.Http.StringContent]::new("978-604-1-00003-17"), "isbn")
    $multipartContent.Add([System.Net.Http.StringContent]::new("Test Book .NET"), "title")
    $multipartContent.Add([System.Net.Http.StringContent]::new("2"), "categoryId")
    $multipartContent.Add([System.Net.Http.StringContent]::new("1"), "publisherId")
    $multipartContent.Add([System.Net.Http.StringContent]::new("100000"), "unitPrice")
    $multipartContent.Add([System.Net.Http.StringContent]::new("2025"), "publishYear")
    $multipartContent.Add([System.Net.Http.StringContent]::new("200"), "pageCount")
    $multipartContent.Add([System.Net.Http.StringContent]::new("1"), "authorIds")
    
    # Add image file
    $imageContent = New-Object System.Net.Http.ByteArrayContent(,$imageBytes)
    $imageContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("image/png")
    $multipartContent.Add($imageContent, "imageFile", "test.png")
    
    Write-Host "Sending request..."
    $response = $httpClient.PostAsync("/api/Book", $multipartContent).Result
    
    Write-Host "Status Code: $($response.StatusCode)"
    $responseContent = $response.Content.ReadAsStringAsync().Result
    Write-Host "Response: $responseContent"
    
    $httpClient.Dispose()
}
catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.InnerException) {
        Write-Host "Inner Exception: $($_.Exception.InnerException.Message)"
    }
}
finally {
    if (Test-Path "test.png") {
        Remove-Item "test.png"
    }
}

Write-Host "Test completed."
