import { Component, input, output, computed } from '@angular/core';
import { RoomResponse, PlayerSlot } from '../../../../core/services/api.service';
import { HlmButton } from '@spartan-ng/helm/button';
import { LucideAngularModule, LogIn, Eye, Bot } from 'lucide-angular';

@Component({
  selector: 'app-room-card',
  standalone: true,
  imports: [HlmButton, LucideAngularModule],
  template: `
    <div class="room-card" [class.completed]="room().status === 'Completed'">
      <!-- Status strip -->
      <div class="status-strip" [class]="statusClass()"></div>

      <div class="card-body">
        <!-- Top row: name + status -->
        <div class="card-top">
          <span class="room-name">{{ room().name }}</span>
          <span class="status-badge" [class]="statusClass()">
            {{ room().status }}
          </span>
        </div>

        <!-- Middle: compass seat preview -->
        <div class="card-middle">
          <div class="compass">
            <!-- Top seat (North) -->
            <div class="compass-seat north" [class.occupied]="getNorth().isOccupied" [class.ai]="getNorth().isAi">
              @if (getNorth().isOccupied) {
                @if (getNorth().isAi) {
                  <i-lucide [img]="BotIcon" [size]="10" [strokeWidth]="2"></i-lucide>
                } @else {
                  <span class="seat-letter">{{ getInitial(getNorth()) }}</span>
                }
              }
            </div>
            <!-- Middle row: West, center, East -->
            <div class="compass-row">
              <div class="compass-seat west" [class.occupied]="getWest().isOccupied" [class.ai]="getWest().isAi">
                @if (getWest().isOccupied) {
                  @if (getWest().isAi) {
                    <i-lucide [img]="BotIcon" [size]="10" [strokeWidth]="2"></i-lucide>
                  } @else {
                    <span class="seat-letter">{{ getInitial(getWest()) }}</span>
                  }
                }
              </div>
              <div class="compass-center">
                <span class="player-count">{{ room().playerCount }}/4</span>
              </div>
              <div class="compass-seat east" [class.occupied]="getEast().isOccupied" [class.ai]="getEast().isAi">
                @if (getEast().isOccupied) {
                  @if (getEast().isAi) {
                    <i-lucide [img]="BotIcon" [size]="10" [strokeWidth]="2"></i-lucide>
                  } @else {
                    <span class="seat-letter">{{ getInitial(getEast()) }}</span>
                  }
                }
              </div>
            </div>
            <!-- Bottom seat (South) -->
            <div class="compass-seat south" [class.occupied]="getSouth().isOccupied" [class.ai]="getSouth().isAi">
              @if (getSouth().isOccupied) {
                @if (getSouth().isAi) {
                  <i-lucide [img]="BotIcon" [size]="10" [strokeWidth]="2"></i-lucide>
                } @else {
                  <span class="seat-letter">{{ getInitial(getSouth()) }}</span>
                }
              }
            </div>
          </div>
        </div>

        <!-- Bottom: action -->
        <div class="card-bottom">
          @if (room().status !== 'Completed') {
            <button
              hlmBtn
              [variant]="canJoin() ? 'default' : 'secondary'"
              size="sm"
              class="action-btn"
              (click)="handleAction()"
            >
              @if (canJoin()) {
                <i-lucide [img]="LogInIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                Join
              } @else {
                <i-lucide [img]="EyeIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                Watch
              }
            </button>
          } @else {
            <span class="finished-label">Finished</span>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .room-card {
      position: relative;
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 0.75rem;
      overflow: hidden;
      transition: border-color 0.15s ease, transform 0.15s ease, box-shadow 0.15s ease;
    }

    .room-card:hover {
      border-color: hsl(var(--primary) / 0.5);
      transform: translateY(-2px);
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.2);
    }

    .room-card.completed {
      opacity: 0.45;
      pointer-events: none;
    }

    /* Status strip at top */
    .status-strip {
      height: 3px;
    }

    .status-strip.waiting {
      background: hsl(var(--primary));
    }

    .status-strip.playing {
      background: hsl(var(--gold));
    }

    .status-strip.completed {
      background: hsl(var(--muted));
    }

    .card-body {
      padding: 0.875rem 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    /* Top row */
    .card-top {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.75rem;
    }

    .room-name {
      font-weight: 600;
      font-size: 0.9375rem;
      color: hsl(var(--foreground));
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      min-width: 0;
    }

    .status-badge {
      font-size: 0.625rem;
      font-weight: 600;
      padding: 0.125rem 0.5rem;
      border-radius: 9999px;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      flex-shrink: 0;
    }

    .status-badge.waiting {
      background: hsl(var(--primary) / 0.15);
      color: hsl(var(--primary));
    }

    .status-badge.playing {
      background: hsl(var(--gold) / 0.15);
      color: hsl(var(--gold));
    }

    .status-badge.completed {
      background: hsl(var(--muted) / 0.3);
      color: hsl(var(--muted-foreground));
    }

    /* Compass layout */
    .card-middle {
      display: flex;
      justify-content: center;
    }

    .compass {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.25rem;
    }

    .compass-row {
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }

    .compass-seat {
      width: 1.5rem;
      height: 1.5rem;
      border-radius: 50%;
      border: 1.5px dashed hsl(var(--border));
      display: flex;
      align-items: center;
      justify-content: center;
      background: transparent;
      transition: all 0.15s ease;
    }

    .compass-seat.occupied {
      border-style: solid;
      border-color: hsl(var(--primary));
      background: hsl(var(--primary) / 0.15);
    }

    .compass-seat.ai {
      border-color: hsl(var(--gold));
      background: hsl(var(--gold) / 0.15);
    }

    .compass-seat.ai i-lucide {
      color: hsl(var(--gold));
    }

    .seat-letter {
      font-size: 0.5625rem;
      font-weight: 700;
      color: hsl(var(--primary));
      text-transform: uppercase;
      line-height: 1;
    }

    .compass-center {
      width: 2.5rem;
      height: 1.5rem;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .player-count {
      font-size: 0.6875rem;
      font-weight: 600;
      color: hsl(var(--muted-foreground));
    }

    /* Bottom row */
    .card-bottom {
      display: flex;
      justify-content: flex-end;
      align-items: center;
    }

    .action-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      font-size: 0.8125rem;
    }

    .finished-label {
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      font-style: italic;
    }
  `],
})
export class RoomCardComponent {
  readonly LogInIcon = LogIn;
  readonly EyeIcon = Eye;
  readonly BotIcon = Bot;

