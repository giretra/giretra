import { Component, inject, OnInit, OnDestroy, effect, signal } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { GameStateService, GamePhase } from '../../core/services/game-state.service';
import { ClientSessionService } from '../../core/services/client-session.service';
import { ApiService, PlayerProfileResponse } from '../../core/services/api.service';
import { GameHubService } from '../../api/game-hub.service';
import { GameMode, PendingActionType, PlayerPosition, SeatAccessMode } from '../../api/generated/signalr-types.generated';
import { getTeam } from '../../core/utils/position-utils';
import { ScoreBarComponent } from './components/score-bar/score-bar.component';
import { TableSurfaceComponent } from './components/table-surface/table-surface.component';
import { HandAreaComponent } from './components/hand-area/hand-area.component';
import { MatchEndOverlayComponent } from './components/center-stage/match-end-overlay/match-end-overlay.component';
import { BidDialogComponent } from './components/bid-dialog/bid-dialog.component';
import { GameModePopupComponent } from './components/game-mode-popup/game-mode-popup.component';
import { PlayerProfilePopupComponent } from '../../shared/components/player-profile-popup/player-profile-popup.component';
import { environment } from '../../../environments/environment';
import { TranslocoDirective } from '@jsverse/transloco';
import { TranslocoService } from '@jsverse/transloco';
import { HotToastService } from '@ngxpert/hot-toast';

