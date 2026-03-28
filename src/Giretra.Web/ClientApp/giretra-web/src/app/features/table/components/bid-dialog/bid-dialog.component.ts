import { Component, input, output, computed, inject, signal } from '@angular/core';
import { GameMode, PlayerPosition } from '../../../../api/generated/signalr-types.generated';
import { ValidAction, NegotiationAction } from '../../../../core/services/api.service';
import { BidButtonRowComponent } from '../hand-area/bid-button-row/bid-button-row.component';
import { GameModeBadgeComponent } from '../../../../shared/components/game-mode-badge/game-mode-badge.component';
import { GameModeIconComponent } from '../../../../shared/components/game-mode-icon/game-mode-icon.component';
import { MultiplierBadgeComponent } from '../../../../shared/components/multiplier-badge/multiplier-badge.component';
import { MultiplierState } from '../../../../core/services/game-state.service';
import { TranslocoDirective, TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { getPositionTranslationKey } from '../../../../core/utils/position-utils';
import { LucideAngularModule } from 'lucide-angular';

@Component({
  selector: 'app-bid-dialog',
  standalone: true,
  imports: [BidButtonRowComponent, GameModeBadgeComponent, GameModeIconComponent, MultiplierBadgeComponent, TranslocoDirective, TranslocoPipe, LucideAngularModule],
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
            <app-multiplier-badge [multiplier]="currentMultiplier()" />
            <span class="bid-by">{{ t('negotiation.bidBy', { player: t(positionKey(bid.player)) }) }}</span>
          </div>
        } @else {
          <p class="no-bid">{{ t('bidDialog.noBidYouOpen') }}</p>
        }

        <!-- Bid history (newest first, collapsible) -->
        @if (reversedHistory().length > 0) {
          <div class="history-section">
            <button class="history-toggle" (click)="historyExpanded.set(!historyExpanded())">
              <span class="history-toggle-label">{{ t('bidDialog.history') }}</span>
              <span class="history-count">{{ reversedHistory().length }}</span>
              <lucide-icon name="chevron-down" [size]="14" class="history-chevron" [class.collapsed]="!historyExpanded()" />
            </button>
            @if (historyExpanded()) {
              <div class="history-list">
                @for (action of reversedHistory(); track $index) {
                  <div class="history-row" [class.latest]="$index === 0" [style.opacity]="$index === 0 ? 1 : Math.max(0.4, 1 - $index * 0.15)">
                    <span class="player-avatar" [class.latest-avatar]="$index === 0">{{ translatedInitial(action.player) }}</span>
                    <span class="player-name" [class.latest-name]="$index === 0">{{ positionKey(action.player) | transloco }}</span>
                    <span class="action-badge" [class]="getActionBadgeClass(action)">
                      @if (action.actionType === 'Announce') {
                        <app-game-mode-icon [mode]="action.mode!" size="0.875rem" />
                        <span>{{ formatModeName(action.mode) }}</span>
                      } @else if (action.actionType === 'Double') {
                        <span class="multiplier-symbol">\u00d72</span>
                        <span>{{ t('negotiation.double') }}</span>
                        @if (action.mode) {
                          <app-game-mode-icon [mode]="action.mode!" size="0.875rem" />
                        }
                      } @else if (action.actionType === 'Redouble') {
                        <span class="multiplier-symbol">\u00d74</span>
                        <span>{{ t('negotiation.redouble') }}</span>
                        @if (action.mode) {
                          <app-game-mode-icon [mode]="action.mode!" size="0.875rem" />
                        }
                      } @else if (action.actionType === 'ReRedouble') {
                        <span class="multiplier-symbol">\u00d78</span>
                        <span>{{ t('negotiation.reRedouble') }}</span>
                        @if (action.mode) {
                          <app-game-mode-icon [mode]="action.mode!" size="0.875rem" />
                        }
                      } @else {
                        <span>{{ t('negotiation.accept') }}</span>
                        @if (action.mode) {
                          <app-game-mode-icon [mode]="action.mode!" size="0.875rem" />
                        }
                      }
                    </span>
                  </div>
                }
              </div>
            }
          </div>
        }

        <!-- Bid buttons -->
        <app-bid-button-row
          [validActions]="validActions()"
          [currentMultiplier]="currentMultiplier()"
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
      background: transparent;
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

    /* History section */
    .history-section {
      width: 100%;
      display: flex;
      flex-direction: column;
      gap: 0;
    }

    .history-toggle {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      width: 100%;
      padding: 0.375rem 0.625rem;
      border: none;
      background: hsl(var(--muted) / 0.2);
      border-radius: 0.375rem;
      cursor: pointer;
      color: hsl(var(--muted-foreground));
      font-size: 0.75rem;
      font-weight: 600;
      transition: background 0.15s ease;
    }

    .history-toggle:hover {
      background: hsl(var(--muted) / 0.35);
    }

    .history-toggle-label {
      flex: 1;
      text-align: left;
    }

    .history-count {
      font-size: 0.625rem;
      font-weight: 700;
      background: hsl(var(--muted) / 0.5);
      color: hsl(var(--muted-foreground));
      border-radius: 9999px;
      min-width: 1.25rem;
      height: 1.25rem;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: 0 0.25rem;
    }

    .history-chevron {
      transition: transform 0.2s ease;
    }

    .history-chevron.collapsed {
      transform: rotate(-90deg);
    }

    /* History list */
    .history-list {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
      width: 100%;
      max-height: 160px;
      overflow-y: auto;
      margin-top: 0.375rem;
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

    .action-badge.reredouble-badge {
      background: hsl(280, 70%, 45% / 0.2);
      color: hsl(280, 70%, 65%);
      border: 1px solid hsl(280, 70%, 45% / 0.4);
    }

    .multiplier-symbol {
      font-weight: 800;
      font-size: 0.8125rem;
    }

    @media (max-width: 640px) {
      .dialog-container {
        align-items: flex-start;
        padding-top: 2rem;
      }
    }
  `],
})
export class BidDialogComponent {
  readonly Math = Math;
  private readonly transloco = inject(TranslocoService);
  readonly validActions = input<ValidAction[]>([]);
  readonly negotiationHistory = input<NegotiationAction[]>([]);
  readonly activePlayer = input<PlayerPosition | null>(null);
  readonly historyExpanded = signal(true);

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

  readonly currentMultiplier = computed<MultiplierState>(() => {
    const history = this.negotiationHistory();
    let multiplier: MultiplierState = 'Normal';
    for (const action of history) {
      switch (action.actionType) {
        case 'Announce':
          multiplier = 'Normal';
          break;
        case 'Double':
          multiplier = 'Doubled';
          break;
        case 'Redouble':
          multiplier = 'Redoubled';
          break;
        case 'ReRedouble':
          multiplier = 'ReRedoubled';
          break;
      }
    }
    return multiplier;
  });

  readonly reversedHistory = computed(() => [...this.negotiationHistory()].reverse());

  positionKey(position: PlayerPosition): string {
    return getPositionTranslationKey(position);
  }

  translatedInitial(player: PlayerPosition): string {
    return this.transloco.translate(getPositionTranslationKey(player)).charAt(0).toUpperCase();
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
      case 'ReRedouble': return 'reredouble-badge';
      default: return '';
    }
  }

  onAction(action: { actionType: string; mode?: string | null }): void {
    this.actionSelected.emit(action);
  }
}
