import { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ClientSessionService } from './core/services/client-session.service';
import { GameStateService } from './core/services/game-state.service';
import { AuthService } from './core/services/auth.service';
import { TranslocoService } from '@jsverse/transloco';

// Guard to ensure user has a clientId, invite token, or is authenticated before accessing table
export const hasClientIdGuard = () => {
  const session = inject(ClientSessionService);
  const auth = inject(AuthService);
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

  // Allow authenticated users — TableComponent will handle rejoin
  if (auth.user()) {
    return true;
  }

  // Redirect to home if no client session, no invite, and not authenticated
  return router.createUrlTree(['/']);
};

// Guard to warn user before navigating away from an active game
export const confirmLeaveGameGuard = async () => {
  const gameState = inject(GameStateService);
  const session = inject(ClientSessionService);
  const transloco = inject(TranslocoService);

  const phase = gameState.phase();
  const gameInProgress = gameState.gameId() && phase !== 'waiting' && phase !== 'matchEnd';

  if (gameInProgress) {
    if (!confirm(transloco.translate('table.leaveConfirm'))) {
      return false;
    }
  }

  // Always clean up session when leaving the table
  await gameState.leaveRoom();
  session.leaveRoom();
  return true;
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
    canDeactivate: [confirmLeaveGameGuard],
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
