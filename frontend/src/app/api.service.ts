import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Page } from './models';
import { apiBaseUrl } from './api-config.generated';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = apiBaseUrl;

  get<T>(path: string, query?: Record<string, string | number | boolean | null | undefined>): Promise<T> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query ?? {})) {
      if (value !== null && value !== undefined && value !== '') params = params.set(key, String(value));
    }
    return firstValueFrom(this.http.get<T>(this.base + path, { params }));
  }

  post<T>(path: string, body: unknown): Promise<T> {
    return firstValueFrom(this.http.post<T>(this.base + path, body));
  }

  put<T>(path: string, body: unknown): Promise<T> {
    return firstValueFrom(this.http.put<T>(this.base + path, body));
  }

  async all<T>(path: string, query?: Record<string, string | number | boolean | null | undefined>): Promise<T[]> {
    const pageSize = 100;
    const first = await this.get<Page<T>>(path, { ...query, page: 1, pageSize });
    const pages = Math.ceil(first.total / pageSize);
    if (pages <= 1) return first.items;
    const rest = await Promise.all(Array.from({ length: pages - 1 }, (_, i) =>
      this.get<Page<T>>(path, { ...query, page: i + 2, pageSize })));
    return first.items.concat(...rest.map(r => r.items));
  }

  blob(path: string, query?: Record<string, string | number>): Promise<Blob> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query ?? {})) params = params.set(key, String(value));
    return firstValueFrom(this.http.get(this.base + path, { params, responseType: 'blob' }));
  }
}

export function apiError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'No se pudo conectar con la API. Verifica que esté iniciada.';
    if (error.status === 401) return 'La sesión expiró o las credenciales son incorrectas.';
    if (typeof error.error === 'string' && error.error.trim()) return error.error;
    if (error.error?.detail) return error.error.detail;
    if (error.error?.title) return error.error.title;
    if (error.error?.errors) return Object.values(error.error.errors).flat().join(' ');
    return `La solicitud no se pudo completar (HTTP ${error.status}).`;
  }
  return error instanceof Error ? error.message : 'Ocurrió un error inesperado.';
}

// Despierta la API (Render Free duerme tras inactividad) mientras el usuario escribe sus credenciales.
export function wakeApi(): void {
  try { void fetch(apiBaseUrl.replace(/\/api\/v1\/?$/, '') + '/health/live', { mode: 'no-cors', cache: 'no-store' }); } catch { /* sin efecto */ }
}
