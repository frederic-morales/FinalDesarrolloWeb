# Frontend Angular

Panel del proyecto Cuentas por pagar. Angular 21 con componentes independientes, formularios y rutas protegidas. Las rutas de API usan `/api/v1`; el proxy local definido en `proxy.conf.json` las envía a `https://localhost:7292`. Inicia la API con el perfil **https** de Visual Studio o `dotnet run --launch-profile https`.

```powershell
npm ci --legacy-peer-deps
npm start
```

Abre `http://127.0.0.1:4200`. También puedes ejecutar `npm run build` para generar `dist/`.

La sesión se conserva en `sessionStorage` hasta cerrar la pestaña o salir del sistema. La API valida el token y los permisos en cada solicitud. `npm start` genera la URL local `/api/v1`; para Vercel, configura `API_BASE_URL=https://TU_API.onrender.com/api/v1` y el build incorporará esa URL. Autoriza el dominio exacto de Vercel en CORS de Render. Consulta [la guía de despliegue](../docs/despliegue.md).
