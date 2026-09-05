import { Routes } from '@angular/router';
import { AppLayout } from './layout/components/app.layout';
import { inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ClientSessionService } from './core/services/client-session.service';
import { GameStateService } from './core/services/game-state.service';
import { AuthService } from './core/services/auth.service';
import { ApiService } from './core/services/api.service';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';

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

// Guard restricting a route to moderators/admins (realm roles from the Keycloak token)
export const moderatorGuard = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isModerator() ? true : router.createUrlTree(['/']);
};

// Guard to warn user before navigating away from an active game
export const confirmLeaveGameGuard = async () => {
  const gameState = inject(GameStateService);
  const session = inject(ClientSessionService);
  const transloco = inject(TranslocoService);
  const api = inject(ApiService);

  const phase = gameState.phase();
  const gameInProgress = gameState.gameId() && phase !== 'waiting' && phase !== 'matchEnd';

  if (gameInProgress && !session.isWatcher()) {
    // The server only records a forfeit if the player has already acted this match
    const confirmKey = gameState.hasActed() ? 'table.leaveConfirm' : 'table.leaveConfirmNoAction';
    if (!confirm(transloco.translate(confirmKey))) {
      return false;
    }

    // Confirmed explicit quit during a match — leave the room on the server
    // too, which abandons (forfeits) the game.
    const roomId = gameState.currentRoom()?.roomId;
    const clientId = session.clientId();
    if (roomId && clientId) {
      try {
        await firstValueFrom(api.leaveRoom(roomId, clientId));
      } catch (e) {
        console.warn('Failed to leave room via API', e);
      }
    }
  }

  // Always clean up session when leaving the table
  await gameState.leaveRoom();
  session.leaveRoom();
  return true;
};

export const routes: Routes = [
  {
    // The game table is a full-bleed HUD and deliberately sits outside the layout shell.
    path: 'table/:roomId',
    loadComponent: () =>
      import('./features/table/table.component').then((m) => m.TableComponent),
    canActivate: [hasClientIdGuard],
    canDeactivate: [confirmLeaveGameGuard],
  },
  {
    path: '',
    component: AppLayout,
    children: [
      {
        path: '',
        data: { breadcrumb: 'breadcrumb.home' },
        loadComponent: () =>
          import('./features/home/home.component').then((m) => m.HomeComponent),
      },
      {
        path: 'settings',
        data: { breadcrumb: 'breadcrumb.settings' },
        loadComponent: () =>
          import('./features/settings/settings.component').then((m) => m.SettingsComponent),
      },
      {
        path: 'achievements',
        data: { breadcrumb: 'breadcrumb.achievements' },
        loadComponent: () =>
          import('./features/achievements/achievements.component').then(
            (m) => m.AchievementsComponent,
          ),
      },
      {
        path: 'achievements/:playerId',
        data: { breadcrumb: 'breadcrumb.playerAchievements' },
        loadComponent: () =>
          import('./features/achievements/achievements.component').then(
            (m) => m.AchievementsComponent,
          ),
      },
      {
        path: 'highlights',
        data: { breadcrumb: 'breadcrumb.highlights' },
        loadComponent: () =>
          import('./features/highlights/highlights.component').then((m) => m.HighlightsComponent),
      },
      {
        path: 'highlights/:playerId',
        data: { breadcrumb: 'breadcrumb.playerHighlights' },
        loadComponent: () =>
          import('./features/highlights/highlights.component').then((m) => m.HighlightsComponent),
      },
      {
        path: 'leaderboard',
        data: { breadcrumb: 'breadcrumb.leaderboard' },
        loadComponent: () =>
          import('./features/leaderboard/leaderboard.component').then(
            (m) => m.LeaderboardComponent,
          ),
      },
      {
        path: 'feedback',
        data: { breadcrumb: 'breadcrumb.feedback' },
        loadComponent: () =>
          import('./features/feedback/feedback.component').then((m) => m.FeedbackComponent),
      },
      {
        path: 'admin',
        canActivate: [moderatorGuard],
        data: { breadcrumb: 'breadcrumb.admin' },
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./features/admin/admin.component').then((m) => m.AdminComponent),
          },
          {
            path: 'users',
            data: { breadcrumb: 'breadcrumb.adminUsers' },
            loadComponent: () =>
              import('./features/admin/users/admin-users.component').then(
                (m) => m.AdminUsersComponent,
              ),
          },
          {
            path: 'games',
            data: { breadcrumb: 'breadcrumb.adminGames' },
            loadComponent: () =>
              import('./features/admin/games/admin-games.component').then(
                (m) => m.AdminGamesComponent,
              ),
          },
        ],
      },
    ],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
