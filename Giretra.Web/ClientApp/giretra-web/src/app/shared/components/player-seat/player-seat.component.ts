import { Component, input, computed } from '@angular/core';
import { PlayerPosition } from '../../../api/generated/signalr-types.generated';
import { getTeam } from '../../../core/utils/position-utils';
import { LucideAngularModule, Layers, Bot, UserPlus } from 'lucide-angular';

@Component({
  selector: 'app-player-seat',
  standalone: true,
  imports: [LucideAngularModule],
  template: `
    <div
      class="player-seat"
      [class.active-turn]="isActiveTurn()"
      [class.team1]="team() === 'Team1'"
      [class.team2]="team() === 'Team2'"
      [class.empty]="!isOccupied()"
    >
      <!-- Avatar circle -->
      <div class="avatar" [class.active-glow]="isActiveTurn()">
        @if (isOccupied()) {
          <span class="avatar-initial">{{ initial() }}</span>
          @if (cardCount() > 0 && showCardBacks()) {
            <span class="card-badge">{{ cardCount() }}</span>
          }
        } @else {
          <i-lucide [img]="UserPlusIcon" [size]="18" [strokeWidth]="1.5" class="empty-icon"></i-lucide>
        }
      </div>

      <!-- Player name -->
      <div class="player-info">
        <span class="player-name">
          @if (isOccupied()) {
            {{ playerName() }}
            @if (isAi()) {
              <i-lucide [img]="BotIcon" [size]="12" [strokeWidth]="2" class="ai-icon"></i-lucide>
            }
          } @else {
            <span class="waiting-text">Open</span>
          }
        </span>
      </div>

      <!-- Tricks won indicator -->
      @if (tricksWon() > 0) {
        <div class="tricks-badge" [class.highlight]="tricksWon() >= 4">
          <i-lucide [img]="LayersIcon" [size]="12" [strokeWidth]="2"></i-lucide>
          <span class="tricks-count">{{ tricksWon() }}</span>
        </div>
      }

      <!-- Active turn glow ring -->
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
      gap: 0.25rem;
      padding: 0.5rem;
      border-radius: 0.75rem;
      background: hsl(var(--card) / 0.8);
      border: 2px solid transparent;
      min-width: 5rem;
      transition: border-color 0.2s ease, box-shadow 0.2s ease;
    }

    .player-seat.empty {
      opacity: 0.5;
      border-style: dashed;
      border-color: hsl(var(--border));
    }

    .player-seat.active-turn.team1 {
      box-shadow: 0 0 16px hsl(var(--team1) / 0.4);
    }

    .player-seat.active-turn.team2 {
      box-shadow: 0 0 16px hsl(var(--team2) / 0.4);
    }

    /* Avatar */
    .avatar {
      position: relative;
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 50%;
      background: hsl(var(--muted));
      display: flex;
      align-items: center;
      justify-content: center;
      border: 2px solid transparent;
      transition: box-shadow 0.3s ease;
    }

    .team1 .avatar {
      border-color: hsl(var(--team1));
    }

    .team2 .avatar {
      border-color: hsl(var(--team2));
    }

    .avatar.active-glow {
      animation: glowPulse 2s ease-in-out infinite;
    }

    .team1 .avatar.active-glow {
      box-shadow: 0 0 12px hsl(var(--team1) / 0.5);
    }

    .team2 .avatar.active-glow {
      box-shadow: 0 0 12px hsl(var(--team2) / 0.5);
    }

    @keyframes glowPulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.7; }
    }

    .avatar-initial {
      font-size: 1rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      text-transform: uppercase;
    }

    .empty-icon {
      color: hsl(var(--muted-foreground));
    }

    .card-badge {
      position: absolute;
      top: -4px;
      right: -6px;
      min-width: 1.125rem;
      height: 1.125rem;
      padding: 0 0.25rem;
      font-size: 0.625rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      background: hsl(var(--secondary));
      border: 1px solid hsl(var(--border));
      border-radius: 9999px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-variant-numeric: tabular-nums;
    }

    /* Name */
    .player-info {
      text-align: center;
      max-width: 5rem;
      overflow: hidden;
    }

    .player-name {
      font-size: 0.75rem;
      font-weight: 600;
      color: hsl(var(--foreground));
      display: flex;
      align-items: center;
      gap: 0.2rem;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .waiting-text {
      font-style: italic;
      color: hsl(var(--muted-foreground));
      font-weight: 400;
    }

    .ai-icon {
      color: hsl(var(--muted-foreground));
      flex-shrink: 0;
    }

    /* Tricks */
    .tricks-badge {
      display: flex;
      align-items: center;
      gap: 0.2rem;
      padding: 0.0625rem 0.375rem;
      background: hsl(var(--muted));
      border-radius: 9999px;
      border: 1px solid hsl(var(--border));
    }

    .tricks-badge.highlight {
      background: hsl(var(--primary) / 0.2);
      border-color: hsl(var(--primary) / 0.5);
    }

    .tricks-badge.highlight i-lucide {
      color: hsl(var(--primary));
    }

    .tricks-count {
      font-size: 0.6875rem;
      font-weight: 700;
      color: hsl(var(--foreground));
      font-variant-numeric: tabular-nums;
    }

    .tricks-badge.highlight .tricks-count {
      color: hsl(var(--primary));
    }

    /* Turn ring */
    .turn-ring {
      position: absolute;
      inset: -4px;
      border-radius: 0.875rem;
      pointer-events: none;
    }

    .team1 .turn-ring {
      border: 2px solid hsl(var(--team1) / 0.6);
    }

    .team2 .turn-ring {
      border: 2px solid hsl(var(--team2) / 0.6);
    }

    /* Dealer chip */
    .dealer-chip {
      position: absolute;
      top: -6px;
      right: -6px;
      width: 18px;
      height: 18px;
      background: hsl(var(--gold));
      color: hsl(220, 20%, 10%);
      border-radius: 50%;
      font-size: 0.5625rem;
      font-weight: 700;
      display: flex;
      align-items: center;
      justify-content: center;
    }
  `],
})
export class PlayerSeatComponent {
  readonly LayersIcon = Layers;
  readonly BotIcon = Bot;
  readonly UserPlusIcon = UserPlus;

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

  readonly initial = computed(() => {
    const name = this.playerName();
    if (!name) return '?';
    return name.charAt(0).toUpperCase();
  });

  readonly cardBackArray = computed(() => {
    const count = Math.min(this.cardCount(), 8);
    return Array.from({ length: count }, (_, i) => i);
  });
}
