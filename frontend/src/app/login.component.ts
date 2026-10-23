import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { apiError } from './api.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <main class="login-wrap">
      <section class="login-intro">
        <div class="brand-mark">CP<span>·</span></div>
        <p class="eyebrow">CONTROL FINANCIERO</p>
        <h1>Las cuentas claras, a tiempo.</h1>
        <p>Proveedores, facturas y pagos en un solo lugar. Tu equipo sabe cuánto debe y qué vence pronto.</p>
        <div class="login-ornament"><span>01 / 03</span><span>PROVEEDORES · FACTURAS · PAGOS</span></div>
      </section>
      <section class="login-panel">
        <div class="login-card">
          <p class="eyebrow">BIENVENIDO</p>
          <h2>Inicia sesión</h2>
          <p class="muted">Ingresa con la cuenta que te asignó el administrador.</p>
          @if (error) { <div class="alert alert-error" role="alert">{{ error }}</div> }
          <form (ngSubmit)="signIn()">
            <label>Usuario <input name="userName" [(ngModel)]="userName" required autocomplete="username" placeholder="Tu usuario" /></label>
            <label>Contraseña <input name="password" [(ngModel)]="password" type="password" required autocomplete="current-password" placeholder="Tu contraseña" /></label>
            <button class="button button-primary full" type="submit" [disabled]="busy || !userName || !password">{{ busy ? 'Ingresando...' : 'Entrar al sistema' }} <span>→</span></button>
          </form>
          <p class="login-foot">Cuentas por pagar · Proyecto Desarrollo Web</p>
        </div>
      </section>
    </main>
  `
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  userName = '';
  password = '';
  error = '';
  busy = false;

  constructor() {
    if (this.auth.user()) void this.router.navigateByUrl('/resumen');
  }

  async signIn(): Promise<void> {
    this.error = '';
    this.busy = true;
    try { await this.auth.login(this.userName.trim(), this.password); }
    catch (error) { this.error = apiError(error); }
    finally { this.busy = false; }
  }
}
