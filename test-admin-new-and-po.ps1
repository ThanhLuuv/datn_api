param(
    [string]$BaseUrl = "http://localhost:5256",
    [string]$AdminEmailPrefix = "admin_new",
    [string]$Password = "Admin123!",
    [long]$PublisherId = 1,
    [int]$Qty = 10,
    [decimal]$UnitPrice = 120000
)

$ErrorActionPreference = "Stop"
function Invoke-Json($Method, $Uri, $Headers = @{}, $Body = $null) {
    $p = @{ Method = $Method; Uri = $Uri; Headers = $Headers }
    if ($Body) { $p.ContentType = 'application/json'; $p.Body = $Body }
    return Invoke-RestMethod @p
}

Write-Host "=== Create new ADMIN, login and create PO ===" -ForegroundColor Green
Write-Host "Base: $BaseUrl" -ForegroundColor Yellow

try {
    $suffix = (Get-Date -Format 'yyyyMMddHHmmss')
    $email = "$AdminEmailPrefix+$suffix@bookstore.com"

    Write-Host "[1] Register admin: $email" -ForegroundColor Cyan
    $registerBody = @{ email = $email; password = $Password; confirmPassword = $Password; roleId = 3 } | ConvertTo-Json
    $regRes = Invoke-Json -Method POST -Uri "$BaseUrl/api/auth/register" -Body $registerBody
    if (-not $regRes.success) { throw "Register failed" }

    Write-Host "[2] Login..." -ForegroundColor Cyan
    $loginBody = @{ email = $email; password = $Password } | ConvertTo-Json
    $loginRes = Invoke-Json -Method POST -Uri "$BaseUrl/api/auth/login" -Body $loginBody
    $TOKEN = $loginRes.data.token
    if ([string]::IsNullOrWhiteSpace($TOKEN)) { throw "No token returned" }
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

    Write-Host "[3] Get books..." -ForegroundColor Cyan
    $booksRes = Invoke-Json -Method GET -Uri "$BaseUrl/api/book" -Headers $HDR
    $isbn = $booksRes.data.books[0].isbn
    if (-not $isbn) { $isbn = $booksRes[0].isbn }
    if (-not $isbn) { throw "No ISBN found" }
    Write-Host "Using ISBN: $isbn" -ForegroundColor Green

    Write-Host "[4] Create purchase order..." -ForegroundColor Cyan
    $poBody = @{ publisherId = $PublisherId; note = "Don test $suffix"; lines = @(@{ isbn = $isbn; qtyOrdered = $Qty; unitPrice = $UnitPrice }) } | ConvertTo-Json
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
    $chgBody = @{ newStatusId = 2; note = "Chuyen giao NXB $suffix" } | ConvertTo-Json
    $chgRes = Invoke-Json -Method POST -Uri "$BaseUrl/api/purchaseorder/$poId/change-status" -Headers $HDR -Body $chgBody
    Write-Host "Status changed. orderFileUrl: $($chgRes.data.orderFileUrl)" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}


