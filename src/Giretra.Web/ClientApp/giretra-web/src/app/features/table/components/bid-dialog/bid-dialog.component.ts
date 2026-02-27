import { Component, input, output, computed, inject } from '@angular/core';
import { GameMode, PlayerPosition } from '../../../../api/generated/signalr-types.generated';
import { ValidAction, NegotiationAction } from '../../../../core/services/api.service';
import { BidButtonRowComponent } from '../hand-area/bid-button-row/bid-button-row.component';
import { GameModeBadgeComponent } from '../../../../shared/components/game-mode-badge/game-mode-badge.component';
import { GameModeIconComponent } from '../../../../shared/components/game-mode-icon/game-mode-icon.component';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-bid-dialog',
  standalone: true,
  imports: [BidButtonRowComponent, GameModeBadgeComponent, GameModeIconComponent, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
    <div class="backdrop"></div>
    <div class="dialog-container">
      <div class="dialog">
        <h2 class="dialog-title">{{ t('bidDialog.title') }}</h2>

        <!-- Current bid -->
        @if (currentBid(); as bid) {
          <div class="current-bid">
            <app-game-mode-badge [mode]="bid.mode" size="1.5rem" />
            <span class="bid-by">{{ t('negotiation.bidBy', { player: bid.player }) }}</span>
          </div>
        } @else {
          <p class="no-bid">{{ t('bidDialog.noBidYouOpen') }}</p>
        }

        <!-- Bid history (newest first) -->
        @if (reversedHistory().length > 0) {
          <div class="history-list">
            @for (action of reversedHistory(); track $index) {
              <div class="history-row" [class.latest]="$index === 0" [style.opacity]="$index === 0 ? 1 : Math.max(0.4, 1 - $index * 0.15)">
                <span class="player-avatar" [class.latest-avatar]="$index === 0">{{ getInitial(action.player) }}</span>
                <span class="player-name" [class.latest-name]="$index === 0">{{ action.player }}</span>
                <span class="action-badge" [class]="getActionBadgeClass(action)">
                  @if (action.actionType === 'Announce') {
                    <app-game-mode-icon [mode]="action.mode!" size="0.875rem" />
                    <span>{{ formatModeName(action.mode) }}</span>
                  } @else if (action.actionType === 'Double') {
                    <span class="multiplier-symbol">\u00d72</span>
                    <span>{{ t('negotiation.double') }}</span>
                  } @else if (action.actionType === 'Redouble') {
                    <span class="multiplier-symbol">\u00d74</span>
                    <span>{{ t('negotiation.redouble') }}</span>
                  } @else {
                    <span>{{ t('negotiation.accept') }}</span>
                  }
                </span>
              </div>
            }
          </div>
        }

        <!-- Bid buttons -->
        <app-bid-button-row
          [validActions]="validActions()"
          (actionSelected)="onAction($event)"
        />
      </div>
    </div>
    </ng-container>
  `,
  styles: [`
    :host {
      display: contents;
    }

    .backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.5);
      z-index: 50;
      pointer-events: none;
      animation: fadeIn 0.3s ease;
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    .dialog-container {
      position: fixed;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
      z-index: 110;
      pointer-events: none;
    }

    .dialog {
      pointer-events: auto;
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 1rem;
      padding: 1.5rem;
      text-align: center;
      min-width: 320px;
      max-width: 480px;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.75rem;
      animation: scaleIn 0.25s ease;
    }

    @keyframes scaleIn {
      from {
        opacity: 0;
        transform: scale(0.9);
      }
      to {
        opacity: 1;
        transform: scale(1);
      }
    }

    .dialog-title {
      font-size: 1.25rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      margin: 0;
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
      font-size: 0.875rem;
    }

    /* History list */
    .history-list {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
      width: 100%;
      max-height: 160px;
      overflow-y: auto;
    }

    .history-row {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.375rem 0.625rem;
      border-radius: 0.375rem;
      background: hsl(var(--muted) / 0.3);
      transition: opacity 0.15s ease;
    }

    .history-row.latest {
      background: hsl(var(--foreground) / 0.08);
      border: 1px solid hsl(var(--foreground) / 0.12);
      padding: 0.5rem 0.75rem;
    }

    .player-avatar {
      width: 1.5rem;
      height: 1.5rem;
      border-radius: 50%;
      background: hsl(var(--muted));
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 0.625rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      text-transform: uppercase;
      flex-shrink: 0;
    }

    .player-avatar.latest-avatar {
      background: hsl(var(--gold) / 0.2);
      color: hsl(var(--gold));
      box-shadow: 0 0 6px hsl(var(--gold) / 0.15);
    }

    .player-name {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      flex: 1;
      text-align: left;
    }

    .player-name.latest-name {
      color: hsl(var(--foreground));
      font-weight: 600;
    }

    .action-badge {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      padding: 0.125rem 0.5rem;
      border-radius: 9999px;
      font-size: 0.75rem;
      font-weight: 600;
      flex-shrink: 0;
    }

    .action-badge.announce-badge {
      background: hsl(var(--gold) / 0.15);
      color: hsl(var(--gold));
      border: 1px solid hsl(var(--gold) / 0.3);
    }

    .action-badge.accept-badge {
      background: hsl(var(--primary) / 0.15);
      color: hsl(var(--primary));
      border: 1px solid hsl(var(--primary) / 0.3);
    }

    .action-badge.double-badge {
      background: hsl(var(--destructive) / 0.15);
      color: hsl(var(--destructive));
      border: 1px solid hsl(var(--destructive) / 0.3);
    }

    .action-badge.redouble-badge {
      background: hsl(var(--destructive) / 0.25);
      color: hsl(0, 72%, 65%);
      border: 1px solid hsl(var(--destructive) / 0.5);
    }

    .multiplier-symbol {
      font-weight: 800;
      font-size: 0.8125rem;
    }
  `],
})
export class BidDialogComponent {
  readonly Math = Math;
  private readonly transloco = inject(TranslocoService);
  readonly validActions = input<ValidAction[]>([]);
  readonly negotiationHistory = input<NegotiationAction[]>([]);
  readonly activePlayer = input<PlayerPosition | null>(null);

  readonly actionSelected = output<{ actionType: string; mode?: string | null }>();

  readonly currentBid = computed(() => {
    const history = this.negotiationHistory();
    for (let i = history.length - 1; i >= 0; i--) {
      if (history[i].actionType === 'Announce' && history[i].mode) {
        return history[i];
      }
    }
    return null;
  });

  readonly reversedHistory = computed(() => [...this.negotiationHistory()].reverse());

  getInitial(player: PlayerPosition): string {
    return player.charAt(0).toUpperCase();
  }

  formatModeName(mode: GameMode | null): string {
    if (!mode) return '?';
    switch (mode) {
      case GameMode.ColourClubs: return this.transloco.translate('game.modes.clubs');
      case GameMode.ColourDiamonds: return this.transloco.translate('game.modes.diamonds');
      case GameMode.ColourHearts: return this.transloco.translate('game.modes.hearts');
      case GameMode.ColourSpades: return this.transloco.translate('game.modes.spades');
      case GameMode.NoTrumps: return this.transloco.translate('game.modes.noTrumps');
      case GameMode.AllTrumps: return this.transloco.translate('game.modes.allTrumps');
      default: return mode;
    }
  }

  getActionBadgeClass(action: NegotiationAction): string {
    switch (action.actionType) {
      case 'Announce': return 'announce-badge';
      case 'Accept': return 'accept-badge';
      case 'Double': return 'double-badge';
      case 'Redouble': return 'redouble-badge';
      default: return '';
    }
  }

  onAction(action: { actionType: string; mode?: string | null }): void {
    this.actionSelected.emit(action);
  }
}
