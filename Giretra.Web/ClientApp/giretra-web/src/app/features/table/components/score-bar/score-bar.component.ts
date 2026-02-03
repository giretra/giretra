import { Component, input, output, computed } from '@angular/core';
import { GameMode, Team } from '../../../../api/generated/signalr-types.generated';
import { RoomResponse } from '../../../../core/services/api.service';
import { MultiplierState } from '../../../../core/services/game-state.service';
import { GameModeBadgeComponent } from '../../../../shared/components/game-mode-badge/game-mode-badge.component';
import { MultiplierBadgeComponent } from '../../../../shared/components/multiplier-badge/multiplier-badge.component';
import { HlmButton } from '@spartan-ng/helm/button';

@Component({
  selector: 'app-score-bar',
  standalone: true,
  imports: [GameModeBadgeComponent, MultiplierBadgeComponent, HlmButton],
  template: `
    <div class="score-bar">
      <!-- Top row -->
      <div class="score-row main-row">
        <!-- Team 1 Score -->
        <div class="team-score" [class.my-team]="myTeam() === 'Team1'">
          <span class="team-label">Team 1</span>
          <span class="match-points">{{ team1MatchPoints() }}</span>
        </div>

        <!-- Center info -->
        <div class="center-info">
          @if (gameMode()) {
            <app-game-mode-badge [mode]="gameMode()" />
            <app-multiplier-badge [multiplier]="multiplier()" />
          }
          @if (dealNumber() > 0) {
            <span class="deal-number">Deal {{ dealNumber() }}</span>
          } @else {
            <span class="room-name">{{ room()?.name }}</span>
          }
        </div>

        <!-- Team 2 Score -->
        <div class="team-score" [class.my-team]="myTeam() === 'Team2'">
          <span class="team-label">Team 2</span>
          <span class="match-points">{{ team2MatchPoints() }}</span>
        </div>

        <!-- Menu button -->
        <button
          hlmBtn
          variant="ghost"
          size="sm"
          class="menu-button"
          (click)="leaveTable.emit()"
          title="Leave Table"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/>
            <polyline points="16 17 21 12 16 7"/>
            <line x1="21" y1="12" x2="9" y2="12"/>
          </svg>
        </button>
      </div>

      <!-- Bottom row (deal card points) -->
      @if (showDealPoints()) {
        <div class="score-row deal-row">
          <div class="deal-points" [class.my-team]="myTeam() === 'Team1'">
            {{ team1CardPoints() }} pts
          </div>
          <div class="deal-points" [class.my-team]="myTeam() === 'Team2'">
            {{ team2CardPoints() }} pts
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .score-bar {
      background: hsl(var(--card));
      border-bottom: 1px solid hsl(var(--border));
      padding: 0.5rem 1rem;
      flex-shrink: 0;
    }

    .score-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
    }

    .main-row {
      min-height: 2.5rem;
    }

    .deal-row {
      margin-top: 0.25rem;
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
    }

    .team-score {
      display: flex;
      flex-direction: column;
      align-items: center;
      min-width: 4rem;
    }

    .team-label {
      font-size: 0.625rem;
      text-transform: uppercase;
      color: hsl(var(--muted-foreground));
      letter-spacing: 0.05em;
    }

    .match-points {
      font-size: 1.25rem;
      font-weight: 700;
      color: hsl(var(--foreground));
    }

    .my-team .match-points,
    .my-team.deal-points {
      color: hsl(var(--primary));
    }

    .center-info {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex: 1;
      justify-content: center;
    }

    .deal-number,
    .room-name {
      font-size: 0.875rem;
      color: hsl(var(--muted-foreground));
    }

    .deal-points {
      flex: 1;
      text-align: center;
    }

    .deal-points:first-child {
      text-align: left;
    }

    .deal-points:last-child {
      text-align: right;
    }

    .menu-button {
      flex-shrink: 0;
    }
  `],
})
export class ScoreBarComponent {
  readonly room = input<RoomResponse | null>(null);
  readonly team1MatchPoints = input<number>(0);
  readonly team2MatchPoints = input<number>(0);
  readonly team1CardPoints = input<number>(0);
  readonly team2CardPoints = input<number>(0);
  readonly dealNumber = input<number>(0);
  readonly gameMode = input<GameMode | null>(null);
  readonly multiplier = input<MultiplierState>('Normal');
  readonly myTeam = input<Team | null>(null);

  readonly leaveTable = output<void>();

  readonly showDealPoints = computed(() => {
    // Show deal points when game is in progress
    return this.gameMode() !== null;
  });
}
