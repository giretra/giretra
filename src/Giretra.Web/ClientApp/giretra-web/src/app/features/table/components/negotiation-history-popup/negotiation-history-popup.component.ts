import { Component, input, output, computed, inject } from '@angular/core';
import { GameMode, PlayerPosition } from '../../../../api/generated/signalr-types.generated';
import { NegotiationAction } from '../../../../core/services/api.service';
import { MultiplierState } from '../../../../core/services/game-state.service';
import { GameModeBadgeComponent } from '../../../../shared/components/game-mode-badge/game-mode-badge.component';
import { GameModeIconComponent } from '../../../../shared/components/game-mode-icon/game-mode-icon.component';
import { MultiplierBadgeComponent } from '../../../../shared/components/multiplier-badge/multiplier-badge.component';
import { LucideAngularModule, X } from 'lucide-angular';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { getPositionTranslationKey } from '../../../../core/utils/position-utils';

@Component({
  selector: 'app-negotiation-history-popup',
  standalone: true,
  imports: [
    GameModeBadgeComponent,
    GameModeIconComponent,
    MultiplierBadgeComponent,
    LucideAngularModule,
    TranslocoDirective,
  ],
  template: `
    <ng-container *transloco="let t">
    <div class="backdrop" (click)="closed.emit()"></div>
    <div class="popup-container" (click)="closed.emit()">
      <div class="popup-panel" (click)="$event.stopPropagation()">
        <!-- Close button -->
        <button class="close-btn" (click)="closed.emit()">
          <i-lucide [img]="XIcon" [size]="16" [strokeWidth]="2"></i-lucide>
        </button>

        <!-- Header: current bid summary -->
        <div class="header">
          <h2 class="title">{{ t('negotiationHistory.title') }}</h2>
          @if (currentBid(); as bid) {
            <div class="current-bid">
              <app-game-mode-badge [mode]="bid.mode" size="1.5rem" />
              <app-multiplier-badge [multiplier]="multiplier()" />
            </div>
          }
        </div>

        <!-- Timeline -->
        @if (negotiationHistory().length > 0) {
          <div class="timeline">
            @for (action of negotiationHistory(); track $index) {
              <div class="timeline-item">
                <div class="timeline-dot" [class]="getDotClass(action)"></div>
                @if (!$last) {
                  <div class="timeline-line"></div>
                }
                <div class="timeline-content">
                  <span class="player-name">{{ t(positionKey(action.player)) }}</span>
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
                    } @else if (action.actionType === 'ReRedouble') {
                      <span class="multiplier-symbol">\u00d78</span>
                      <span>{{ t('negotiation.reRedouble') }}</span>
                    } @else {
                      <span>{{ t('negotiation.accept') }}</span>
                    }
                  </span>
                </div>
              </div>
            }
          </div>
        } @else {
          <p class="empty-state">{{ t('negotiationHistory.empty') }}</p>
        }
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
      z-index: 100;
      background: rgba(0, 0, 0, 0.5);
      animation: fadeIn 0.2s ease;
    }

    .popup-container {
      position: fixed;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 110;
      pointer-events: none;
    }

    .popup-panel {
      pointer-events: auto;
      position: relative;
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 1rem;
      padding: 1.5rem;
      max-width: 360px;
      width: calc(100% - 2rem);
      animation: scaleIn 0.25s ease;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .close-btn {
      position: absolute;
      top: 0.75rem;
      right: 0.75rem;
      background: none;
      border: none;
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      padding: 0.25rem;
      border-radius: 0.25rem;
      transition: color 0.15s ease;
    }

    .close-btn:hover {
      color: hsl(var(--foreground));
    }

    .header {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.625rem;
    }

    .title {
      font-size: 1.125rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      margin: 0;
    }

    .current-bid {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.375rem 0.75rem;
      background: hsl(var(--gold) / 0.08);
      border: 1px solid hsl(var(--gold) / 0.15);
      border-radius: 0.5rem;
    }

    /* Timeline */
    .timeline {
      display: flex;
      flex-direction: column;
      padding-left: 0.25rem;
      max-height: 280px;
      overflow-y: auto;
    }

    .timeline-item {
      position: relative;
      display: flex;
      align-items: flex-start;
      padding-left: 1.5rem;
      padding-bottom: 0.75rem;
    }

    .timeline-item:last-child {
      padding-bottom: 0;
    }

    .timeline-dot {
      position: absolute;
      left: 0;
      top: 0.375rem;
      width: 0.625rem;
      height: 0.625rem;
      border-radius: 50%;
      background: hsl(var(--muted));
      border: 2px solid hsl(var(--border));
      z-index: 1;
    }

    .timeline-dot.announce-dot {
      background: hsl(var(--gold));
      border-color: hsl(var(--gold));
    }

    .timeline-dot.accept-dot {
      background: hsl(var(--primary));
      border-color: hsl(var(--primary));
    }

    .timeline-dot.double-dot {
      background: hsl(var(--destructive));
      border-color: hsl(var(--destructive));
    }

    .timeline-dot.redouble-dot {
      background: hsl(0, 72%, 65%);
      border-color: hsl(0, 72%, 65%);
    }

    .timeline-dot.reredouble-dot {
      background: hsl(280, 70%, 65%);
      border-color: hsl(280, 70%, 65%);
    }

    .timeline-line {
      position: absolute;
      left: 0.25rem;
      top: 1rem;
      bottom: 0;
      width: 1px;
      background: hsl(var(--border));
    }

    .timeline-content {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex: 1;
    }

    .player-name {
      font-size: 0.8125rem;
      font-weight: 600;
      color: hsl(var(--foreground));
      min-width: 3.5rem;
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

    .empty-state {
      color: hsl(var(--muted-foreground));
      font-size: 0.875rem;
      text-align: center;
      margin: 0;
      font-style: italic;
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
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
  `],
})
export class NegotiationHistoryPopupComponent {
  readonly XIcon = X;
  private readonly transloco = inject(TranslocoService);

  readonly negotiationHistory = input<NegotiationAction[]>([]);
  readonly multiplier = input<MultiplierState>('Normal');

  readonly closed = output<void>();

  readonly currentBid = computed(() => {
    const history = this.negotiationHistory();
    for (let i = history.length - 1; i >= 0; i--) {
      if (history[i].actionType === 'Announce' && history[i].mode) {
        return history[i];
      }
    }
    return null;
  });

  positionKey(position: PlayerPosition): string {
    return getPositionTranslationKey(position);
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

  getDotClass(action: NegotiationAction): string {
    switch (action.actionType) {
      case 'Announce': return 'announce-dot';
      case 'Accept': return 'accept-dot';
      case 'Double': return 'double-dot';
      case 'Redouble': return 'redouble-dot';
      case 'ReRedouble': return 'reredouble-dot';
      default: return '';
    }
  }
}
