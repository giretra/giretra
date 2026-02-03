import { Component, input, computed } from '@angular/core';
import { GameMode, PlayerPosition } from '../../../../../api/generated/signalr-types.generated';
import { NegotiationAction } from '../../../../../core/services/api.service';
import { GameModeBadgeComponent } from '../../../../../shared/components/game-mode-badge/game-mode-badge.component';

@Component({
  selector: 'app-negotiation-stage',
  standalone: true,
  imports: [GameModeBadgeComponent],
  template: `
    <div class="negotiation-stage">
      <!-- Current bid -->
      @if (currentBid(); as bid) {
        <div class="current-bid">
          <span class="bid-label">Current bid:</span>
          <app-game-mode-badge [mode]="bid.mode" size="1.5rem" />
          <span class="bid-by">by {{ bid.player }}</span>
        </div>
      } @else {
        <p class="no-bid">No bid yet</p>
      }

      <!-- Bid history trail -->
      @if (negotiationHistory().length > 0) {
        <div class="bid-history">
          @for (action of negotiationHistory(); track $index; let last = $last) {
            <span class="bid-item">
              <span class="bid-player">{{ action.player }}:</span>
              <span class="bid-action">{{ formatAction(action) }}</span>
            </span>
            @if (!last) {
              <span class="bid-arrow">\u2192</span>
            }
          }
        </div>
      }

      <!-- Who's bidding -->
      <p class="active-bidder">
        @if (activePlayer()) {
          {{ activePlayer() }} is bidding...
        }
      </p>
    </div>
  `,
  styles: [`
    .negotiation-stage {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      padding: 1rem;
      gap: 0.75rem;
    }

    .current-bid {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 1.125rem;
    }

    .bid-label {
      color: hsl(var(--muted-foreground));
    }

    .bid-by {
      font-size: 0.875rem;
      color: hsl(var(--muted-foreground));
    }

    .no-bid {
      color: hsl(var(--muted-foreground));
      font-style: italic;
      margin: 0;
    }

    .bid-history {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: center;
      gap: 0.375rem;
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      max-width: 300px;
    }

    .bid-item {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
    }

    .bid-player {
      font-weight: 500;
    }

    .bid-action {
      color: hsl(var(--foreground));
    }

    .bid-arrow {
      color: hsl(var(--muted-foreground));
    }

    .active-bidder {
      font-size: 0.875rem;
      color: hsl(var(--accent));
      margin: 0;
      animation: pulse 1.5s ease-in-out infinite;
    }

    @keyframes pulse {
      0%, 100% {
        opacity: 1;
      }
      50% {
        opacity: 0.6;
      }
    }
  `],
})
export class NegotiationStageComponent {
  readonly negotiationHistory = input<NegotiationAction[]>([]);
  readonly activePlayer = input<PlayerPosition | null>(null);
  readonly myPosition = input<PlayerPosition | null>(null);
  readonly gameMode = input<GameMode | null>(null);

  readonly currentBid = computed(() => {
    const history = this.negotiationHistory();
    // Find the last Announce action
    for (let i = history.length - 1; i >= 0; i--) {
      if (history[i].actionType === 'Announce' && history[i].mode) {
        return history[i];
      }
    }
    return null;
  });

  formatAction(action: NegotiationAction): string {
    switch (action.actionType) {
      case 'Accept':
        return 'Accept';
      case 'Double':
        return '\u00d72';
      case 'Redouble':
        return '\u00d74';
      case 'Announce':
        return this.formatMode(action.mode);
      default:
        return action.actionType;
    }
  }

  private formatMode(mode: GameMode | null): string {
    if (!mode) return '?';
    switch (mode) {
      case GameMode.ColourClubs:
        return '\u2663';
      case GameMode.ColourDiamonds:
        return '\u2666';
      case GameMode.ColourHearts:
        return '\u2665';
      case GameMode.ColourSpades:
        return '\u2660';
      case GameMode.SansAs:
        return 'Sans As';
      case GameMode.ToutAs:
        return 'Tout As';
      default:
        return mode;
    }
  }
}
