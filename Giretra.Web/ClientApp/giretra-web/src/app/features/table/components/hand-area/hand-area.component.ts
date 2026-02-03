import { Component, input, output, computed, OnInit, DoCheck } from '@angular/core';
import { CardResponse, GameMode, PlayerPosition, PendingActionType } from '../../../../api/generated/signalr-types.generated';
import { ValidAction } from '../../../../core/services/api.service';
import { GamePhase } from '../../../../core/services/game-state.service';
import { cardEquals, Card } from '../../../../core/models';
import { CardFanComponent } from '../../../../shared/components/card-fan/card-fan.component';
import { BidButtonRowComponent } from './bid-button-row/bid-button-row.component';
import { WatcherBarComponent } from '../watcher-bar/watcher-bar.component';

@Component({
  selector: 'app-hand-area',
  standalone: true,
  imports: [CardFanComponent, BidButtonRowComponent, WatcherBarComponent],
  template: `
    <div class="hand-area">
      @if (isWatcher()) {
        <!-- Watcher view -->
        <app-watcher-bar
          [playerCardCounts]="playerCardCounts()"
        />
      } @else {
        @switch (phase()) {
          @case ('waiting') {
            <p class="waiting-message">Game hasn't started yet</p>
          }
          @case ('cut') {
            @if (!isMyTurn()) {
              <p class="waiting-message">Waiting for cut...</p>
            }
          }
          @case ('negotiation') {
            <!-- Always show hand during negotiation -->
            <app-card-fan
              [cards]="hand()"
              [validCards]="[]"
              [gameMode]="gameMode()"
              [interactive]="false"
            />
            @if (isMyTurn() && pendingActionType() === 'Negotiate') {
              <app-bid-button-row
                [validActions]="validActions()"
                (actionSelected)="onNegotiationAction($event)"
              />
            }
          }
          @case ('playing') {
            <app-card-fan
              [cards]="hand()"
              [validCards]="isMyTurn() ? validCards() : []"
              [gameMode]="gameMode()"
              [interactive]="isMyTurn()"
              (cardSelected)="onCardSelected($event)"
            />
          }
          @case ('dealSummary') {
            <p class="waiting-message">Deal complete</p>
          }
        }

        <!-- Turn indicator -->
        @if (!isMyTurn() && phase() === 'playing' && activePlayer()) {
          <p class="turn-indicator">{{ activePlayer() }} is thinking...</p>
        }
      }
    </div>
  `,
  styles: [`
    .hand-area {
      background: hsl(var(--card));
      border-top: 1px solid hsl(var(--border));
      padding: 0.75rem 1rem;
      min-height: 120px;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .waiting-message {
      color: hsl(var(--muted-foreground));
      font-size: 0.875rem;
      margin: 0;
    }

    .turn-indicator {
      position: absolute;
      bottom: 0.5rem;
      left: 50%;
      transform: translateX(-50%);
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      margin: 0;
    }
  `],
})
export class HandAreaComponent implements OnInit, DoCheck {
  readonly phase = input.required<GamePhase>();
  readonly isMyTurn = input<boolean>(false);
  readonly isWatcher = input<boolean>(false);
  readonly hand = input<CardResponse[]>([]);
  readonly validCards = input<CardResponse[]>([]);
  readonly validActions = input<ValidAction[]>([]);
  readonly pendingActionType = input<PendingActionType | null>(null);
  readonly gameMode = input<GameMode | null>(null);
  readonly activePlayer = input<PlayerPosition | null>(null);
  readonly playerCardCounts = input<Record<PlayerPosition, number> | null>(null);

  readonly playCard = output<{ rank: string; suit: string }>();
  readonly submitNegotiation = output<{ actionType: string; mode?: string | null }>();

  onCardSelected(card: Card): void {
    console.log('[HandArea] onCardSelected:', {
      card: `${card.rank} of ${card.suit}`,
      phase: this.phase(),
      isMyTurn: this.isMyTurn(),
      pendingActionType: this.pendingActionType(),
    });
    this.playCard.emit({ rank: card.rank, suit: card.suit });
  }

  onNegotiationAction(action: { actionType: string; mode?: string | null }): void {
    console.log('[HandArea] onNegotiationAction:', action);
    this.submitNegotiation.emit(action);
  }

  private _lastLoggedState: string = '';

  // Debug helper - log state on template evaluation
  ngOnInit(): void {
    console.log('[HandArea] Component initialized');
  }

  ngDoCheck(): void {
    // Log state changes for debugging (only on change)
    const state = {
      phase: this.phase(),
      isMyTurn: this.isMyTurn(),
      isWatcher: this.isWatcher(),
      pendingActionType: this.pendingActionType(),
      handCount: this.hand().length,
      validCardsCount: this.validCards().length,
      validActionsCount: this.validActions().length,
    };
    const stateKey = JSON.stringify(state);
    if (stateKey !== this._lastLoggedState) {
      console.log('[HandArea] State changed:', state);
      this._lastLoggedState = stateKey;
    }
  }
}
