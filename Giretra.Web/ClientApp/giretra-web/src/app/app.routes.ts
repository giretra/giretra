import { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { ClientSessionService } from './core/services/client-session.service';

// Guard to ensure user has a clientId before accessing table
export const hasClientIdGuard = () => {
  const session = inject(ClientSessionService);
  const router = inject(Router);

  if (session.clientId()) {
    return true;
  }

  // Redirect to home if no client session
  return router.createUrlTree(['/']);
};

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/home/home.component').then((m) => m.HomeComponent),
  },
  {
    path: 'table/:roomId',
    loadComponent: () =>
      import('./features/table/table.component').then((m) => m.TableComponent),
    canActivate: [hasClientIdGuard],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
