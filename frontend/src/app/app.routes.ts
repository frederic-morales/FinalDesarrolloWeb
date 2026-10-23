import { inject } from '@angular/core';
import { CanActivateFn, Routes, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { LoginComponent } from './login.component';
import { WorkspaceComponent } from './workspace.component';

const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.checking()) await auth.restore();
  return auth.user() ? true : router.createUrlTree(['/ingresar']);
};

export const routes: Routes = [
  { path: 'ingresar', component: LoginComponent },
  { path: ':section', component: WorkspaceComponent, canActivate: [authGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'resumen' },
  { path: '**', redirectTo: 'resumen' }
];
