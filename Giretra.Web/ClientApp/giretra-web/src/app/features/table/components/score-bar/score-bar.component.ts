import { Component, input, output, computed } from '@angular/core';
import { GameMode, Team } from '../../../../api/generated/signalr-types.generated';
import { RoomResponse } from '../../../../core/services/api.service';
import { MultiplierState } from '../../../../core/services/game-state.service';
import { GameModeBadgeComponent } from '../../../../shared/components/game-mode-badge/game-mode-badge.component';
import { MultiplierBadgeComponent } from '../../../../shared/components/multiplier-badge/multiplier-badge.component';
import { HlmButton } from '@spartan-ng/helm/button';
import { LucideAngularModule, LogOut } from 'lucide-angular';

@Component({
  selector: 'app-score-bar',
  standalone: true,
  imports: [GameModeBadgeComponent, MultiplierBadgeComponent, HlmButton, LucideAngularModule],
  template: `
    <div class="score-bar">
      <!-- Main row -->
      <div class="score-row main-row">
        <!-- Team 1 Score -->
        <div class="team-pill" [class.team1]="true" [class.my-team]="myTeam() === 'Team1'">
          <span class="team-label">{{ team1Label() }}</span>
          <span class="match-points">{{ team1MatchPoints() }}</span>
        </div>

        <!-- Center info -->
        <div class="center-info">
          @if (gameMode()) {
            <div class="mode-glow">
              <app-game-mode-badge [mode]="gameMode()" />
              <app-multiplier-badge [multiplier]="multiplier()" />
            </div>
          }
          @if (dealNumber() > 0) {
            <span class="deal-number">Deal {{ dealNumber() }}</span>
          } @else {
            <span class="room-name">{{ room()?.name }}</span>
          }
        </div>

        <!-- Team 2 Score -->
        <div class="team-pill" [class.team2]="true" [class.my-team]="myTeam() === 'Team2'">
          <span class="team-label">{{ team2Label() }}</span>
          <span class="match-points">{{ team2MatchPoints() }}</span>
        </div>

        <!-- Leave button -->
        <button
          hlmBtn
          variant="ghost"
          size="sm"
          class="menu-button"
          (click)="leaveTable.emit()"
          title="Leave Table"
        >
          <i-lucide [img]="LogOutIcon" [size]="18" [strokeWidth]="2"></i-lucide>
        </button>
      </div>

      <!-- Deal card points progress -->
      @if (showDealPoints()) {
        <div class="score-row deal-row">
          <div class="progress-section" [class.my-team]="myTeam() === 'Team1'">
            <div class="progress-bar">
              <div
                class="progress-fill team1-fill"
                [style.width.%]="team1ProgressPercent()"
              ></div>
              <div class="threshold-marker" [style.left.%]="thresholdPercent()"></div>
            </div>
            <span class="points-label">{{ team1CardPoints() }}</span>
          </div>
          <div class="progress-section" [class.my-team]="myTeam() === 'Team2'">
            <div class="progress-bar">
              <div
                class="progress-fill team2-fill"
                [style.width.%]="team2ProgressPercent()"
              ></div>
              <div class="threshold-marker" [style.left.%]="thresholdPercent()"></div>
            </div>
            <span class="points-label">{{ team2CardPoints() }}</span>
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
      gap: 0.75rem;
    }

    .main-row {
      min-height: 2.5rem;
    }

    .team-pill {
      display: flex;
      flex-direction: column;
      align-items: center;
      min-width: 5rem;
      padding: 0.25rem 0.75rem;
      border-radius: 9999px;
      background: hsl(var(--muted) / 0.5);
      border: 1px solid hsl(var(--border));
      transition: border-color 0.2s ease, background 0.2s ease;
    }

    .team-pill.team1 {
      border-color: hsl(var(--team1) / 0.4);
    }

    .team-pill.team2 {
      border-color: hsl(var(--team2) / 0.4);
    }

    .team-pill.my-team {
      background: hsl(var(--primary) / 0.1);
    }

    .team-label {
      font-size: 0.6rem;
      text-transform: uppercase;
      color: hsl(var(--muted-foreground));
      letter-spacing: 0.05em;
      line-height: 1;
    }

    .team-pill.team1 .match-points {
      color: hsl(var(--team1));
    }

    .team-pill.team2 .match-points {
      color: hsl(var(--team2));
    }

    .match-points {
      font-size: 1.25rem;
      font-weight: 700;
      line-height: 1.2;
    }

    .center-info {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex: 1;
      justify-content: center;
    }

    .mode-glow {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      padding: 0.125rem 0.5rem;
      border-radius: 0.375rem;
      background: hsl(var(--gold) / 0.08);
      border: 1px solid hsl(var(--gold) / 0.15);
    }

    .deal-number,
    .room-name {
      font-size: 0.875rem;
      color: hsl(var(--muted-foreground));
    }

    .menu-button {
      flex-shrink: 0;
    }

    /* Deal points progress */
    .deal-row {
      margin-top: 0.375rem;
      gap: 1rem;
    }

    .progress-section {
      flex: 1;
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }

    .progress-bar {
      flex: 1;
      height: 4px;
      background: hsl(var(--muted));
      border-radius: 2px;
      position: relative;
      overflow: visible;
    }

    .progress-fill {
      height: 100%;
      border-radius: 2px;
      transition: width 0.4s ease;
    }

    .team1-fill {
      background: hsl(var(--team1));
    }

    .team2-fill {
      background: hsl(var(--team2));
    }

    .threshold-marker {
      position: absolute;
      top: -3px;
      width: 2px;
      height: 10px;
      background: hsl(var(--foreground) / 0.3);
      border-radius: 1px;
      transform: translateX(-50%);
    }

    .points-label {
      font-size: 0.7rem;
      color: hsl(var(--muted-foreground));
      min-width: 2rem;
      text-align: right;
      font-variant-numeric: tabular-nums;
    }
  `],
})
export class ScoreBarComponent {
  readonly LogOutIcon = LogOut;

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
    return this.gameMode() !== null;
  });

  readonly team1Label = computed(() => {
    return this.myTeam() === 'Team1' ? 'Your Team' : this.myTeam() === 'Team2' ? 'Opponents' : 'Team 1';
  });

  readonly team2Label = computed(() => {
    return this.myTeam() === 'Team2' ? 'Your Team' : this.myTeam() === 'Team1' ? 'Opponents' : 'Team 2';
  });

  readonly totalPoints = computed(() => {
    const mode = this.gameMode();
    if (!mode) return 162;
    if (mode === GameMode.ToutAs) return 258;
    if (mode === GameMode.SansAs) return 130;
    return 162;
  });

  readonly thresholdPercent = computed(() => {
    const mode = this.gameMode();
    if (!mode) return 50;
    if (mode === GameMode.ToutAs) return (129 / 258) * 100;
    if (mode === GameMode.SansAs) return (65 / 130) * 100;
    return (82 / 162) * 100;
  });

  readonly team1ProgressPercent = computed(() => {
    const total = this.totalPoints();
    return Math.min((this.team1CardPoints() / total) * 100, 100);
  });

  readonly team2ProgressPercent = computed(() => {
    const total = this.totalPoints();
    return Math.min((this.team2CardPoints() / total) * 100, 100);
  });
}
