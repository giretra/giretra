import { Component, input, output, computed } from '@angular/core';
import { Team } from '../../../../../api/generated/signalr-types.generated';
import { HlmButton } from '@spartan-ng/helm/button';

@Component({
  selector: 'app-match-end-overlay',
  standalone: true,
  imports: [HlmButton],
  template: `
    <div class="overlay">
      <div class="modal">
        <div class="trophy">
          @if (isWinner()) {
            <span class="trophy-icon">&#127942;</span>
          }
        </div>

        <h1 class="title">
          @if (isWinner()) {
            You Win!
          } @else {
            Game Over
          }
        </h1>

        <p class="winner-text">
          {{ winner() }} Wins
        </p>

        <div class="final-score">
          <span class="score">{{ team1Points() }}</span>
          <span class="divider">-</span>
          <span class="score">{{ team2Points() }}</span>
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
      from {
        opacity: 0;
      }
      to {
        opacity: 1;
      }
    }

    .modal {
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 1rem;
      padding: 2rem;
      text-align: center;
      min-width: 300px;
      animation: slideUp 0.3s ease;
    }

    @keyframes slideUp {
      from {
        opacity: 0;
        transform: translateY(20px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    .trophy {
      margin-bottom: 0.5rem;
    }

    .trophy-icon {
      font-size: 3rem;
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

    .score {
      font-size: 2.5rem;
      font-weight: 700;
      color: hsl(var(--foreground));
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
}
