import { writeFileSync } from 'node:fs';

const base = (process.env.API_BASE_URL || '/api/v1').replace(/\/+$/, '');
if (process.env.VERCEL && !process.env.API_BASE_URL) {
  throw new Error('Configura API_BASE_URL con la URL HTTPS de Render seguida de /api/v1.');
}
if (base !== '/api/v1' && !/^https:\/\/[^/]+\/api\/v1$/.test(base)) {
  throw new Error('API_BASE_URL debe ser /api/v1 o https://<host>/api/v1.');
}
writeFileSync(new URL('../src/app/api-config.generated.ts', import.meta.url),
  `// Generado por npm prestart/prebuild. No editar.\nexport const apiBaseUrl = ${JSON.stringify(base)};\n`);
console.log(`API base: ${base}`);
