# Publicar Cuentas por pagar

Arquitectura: **PostgreSQL local en Docker para cada integrante**, **PostgreSQL en Neon Free para el sitio publicado**, **API .NET 10 en Render Free** y **Angular en Vercel Hobby**. Las migraciones EF Core son las mismas para PostgreSQL local y Neon. No compartas la base de producción para pruebas: si el grupo necesita datos comunes de desarrollo, crea otra base o rama de Neon.

## 1. Preparar GitHub y la base local

Trabaja desde la rama principal del repositorio `frederic-morales/FinalDesarrolloWeb`. El código incluye [`compose.yaml`](../compose.yaml), la migración inicial de PostgreSQL, [`Dockerfile.api`](../Dockerfile.api), [`render.yaml`](../render.yaml) y [`frontend/vercel.json`](../frontend/vercel.json). Los archivos `.env` y User Secrets están excluidos de Git.

Para configurar otra computadora, desde la raíz del repositorio:

```powershell
Copy-Item .env.example .env
# Edita .env y reemplaza POSTGRES_PASSWORD por tu propia contraseña.
docker compose up -d postgres
```

Dentro de `CuentasPorPagarAPI/CuentasPorPagarAPI`, registra User Secrets con la misma contraseña de `.env`:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5432;Database=CuentasPorPagar;Username=cpp_local;Password=TU_CLAVE"
dotnet user-secrets set "Jwt:Key" "CLAVE_ALEATORIA_DE_AL_MENOS_32_BYTES"
dotnet user-secrets set "SeedAdmin:UserName" "admin"
dotnet user-secrets set "SeedAdmin:Password" "CLAVE_ADMIN_DE_AL_MENOS_12_CARACTERES"
dotnet ef database update
dotnet run -- --seed-admin
dotnet run --launch-profile https
```

`--seed-admin` se ejecuta una sola vez por base vacía. Para DBeaver local: controlador PostgreSQL, host `127.0.0.1`, puerto `5432`, base `CuentasPorPagar`, usuario `cpp_local`, contraseña de `.env`. El antiguo volumen de SQL Server permanece intacto pero ya no forma parte del Compose actual.

## 2. Crear PostgreSQL en Neon

1. En [Neon](https://console.neon.tech/) crea un proyecto **Free** en una región cercana a Render. Selecciona PostgreSQL y anota host, nombre de base, usuario y contraseña desde **Connect**. Guarda estos datos en un gestor privado; no los subas a Git.
2. Construye una cadena para Npgsql con formato `Host=HOST_NEON;Database=BASE;Username=USUARIO;Password=CLAVE;SSL Mode=Require`. La cadena `postgresql://...` que muestra Neon es una URI y no se debe pegar sin conversión en `ConnectionStrings__DefaultConnection`. Usa la conexión **directa** para aplicar migraciones.
3. En tu equipo, con el SDK .NET y `dotnet-ef`, configura temporalmente `ConnectionStrings__DefaultConnection` con esa cadena en la sesión de terminal y ejecuta `dotnet ef database update` desde `CuentasPorPagarAPI/CuentasPorPagarAPI`. La variable de entorno tiene prioridad sobre el User Secret local.
4. Con `ASPNETCORE_ENVIRONMENT=Development` y los User Secrets del administrador ya configurados, ejecuta `dotnet run -- --seed-admin` en esa misma terminal. Esto crea el primer administrador en Neon. Limpia después las variables temporales con `Remove-Item Env:ConnectionStrings__DefaultConnection` y `Remove-Item Env:ASPNETCORE_ENVIRONMENT`.
5. Comprueba en DBeaver con el controlador PostgreSQL y SSL activado que aparecen las tablas y `__EFMigrationsHistory`. Si otros integrantes necesitan acceso, concédeles acceso individual desde Neon según las opciones del proyecto; evita publicar la contraseña en el chat o en GitHub.

La contraseña inicial del administrador de Neon será la configurada en `SeedAdmin:Password` de la terminal usada para el paso 4. Cambia esa contraseña desde la aplicación tras el primer acceso. Cuando agregues un modelo, genera una nueva migración local, pruébala y aplícala a Neon antes de desplegar el código que depende de ella. El contenedor de Render no aplica migraciones automáticamente.

