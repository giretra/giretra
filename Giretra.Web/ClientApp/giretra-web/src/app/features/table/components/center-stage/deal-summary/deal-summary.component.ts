import { Component, input, output, OnInit, OnDestroy } from '@angular/core';
import { GameMode, Team } from '../../../../../api/generated/signalr-types.generated';
import { GameModeBadgeComponent } from '../../../../../shared/components/game-mode-badge/game-mode-badge.component';

@Component({
  selector: 'app-deal-summary',
  standalone: true,
  imports: [GameModeBadgeComponent],
  template: `
    @if (summary(); as s) {
      <div class="deal-summary">
        <h2 class="title">Deal Complete</h2>

        <div class="mode-display">
          <app-game-mode-badge [mode]="s.gameMode" size="1.5rem" />
        </div>

        <div class="scores">
          <div class="team-score">
            <span class="team-label">Team 1</span>
            <span class="card-points">{{ s.team1CardPoints }} pts</span>
            <span class="earned" [class.winner]="s.team1MatchPointsEarned > 0">
              +{{ s.team1MatchPointsEarned }}
            </span>
          </div>

          <div class="divider"></div>

          <div class="team-score">
            <span class="team-label">Team 2</span>
            <span class="card-points">{{ s.team2CardPoints }} pts</span>
            <span class="earned" [class.winner]="s.team2MatchPointsEarned > 0">
              +{{ s.team2MatchPointsEarned }}
            </span>
          </div>
        </div>

        @if (s.wasSweep) {
          <div class="sweep-banner">
            SWEEP by {{ s.sweepingTeam }}!
          </div>
        }

        <div class="totals">
          <span class="total-points">{{ s.team1TotalMatchPoints }}</span>
          <span class="total-label">Match Score</span>
          <span class="total-points">{{ s.team2TotalMatchPoints }}</span>
        </div>

        <p class="countdown">Next deal in {{ countdown }}...</p>
      </div>
    }
  `,
  styles: [`
    .deal-summary {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 1.5rem;
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 0.75rem;
      min-width: 280px;
      animation: slideIn 0.3s ease;
    }

    @keyframes slideIn {
      from {
        opacity: 0;
        transform: scale(0.95);
      }
      to {
        opacity: 1;
        transform: scale(1);
      }
    }

    .title {
      font-size: 1.25rem;
      font-weight: 600;
      margin: 0 0 0.75rem 0;
      color: hsl(var(--foreground));
    }

    .mode-display {
      margin-bottom: 1rem;
    }

    .scores {
      display: flex;
      align-items: center;
      gap: 1.5rem;
      margin-bottom: 1rem;
    }

    .team-score {
      display: flex;
      flex-direction: column;
      align-items: center;
      min-width: 80px;
    }

    .team-label {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
    }

    .card-points {
      font-size: 1.25rem;
      font-weight: 600;
      color: hsl(var(--foreground));
    }

    .earned {
      font-size: 0.875rem;
      color: hsl(var(--muted-foreground));
    }

    .earned.winner {
      color: hsl(var(--primary));
      font-weight: 600;
    }

    .divider {
      width: 1px;
      height: 3rem;
      background: hsl(var(--border));
    }

    .sweep-banner {
      background: hsl(var(--accent));
      color: hsl(var(--accent-foreground));
      padding: 0.375rem 1rem;
      border-radius: 0.25rem;
      font-weight: 700;
      font-size: 0.875rem;
      margin-bottom: 1rem;
      animation: pulse 1s ease-in-out infinite;
    }

    @keyframes pulse {
      0%, 100% {
        transform: scale(1);
      }
      50% {
        transform: scale(1.05);
      }
    }

    .totals {
      display: flex;
      align-items: center;
      gap: 1rem;
      margin-bottom: 1rem;
    }

    .total-points {
      font-size: 1.5rem;
      font-weight: 700;
      color: hsl(var(--foreground));
    }

    .total-label {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
    }

    .countdown {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      margin: 0;
    }
  `],
})
export class DealSummaryComponent implements OnInit, OnDestroy {
  readonly summary = input<{
    gameMode: GameMode;
    team1CardPoints: number;
    team2CardPoints: number;
    team1MatchPointsEarned: number;
    team2MatchPointsEarned: number;
    team1TotalMatchPoints: number;
    team2TotalMatchPoints: number;
    wasSweep: boolean;
    sweepingTeam: Team | null;
  } | null>(null);

  readonly dismissed = output<void>();

  countdown = 5;
  private intervalId: any;

  ngOnInit(): void {
    this.countdown = 5;
    this.intervalId = setInterval(() => {
      this.countdown--;
      if (this.countdown <= 0) {
        this.dismissed.emit();
      }
    }, 1000);
  }

  ngOnDestroy(): void {
    if (this.intervalId) {
      clearInterval(this.intervalId);
    }
  }
}