@Component({
  selector: 'app-table',
  standalone: true,
  imports: [
    ScoreBarComponent,
    TableSurfaceComponent,
    HandAreaComponent,
    MatchEndOverlayComponent,
    BidDialogComponent,
    GameModePopupComponent,
    PlayerProfilePopupComponent,
    TranslocoDirective,
  ],
  template: `
    <ng-container *transloco="let t">
    <div class="table-container"
         [class.bid-dialog-open]="gameState.phase() === 'negotiation' && gameState.isMyTurn() && gameState.pendingActionType() === 'Negotiate'"
         [class.deal-summary-open]="gameState.dealSummary()">

      <!-- Connection Status Banner -->
      @if (hub.connectionStatus() === 'reconnecting') {
        <div class="connection-banner reconnecting">
          <span class="connection-spinner"></span>
          {{ t('table.reconnecting') }}
        </div>
      } @else if (hub.connectionStatus() === 'disconnected' && gameState.gameId()) {
        <div class="connection-banner disconnected">
          {{ t('table.connectionLost') }}
          <button class="retry-btn" (click)="onRetryConnection()">{{ t('common.retry') }}</button>
        </div>
      }

      <!-- Zone A: Score Bar -->
      <app-score-bar
        [room]="gameState.currentRoom()"
        [team1MatchPoints]="gameState.team1MatchPoints()"
        [team2MatchPoints]="gameState.team2MatchPoints()"
        [team1CardPoints]="gameState.team1CardPoints()"
        [team2CardPoints]="gameState.team2CardPoints()"
        [dealNumber]="gameState.dealNumber()"
        [gameMode]="gameState.gameMode()"
        [multiplier]="gameState.multiplier()"
        [myTeam]="gameState.myTeam()"
        [isMyTurn]="gameState.isMyTurn()"
        [turnTimeoutAt]="gameState.turnTimeoutAt()"
        (leaveTable)="onLeaveTable()"
      />

      <!-- Zone B: Table Surface (players + center stage) -->
      <app-table-surface
        [room]="gameState.currentRoom()"
        [phase]="gameState.phase()"
        [myPosition]="gameState.myPosition()"
        [activePlayer]="gameState.activePlayer()"
        [currentTrick]="gameState.currentTrick()"
        [completedTrickToShow]="gameState.completedTrickToShow()"
        [showingCompletedTrick]="gameState.showingCompletedTrick()"
        [gameMode]="gameState.gameMode()"
        [dealer]="gameState.dealer()"
        [negotiationHistory]="gameState.negotiationHistory()"
        [isCreator]="gameState.isCreator()"
        [isWatcher]="gameState.isWatcher()"
        [dealSummary]="gameState.dealSummary()"
        [matchWinner]="gameState.matchWinner()"
        [team1MatchPoints]="gameState.team1MatchPoints()"
        [team2MatchPoints]="gameState.team2MatchPoints()"
        [completedDeals]="gameState.dealNumber() - 1"
        [playerCardCounts]="gameState.playerCardCounts()"
        [team1Tricks]="gameState.team1TricksWon()"
        [team2Tricks]="gameState.team2TricksWon()"
        [myTeam]="gameState.myTeam()"
        [tricksWonByPosition]="gameState.tricksWonByPosition()"
        [idleDeadline]="gameState.idleDeadline()"
        (startGame)="onStartGame()"
        (submitCut)="onSubmitCut()"
        (hideDealSummary)="onHideDealSummary()"
        (dismissCompletedTrick)="onDismissCompletedTrick()"
        (setSeatMode)="onSetSeatMode($event)"
        (generateInvite)="onGenerateInvite($event)"
        (kickPlayer)="onKickPlayer($event)"
        (seatClicked)="onSeatClicked($event)"
      />

      <!-- Zone C: Hand / Action Area -->
      <app-hand-area
        [phase]="gameState.phase()"
        [isMyTurn]="gameState.isMyTurn()"
        [isWatcher]="gameState.isWatcher()"
        [hand]="gameState.hand()"
        [validCards]="gameState.validCards()"
        [gameMode]="gameState.gameMode()"
        [activePlayer]="gameState.activePlayer()"
        [playerCardCounts]="gameState.playerCardCounts()"
        [disabled]="gameState.isSubmittingAction()"
        (playCard)="onPlayCard($event)"
      />

      <!-- Bid Dialog Overlay -->
      @if (gameState.phase() === 'negotiation' && gameState.isMyTurn() && gameState.pendingActionType() === 'Negotiate') {
        <app-bid-dialog
          [validActions]="gameState.validActions()"
          [negotiationHistory]="gameState.negotiationHistory()"
          [activePlayer]="gameState.activePlayer()"
          (actionSelected)="onSubmitNegotiation($event)"
        />
      }

      <!-- Game Mode Popup (shown briefly after negotiation ends) -->
      @if (gameModePopup(); as popup) {
        <app-game-mode-popup
          [gameMode]="popup.mode"
          [multiplier]="popup.multiplier"
          (dismissed)="dismissGameModePopup()"
        />
      }

      <!-- Match End Overlay -->
      @if (gameState.phase() === 'matchEnd') {
        <app-match-end-overlay
          [winner]="gameState.matchWinner()"
          [myTeam]="gameState.myTeam()"
          [team1Points]="gameState.team1MatchPoints()"
          [team2Points]="gameState.team2MatchPoints()"
          [totalDeals]="gameState.dealNumber() - 1"
          [isCreator]="gameState.isCreator()"
          [eloChange]="gameState.myEloChange()"
          [isRanked]="gameState.isRanked()"
          [idleDeadline]="gameState.idleDeadline()"
          (playAgain)="onPlayAgain()"
          (leaveTable)="onLeaveTable()"
        />
      }

      <!-- Player Profile Popup -->
      @if (profilePopupData()) {
        <app-player-profile-popup
          [profile]="profilePopupData()!"
          [teamClass]="profilePopupTeam()"
          (closed)="closeProfilePopup()"
        />
      }
    </div>
    </ng-container>
  `,
  styles: [`
    :host {
      display: block;
      height: 100%;
    }

    .table-container {
      display: flex;
      flex-direction: column;
      height: 100%;
      min-height: 100vh;
      min-height: 100dvh;
      background: hsl(var(--background));
      overflow: hidden;
    }

    app-score-bar {
      flex-shrink: 0;
    }

    app-table-surface {
      flex: 1;
      min-height: 0;
      overflow: hidden;
    }

    app-hand-area {
      flex-shrink: 0;
    }

    .deal-summary-open app-table-surface {
      overflow: visible;
      position: relative;
      z-index: 10;
    }

    .deal-summary-open app-hand-area {
      position: relative;
      z-index: 1;
    }

    .bid-dialog-open app-table-surface {
      position: relative;
      z-index: 52;
    }

    .bid-dialog-open app-hand-area {
      position: relative;
      z-index: 60;
    }

    .connection-banner {
      flex-shrink: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      padding: 0.375rem 1rem;
      font-size: 0.75rem;
      font-weight: 500;
      z-index: 100;
    }

    .connection-banner.reconnecting {
      background: hsl(45 90% 55% / 0.15);
      color: hsl(45 90% 65%);
      border-bottom: 1px solid hsl(45 90% 55% / 0.3);
    }

    .connection-banner.disconnected {
      background: hsl(0 72% 51% / 0.15);
      color: hsl(0 72% 65%);
      border-bottom: 1px solid hsl(0 72% 51% / 0.3);
    }

    .connection-spinner {
      width: 12px;
      height: 12px;
      border: 2px solid hsl(45 90% 55% / 0.3);
      border-top-color: hsl(45 90% 65%);
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    .retry-btn {
      padding: 0.125rem 0.5rem;
      font-size: 0.75rem;
      background: hsl(0 72% 51% / 0.2);
      color: hsl(0 72% 65%);
      border: 1px solid hsl(0 72% 51% / 0.4);
      border-radius: 4px;
      cursor: pointer;
      transition: background 0.15s ease;
    }

    .retry-btn:hover {
      background: hsl(0 72% 51% / 0.35);
    }
  `],
})
export class TableComponent implements OnInit, OnDestroy {
  readonly gameState = inject(GameStateService);
  readonly hub = inject(GameHubService);
  private readonly session = inject(ClientSessionService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(HotToastService);

  readonly profilePopupData = signal<PlayerProfileResponse | null>(null);
  readonly profilePopupTeam = signal<'team1' | 'team2'>('team1');

  readonly gameModePopup = signal<{
    mode: GameMode;
    multiplier: 'Normal' | 'Doubled' | 'Redoubled';
  } | null>(null);
  private gameModePopupTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private previousPhase: GamePhase | null = null;

  private readonly beforeUnloadHandler = (e: BeforeUnloadEvent) => {
    const phase = this.gameState.phase();
    const gameInProgress = this.gameState.gameId() && phase !== 'waiting' && phase !== 'matchEnd';
    if (gameInProgress) {
      e.preventDefault();
      // SignalR disconnect handles the grace period — no beacon needed.
      // sendBeacon can't include JWT auth headers, and the disconnect handler
      // already keeps the player's seat for rejoin.
    }
  };

  constructor() {
    // Watch for kick — navigate home
    effect(() => {
      if (this.gameState.wasKicked()) {
        this.gameState.leaveRoom();
        this.session.leaveRoom();
        this.router.navigate(['/']);
      }
    });

    // Watch for idle close — navigate home
    effect(() => {
      if (this.gameState.roomIdleClosed()) {
        this.gameState.leaveRoom();
        this.session.leaveRoom();
        this.router.navigate(['/']);
      }
    });

    // Close popup when bid dialog opens
    effect(() => {
      if (this.gameState.isMyTurn()) {
        this.profilePopupData.set(null);
      }
    });

    // Show game mode popup when negotiation ends
    effect(() => {
      const phase = this.gameState.phase();
      const prevPhase = this.previousPhase;
      this.previousPhase = phase;

      if (prevPhase === 'negotiation' && phase === 'playing') {
        const mode = this.gameState.gameMode();
        if (mode) {
          this.showGameModePopup(mode, this.gameState.multiplier());
        }
      }
    });
  }

  private showGameModePopup(
    mode: GameMode,
    multiplier: 'Normal' | 'Doubled' | 'Redoubled'
  ): void {
    if (this.gameModePopupTimeoutId) {
      clearTimeout(this.gameModePopupTimeoutId);
    }
    this.gameModePopup.set({ mode, multiplier });
    this.gameModePopupTimeoutId = setTimeout(() => {
      this.gameModePopup.set(null);
      this.gameModePopupTimeoutId = null;
    }, 4000);
  }

  dismissGameModePopup(): void {
    if (this.gameModePopupTimeoutId) {
      clearTimeout(this.gameModePopupTimeoutId);
      this.gameModePopupTimeoutId = null;
    }
    this.gameModePopup.set(null);
  }

  ngOnInit(): void {
    window.addEventListener('beforeunload', this.beforeUnloadHandler);

    const roomId = this.route.snapshot.paramMap.get('roomId');
    const inviteToken = this.route.snapshot.queryParamMap.get('invite');
    const quickstart = this.route.snapshot.queryParamMap.get('quickstart') === 'true';

    // Quick game: room already loaded from home, auto-start
    if (quickstart && this.gameState.currentRoom()) {
      this.onStartGame();
      // Remove query param to avoid re-trigger on refresh
      this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
    }

    if (roomId && !this.gameState.currentRoom()) {
      if (inviteToken && !this.session.clientId()) {
        // Invite flow: join via invite token
        this.api.joinRoom(roomId, undefined, inviteToken).subscribe({
          next: async (response) => {
            if (response.position) {
              this.session.joinRoom(roomId, response.clientId, response.position);
            }
            await this.gameState.enterRoom(response.room, false);
          },
          error: () => {
            this.router.navigate(['/']);
          },
        });
      } else if (!this.session.clientId()) {
        // Rejoin flow: authenticated user without a clientId (new tab / device)
        this.api.rejoinRoom(roomId).subscribe({
          next: async (response) => {
            if (response.position) {
              this.session.joinRoom(roomId, response.clientId, response.position);
            }
            await this.gameState.enterRoom(response.room, response.room.isOwner);
          },
          error: () => {
            this.router.navigate(['/']);
          },
        });
      } else {
        // Normal flow: fetch room and initialize
        this.api.getRoom(roomId).subscribe({
          next: async (room) => {
            const isCreator = room.isOwner;
            await this.gameState.enterRoom(room, isCreator);
          },
          error: () => {
            this.router.navigate(['/']);
          },
        });
      }
    }
  }

  ngOnDestroy(): void {
    window.removeEventListener('beforeunload', this.beforeUnloadHandler);
    if (this.gameModePopupTimeoutId) {
      clearTimeout(this.gameModePopupTimeoutId);
    }
  }

  onStartGame(): void {
    const roomId = this.gameState.currentRoom()?.roomId;
    const clientId = this.session.clientId();
    const isCreator = this.gameState.isCreator();

    console.log('[Table] Starting game', {
      roomId,
      clientId,
      isCreator,
      playerName: this.session.playerName(),
      sessionSnapshot: this.session.getSession(),
    });

    if (roomId && clientId) {
      this.api.startGame(roomId, clientId).subscribe({
        next: async (response) => {
          console.log('[Table] Game started', response);
          this.gameState.setGameId(response.gameId);
          // Immediately refresh state after game starts (don't wait for SignalR)
          await this.gameState.refreshState();
        },
        error: () => {
          // Toast shown by ApiService
        },
      });
    } else {
      console.warn('[Table] Cannot start game - missing roomId or clientId', { roomId, clientId });
    }
  }

  onSubmitCut(): void {
    if (this.gameState.isSubmittingAction()) return;

    const gameId = this.gameState.gameId();
    const clientId = this.session.clientId();

    if (gameId && clientId) {
      this.gameState.beginSubmit();
      // Random cut position between 6 and 26
      const position = Math.floor(Math.random() * 21) + 6;
      this.api.submitCut(gameId, clientId, position, true).subscribe({
        next: () => this.gameState.endSubmit(),
        error: (err) => {
          console.error('Failed to submit cut', err);
          this.gameState.endSubmit();
        },
      });
    }
  }

  onPlayCard(card: { rank: string; suit: string }): void {
    if (this.gameState.isSubmittingAction()) return;

    const gameId = this.gameState.gameId();
    const clientId = this.session.clientId();

    console.log('[Table] Playing card', { card, gameId, clientId });
    if (gameId && clientId) {
      this.gameState.beginSubmit();
      this.api.playCard(gameId, clientId, card.rank as any, card.suit as any).subscribe({
        next: () => {
          console.log('[Table] Card played successfully');
          this.gameState.endSubmit();
        },
        error: (err) => {
          console.error('Failed to play card', err);
          this.gameState.endSubmit();
          // Refresh state to get true server state
          this.gameState.refreshState();
        },
      });
    } else {
      console.warn('[Table] Cannot play card - missing gameId or clientId');
    }
  }

  onSubmitNegotiation(action: { actionType: string; mode?: string | null }): void {
    if (this.gameState.isSubmittingAction()) return;

    const gameId = this.gameState.gameId();
    const clientId = this.session.clientId();

    console.log('[Table] Submitting negotiation', { action, gameId, clientId });
    if (gameId && clientId) {
      this.gameState.beginSubmit();
      this.api
        .submitNegotiation(
          gameId,
          clientId,
          action.actionType as any,
          action.mode as any
        )
        .subscribe({
          next: () => {
            console.log('[Table] Negotiation submitted successfully');
            this.gameState.endSubmit();
          },
          error: (err) => {
            console.error('Failed to submit negotiation', err);
            this.gameState.endSubmit();
            this.gameState.refreshState();
          },
        });
    } else {
      console.warn('[Table] Cannot submit negotiation - missing gameId or clientId');
    }
  }

  async onRetryConnection(): Promise<void> {
    try {
      await this.hub.connect(environment.hubUrl);
      const roomId = this.gameState.currentRoom()?.roomId;
      const clientId = this.session.clientId();
      if (roomId && clientId) {
        await this.hub.joinRoom(roomId, clientId);
      }
      await this.gameState.refreshState();
    } catch (e) {
      console.error('Failed to retry connection', e);
    }
  }

  onHideDealSummary(): void {
    const gameId = this.gameState.gameId();
    const clientId = this.session.clientId();
    const pendingAction = this.gameState.pendingActionType();

    // If waiting for ContinueDeal confirmation, call the API
    // The DealStarted SignalR event will hide the summary
    if (pendingAction === PendingActionType.ContinueDeal && gameId && clientId) {
      console.log('[Table] Submitting continue deal confirmation');
      this.api.submitContinueDeal(gameId, clientId).subscribe({
        next: () => {
          console.log('[Table] Continue deal submitted successfully');
          // Don't hide summary here - wait for DealStarted event
        },
        error: (err) => {
          console.error('Failed to submit continue deal', err);
          // On error, still hide the summary to not block the UI
          this.gameState.hideDealSummary();
        },
      });
    } else {
      // No ContinueDeal action pending (e.g., watcher or AI game)
      // Just hide the summary immediately
      this.gameState.hideDealSummary();
    }
  }

  onDismissCompletedTrick(): void {
    this.gameState.dismissCompletedTrick();
  }

  async onPlayAgain(): Promise<void> {
    const gameId = this.gameState.gameId();
    const clientId = this.session.clientId();
    const pendingAction = this.gameState.pendingActionType();

    // If waiting for ContinueMatch confirmation, call the API first
    if (pendingAction === PendingActionType.ContinueMatch && gameId && clientId) {
      console.log('[Table] Submitting continue match confirmation');
      this.api.submitContinueMatch(gameId, clientId).subscribe({
        next: () => {
          console.log('[Table] Continue match submitted successfully');
          // After confirming, start a new game
          this.onStartGame();
        },
        error: (err) => {
          console.error('Failed to submit continue match', err);
          // On error, still try to start new game
          this.onStartGame();
        },
      });
    } else {
      // No ContinueMatch action pending, just start new game
      this.onStartGame();
    }
  }

  onSetSeatMode(event: { position: PlayerPosition; accessMode: SeatAccessMode }): void {
    const roomId = this.gameState.currentRoom()?.roomId;
    if (roomId) {
      this.api.setSeatMode(roomId, event.position, event.accessMode).subscribe({
        error: (err) => console.error('Failed to set seat mode', err),
      });
    }
  }

  onGenerateInvite(position: PlayerPosition): void {
    const roomId = this.gameState.currentRoom()?.roomId;
    if (!roomId) return;

    this.api.generateInvite(roomId, position).subscribe({
      next: (response) => {
        const inviteUrl = `${window.location.origin}/table/${roomId}?invite=${response.token}`;
        this.shareOrCopy(inviteUrl);
      },
      error: (err) => console.error('Failed to generate invite', err),
    });
  }

  private async shareOrCopy(url: string): Promise<void> {
    if (navigator.share) {
      try {
        await navigator.share({
          title: this.transloco.translate('waiting.shareTitle'),
          text: this.transloco.translate('waiting.shareText'),
          url,
        });
      } catch (err: unknown) {
        if (err instanceof DOMException && err.name === 'AbortError') return;
        await this.copyToClipboard(url);
      }
    } else {
      await this.copyToClipboard(url);
    }
  }

  private async copyToClipboard(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      this.toast.success(this.transloco.translate('waiting.linkCopied'));
    } catch {
      this.toast.error(this.transloco.translate('waiting.linkCopyFailed'));
    }
  }