## 3. Desplegar la API en Render Free

1. En [Render](https://dashboard.render.com/) crea un **Blueprint** desde el repositorio público `frederic-morales/FinalDesarrolloWeb`; Render leerá `render.yaml`. El Blueprint define un servicio web Docker con plan `free`, construye `Dockerfile.api` y comprueba `/health/live`. Elige explícitamente el plan gratuito en la pantalla antes de crear el servicio.
2. Cuando Render solicite `ConnectionStrings__DefaultConnection`, desde la raíz del repositorio ejecuta `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\copy-neon-connection.ps1` y pega el contenido del portapapeles en ese campo. El script convierte la URI `DATABASE_NEON` de `.env` a formato Npgsql sin mostrarla en pantalla. `Jwt__Key` se genera automáticamente por Render. Conserva el valor fuera del repositorio; cambiarlo invalidará los tokens existentes.
3. Espera el despliegue y visita `https://NOMBRE.onrender.com/health/live` y `/health/ready`. La segunda ruta debe responder `{"status":"ready"}`. Guarda la URL HTTPS exacta.
4. Más adelante, después de conocer el dominio de Vercel, añade en **Environment** la variable `Cors__AllowedOrigins__0` con `https://TU_PROYECTO.vercel.app`, sin barra final, y vuelve a desplegar. Para un dominio propio u otra URL de vista previa, agrega `Cors__AllowedOrigins__1`, etc. CORS exige el origen exacto.

Render termina HTTPS antes de entregar HTTP al contenedor; la API escucha `0.0.0.0:10000`. El plan Free duerme el servicio tras un periodo de inactividad, así que la primera solicitud puede tardar aproximadamente un minuto. No guardes datos en el sistema de archivos del contenedor.

## 4. Desplegar Angular en Vercel Hobby

1. En [Vercel](https://vercel.com/new) importa **el mismo repositorio**. Selecciona tu cuenta personal y el plan **Hobby**. Configura **Root Directory** como `frontend`.
2. Usa Angular como Framework Preset. El comando de instalación puede ser `npm ci --legacy-peer-deps`; el de construcción es `npm run build`. [`frontend/vercel.json`](../frontend/vercel.json) fija la salida `dist/cuentas-por-pagar-web/browser` y permite recargar rutas de Angular.
3. Antes de desplegar, crea la variable de entorno `API_BASE_URL` con el valor `https://NOMBRE.onrender.com/api/v1`, sin barra final. Configúrala para Production; añádela también a Preview si vas a usar vistas previas. El build de Vercel falla de forma explícita si falta esta variable.
4. Despliega y copia el dominio de producción `https://TU_PROYECTO.vercel.app`. Ponlo en `Cors__AllowedOrigins__0` en Render como indica el paso anterior.
5. Abre el sitio, inicia sesión y comprueba en las herramientas del navegador que `POST /api/v1/auth/login` y `GET /api/v1/auth/me` responden desde Render. Prueba proveedores, facturas, pagos y reportes.

En local, `npm start` sigue usando `/api/v1` y el proxy de Angular hacia `https://localhost:7292`. El archivo `api-config.generated.ts` se crea automáticamente al iniciar o compilar y está excluido de Git. La URL de la API es pública; las contraseñas y claves JWT nunca deben introducirse en Vercel.

## Costos y límites

Selecciona siempre los planes **Free/Hobby** y revisa el panel de uso y facturación de cada cuenta antes de activar opciones de pago. Neon Free ofrece almacenamiento y cómputo limitados. Render Free puede suspender o dormir el servicio y sus condiciones de ancho de banda pueden cambiar; no uses Render Postgres Free como base final porque caduca tras 30 días. Vercel Hobby está destinado a proyectos personales. Si alguna plataforma solicita una tarjeta o habilita cargos por exceso, detén la creación y revisa esa pantalla antes de continuar.

Fuentes oficiales: [Neon Free](https://neon.com/blog/neon-free-plan-1-gb-per-project), [Render Free](https://render.com/docs/free), [Render Docker](https://render.com/docs/docker), [Render variables](https://render.com/docs/configure-environment-variables), [Vercel Hobby](https://vercel.com/docs/plans/hobby), [Vercel configuración](https://vercel.com/docs/project-configuration/vercel-json).
