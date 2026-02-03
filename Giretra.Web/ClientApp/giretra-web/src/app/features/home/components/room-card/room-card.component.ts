import { Component, input, output, computed } from '@angular/core';
import { RoomResponse, PlayerSlot } from '../../../../core/services/api.service';
import { HlmButton } from '@spartan-ng/helm/button';

@Component({
  selector: 'app-room-card',
  standalone: true,
  imports: [HlmButton],
  template: `
    <div class="room-card" [class.completed]="room().status === 'Completed'">
      <div class="room-header">
        <span class="room-name">{{ room().name }}</span>
        <div class="room-meta">
          <span class="player-count">{{ room().playerCount }}/4</span>
          <span class="status-badge" [class]="statusClass()">
            {{ room().status }}
          </span>
        </div>
      </div>

      <div class="room-footer">
        <div class="seat-dots">
          @for (slot of room().playerSlots; track slot.position) {
            <span
              class="seat-dot"
              [class.occupied]="slot.isOccupied"
              [class.ai]="slot.isAi"
              [title]="getSlotTitle(slot)"
            ></span>
          }
        </div>

        @if (room().status !== 'Completed') {
          <button
            hlmBtn
            [variant]="canJoin() ? 'default' : 'secondary'"
            size="sm"
            (click)="handleAction()"
          >
            {{ actionLabel() }}
          </button>
        }
      </div>
    </div>
  `,
  styles: [`
    .room-card {
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 0.5rem;
      padding: 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      transition: border-color 0.15s ease;
    }

    .room-card:hover {
      border-color: hsl(var(--primary));
    }

    .room-card.completed {
      opacity: 0.5;
    }

    .room-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
    }

    .room-name {
      font-weight: 600;
      font-size: 1rem;
      color: hsl(var(--foreground));
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .room-meta {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-shrink: 0;
    }

    .player-count {
      font-size: 0.875rem;
      color: hsl(var(--muted-foreground));
    }

    .status-badge {
      font-size: 0.75rem;
      font-weight: 500;
      padding: 0.125rem 0.375rem;
      border-radius: 0.25rem;
      text-transform: uppercase;
    }

    .status-badge.waiting {
      background: hsl(var(--primary) / 0.2);
      color: hsl(var(--primary));
    }

    .status-badge.playing {
      background: hsl(var(--accent) / 0.2);
      color: hsl(var(--accent));
    }

    .status-badge.completed {
      background: hsl(var(--muted));
      color: hsl(var(--muted-foreground));
    }

    .room-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .seat-dots {
      display: flex;
      gap: 0.375rem;
    }

    .seat-dot {
      width: 0.75rem;
      height: 0.75rem;
      border-radius: 50%;
      background: hsl(var(--muted));
      border: 2px solid hsl(var(--border));
    }

    .seat-dot.occupied {
      background: hsl(var(--primary));
      border-color: hsl(var(--primary));
    }

    .seat-dot.ai {
      background: hsl(var(--accent));
      border-color: hsl(var(--accent));
    }
  `],
})
export class RoomCardComponent {
  readonly room = input.required<RoomResponse>();

  readonly joinClicked = output<void>();
  readonly watchClicked = output<void>();

  readonly canJoin = computed(() => {
    const r = this.room();
    return r.status === 'Waiting' && r.playerCount < 4;
  });

  readonly actionLabel = computed(() => {
    return this.canJoin() ? 'Join' : 'Watch';
  });

  readonly statusClass = computed(() => {
    return this.room().status.toLowerCase();
  });

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
