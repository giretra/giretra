import { Component, input, output, computed } from '@angular/core';
import { Team } from '../../../../../api/generated/signalr-types.generated';
import { getTeamLabel } from '../../../../../core/utils';
import { HlmButton } from '@spartan-ng/helm/button';
import { LucideAngularModule, Trophy } from 'lucide-angular';

@Component({
  selector: 'app-match-end-overlay',
  standalone: true,
  imports: [HlmButton, LucideAngularModule],
  template: `
    <div class="overlay">
      <div class="modal">
        @if (isWinner()) {
          <div class="trophy-container">
            <i-lucide [img]="TrophyIcon" [size]="48" [strokeWidth]="1.5" class="trophy-icon winner"></i-lucide>
          </div>
        }

        <h1 class="title">
          @if (isWinner()) {
            You Win!
          } @else {
            Game Over
          }
        </h1>

        <p class="winner-text">
          {{ winnerLabel() }} Wins
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

        <p class="deals-played">{{ totalDeals() }} deals played</p>

        <div class="actions">
          @if (isCreator()) {
            <button
              hlmBtn
              variant="default"
              (click)="playAgain.emit()"
            >
              Play Again
            </button>
          }
          <button
            hlmBtn
            [variant]="isCreator() ? 'secondary' : 'default'"
            (click)="leaveTable.emit()"
          >
            Leave Table
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
      margin: 0 0 1.5rem 0;
    }

    .actions {
      display: flex;
      gap: 0.75rem;
      justify-content: center;
    }
  `],
})
export class MatchEndOverlayComponent {
  readonly TrophyIcon = Trophy;

  readonly winner = input<Team | null>(null);
  readonly myTeam = input<Team | null>(null);
  readonly team1Points = input<number>(0);
  readonly team2Points = input<number>(0);
  readonly totalDeals = input<number>(0);
  readonly isCreator = input<boolean>(false);

  readonly playAgain = output<void>();
  readonly leaveTable = output<void>();

  readonly isWinner = computed(() => {
    const w = this.winner();
    const mt = this.myTeam();
    return w && mt && w === mt;
  });

  readonly winnerLabel = computed(() => {
    const w = this.winner();
    if (!w) return '';
    return getTeamLabel(w as 'Team1' | 'Team2', this.myTeam());
  });

  readonly team1Label = computed(() => getTeamLabel('Team1', this.myTeam()));
  readonly team2Label = computed(() => getTeamLabel('Team2', this.myTeam()));
}
