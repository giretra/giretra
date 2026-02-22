import { Component, input, computed } from '@angular/core';
import { Team } from '../../../api/generated/signalr-types.generated';

@Component({
  selector: 'app-trick-counter',
  standalone: true,
  template: `
    <div class="trick-counter">
      <span class="label">Tricks</span>
      <div class="scores">
        <span
          class="score team1"
          [class.my-team]="myTeam() === 'Team1'"
        >
          {{ team1Tricks() }}
        </span>
        <span class="separator">-</span>
        <span
          class="score team2"
          [class.my-team]="myTeam() === 'Team2'"
        >
          {{ team2Tricks() }}
        </span>
      </div>
    </div>
  `,
  styles: [`
    .trick-counter {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.5rem 1rem;
      background: hsl(var(--card) / 0.9);
      backdrop-filter: blur(8px);
      border-radius: 9999px;
      border: 1px solid hsl(var(--border));
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
    }

    .label {
      font-size: 0.75rem;
      font-weight: 500;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: hsl(var(--muted-foreground));
    }

    .scores {
      display: flex;
      align-items: baseline;
      gap: 0.5rem;
    }

    .score {
      font-size: 1.5rem;
      font-weight: 700;
      font-variant-numeric: tabular-nums;
      min-width: 1.5ch;
      text-align: center;
    }

    .score.team1 {
      color: hsl(210, 70%, 55%);
    }

    .score.team2 {
      color: hsl(142, 50%, 50%);
    }

    .score.my-team {
      text-shadow: 0 0 12px currentColor;
    }

    .separator {
      font-size: 1rem;
      color: hsl(var(--muted-foreground));
    }

    /* Responsive: smaller on mobile */
    @media (max-width: 480px) {
      .trick-counter {
        padding: 0.375rem 0.75rem;
        gap: 0.5rem;
      }

      .label {
        font-size: 0.625rem;
      }

      .score {
        font-size: 1.25rem;
      }
    }
  `],
})
export class TrickCounterComponent {
  readonly team1Tricks = input<number>(0);
  readonly team2Tricks = input<number>(0);
  readonly myTeam = input<Team | null>(null);
}
