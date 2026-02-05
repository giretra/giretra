import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { GameStateService } from '../../core/services/game-state.service';
import { ClientSessionService } from '../../core/services/client-session.service';
import { ApiService } from '../../core/services/api.service';
import { ScoreBarComponent } from './components/score-bar/score-bar.component';
import { TableSurfaceComponent } from './components/table-surface/table-surface.component';
import { HandAreaComponent } from './components/hand-area/hand-area.component';
import { MatchEndOverlayComponent } from './components/center-stage/match-end-overlay/match-end-overlay.component';

@Component({
  selector: 'app-table',
  standalone: true,
  imports: [
    ScoreBarComponent,
    TableSurfaceComponent,
    HandAreaComponent,
    MatchEndOverlayComponent,
  ],
  template: `
    <div class="table-container">
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
        (startGame)="onStartGame()"
        (submitCut)="onSubmitCut()"
        (hideDealSummary)="onHideDealSummary()"
        (dismissCompletedTrick)="onDismissCompletedTrick()"
      />

      <!-- Zone C: Hand / Action Area -->
      <app-hand-area
        [phase]="gameState.phase()"
        [isMyTurn]="gameState.isMyTurn()"
        [isWatcher]="gameState.isWatcher()"
        [hand]="gameState.hand()"
        [validCards]="gameState.validCards()"
        [validActions]="gameState.validActions()"
        [pendingActionType]="gameState.pendingActionType()"
        [gameMode]="gameState.gameMode()"
        [activePlayer]="gameState.activePlayer()"
        [playerCardCounts]="gameState.playerCardCounts()"
        (playCard)="onPlayCard($event)"
        (submitNegotiation)="onSubmitNegotiation($event)"
      />

      <!-- Match End Overlay -->
      @if (gameState.phase() === 'matchEnd') {
        <app-match-end-overlay
          [winner]="gameState.matchWinner()"
          [myTeam]="gameState.myTeam()"
          [team1Points]="gameState.team1MatchPoints()"
          [team2Points]="gameState.team2MatchPoints()"
          [totalDeals]="gameState.dealNumber() - 1"
          [isCreator]="gameState.isCreator()"
          (playAgain)="onPlayAgain()"
          (leaveTable)="onLeaveTable()"
        />
      }
    </div>
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
  `],
})
export class TableComponent implements OnInit, OnDestroy {
  readonly gameState = inject(GameStateService);
  private readonly session = inject(ClientSessionService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  ngOnInit(): void {
    // Ensure we have room data loaded
    const roomId = this.route.snapshot.paramMap.get('roomId');
    if (roomId && !this.gameState.currentRoom()) {
      // Fetch room and initialize
      this.api.getRoom(roomId).subscribe({
        next: async (room) => {
          const isCreator = room.playerSlots[0]?.playerName === this.session.playerName();
          await this.gameState.enterRoom(room, isCreator);
        },
        error: () => {
          this.router.navigate(['/']);
        },
      });
    }
  }

  ngOnDestroy(): void {
    // Clean up will be handled when explicitly leaving
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
        error: (err) => {
          console.error('Failed to start game', err.message || err);
          // Show alert with error message for debugging
          alert(`Failed to start game: ${err.message || 'Unknown error'}`);
        },
      });
    } else {
      console.warn('[Table] Cannot start game - missing roomId or clientId', { roomId, clientId });
    }
  }

  onSubmitCut(): void {
    const gameId = this.gameState.gameId();
    const clientId = this.session.clientId();

    if (gameId && clientId) {
      // Random cut position between 6 and 26
      const position = Math.floor(Math.random() * 21) + 6;
      this.api.submitCut(gameId, clientId, position, true).subscribe({
        error: (err) => {
          console.error('Failed to submit cut', err);
        },
      });
    }
  }

  onPlayCard(card: { rank: string; suit: string }): void {
    const gameId = this.gameState.gameId();
    const clientId = this.session.clientId();

    console.log('[Table] Playing card', { card, gameId, clientId });
    if (gameId && clientId) {
      this.api.playCard(gameId, clientId, card.rank as any, card.suit as any).subscribe({
        next: () => {
          console.log('[Table] Card played successfully');
        },
        error: (err) => {
          console.error('Failed to play card', err);
          // Refresh state to revert optimistic update
          this.gameState.refreshState();
        },
      });
    } else {
      console.warn('[Table] Cannot play card - missing gameId or clientId');
    }
  }

  onSubmitNegotiation(action: { actionType: string; mode?: string | null }): void {
    const gameId = this.gameState.gameId();
    const clientId = this.session.clientId();

    console.log('[Table] Submitting negotiation', { action, gameId, clientId });
    if (gameId && clientId) {
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
          },
          error: (err) => {
            console.error('Failed to submit negotiation', err);
            this.gameState.refreshState();
          },
        });
    } else {
      console.warn('[Table] Cannot submit negotiation - missing gameId or clientId');
    }
  }

  onHideDealSummary(): void {
    this.gameState.hideDealSummary();
  }

  onDismissCompletedTrick(): void {
    this.gameState.dismissCompletedTrick();
  }

  async onPlayAgain(): Promise<void> {
    // For now, just restart the game
    // In a real app, might need to handle re-lobby logic
    this.onStartGame();
  }

  async onLeaveTable(): Promise<void> {
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
