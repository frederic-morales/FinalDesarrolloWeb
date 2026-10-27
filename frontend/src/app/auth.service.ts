import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { ApiService } from './api.service';
import { LoginResponse, User } from './models';
import { apiBaseUrl } from './api-config.generated';

const tokenKey = 'cpp.token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly user = signal<User | null>(null);
  readonly isAdmin = computed(() => this.user()?.role === 'Administrador');
  readonly checking = signal(true);
  private restoreInFlight: Promise<void> | null = null;

  restore(): Promise<void> {
    if (!this.checking()) return Promise.resolve();
    if (this.restoreInFlight) return this.restoreInFlight;
    this.restoreInFlight = (async () => {
      if (sessionStorage.getItem(tokenKey)) {
        try { this.user.set(await this.api.get<User>('/auth/me')); }
        catch { this.logout(false); }
      }
    })().finally(() => {
      this.checking.set(false);
      this.restoreInFlight = null;
    });
    return this.restoreInFlight;
  }

  async login(userName: string, password: string): Promise<void> {
    const response = await this.api.post<LoginResponse>('/auth/login', { userName, password });
    sessionStorage.setItem(tokenKey, response.token);
    this.user.set(response.user);
    this.checking.set(false);
    const navigated = await this.router.navigateByUrl('/resumen');
    if (!navigated) {
      this.logout(false);
      throw new Error('No se pudo abrir el resumen. Intenta entrar de nuevo.');
    }
  }

  logout(navigate = true): void {
    sessionStorage.removeItem(tokenKey);
    this.user.set(null);
    if (navigate) void this.router.navigateByUrl('/ingresar');
  }

  token(): string | null { return sessionStorage.getItem(tokenKey); }
}

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.token();
  return next(token && request.url.startsWith(`${apiBaseUrl}/`)
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request);
};

export function isUnauthorized(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 401;
}
