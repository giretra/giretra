import { Component, input, computed } from '@angular/core';
import { GameMode, PlayerPosition } from '../../../../../api/generated/signalr-types.generated';
import { NegotiationAction } from '../../../../../core/services/api.service';
import { GameModeBadgeComponent } from '../../../../../shared/components/game-mode-badge/game-mode-badge.component';
import { GameModeIconComponent } from '../../../../../shared/components/game-mode-icon/game-mode-icon.component';
import { TranslocoDirective } from '@jsverse/transloco';

@Component({
  selector: 'app-negotiation-stage',
  standalone: true,
  imports: [GameModeBadgeComponent, GameModeIconComponent, TranslocoDirective],
  template: `
    <div class="negotiation-stage" *transloco="let t">
      <!-- Current bid -->
      @if (currentBid(); as bid) {
        <div class="current-bid">
          <app-game-mode-badge [mode]="bid.mode" size="1.75rem" />
          <span class="bid-by">{{ t('negotiation.bidBy', { player: bid.player }) }}</span>
        </div>
      } @else {
        <p class="no-bid">{{ t('negotiation.noBidYet') }}</p>
      }

      <!-- Compact bid timeline -->
      @if (negotiationHistory().length > 0) {
        <div class="bid-timeline">
          @for (action of negotiationHistory(); track $index; let last = $last) {
            <div class="timeline-item" [class.active]="last">
              <span class="timeline-initial">{{ getInitial(action.player) }}</span>
              @if (action.actionType === 'Announce' && action.mode) {
                <app-game-mode-icon [mode]="action.mode" size="0.75rem" />
              } @else {
                <span class="timeline-action">{{ formatAction(action) }}</span>
              }
            </div>
            @if (!last) {
              <span class="timeline-arrow">\u203a</span>
            }
          }
        </div>
      }

      <!-- Active bidder -->
      @if (activePlayer()) {
        <p class="active-bidder">
          <span class="bidder-dot"></span>
          {{ t('negotiation.playerBidding', { player: activePlayer() }) }}
        </p>
      }
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
      padding: 0.5rem 1rem;
      background: hsl(var(--gold) / 0.08);
      border: 1px solid hsl(var(--gold) / 0.2);
      border-radius: 0.5rem;
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

    .bid-timeline {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: center;
      gap: 0.25rem;
      font-size: 0.75rem;
      max-width: 320px;
    }

    .timeline-item {
      display: inline-flex;
      align-items: center;
      gap: 0.2rem;
      padding: 0.125rem 0.375rem;
      border-radius: 0.25rem;
      background: hsl(var(--muted) / 0.5);
    }

    .timeline-item.active {
      background: hsl(var(--primary) / 0.15);
      border: 1px solid hsl(var(--primary) / 0.3);
    }

    .timeline-initial {
      font-weight: 700;
      color: hsl(var(--foreground));
      font-size: 0.625rem;
      text-transform: uppercase;
    }

    .timeline-action {
      color: hsl(var(--foreground));
      font-weight: 500;
    }

    .timeline-arrow {
      color: hsl(var(--muted-foreground));
      font-size: 1rem;
    }

    .active-bidder {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      font-size: 0.875rem;
      color: hsl(var(--gold));
      margin: 0;
    }

    .bidder-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: hsl(var(--gold));
      animation: pulse 1.5s ease-in-out infinite;
    }

    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.4; }
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
    for (let i = history.length - 1; i >= 0; i--) {
      if (history[i].actionType === 'Announce' && history[i].mode) {
        return history[i];
      }
    }
    return null;
  });

  getInitial(player: PlayerPosition): string {
    return player.charAt(0).toUpperCase();
  }

  formatAction(action: NegotiationAction): string {
    switch (action.actionType) {
      case 'Accept':
        return 'Accept';
      case 'Double':
        return '\u00d72';
      case 'Redouble':
        return '\u00d74';
      default:
        return action.actionType;
    }
  }
}
