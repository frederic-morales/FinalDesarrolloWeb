# Contrato inicial de la API

Base local: `http://127.0.0.1:5296` (también puede ejecutarse el perfil HTTPS). Los cuerpos son JSON y las fechas usan `YYYY-MM-DD`. Salvo el login y `/health/live`, enviar `Authorization: Bearer <token>`. La respuesta de error usa `ProblemDetails`; los listados paginados devuelven `items`, `total`, `page` y `pageSize`.

## Acceso

`POST /api/v1/auth/login`

```json
{ "userName": "admin", "password": "<contraseña local>" }
```

La respuesta incluye `token`, `expiresAtUtc` y `user`. Los roles son `Administrador` y `Operador`. El administrador crea usuarios con `POST /api/v1/users`, cambia contraseñas con `PUT /api/v1/users/{id}/password` y activa/desactiva cuentas con `PUT /api/v1/users/{id}/status`.

## Proveedor

`POST /api/v1/suppliers`

```json
{
  "name": "Papelería Ejemplo",
  "nit": "1234567-8",
  "contactName": "Ana López",
  "email": "contacto@example.com",
  "phone": "5555-0000",
  "address": "Cuilapa, Santa Rosa"
}
```

`GET /api/v1/suppliers?search=&active=true&page=1&pageSize=20` permite consultar. `PUT /api/v1/suppliers/{id}` modifica datos. `PUT /api/v1/suppliers/{id}/status` recibe `{ "isActive": false }` y requiere Administrador.

## Factura

`POST /api/v1/invoices`

```json
{
  "supplierId": 1,
  "series": "A",
  "number": "1001",
  "issueDate": "2026-10-21",
  "dueDate": "2026-11-21",
  "lines": [
    { "description": "Material de oficina", "quantity": 2, "unitPrice": 125.00 }
  ]
}
```

La API calcula `subtotal` y `total`. `GET /api/v1/invoices?supplierId=1&pending=true&overdue=false&page=1&pageSize=20` devuelve `paid`, `balance` e `isOverdue`. `GET /api/v1/invoices/{id}` incluye conceptos. `PUT /api/v1/invoices/{id}` permite editar mientras no tenga pagos activos. `POST /api/v1/invoices/{id}/cancel` anula una factura sin pagos activos.

## Pago

`POST /api/v1/payments`

```json
{
  "supplierId": 1,
  "paymentMethodId": 2,
  "date": "2026-10-21",
  "reference": "TRX-001",
  "amount": 100.00,
  "allocations": [
    { "invoiceId": 1, "amount": 100.00 }
  ]
}
```

La suma de aplicaciones debe coincidir con `amount`; cada factura debe pertenecer al mismo proveedor y mantener saldo suficiente. `GET /api/v1/payments?supplierId=1&from=2026-10-01&to=2026-10-31` lista pagos. `POST /api/v1/payments/{id}/cancel` anula un pago y requiere Administrador; sus aplicaciones dejan de afectar saldos, pero permanecen como historial.

## Reportes

- `GET /api/v1/reports/dashboard` devuelve proveedores activos, facturas pendientes/vencidas e importes.
- `GET /api/v1/reports/payables?asOf=2026-10-21&overdueOnly=false` devuelve facturas con saldo a esa fecha.
- `GET /api/v1/reports/suppliers/1/statement?from=2026-10-01&to=2026-10-31` devuelve saldo inicial, movimientos y saldo final.
- `GET /api/v1/reports/suppliers/1/statement.csv?from=2026-10-01&to=2026-10-31` descarga el mismo movimiento en CSV.

Los métodos de pago están en `GET /api/v1/payment-methods`; crear y modificar esos parámetros requiere Administrador.