  readonly room = input.required<RoomResponse>();

  readonly joinClicked = output<void>();
  readonly watchClicked = output<void>();

  readonly canJoin = computed(() => {
    const r = this.room();
    return r.status === 'Waiting' && r.playerCount < 4;
  });

  readonly statusClass = computed(() => {
    return this.room().status.toLowerCase();
  });

  // Compass position helpers — slots are ordered Bottom, Left, Top, Right
  private getSlot(index: number): PlayerSlot {
    return this.room().playerSlots[index] ?? { position: '', isOccupied: false, isAi: false, playerName: null };
  }

  getSouth(): PlayerSlot { return this.getSlot(0); }
  getWest(): PlayerSlot { return this.getSlot(1); }
  getNorth(): PlayerSlot { return this.getSlot(2); }
  getEast(): PlayerSlot { return this.getSlot(3); }

  getInitial(slot: PlayerSlot): string {
    return slot.playerName ? slot.playerName.charAt(0).toUpperCase() : '?';
  }

  getSlotTitle(slot: PlayerSlot): string {
    if (!slot.isOccupied) return `${slot.position} (Open)`;
    if (slot.isAi) return `${slot.position}: AI`;
    return `${slot.position}: ${slot.playerName}`;
  }

  handleAction(): void {
    if (this.canJoin()) {
      this.joinClicked.emit();
    } else {
      this.watchClicked.emit();
    }
  }
}
