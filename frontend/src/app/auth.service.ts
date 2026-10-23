import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { ApiService } from './api.service';
import { LoginResponse, User } from './models';

const tokenKey = 'cpp.token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  readonly user = signal<User | null>(null);
  readonly isAdmin = computed(() => this.user()?.role === 'Administrador');
  readonly checking = signal(true);

  async restore(): Promise<void> {
    if (sessionStorage.getItem(tokenKey)) {
      try { this.user.set(await this.api.get<User>('/auth/me')); }
      catch { this.logout(false); }
    }
    this.checking.set(false);
  }

  async login(userName: string, password: string): Promise<void> {
    const response = await this.api.post<LoginResponse>('/auth/login', { userName, password });
    sessionStorage.setItem(tokenKey, response.token);
    this.user.set(response.user);
    await this.router.navigateByUrl('/resumen');
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
  return next(token && request.url.startsWith('/api/')
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request);
};

export function isUnauthorized(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 401;
}
