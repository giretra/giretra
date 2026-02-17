import { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ClientSessionService } from './core/services/client-session.service';

// Guard to ensure user has a clientId or invite token before accessing table
export const hasClientIdGuard = () => {
  const session = inject(ClientSessionService);
  const router = inject(Router);

  if (session.clientId()) {
    return true;
  }

  // Allow access if invite query param is present (user will auto-join)
  const currentNav = router.getCurrentNavigation();
  const inviteToken = currentNav?.extractedUrl?.queryParamMap?.get('invite');
  if (inviteToken) {
    return true;
  }

  // Redirect to home if no client session and no invite
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
    path: 'settings',
    loadComponent: () =>
      import('./features/settings/settings.component').then((m) => m.SettingsComponent),
  },
  {
    path: 'leaderboard',
    loadComponent: () =>
      import('./features/leaderboard/leaderboard.component').then((m) => m.LeaderboardComponent),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
