import { Component, input, output } from '@angular/core';
import { GameMode, PlayerPosition, Team } from '../../../../api/generated/signalr-types.generated';
import { MultiplierState } from '../../../../core/services/game-state.service';
import { POSITIONS_CLOCKWISE } from '../../../../core/utils/position-utils';
import { GameModeBadgeComponent } from '../../../../shared/components/game-mode-badge/game-mode-badge.component';
import { MultiplierBadgeComponent } from '../../../../shared/components/multiplier-badge/multiplier-badge.component';
import { TurnTimerComponent } from '../../../../shared/components/turn-timer/turn-timer.component';
import { TranslocoDirective } from '@jsverse/transloco';

@Component({
  selector: 'app-watcher-bar',
  standalone: true,
  imports: [TranslocoDirective, GameModeBadgeComponent, MultiplierBadgeComponent, TurnTimerComponent],
  template: `
    <div class="watcher-bar" *transloco="let t">
      <!-- Top row: score, mode, ranked badge, deal number -->
      <div class="info-row">
        <div class="score-pills">
          <span class="score-pill team1-pill">{{ team1MatchPoints() }}</span>
          <span class="score-sep">–</span>
          <span class="score-pill team2-pill">{{ team2MatchPoints() }}</span>
        </div>

        @if (gameMode()) {
          <app-game-mode-badge [mode]="gameMode()!" size="1rem" />
          @if (multiplier() !== 'Normal') {
            <app-multiplier-badge [multiplier]="multiplier()" />
          }
        }

        <span class="badge" [class.ranked]="isRanked()">
          {{ isRanked() ? t('watcher.ranked') : t('watcher.unranked') }}
        </span>

        @if (dealNumber() > 0) {
          <span class="deal-num">{{ t('scoreBar.dealNumber', { number: dealNumber() }) }}</span>
        }

        @if (idleDeadline()) {
          <span class="idle-timer">
            <span class="idle-label">{{ t('waiting.autoClose') }}</span>
            <app-turn-timer [deadline]="idleDeadline()" (expired)="idleExpired.emit()" />
          </span>
        }
      </div>

      <!-- Bottom row: card counts -->
      <div class="card-counts">
        <span class="cards-label">{{ t('watcher.cardsLeft') }}</span>
        @for (pos of positions; track pos) {
          <span class="count-item" [class.active-turn]="pos === activePlayer()">
            <span class="position">{{ t('positions.' + positionKey(pos)) }}</span>
            <span class="count">{{ getCount(pos) }}</span>
          </span>
        }
      </div>
    </div>
  `,
  styles: [`
    .watcher-bar {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.375rem;
      padding: 0.5rem;
    }

    .info-row {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-wrap: wrap;
      justify-content: center;
    }

    .score-pills {
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }

    .score-pill {
      font-size: 0.875rem;
      font-weight: 700;
      padding: 0.125rem 0.375rem;
      border-radius: 0.25rem;
      font-variant-numeric: tabular-nums;
    }

    .team1-pill {
      color: hsl(var(--team1));
      background: hsl(var(--team1) / 0.12);
    }

    .team2-pill {
      color: hsl(var(--team2));
      background: hsl(var(--team2) / 0.12);
    }

    .score-sep {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
    }

    .badge {
      font-size: 0.625rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      padding: 0.125rem 0.375rem;
      border-radius: 9999px;
      border: 1px solid hsl(var(--border));
      color: hsl(var(--muted-foreground));
    }

    .badge.ranked {
      border-color: hsl(var(--gold) / 0.4);
      color: hsl(var(--gold));
      background: hsl(var(--gold) / 0.08);
    }

    .deal-num {
      font-size: 0.6875rem;
      color: hsl(var(--muted-foreground));
    }

    .card-counts {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      font-size: 0.8125rem;
    }

    .cards-label {
      font-size: 0.6875rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: hsl(var(--muted-foreground) / 0.7);
    }

    .count-item {
      display: flex;
      gap: 0.25rem;
      align-items: center;
      padding: 0.125rem 0.25rem;
      border-radius: 0.25rem;
      transition: background 0.15s ease;
    }

    .count-item.active-turn {
      background: hsl(var(--primary) / 0.12);
    }

    .position {
      color: hsl(var(--muted-foreground));
      font-size: 0.75rem;
    }

    .count-item.active-turn .position {
      color: hsl(var(--primary));
    }

    .count {
      font-weight: 600;
      color: hsl(var(--foreground));
    }

    .idle-timer {
      display: flex;
      align-items: center;
      gap: 0.375rem;
    }

    .idle-label {
      font-size: 0.625rem;
      color: hsl(var(--muted-foreground));
    }
  `],
})
export class WatcherBarComponent {
  readonly playerCardCounts = input<Record<PlayerPosition, number> | null>(null);
  readonly team1MatchPoints = input<number>(0);
  readonly team2MatchPoints = input<number>(0);
  readonly gameMode = input<GameMode | null>(null);
  readonly multiplier = input<MultiplierState>('Normal');
  readonly isRanked = input<boolean>(false);
  readonly dealNumber = input<number>(0);
  readonly activePlayer = input<PlayerPosition | null>(null);
  readonly idleDeadline = input<Date | null>(null);

  readonly idleExpired = output<void>();

  readonly positions = POSITIONS_CLOCKWISE;

  private static readonly POSITION_KEYS: Record<PlayerPosition, string> = {
    [PlayerPosition.Bottom]: 'bottom',
    [PlayerPosition.Left]: 'left',
    [PlayerPosition.Top]: 'top',
    [PlayerPosition.Right]: 'right',
  };

  positionKey(pos: PlayerPosition): string {
    return WatcherBarComponent.POSITION_KEYS[pos] ?? pos;
  }

  getCount(position: PlayerPosition): number {
    return this.playerCardCounts()?.[position] ?? 0;
  }
}
