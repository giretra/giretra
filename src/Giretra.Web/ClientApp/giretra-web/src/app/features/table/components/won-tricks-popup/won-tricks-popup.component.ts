import { Component, input, output, computed, inject } from '@angular/core';
import { PlayerPosition, Team } from '../../../../api/generated/signalr-types.generated';
import { TrickResponse } from '../../../../core/services/api.service';
import { cardToString } from '../../../../core/models/card.model';
import { isRedSuit } from '../../../../core/utils/card-utils';
import { getTeam, toRelativePosition } from '../../../../core/utils/position-utils';
import { LucideAngularModule, X, Layers } from 'lucide-angular';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-won-tricks-popup',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
    <div class="backdrop" (click)="closed.emit()"></div>
    <div class="popup-container" (click)="closed.emit()">
      <div class="popup-panel" (click)="$event.stopPropagation()">
        <!-- Close button -->
        <button class="close-btn" (click)="closed.emit()">
          <i-lucide [img]="XIcon" [size]="16" [strokeWidth]="2"></i-lucide>
        </button>

        <div class="header">
          <h2 class="title">
            <i-lucide [img]="LayersIcon" [size]="16" [strokeWidth]="2"></i-lucide>
            {{ t('wonTricks.title') }}
          </h2>
          <span class="trick-count-badge">{{ wonTricks().length }}</span>
        </div>

        @if (wonTricks().length > 0) {
          <div class="trick-list">
            @for (trick of wonTricks(); track trick.trickNumber) {
              <div class="trick-row">
                <div class="trick-head">
                  <span class="trick-num">#{{ trick.trickNumber }}</span>
                  <span class="trick-winner">{{ trick.winnerLabel }}</span>
                </div>
                <div class="trick-cards">
                  @for (card of trick.cards; track $index) {
                    <div class="tc-card" [class.tc-winner]="card.isWinner">
                      <span class="tc-rank" [class.tc-red]="card.isRed">{{ card.cardText }}</span>
                      <span class="tc-player">{{ card.playerLabel }}</span>
                    </div>
                  }
                </div>
              </div>
            }
          </div>
        } @else {
          <p class="empty-state">{{ t('wonTricks.empty') }}</p>
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
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
    }

    .title {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      font-size: 1.125rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      margin: 0;
    }

    .trick-count-badge {
      font-size: 0.6875rem;
      font-weight: 700;
      color: hsl(var(--primary));
      background: hsl(var(--primary) / 0.15);
      border: 1px solid hsl(var(--primary) / 0.3);
      padding: 0.0625rem 0.5rem;
      border-radius: 9999px;
      font-variant-numeric: tabular-nums;
    }

    .trick-list {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      max-height: min(50vh, 420px);
      overflow-y: auto;
      padding-right: 0.25rem;
    }

    .trick-list::-webkit-scrollbar {
      width: 4px;
    }

    .trick-list::-webkit-scrollbar-thumb {
      background: hsl(var(--muted-foreground) / 0.3);
      border-radius: 2px;
    }

    .trick-row {
      padding: 0.375rem 0.5rem;
      border-radius: 0.375rem;
      background: hsl(var(--muted) / 0.15);
    }

    .trick-head {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      margin-bottom: 0.25rem;
      font-size: 0.75rem;
    }

    .trick-num {
      font-weight: 700;
      color: hsl(var(--foreground));
      font-size: 0.8rem;
    }

    .trick-winner {
      flex: 1;
      color: hsl(var(--muted-foreground));
    }

    .trick-cards {
      display: flex;
      justify-content: center;
      gap: 0.375rem;
    }

    .tc-card {
      display: flex;
      flex-direction: column;
      align-items: center;
      min-width: 3.5rem;
      padding: 0.25rem 0.375rem;
      border-radius: 0.25rem;
      border: 1px solid hsl(var(--border) / 0.5);
      background: hsl(var(--card));
    }

    .tc-card.tc-winner {
      border-color: hsl(var(--gold));
      background: hsl(var(--gold) / 0.08);
    }

    .tc-rank {
      font-size: 0.875rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      line-height: 1.2;
    }

    .tc-rank.tc-red {
      color: #ef4444;
    }

    .tc-player {
      font-size: 0.5625rem;
      color: hsl(var(--muted-foreground));
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      max-width: 100%;
      line-height: 1.2;
    }

    .tc-card.tc-winner .tc-player {
      color: hsl(var(--gold));
      font-weight: 500;
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
export class WonTricksPopupComponent {
  readonly XIcon = X;
  readonly LayersIcon = Layers;
  private readonly transloco = inject(TranslocoService);

  readonly tricks = input<TrickResponse[]>([]);
  readonly myTeam = input<Team | null>(null);
  readonly myPosition = input<PlayerPosition | null>(null);

  readonly closed = output<void>();

  readonly wonTricks = computed(() => {
    const myTeam = this.myTeam();
    const myPos = this.myPosition() ?? PlayerPosition.Bottom;
    if (!myTeam) return [];

    return this.tricks()
      .filter((t) => t.isComplete && t.winner && getTeam(t.winner) === myTeam)
      .map((t) => ({
        trickNumber: t.trickNumber,
        winnerLabel: this.transloco.translate(`positions.${toRelativePosition(t.winner!, myPos)}`),
        cards: t.playedCards.map((pc) => ({
          cardText: cardToString(pc.card),
          playerLabel: this.transloco.translate(`positions.${toRelativePosition(pc.player, myPos)}`),
          isRed: isRedSuit(pc.card.suit),
          isWinner: pc.player === t.winner,
        })),
      }));
  });
}
