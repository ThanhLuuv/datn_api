param(
    [string]$BaseUrl = "http://localhost:5256",
    [string]$Email = "admin@bookstore.com",
    [string]$Password = "Admin123!",
    [int]$Qty = 10,
    [decimal]$UnitPrice = 120000,
    [long]$PublisherId = 1
)

$ErrorActionPreference = "Stop"
Write-Host "=== Test Purchase Order Flow ===" -ForegroundColor Green
Write-Host "Base: $BaseUrl" -ForegroundColor Yellow

function Invoke-Json($Method, $Uri, $Headers = @{}, $Body = $null) {
    $p = @{ Method = $Method; Uri = $Uri; Headers = $Headers }
    if ($Body) { $p.ContentType = 'application/json'; $p.Body = $Body }
    return Invoke-RestMethod @p
}

try {
    # 1) Login
    Write-Host "\n[1] Login..." -ForegroundColor Cyan
    $loginBody = @{ email = $Email; password = $Password } | ConvertTo-Json
    $loginRes = Invoke-Json -Method POST -Uri "$BaseUrl/api/auth/login" -Body $loginBody
    $TOKEN = $loginRes.data.token
    if (-not $TOKEN) { throw "Login failed: no token" }
    $HDR = @{ Authorization = "Bearer $TOKEN" }
    Write-Host "Token acquired." -ForegroundColor Green

    # 2) Get books & choose first ISBN
    Write-Host "\n[2] Get books..." -ForegroundColor Cyan
    $booksRes = Invoke-Json -Method GET -Uri "$BaseUrl/api/book" -Headers $HDR
    if ($booksRes.data) { $ISBN = $booksRes.data.books[0].isbn } else { $ISBN = $booksRes[0].isbn }
    if (-not $ISBN) { throw "No book found to order" }
    Write-Host "Using ISBN: $ISBN" -ForegroundColor Green

    # 3) Create Purchase Order
    Write-Host "\n[3] Create purchase order..." -ForegroundColor Cyan
    $poBody = @{ publisherId = $PublisherId; note = 'Don test'; lines = @(@{ isbn = $ISBN; qtyOrdered = $Qty; unitPrice = $UnitPrice }) } | ConvertTo-Json
    $createRes = Invoke-Json -Method POST -Uri "$BaseUrl/api/purchaseorder" -Headers $HDR -Body $poBody
    $POID = $createRes.data.poId
    if (-not $POID) { throw "Create PO failed" }
    Write-Host "PO created: $POID" -ForegroundColor Green

    # 4) Change Status 1 -> 2
    Write-Host "\n[4] Change status 1 -> 2..." -ForegroundColor Cyan
    $chgBody = @{ newStatusId = 2; note = 'Chuyen giao NXB' } | ConvertTo-Json
    $chgRes = Invoke-Json -Method POST -Uri "$BaseUrl/api/purchaseorder/$POID/change-status" -Headers $HDR -Body $chgBody
    Write-Host "Status changed. orderFileUrl:" -ForegroundColor Green
    $chgRes.data.orderFileUrl | Out-String | Write-Host

    Write-Host "\nAll done." -ForegroundColor Green
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream();
        $reader = New-Object System.IO.StreamReader($stream);
        $err = $reader.ReadToEnd();
        Write-Host $err -ForegroundColor Yellow
    }
    exit 1
}


