param(
    [string]$BaseUrl = "http://localhost:5256",
    [string]$Email = "admin@bookstore.com",
    [string]$Password = "Admin123!",
    [long]$PublisherId = 1,
    [int]$Qty = 10,
    [decimal]$UnitPrice = 120000
)

$ErrorActionPreference = "Stop"

function Invoke-Json {
    param(
        [string]$Method,
        [string]$Uri,
        [hashtable]$Headers = @{},
        [string]$Body = $null
    )
    $p = @{ Method = $Method; Uri = $Uri; Headers = $Headers }
    if ($Body) { $p.ContentType = 'application/json'; $p.Body = $Body }
    return Invoke-RestMethod @p
}

Write-Host "=== PO FLOW TEST ===" -ForegroundColor Green
Write-Host "Base: $BaseUrl" -ForegroundColor Yellow
Write-Host "Email: $Email" -ForegroundColor Yellow

try {
    Write-Host "[1] Login..." -ForegroundColor Cyan
    $loginBody = @{ email = $Email; password = $Password } | ConvertTo-Json
    $loginRes = Invoke-Json -Method POST -Uri "$BaseUrl/api/auth/login" -Body $loginBody
    if (-not $loginRes.success) { throw "Login failed: $($loginRes.message)" }
    $TOKEN = $loginRes.data.token
    $HDR = @{ Authorization = "Bearer $TOKEN" }
    Write-Host ("Token length: " + $TOKEN.Length)

    # Decode JWT payload for visibility
    try {
        $payload = $TOKEN.Split('.')[1]
        $pad = 4 - ($payload.Length % 4); if($pad -lt 4){ $payload += ('=' * $pad) }
        $json = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload.Replace('-', '+').Replace('_', '/')))
        Write-Host "JWT Payload:" -ForegroundColor DarkYellow
        Write-Host $json
    } catch {}

    Write-Host "[2] Sanity check accounts/employees..." -ForegroundColor Cyan
    $acc = Invoke-Json -Method GET -Uri "$BaseUrl/api/test/accounts?email=$Email"
    $emp = Invoke-Json -Method GET -Uri "$BaseUrl/api/test/employees?email=$Email"
    Write-Host ("Accounts: " + ($acc.count)) -ForegroundColor Yellow
    Write-Host ("Employees: " + ($emp.count)) -ForegroundColor Yellow

    Write-Host "[3] Get books..." -ForegroundColor Cyan
    $booksRes = Invoke-Json -Method GET -Uri "$BaseUrl/api/book" -Headers $HDR
    $isbn = if ($booksRes.data) { $booksRes.data.books[0].isbn } else { $booksRes[0].isbn }
    if (-not $isbn) { throw "No ISBN found" }
    Write-Host "Using ISBN: $isbn" -ForegroundColor Green

    Write-Host "[4] Create PO..." -ForegroundColor Cyan
    $poBody = @{ publisherId = $PublisherId; note = "Don test"; lines = @(@{ isbn = $isbn; qtyOrdered = $Qty; unitPrice = $UnitPrice }) } | ConvertTo-Json
    try {
        $createRes = Invoke-Json -Method POST -Uri "$BaseUrl/api/purchaseorder" -Headers $HDR -Body $poBody
    } catch {
        Write-Host "Create PO failed:" -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream());
            Write-Host ($reader.ReadToEnd())
        } else { Write-Host $_.Exception.Message }
        throw
    }
    $poId = $createRes.data.poId
    Write-Host "PO created: $poId" -ForegroundColor Green

    Write-Host "[5] Change status 1 -> 2..." -ForegroundColor Cyan
    $chgBody = @{ newStatusId = 2; note = "Chuyen giao NXB" } | ConvertTo-Json
    try {
        $chgRes = Invoke-Json -Method POST -Uri "$BaseUrl/api/purchaseorder/$poId/change-status" -Headers $HDR -Body $chgBody
        Write-Host "Status changed. orderFileUrl: $($chgRes.data.orderFileUrl)" -ForegroundColor Green
    } catch {
        Write-Host "Change status failed:" -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream());
            Write-Host ($reader.ReadToEnd())
        } else { Write-Host $_.Exception.Message }
        throw
    }
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}


