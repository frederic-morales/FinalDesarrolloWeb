import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Page } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1';

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
    const items: T[] = [];
    for (let page = 1; ; page++) {
      const result = await this.get<Page<T>>(path, { ...query, page, pageSize: 100 });
      items.push(...result.items);
      if (items.length >= result.total || result.items.length === 0) return items;
    }
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
