$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'CuentasPorPagarAPI/CuentasPorPagarAPI/CuentasPorPagarAPI.csproj'
$dll = Join-Path $root 'CuentasPorPagarAPI/CuentasPorPagarAPI/bin/Debug/net10.0/CuentasPorPagarAPI.dll'
$secrets = dotnet user-secrets list --project $project
function Secret([string] $name) {
    $line = $secrets | Where-Object { $_.StartsWith("$name = ") } | Select-Object -First 1
    if (-not $line) { throw "Falta $name en User Secrets." }
    return $line.Substring($name.Length + 3)
}
$connection = Secret 'ConnectionStrings:DefaultConnection'
$adminName = Secret 'SeedAdmin:UserName'
$adminPassword = Secret 'SeedAdmin:Password'
if ($connection -notmatch '(?i)(^|;)\s*Host=127\.0\.0\.1(;|$)' -and
    $connection -notmatch '(?i)(^|;)\s*Host=localhost(;|$)') {
    throw 'La prueba solo se ejecuta con PostgreSQL local.'
}
$database = 'cpp_smoke_' + [guid]::NewGuid().ToString('N').Substring(0, 8)
if ($database -notmatch '^cpp_smoke_[0-9a-f]{8}$') { throw 'Nombre de base temporal inválido.' }
$parts = $connection -split ';' | ForEach-Object {
    if ($_ -match '(?i)^\s*Database=') { "Database=$database" } else { $_ }
}
$env:ConnectionStrings__DefaultConnection = $parts -join ';'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5198'
$created = $false
$apiProcess = $null
try {
    docker exec cuentas-por-pagar-postgres createdb -U cpp_local $database
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear la base temporal.' }
    $created = $true
    dotnet ef database update --project $project --startup-project $project | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Falló la migración en la base temporal.' }
    dotnet $dll --seed-admin | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Falló la creación del administrador temporal.' }
    $apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($dll) -WindowStyle Hidden -PassThru
    $base = 'http://127.0.0.1:5198/api/v1'
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $null = Invoke-RestMethod 'http://127.0.0.1:5198/health/ready'
            $ready = $true
            break
        } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw 'La API temporal no inició.' }

    function Post([string] $path, $body) {
        $json = $body | ConvertTo-Json -Depth 10 -Compress
        return Invoke-RestMethod -Method Post -Uri "$base$path" -Headers $headers -ContentType 'application/json' -Body $json
    }
    $login = Invoke-RestMethod -Method Post -Uri "$base/auth/login" -ContentType 'application/json' -Body (
        @{ userName = $adminName; password = $adminPassword } | ConvertTo-Json -Compress)
    $headers = @{ Authorization = "Bearer $($login.token)" }
    $today = [DateTime]::Now.ToString('yyyy-MM-dd')
    $yesterday = [DateTime]::Now.AddDays(-1).ToString('yyyy-MM-dd')
    $supplier = Post '/suppliers' @{ name = 'Proveedor prueba movimientos' }
    $invoice = Post '/invoices' @{
        supplierId = $supplier.id; series = 'SM'; number = '1'; issueDate = $yesterday
        dueDate = $today; lines = @(@{ description = 'Prueba'; quantity = 1; unitPrice = 1000 })
    }
    $debit = Post '/adjustments' @{
        supplierId = $supplier.id; kind = 'DebitNote'; series = 'ND'; number = '1'
        date = $yesterday; description = 'Cargo de prueba'; amount = 100
        allocations = @(@{ invoiceId = $invoice.id; amount = 100 })
    }
    $credit = Post '/adjustments' @{
        supplierId = $supplier.id; kind = 'CreditNote'; series = 'NC'; number = '1'
        date = $yesterday; description = 'Descuento de prueba'; amount = 150
        allocations = @(@{ invoiceId = $invoice.id; amount = 150 })
    }
    $payment = Post '/payments' @{
        supplierId = $supplier.id; paymentMethodId = 3; date = $yesterday
        reference = 'CH-1'; amount = 400
        allocations = @(@{ invoiceId = $invoice.id; amount = 400 })
        check = @{ bankName = 'Banco de prueba'; checkNumber = '12345'; clearedOn = $null }
    }
    if ($payment.check.checkNumber -ne '12345') { throw 'No se guardó el detalle de cheque.' }
    $current = Invoke-RestMethod -Uri "$base/invoices/$($invoice.id)" -Headers $headers
    if ($current.balance -ne 550 -or $current.debit -ne 100 -or $current.credit -ne 150) {
        throw "Saldo de factura incorrecto: $($current.balance)"
    }
    $statement = Invoke-RestMethod -Uri "$base/reports/suppliers/$($supplier.id)/statement?from=$yesterday&to=$yesterday" -Headers $headers
    if ($statement.closingBalance -ne 550 -or $statement.movements.Count -ne 4) {
        throw "Estado de cuenta incorrecto: $($statement.closingBalance)"
    }
    $balances = Invoke-RestMethod -Uri "$base/reports/supplier-balances?asOf=$yesterday" -Headers $headers
    $row = $balances | Where-Object { $_.supplierId -eq $supplier.id }
    if ($row.outstanding -ne 550) { throw 'Balance por proveedor incorrecto.' }
    $null = Post "/payments/$($payment.id)/cancel" @{}
    $historical = Invoke-RestMethod -Uri "$base/reports/supplier-balances?asOf=$yesterday" -Headers $headers
    $historicalRow = $historical | Where-Object { $_.supplierId -eq $supplier.id }
    if ($historicalRow.outstanding -ne 550) { throw 'La anulación alteró el saldo histórico.' }
    $currentStatement = Invoke-RestMethod -Uri "$base/reports/suppliers/$($supplier.id)/statement?from=$yesterday&to=$today" -Headers $headers
    if ($currentStatement.closingBalance -ne 950 -or $currentStatement.movements.Count -ne 5) {
        throw 'La anulación no generó el contramovimiento esperado.'
    }
    $secondPayment = Post '/payments' @{
        supplierId = $supplier.id; paymentMethodId = 2; date = $today
        reference = 'TRX-2'; amount = 900
        allocations = @(@{ invoiceId = $invoice.id; amount = 900 })
    }
    try {
        $null = Post "/adjustments/$($debit.id)/cancel" @{}
        throw 'Se permitió anular un débito necesario para cubrir un pago.'
    } catch {
        if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 409) { throw }
    }
    $null = Post "/payments/$($secondPayment.id)/cancel" @{}
    $null = Post "/adjustments/$($debit.id)/cancel" @{}
    $afterDebitCancel = Invoke-RestMethod -Uri "$base/invoices/$($invoice.id)" -Headers $headers
    if ($afterDebitCancel.balance -ne 850) { throw 'La anulación de la nota de débito no ajustó el saldo.' }
    $historicalAgain = Invoke-RestMethod -Uri "$base/reports/supplier-balances?asOf=$yesterday" -Headers $headers
    $historicalRow = $historicalAgain | Where-Object { $_.supplierId -eq $supplier.id }
    if ($historicalRow.outstanding -ne 550) { throw 'La anulación de la nota alteró el saldo histórico.' }
    $edgeInvoice = Post '/invoices' @{
        supplierId = $supplier.id; series = 'SM'; number = '2'; issueDate = $yesterday
        dueDate = $today; lines = @(@{ description = 'Corte histórico'; quantity = 1; unitPrice = 100 })
    }
    $null = Post '/adjustments' @{
        supplierId = $supplier.id; kind = 'DebitNote'; series = 'ND'; number = '2'
        date = $today; description = 'Cargo posterior'; amount = 100
        allocations = @(@{ invoiceId = $edgeInvoice.id; amount = 100 })
    }
    try {
        $null = Post '/payments' @{
            supplierId = $supplier.id; paymentMethodId = 2; date = $yesterday
            reference = 'ATRASADO'; amount = 150
            allocations = @(@{ invoiceId = $edgeInvoice.id; amount = 150 })
        }
        throw 'Se permitió pagar ayer con un débito registrado para hoy.'
    } catch {
        if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 409) { throw }
    }
    Write-Output 'Prueba completa: factura, notas, cheque, saldos, anulaciones y protección contra saldo negativo correctos.'
} finally {
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
        $apiProcess.WaitForExit()
    }
    if ($created) {
        docker exec cuentas-por-pagar-postgres dropdb -U cpp_local --if-exists --force $database | Out-Null
    }
    Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
    Remove-Item Env:ASPNETCORE_URLS -ErrorAction SilentlyContinue
}
