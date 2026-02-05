import { Component, input, output } from '@angular/core';
import { CardPointsBreakdownResponse, GameMode, Team } from '../../../../../api/generated/signalr-types.generated';
import { GameModeBadgeComponent } from '../../../../../shared/components/game-mode-badge/game-mode-badge.component';
import { HlmButton } from '@spartan-ng/helm/button';

@Component({
  selector: 'app-deal-summary',
  standalone: true,
  imports: [GameModeBadgeComponent, HlmButton],
  template: `
    @if (summary(); as s) {
      <div class="deal-summary">
        <h2 class="title">Deal Complete</h2>

        <div class="mode-display">
          <app-game-mode-badge [mode]="s.gameMode" size="1.5rem" />
        </div>

        <div class="scores-section">
          <div class="team-column team1">
            <span class="team-label">Team 1</span>
            <span class="card-points">{{ s.team1CardPoints }}</span>
            <span class="pts-label">pts</span>
            <span class="earned" [class.winner]="s.team1MatchPointsEarned > 0">
              +{{ s.team1MatchPointsEarned }} match
            </span>
          </div>

          <div class="divider"></div>

          <div class="team-column team2">
            <span class="team-label">Team 2</span>
            <span class="card-points">{{ s.team2CardPoints }}</span>
            <span class="pts-label">pts</span>
            <span class="earned" [class.winner]="s.team2MatchPointsEarned > 0">
              +{{ s.team2MatchPointsEarned }} match
            </span>
          </div>
        </div>

        <!-- Card Points Breakdown -->
        <div class="breakdown-section">
          <h3 class="breakdown-title">Points Breakdown</h3>
          <table class="breakdown-table">
            <thead>
              <tr>
                <th class="card-type">Card</th>
                <th class="team1-col">Team 1</th>
                <th class="team2-col">Team 2</th>
              </tr>
            </thead>
            <tbody>
              @if (s.team1Breakdown.jacks > 0 || s.team2Breakdown.jacks > 0) {
                <tr>
                  <td class="card-type">Jacks</td>
                  <td class="team1-col" [class.has-points]="s.team1Breakdown.jacks > 0">{{ s.team1Breakdown.jacks }}</td>
                  <td class="team2-col" [class.has-points]="s.team2Breakdown.jacks > 0">{{ s.team2Breakdown.jacks }}</td>
                </tr>
              }
              @if (s.team1Breakdown.nines > 0 || s.team2Breakdown.nines > 0) {
                <tr>
                  <td class="card-type">Nines</td>
                  <td class="team1-col" [class.has-points]="s.team1Breakdown.nines > 0">{{ s.team1Breakdown.nines }}</td>
                  <td class="team2-col" [class.has-points]="s.team2Breakdown.nines > 0">{{ s.team2Breakdown.nines }}</td>
                </tr>
              }
              @if (s.team1Breakdown.aces > 0 || s.team2Breakdown.aces > 0) {
                <tr>
                  <td class="card-type">Aces</td>
                  <td class="team1-col" [class.has-points]="s.team1Breakdown.aces > 0">{{ s.team1Breakdown.aces }}</td>
                  <td class="team2-col" [class.has-points]="s.team2Breakdown.aces > 0">{{ s.team2Breakdown.aces }}</td>
                </tr>
              }
              @if (s.team1Breakdown.tens > 0 || s.team2Breakdown.tens > 0) {
                <tr>
                  <td class="card-type">Tens</td>
                  <td class="team1-col" [class.has-points]="s.team1Breakdown.tens > 0">{{ s.team1Breakdown.tens }}</td>
                  <td class="team2-col" [class.has-points]="s.team2Breakdown.tens > 0">{{ s.team2Breakdown.tens }}</td>
                </tr>
              }
              @if (s.team1Breakdown.kings > 0 || s.team2Breakdown.kings > 0) {
                <tr>
                  <td class="card-type">Kings</td>
                  <td class="team1-col" [class.has-points]="s.team1Breakdown.kings > 0">{{ s.team1Breakdown.kings }}</td>
                  <td class="team2-col" [class.has-points]="s.team2Breakdown.kings > 0">{{ s.team2Breakdown.kings }}</td>
                </tr>
              }
              @if (s.team1Breakdown.queens > 0 || s.team2Breakdown.queens > 0) {
                <tr>
                  <td class="card-type">Queens</td>
                  <td class="team1-col" [class.has-points]="s.team1Breakdown.queens > 0">{{ s.team1Breakdown.queens }}</td>
                  <td class="team2-col" [class.has-points]="s.team2Breakdown.queens > 0">{{ s.team2Breakdown.queens }}</td>
                </tr>
              }
              <tr class="last-trick-row">
                <td class="card-type">Last Trick</td>
                <td class="team1-col" [class.has-points]="s.team1Breakdown.lastTrickBonus > 0">{{ s.team1Breakdown.lastTrickBonus > 0 ? '+10' : '-' }}</td>
                <td class="team2-col" [class.has-points]="s.team2Breakdown.lastTrickBonus > 0">{{ s.team2Breakdown.lastTrickBonus > 0 ? '+10' : '-' }}</td>
              </tr>
              <tr class="total-row">
                <td class="card-type">Total</td>
                <td class="team1-col has-points">{{ s.team1Breakdown.total }}</td>
                <td class="team2-col has-points">{{ s.team2Breakdown.total }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        @if (s.wasSweep) {
          <div class="sweep-banner">
            SWEEP by {{ s.sweepingTeam }}!
          </div>
        }

        <!-- Match progress -->
        <div class="match-progress">
          <div class="progress-team team1-text">
            <span class="progress-score">{{ s.team1TotalMatchPoints }}</span>
            <span class="progress-target">/ 150</span>
          </div>
          <span class="progress-label">Match Score</span>
          <div class="progress-team team2-text">
            <span class="progress-score">{{ s.team2TotalMatchPoints }}</span>
            <span class="progress-target">/ 150</span>
          </div>
        </div>

        <button hlmBtn variant="default" class="continue-button" (click)="dismissed.emit()">
          Continue
        </button>
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
      min-width: 320px;
      max-width: 400px;
      animation: scaleIn 0.25s ease;
      position: relative;
      z-index: 10;
    }

    @keyframes scaleIn {
      from {
        opacity: 0;
        transform: scale(0.93);
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

    .scores-section {
      display: flex;
      align-items: stretch;
      gap: 1.5rem;
      margin-bottom: 1rem;
      width: 100%;
    }

    .team-column {
      display: flex;
      flex-direction: column;
      align-items: center;
      flex: 1;
      padding: 0.5rem;
      border-radius: 0.375rem;
    }

    .team1 {
      background: hsl(var(--team1) / 0.1);
    }

    .team2 {
      background: hsl(var(--team2) / 0.1);
    }

    .team-label {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
      margin-bottom: 0.125rem;
    }

    .card-points {
      font-size: 1.5rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      line-height: 1;
    }

    .pts-label {
      font-size: 0.625rem;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
    }

    .earned {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      margin-top: 0.25rem;
    }

    .earned.winner {
      color: hsl(var(--primary));
      font-weight: 600;
    }

    .divider {
      width: 1px;
      background: hsl(var(--border));
    }

    .breakdown-section {
      width: 100%;
      margin-bottom: 1rem;
    }

    .breakdown-title {
      font-size: 0.75rem;
      font-weight: 600;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
      margin: 0 0 0.5rem 0;
      text-align: center;
    }

    .breakdown-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.8rem;
    }

    .breakdown-table th,
    .breakdown-table td {
      padding: 0.25rem 0.5rem;
      text-align: center;
    }

    .breakdown-table th {
      font-weight: 500;
      color: hsl(var(--muted-foreground));
      border-bottom: 1px solid hsl(var(--border));
    }

    .breakdown-table .card-type {
      text-align: left;
      color: hsl(var(--muted-foreground));
    }

    .breakdown-table .team1-col {
      color: hsl(var(--muted-foreground));
    }

    .breakdown-table .team2-col {
      color: hsl(var(--muted-foreground));
    }

    .breakdown-table .has-points {
      color: hsl(var(--foreground));
      font-weight: 500;
    }

    .breakdown-table tbody tr:nth-child(even) {
      background: hsl(var(--muted) / 0.2);
    }

    .breakdown-table .last-trick-row {
      border-top: 1px dashed hsl(var(--border));
    }

    .breakdown-table .total-row {
      border-top: 1px solid hsl(var(--border));
      font-weight: 600;
    }

    .breakdown-table .total-row .card-type {
      color: hsl(var(--foreground));
    }

    .sweep-banner {
      background: hsl(var(--gold));
      color: hsl(220, 20%, 10%);
      padding: 0.375rem 1rem;
      border-radius: 0.25rem;
      font-weight: 700;
      font-size: 0.875rem;
      margin-bottom: 1rem;
    }

    .match-progress {
      display: flex;
      align-items: center;
      gap: 1rem;
      margin-bottom: 1rem;
    }

    .progress-team {
      display: flex;
      align-items: baseline;
      gap: 0.25rem;
    }

    .progress-score {
      font-size: 1.5rem;
      font-weight: 700;
    }

    .progress-target {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
    }

    .team1-text .progress-score {
      color: hsl(var(--team1));
    }

    .team2-text .progress-score {
      color: hsl(var(--team2));
    }

    .progress-label {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
    }

    .continue-button {
      width: 100%;
      margin-top: 0.5rem;
    }
  `],
})
export class DealSummaryComponent {
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
    team1Breakdown: CardPointsBreakdownResponse;
    team2Breakdown: CardPointsBreakdownResponse;
  } | null>(null);

  readonly dismissed = output<void>();
}
