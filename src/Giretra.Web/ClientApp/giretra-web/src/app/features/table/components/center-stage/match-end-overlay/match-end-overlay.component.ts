import { Component, input, output, computed, inject } from '@angular/core';
import { Team } from '../../../../../api/generated/signalr-types.generated';
import { EloChangeResponse } from '../../../../../core/services/api.service';
import { getTeamLabel } from '../../../../../core/utils';
import { HlmButton } from '@spartan-ng/helm/button';
import { LucideAngularModule, Trophy, ArrowUp, ArrowDown } from 'lucide-angular';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-match-end-overlay',
  standalone: true,
  imports: [HlmButton, LucideAngularModule, TranslocoDirective],
  template: `
    <div class="overlay" *transloco="let t">
      <div class="modal">
        @if (isWinner()) {
          <div class="trophy-container">
            <i-lucide [img]="TrophyIcon" [size]="48" [strokeWidth]="1.5" class="trophy-icon winner"></i-lucide>
          </div>
        }

        <h1 class="title">
          @if (isWinner()) {
            {{ t('matchEnd.youWin') }}
          } @else {
            {{ t('matchEnd.gameOver') }}
          }
        </h1>

        <p class="winner-text">
          {{ winnerLabel() }} {{ t('matchEnd.wins') }}
        </p>

        <div class="final-score">
          <div class="score-column">
            <span class="score-label">{{ team1Label() }}</span>
            <span class="score team1-score">{{ team1Points() }}</span>
          </div>
          <span class="divider">-</span>
          <div class="score-column">
            <span class="score-label">{{ team2Label() }}</span>
            <span class="score team2-score">{{ team2Points() }}</span>
          </div>
        </div>

        <p class="deals-played">{{ t('matchEnd.dealsPlayed', { count: totalDeals() }) }}</p>

        @if (showElo()) {
          <div class="elo-card" [class.elo-positive]="eloIsPositive()" [class.elo-negative]="eloIsNegative()">
            <div class="elo-change-row">
              <span class="elo-change-value">{{ eloChange()!.eloChange >= 0 ? '+' : '' }}{{ eloChange()!.eloChange }}</span>
              @if (eloIsPositive()) {
                <i-lucide [img]="ArrowUpIcon" [size]="18" [strokeWidth]="2.5" class="elo-arrow-icon"></i-lucide>
              } @else if (eloIsNegative()) {
                <i-lucide [img]="ArrowDownIcon" [size]="18" [strokeWidth]="2.5" class="elo-arrow-icon"></i-lucide>
              }
            </div>
            <div class="elo-rating-label">
              {{ t('matchEnd.rating') }}: {{ eloChange()!.eloAfter }}
            </div>
          </div>
        }

        <div class="actions">
          @if (isCreator()) {
            <button
              hlmBtn
              variant="default"
              (click)="playAgain.emit()"
            >
              {{ t('matchEnd.playAgain') }}
            </button>
          }
          <button
            hlmBtn
            [variant]="isCreator() ? 'secondary' : 'default'"
            (click)="leaveTable.emit()"
          >
            {{ t('matchEnd.leaveTable') }}
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.85);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
      z-index: 100;
      animation: fadeIn 0.3s ease;
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    .modal {
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 1rem;
      padding: 2rem;
      text-align: center;
      min-width: 300px;
      animation: scaleIn 0.3s ease;
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

    .trophy-container {
      margin-bottom: 0.75rem;
    }

    .trophy-icon {
      color: hsl(var(--muted-foreground));
    }

    .trophy-icon.winner {
      color: hsl(var(--gold));
    }

    .title {
      font-size: 2rem;
      font-weight: 700;
      margin: 0 0 0.5rem 0;
      color: hsl(var(--foreground));
    }

    .winner-text {
      font-size: 1.125rem;
      color: hsl(var(--primary));
      margin: 0 0 1rem 0;
    }

    .final-score {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 1rem;
      margin-bottom: 0.5rem;
    }

    .score-column {
      display: flex;
      flex-direction: column;
      align-items: center;
    }

    .score-label {
      font-size: 0.625rem;
      text-transform: uppercase;
      color: hsl(var(--muted-foreground));
      letter-spacing: 0.05em;
    }

    .score {
      font-size: 2.5rem;
      font-weight: 700;
    }

    .team1-score {
      color: hsl(var(--team1));
    }

    .team2-score {
      color: hsl(var(--team2));
    }

    .divider {
      font-size: 2rem;
      color: hsl(var(--muted-foreground));
    }

    .deals-played {
      font-size: 0.875rem;
      color: hsl(var(--muted-foreground));
      margin: 0 0 1rem 0;
    }

    .elo-card {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.25rem;
      padding: 0.75rem 1.5rem;
      border-radius: 0.75rem;
      border: 1px solid hsl(var(--border));
      margin-bottom: 1.5rem;
    }

    .elo-card.elo-positive {
      background: hsl(142 70% 45% / 0.1);
      border-color: hsl(142 70% 45% / 0.3);
    }

    .elo-card.elo-negative {
      background: hsl(0 72% 51% / 0.1);
      border-color: hsl(0 72% 51% / 0.3);
    }

    .elo-change-row {
      display: flex;
      align-items: center;
      gap: 0.375rem;
    }

    .elo-change-value {
      font-size: 1.5rem;
      font-weight: 700;
    }

    .elo-positive .elo-change-value,
    .elo-positive .elo-arrow-icon {
      color: hsl(142 70% 45%);
    }

    .elo-negative .elo-change-value,
    .elo-negative .elo-arrow-icon {
      color: hsl(0 72% 51%);
    }

    .elo-rating-label {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
    }

    .actions {
      display: flex;
      gap: 0.75rem;
      justify-content: center;
    }
  `],
})
export class MatchEndOverlayComponent {
  private readonly transloco = inject(TranslocoService);
  readonly TrophyIcon = Trophy;
  readonly ArrowUpIcon = ArrowUp;
  readonly ArrowDownIcon = ArrowDown;

  readonly winner = input<Team | null>(null);
  readonly myTeam = input<Team | null>(null);
  readonly team1Points = input<number>(0);
  readonly team2Points = input<number>(0);
  readonly totalDeals = input<number>(0);
  readonly isCreator = input<boolean>(false);
  readonly eloChange = input<EloChangeResponse | null>(null);
  readonly isRanked = input<boolean>(false);

  readonly playAgain = output<void>();
  readonly leaveTable = output<void>();

  readonly showElo = computed(() => this.isRanked() && this.eloChange() !== null);
  readonly eloIsPositive = computed(() => (this.eloChange()?.eloChange ?? 0) > 0);
  readonly eloIsNegative = computed(() => (this.eloChange()?.eloChange ?? 0) < 0);

  readonly isWinner = computed(() => {
    const w = this.winner();
    const mt = this.myTeam();
    return w && mt && w === mt;
  });

  readonly winnerLabel = computed(() => {
    const w = this.winner();
    if (!w) return '';
    return getTeamLabel(w as 'Team1' | 'Team2', this.myTeam(), (k) => this.transloco.translate(k));
  });

  readonly team1Label = computed(() => getTeamLabel('Team1', this.myTeam(), (k) => this.transloco.translate(k)));
  readonly team2Label = computed(() => getTeamLabel('Team2', this.myTeam(), (k) => this.transloco.translate(k)));
}
