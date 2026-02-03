import { Component, input, computed } from '@angular/core';
import { PlayerPosition } from '../../../api/generated/signalr-types.generated';
import { getTeam } from '../../../core/utils/position-utils';

@Component({
  selector: 'app-player-seat',
  standalone: true,
  template: `
    <div
      class="player-seat"
      [class.active-turn]="isActiveTurn()"
      [class.team1]="team() === 'Team1'"
      [class.team2]="team() === 'Team2'"
      [class.empty]="!isOccupied()"
    >
      <!-- Player info -->
      <div class="player-info">
        <span class="player-name">
          @if (isOccupied()) {
            {{ playerName() }}
            @if (isAi()) {
              <span class="ai-badge" title="AI Player">AI</span>
            }
          } @else {
            <span class="waiting-text">Waiting...</span>
          }
        </span>
      </div>

      <!-- Card backs (for opponents) -->
      @if (showCardBacks() && cardCount() > 0) {
        <div class="card-backs">
          @for (i of cardBackArray(); track i) {
            <div class="mini-card-back"></div>
          }
        </div>
      }

      <!-- Tricks won indicator -->
      @if (tricksWon() > 0) {
        <div class="tricks-won">
          @for (i of tricksArray(); track i) {
            <span class="trick-dot"></span>
          }
        </div>
      }

      <!-- Active turn indicator -->
      @if (isActiveTurn()) {
        <div class="turn-ring"></div>
      }

      <!-- Dealer chip -->
      @if (isDealer()) {
        <div class="dealer-chip" title="Dealer">D</div>
      }
    </div>
  `,
  styles: [`
    .player-seat {
      position: relative;
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 0.5rem;
      border-radius: 0.5rem;
      background: hsl(var(--card));
      border: 2px solid transparent;
      min-width: 5rem;
      transition: border-color 0.2s ease, box-shadow 0.2s ease;
    }

    .player-seat.team1 {
      border-left: 3px solid hsl(210, 70%, 50%);
    }

    .player-seat.team2 {
      border-left: 3px solid hsl(142, 50%, 45%);
    }

    .player-seat.empty {
      opacity: 0.6;
      border-style: dashed;
      border-color: hsl(var(--border));
    }

    .player-seat.active-turn {
      border-color: hsl(var(--accent));
      box-shadow: 0 0 12px hsl(var(--accent) / 0.4);
    }

    .player-info {
      text-align: center;
    }

    .player-name {
      font-size: 0.875rem;
      font-weight: 600;
      color: hsl(var(--foreground));
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }

    .waiting-text {
      font-style: italic;
      color: hsl(var(--muted-foreground));
      font-weight: 400;
    }

    .ai-badge {
      font-size: 0.625rem;
      background: hsl(var(--muted));
      color: hsl(var(--muted-foreground));
      padding: 0.0625rem 0.25rem;
      border-radius: 0.25rem;
      text-transform: uppercase;
    }

    .card-backs {
      display: flex;
      margin-top: 0.375rem;
      gap: 2px;
    }

    .mini-card-back {
      width: 12px;
      height: 18px;
      background: hsl(220, 20%, 35%);
      border: 1px solid hsl(220, 20%, 45%);
      border-radius: 2px;
    }

    .tricks-won {
      display: flex;
      gap: 0.25rem;
      margin-top: 0.375rem;
    }

    .trick-dot {
      width: 6px;
      height: 6px;
      background: hsl(var(--primary));
      border-radius: 50%;
    }

    .turn-ring {
      position: absolute;
      inset: -4px;
      border: 2px solid hsl(var(--accent));
      border-radius: 0.625rem;
      animation: pulse 1.5s ease-in-out infinite;
    }

    @keyframes pulse {
      0%, 100% {
        opacity: 1;
      }
      50% {
        opacity: 0.5;
      }
    }

    .dealer-chip {
      position: absolute;
      top: -8px;
      right: -8px;
      width: 20px;
      height: 20px;
      background: hsl(var(--accent));
      color: hsl(var(--accent-foreground));
      border-radius: 50%;
      font-size: 0.625rem;
      font-weight: 700;
      display: flex;
      align-items: center;
      justify-content: center;
    }
  `],
})
export class PlayerSeatComponent {
  readonly position = input.required<PlayerPosition>();
  readonly playerName = input<string | null>(null);
  readonly cardCount = input<number>(0);
  readonly tricksWon = input<number>(0);
  readonly isAi = input<boolean>(false);
  readonly isActiveTurn = input<boolean>(false);
  readonly isDealer = input<boolean>(false);
  readonly isOccupied = input<boolean>(false);
  readonly showCardBacks = input<boolean>(true);

  readonly team = computed(() => getTeam(this.position()));

  readonly cardBackArray = computed(() => {
    const count = Math.min(this.cardCount(), 8);
    return Array.from({ length: count }, (_, i) => i);
  });

  readonly tricksArray = computed(() => {
    return Array.from({ length: this.tricksWon() }, (_, i) => i);
  });
}
