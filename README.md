# Cuentas por pagar

Proyecto del curso Desarrollo Web. Monorepositorio con **ASP.NET Core Web API (.NET 10)**, **SQL Server Express en Docker** y **Angular 21**.

La API, las migraciones code-first, el contenedor SQL Server y el frontend están implementados. Cada integrante puede trabajar con **su propia base local**. La publicación web y, si el equipo la necesita, una base compartida por Tailscale quedan como pasos de despliegue.

## Requisitos

- SDK .NET 10 y herramienta `dotnet-ef` versión 10.
- Docker Desktop con memoria suficiente para SQL Server Express.
- Node.js 20.19 o superior y npm para Angular 21.

## Iniciar la base local

Desde la raíz, copia `.env.example` a `.env` y sustituye la clave de ejemplo por una contraseña fuerte propia. Después:

```powershell
docker compose up -d
```

El contenedor vincula SQL Server a `127.0.0.1:1433` y usa un volumen persistente. En DBeaver selecciona SQL Server y configura host `127.0.0.1`, puerto `1433`, base `CuentasPorPagar`, usuario `sa` y la contraseña de tu `.env`. Para el certificado local, activa *Trust server certificate* si el controlador lo solicita.

## Configurar e iniciar la API

Desde `CuentasPorPagarAPI/CuentasPorPagarAPI`, guarda tus claves con User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=127.0.0.1,1433;Database=CuentasPorPagar;User Id=sa;Password=TU_CLAVE_LOCAL;Encrypt=True;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "CLAVE_ALEATORIA_DE_AL_MENOS_32_BYTES"
dotnet user-secrets set "SeedAdmin:UserName" "admin"
dotnet user-secrets set "SeedAdmin:Password" "CLAVE_ADMIN_DE_AL_MENOS_12_CARACTERES"
dotnet ef database update
dotnet run -- --seed-admin
dotnet run --launch-profile https
```

Las claves son ejemplos: cada integrante debe crear las suyas y no subir `.env` ni User Secrets al repositorio. `--seed-admin` se ejecuta solo la primera vez, antes de crear otros usuarios. Las migraciones se aplican mediante `dotnet ef database update`; la API no las aplica automáticamente al arrancar. Para Angular, usa el perfil **https** de Visual Studio o el comando anterior: la API escucha en `https://localhost:7292` y también en `http://localhost:5296`. El puerto HTTP redirige a HTTPS; el proxy Angular se conecta directamente al puerto HTTPS para conservar el encabezado de autenticación.

En Visual Studio, abre `CuentasPorPagarAPI/CuentasPorPagarAPI.slnx`, elige el perfil **https** junto al botón de ejecución y pulsa **F5**. Para comprobar que API y base están listas, abre `https://localhost:7292/health/ready`: debe responder `{"status":"ready"}`. Deja Visual Studio en ejecución mientras uses Angular.

## Iniciar Angular

Desde `frontend`:

```powershell
npm ci --legacy-peer-deps
npm start
```

Abre `http://localhost:4200`. El proxy de desarrollo envía `/api` a `https://localhost:7292`, así que inicia la API antes y reinicia `npm start` si cambiaste el proxy. Usa la cuenta inicial guardada en tus User Secrets para acceder; `dotnet user-secrets list` permite consultarla localmente. Cambia la contraseña inicial antes de compartir la instancia o publicar la aplicación.

El frontend incluye acceso, resumen, proveedores, facturas, pagos, reportes y CSV, métodos de pago y usuarios. Para compilarlo: `npm run build`.

## API y despliegue

Las rutas `/api/v1` cubren autenticación, usuarios, proveedores, métodos de pago, facturas, pagos y reportes. Todas las rutas de negocio requieren un token JWT. En desarrollo, OpenAPI está disponible en `/openapi/v1.json`; `/health/live` y `/health/ready` indican el estado de la API y la conexión SQL.

Para publicar la solución completa se necesita alojar Angular y una API HTTPS capaz de conectarse a SQL Server. Configura un proxy de `/api` a la API desde el mismo origen web, o ajusta URL de API y CORS para la infraestructura elegida. No publiques el puerto SQL directamente en Internet.

Consulta el [plan y análisis](docs/plan-proyecto.md) y el [contrato de la API](docs/api.md). El recorrido integrado se puede comprobar con `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-api.ps1` desde la raíz, con la API y SQL Server iniciados; ese script crea datos de prueba, anula sus documentos y desactiva el proveedor temporal.
