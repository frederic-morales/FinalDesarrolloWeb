# Copia al portapapeles una cadena Npgsql a partir de DATABASE_NEON en .env.
# No muestra ni guarda la contraseña en archivos versionados.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repoRoot '.env'
if (-not (Test-Path -LiteralPath $envPath)) { throw 'No existe .env en la raíz del repositorio.' }

$line = Get-Content -LiteralPath $envPath |
    Where-Object { $_ -match '^\s*DATABASE_NEON\s*=' } |
    Select-Object -Last 1
if (-not $line) { throw 'No existe DATABASE_NEON en .env.' }

$raw = ($line -split '=', 2)[1].Trim().Trim('"').Trim([char]39)
$uri = [Uri]$raw
if ($uri.Scheme -notin @('postgres', 'postgresql') -or -not $uri.Host.EndsWith('.neon.tech')) {
    throw 'DATABASE_NEON debe ser una URI PostgreSQL de Neon.'
}
$credentials = $uri.UserInfo.Split(':', 2)
if ($credentials.Length -ne 2) { throw 'La URI no tiene usuario y contraseña.' }

$connection = New-Object System.Data.Common.DbConnectionStringBuilder
$connection['Host'] = $uri.Host
$connection['Port'] = if ($uri.Port -gt 0) { $uri.Port } else { 5432 }
$connection['Database'] = [Uri]::UnescapeDataString($uri.AbsolutePath.TrimStart('/'))
$connection['Username'] = [Uri]::UnescapeDataString($credentials[0])
$connection['Password'] = [Uri]::UnescapeDataString($credentials[1])
$connection['SSL Mode'] = 'Require'
if ($uri.Query -match '(?i)channel_binding=require') { $connection['Channel Binding'] = 'Require' }

Set-Clipboard -Value $connection.ConnectionString
Write-Output 'Conexión Npgsql de Neon copiada al portapapeles. Pégala en Render como ConnectionStrings__DefaultConnection.'
