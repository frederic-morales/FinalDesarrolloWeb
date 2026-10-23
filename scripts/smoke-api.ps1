param([string]$BaseUrl = 'https://localhost:7292')

$ErrorActionPreference = 'Stop'
$secretPath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\CuentasPorPagarAPI-LocalDevelopment\secrets.json'
if (-not (Test-Path -LiteralPath $secretPath)) { throw 'Configure .NET User Secrets antes de ejecutar esta prueba.' }
$settings = Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json

function Send-Json([string]$Path, [string]$Method, [object]$Data, [hashtable]$Headers) {
    $body = $Data | ConvertTo-Json -Depth 8
    return Invoke-RestMethod -Uri "$BaseUrl$Path" -Method $Method -Headers $Headers -ContentType 'application/json' -Body $body
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { throw "$Message. Esperado: $Expected; obtenido: $Actual" }
}

$login = Send-Json '/api/v1/auth/login' 'Post' @{
    userName = $settings.'SeedAdmin:UserName'
    password = $settings.'SeedAdmin:Password'
} @{}
if ([string]::IsNullOrWhiteSpace($login.token)) { throw 'El login no devolvió un token.' }
$headers = @{ Authorization = "Bearer $($login.token)" }
$me = Invoke-RestMethod -Uri "$BaseUrl/api/v1/auth/me" -Headers $headers
Assert-Equal $me.userName $settings.'SeedAdmin:UserName' 'El token del login debe funcionar en /auth/me'

$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$supplier = Send-Json '/api/v1/suppliers' 'Post' @{
    name = "Proveedor prueba $suffix"
    nit = "TEST-$suffix"
    email = 'prueba@example.com'
} $headers

$invoice = Send-Json '/api/v1/invoices' 'Post' @{
    supplierId = $supplier.id
    series = 'TEST'
    number = $suffix
    issueDate = '2026-10-21'
    dueDate = '2026-11-21'
    lines = @(@{ description = 'Concepto de prueba'; quantity = 1; unitPrice = 100 })
} $headers
Assert-Equal $invoice.total 100 'Total de factura incorrecto'

$payment = Send-Json '/api/v1/payments' 'Post' @{
    supplierId = $supplier.id
    paymentMethodId = 2
    date = '2026-10-21'
    reference = "TEST-$suffix"
    amount = 40
    allocations = @(@{ invoiceId = $invoice.id; amount = 40 })
} $headers

$afterPayment = Invoke-RestMethod -Uri "$BaseUrl/api/v1/invoices/$($invoice.id)" -Headers $headers
Assert-Equal $afterPayment.paid 40 'Abono incorrecto'
Assert-Equal $afterPayment.balance 60 'Saldo tras abono incorrecto'

$statement = Invoke-RestMethod -Uri "$BaseUrl/api/v1/reports/suppliers/$($supplier.id)/statement?from=2026-10-01&to=2026-10-31" -Headers $headers
Assert-Equal $statement.closingBalance 60 'Saldo del estado de cuenta incorrecto'
Assert-Equal $statement.movements.Count 2 'Movimientos del estado de cuenta incorrectos'

$payables = Invoke-RestMethod -Uri "$BaseUrl/api/v1/reports/payables?asOf=2026-10-21&supplierId=$($supplier.id)" -Headers $headers
Assert-Equal $payables.total 1 'Número de facturas pendientes incorrecto'
Assert-Equal $payables.items[0].balance 60 'Saldo del reporte de pendientes incorrecto'

$overpayStatus = 0
try {
    Send-Json '/api/v1/payments' 'Post' @{
        supplierId = $supplier.id
        paymentMethodId = 2
        date = '2026-10-21'
        amount = 70
        allocations = @(@{ invoiceId = $invoice.id; amount = 70 })
    } $headers | Out-Null
} catch {
    $overpayStatus = [int]$_.Exception.Response.StatusCode
}
Assert-Equal $overpayStatus 409 'El sobrepago debe rechazarse'

Invoke-RestMethod -Uri "$BaseUrl/api/v1/payments/$($payment.id)/cancel" -Method Post -Headers $headers | Out-Null
$restored = Invoke-RestMethod -Uri "$BaseUrl/api/v1/invoices/$($invoice.id)" -Headers $headers
Assert-Equal $restored.balance 100 'La anulación del pago debe restaurar el saldo'

Invoke-RestMethod -Uri "$BaseUrl/api/v1/invoices/$($invoice.id)/cancel" -Method Post -Headers $headers | Out-Null
$cancelled = Invoke-RestMethod -Uri "$BaseUrl/api/v1/invoices/$($invoice.id)" -Headers $headers
Assert-Equal $cancelled.status 'Cancelled' 'La factura debe quedar anulada'
Assert-Equal $cancelled.balance 0 'La factura anulada debe tener saldo cero'

Send-Json "/api/v1/suppliers/$($supplier.id)/status" 'Put' @{ isActive = $false } $headers | Out-Null

Write-Output "OK: proveedor $($supplier.id), factura $($invoice.id), pago $($payment.id); saldos 100→60→100→0; sobrepago HTTP 409."
