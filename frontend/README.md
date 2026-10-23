# Frontend Angular

Panel del proyecto Cuentas por pagar. Angular 21 con componentes independientes, formularios y rutas protegidas. Las rutas de API usan `/api/v1`; el proxy local definido en `proxy.conf.json` las envía a `http://127.0.0.1:5296`.

```powershell
npm ci --legacy-peer-deps
npm start
```

Abre `http://127.0.0.1:4200`. También puedes ejecutar `npm run build` para generar `dist/`.

La sesión se conserva en `sessionStorage` hasta cerrar la pestaña o salir del sistema. La API valida el token y los permisos en cada solicitud. Para publicar, configura un proxy HTTPS de `/api` hacia la API o ajusta la URL base y CORS.
