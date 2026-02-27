import { Component, input, output, OnInit, DoCheck } from '@angular/core';
import { CardResponse, GameMode, PlayerPosition } from '../../../../api/generated/signalr-types.generated';
import { GamePhase } from '../../../../core/services/game-state.service';
import { Card } from '../../../../core/models';
import { CardFanComponent } from '../../../../shared/components/card-fan/card-fan.component';
import { WatcherBarComponent } from '../watcher-bar/watcher-bar.component';
import { TranslocoDirective } from '@jsverse/transloco';

@Component({
  selector: 'app-hand-area',
  standalone: true,
  imports: [CardFanComponent, WatcherBarComponent, TranslocoDirective],
  template: `
    <div class="hand-area" *transloco="let t">
      @if (isWatcher()) {
        <!-- Watcher view -->
        <app-watcher-bar
          [playerCardCounts]="playerCardCounts()"
        />
      } @else {
        @switch (phase()) {
          @case ('waiting') {
            <p class="waiting-message">{{ t('handArea.notStarted') }}</p>
          }
          @case ('cut') {
            @if (!isMyTurn()) {
              <p class="waiting-message">{{ t('handArea.waitingForCut') }}</p>
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
          }
          @case ('playing') {
            <app-card-fan
              [cards]="hand()"
              [validCards]="isMyTurn() && !disabled() ? validCards() : []"
              [gameMode]="gameMode()"
              [interactive]="isMyTurn() && !disabled()"
              (cardSelected)="onCardSelected($event)"
            />
          }
          @case ('dealSummary') {
            <p class="waiting-message">{{ t('handArea.dealComplete') }}</p>
          }
        }

        <!-- Turn indicator -->
        @if (!isMyTurn() && phase() === 'playing' && activePlayer()) {
          <div class="turn-pill">
            <span class="turn-text">{{ t('handArea.playerThinking', { player: activePlayer() }) }}</span>
            <span class="thinking-dots">
              <span class="dot"></span>
              <span class="dot"></span>
              <span class="dot"></span>
            </span>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .hand-area {
      background: hsl(var(--card));
      border-top: 2px solid transparent;
      border-image: linear-gradient(
        90deg,
        transparent,
        hsl(var(--border)),
        transparent
      ) 1;
      padding: 0.75rem 1rem;
      min-height: 170px;
      max-height: 240px;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      overflow: hidden;
      position: relative;
      box-shadow: 0 -4px 16px rgba(0, 0, 0, 0.15);
    }

    .waiting-message {
      color: hsl(var(--muted-foreground));
      font-size: 0.875rem;
      margin: 0;
    }

    .turn-pill {
      position: absolute;
      bottom: 0.5rem;
      left: 50%;
      transform: translateX(-50%);
      display: flex;
      align-items: center;
      gap: 0.375rem;
      padding: 0.25rem 0.75rem;
      background: hsl(var(--muted) / 0.8);
      border-radius: 9999px;
      border: 1px solid hsl(var(--border));
    }

    .turn-text {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
    }

    .thinking-dots {
      display: flex;
      gap: 2px;
    }

    .dot {
      width: 4px;
      height: 4px;
      border-radius: 50%;
      background: hsl(var(--muted-foreground));
      animation: dotBounce 1.4s ease-in-out infinite;
    }

    .dot:nth-child(2) {
      animation-delay: 0.2s;
    }

    .dot:nth-child(3) {
      animation-delay: 0.4s;
    }

    @keyframes dotBounce {
      0%, 80%, 100% {
        opacity: 0.3;
      }
      40% {
        opacity: 1;
      }
    }
  `],
})
export class HandAreaComponent implements OnInit, DoCheck {
  readonly phase = input.required<GamePhase>();
  readonly isMyTurn = input<boolean>(false);
  readonly isWatcher = input<boolean>(false);
  readonly hand = input<CardResponse[]>([]);
  readonly validCards = input<CardResponse[]>([]);
  readonly gameMode = input<GameMode | null>(null);
  readonly activePlayer = input<PlayerPosition | null>(null);
  readonly playerCardCounts = input<Record<PlayerPosition, number> | null>(null);
  readonly disabled = input<boolean>(false);

  readonly playCard = output<{ rank: string; suit: string }>();
  onCardSelected(card: Card): void {
    console.log('[HandArea] onCardSelected:', {
      card: `${card.rank} of ${card.suit}`,
      phase: this.phase(),
      isMyTurn: this.isMyTurn(),
    });
    this.playCard.emit({ rank: card.rank, suit: card.suit });
  }

  private _lastLoggedState: string = '';

  ngOnInit(): void {
    console.log('[HandArea] Component initialized');
  }

  ngDoCheck(): void {
    const state = {
      phase: this.phase(),
      isMyTurn: this.isMyTurn(),
      isWatcher: this.isWatcher(),
      handCount: this.hand().length,
      validCardsCount: this.validCards().length,
    };
    const stateKey = JSON.stringify(state);
    if (stateKey !== this._lastLoggedState) {
      console.log('[HandArea] State changed:', state);
      this._lastLoggedState = stateKey;
    }
  }
}
