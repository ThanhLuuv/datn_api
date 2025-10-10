# Test creating book without image first
Write-Host "Testing Book Creation API without image..."

try {
    $httpClient = New-Object System.Net.Http.HttpClient
    $httpClientHandler = New-Object System.Net.Http.HttpClientHandler
    $httpClientHandler.ServerCertificateCustomValidationCallback = {$true}
    $httpClient = New-Object System.Net.Http.HttpClient($httpClientHandler)
    $httpClient.BaseAddress = "https://localhost:7001"
    
    # Create multipart form data
    $multipartContent = New-Object System.Net.Http.MultipartFormDataContent
    
    # Add form fields
    $multipartContent.Add([System.Net.Http.StringContent]::new("978-604-1-00003-18"), "isbn")
    $multipartContent.Add([System.Net.Http.StringContent]::new("Test Book No Image"), "title")
    $multipartContent.Add([System.Net.Http.StringContent]::new("2"), "categoryId")
    $multipartContent.Add([System.Net.Http.StringContent]::new("1"), "publisherId")
    $multipartContent.Add([System.Net.Http.StringContent]::new("100000"), "unitPrice")
    $multipartContent.Add([System.Net.Http.StringContent]::new("2025"), "publishYear")
    $multipartContent.Add([System.Net.Http.StringContent]::new("200"), "pageCount")
    $multipartContent.Add([System.Net.Http.StringContent]::new("1"), "authorIds")
    
    Write-Host "Sending request without image..."
    $response = $httpClient.PostAsync("/api/Book", $multipartContent).Result
    
    Write-Host "Status Code: $($response.StatusCode)"
    if ($response.IsSuccessStatusCode) {
        $responseContent = $response.Content.ReadAsStringAsync().Result
        Write-Host "Success! Response: $responseContent"
    } else {
        Write-Host "Error Response: $($response.Content.ReadAsStringAsync().Result)"
    }
    
    $httpClient.Dispose()
}
catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.InnerException) {
        Write-Host "Inner Exception: $($_.Exception.InnerException.Message)"
    }
}

Write-Host "Test completed."