  onKickPlayer(position: PlayerPosition): void {
    const roomId = this.gameState.currentRoom()?.roomId;
    if (roomId) {
      this.api.kickPlayer(roomId, position).subscribe({
        error: (err) => console.error('Failed to kick player', err),
      });
    }
  }

  onSeatClicked(position: PlayerPosition): void {
    const roomId = this.gameState.currentRoom()?.roomId;
    if (!roomId) return;

    this.api.getPlayerProfile(roomId, position).subscribe({
      next: (profile) => {
        const team = getTeam(position);
        this.profilePopupTeam.set(team === 'Team1' ? 'team1' : 'team2');
        this.profilePopupData.set(profile);
      },
    });
  }

  closeProfilePopup(): void {
    this.profilePopupData.set(null);
  }

  async onLeaveTable(): Promise<void> {
    // The canDeactivate guard handles the confirmation dialog and session cleanup.
    // For the explicit "Leave Table" button, also call the leave API before navigating.
    const phase = this.gameState.phase();
    const gameInProgress = this.gameState.gameId() && phase !== 'waiting' && phase !== 'matchEnd';

    if (gameInProgress && !confirm(this.transloco.translate('table.leaveConfirm'))) {
      return;
    }

    const roomId = this.gameState.currentRoom()?.roomId;
    const clientId = this.session.clientId();

    if (roomId && clientId) {
      try {
        await this.api.leaveRoom(roomId, clientId).toPromise();
      } catch (e) {
        console.warn('Failed to leave room via API', e);
      }
    }

    await this.gameState.leaveRoom();
    this.session.leaveRoom();
    this.router.navigate(['/']);
  }
}
