import { Component, input, output } from '@angular/core';
import { RoomResponse } from '../../../../core/services/api.service';
import { RoomCardComponent } from '../room-card/room-card.component';

@Component({
  selector: 'app-room-list',
  standalone: true,
  imports: [RoomCardComponent],
  template: `
    @if (loading()) {
      <div class="loading-state">
        <div class="spinner"></div>
        <span>Loading rooms...</span>
      </div>
    } @else if (rooms().length === 0) {
      <div class="empty-state">
        <p>No rooms yet</p>
        <p class="hint">Create one to get started</p>
      </div>
    } @else {
      <div class="room-list">
        @for (room of rooms(); track room.roomId) {
          <app-room-card
            [room]="room"
            (joinClicked)="joinRoom.emit(room)"
            (watchClicked)="watchRoom.emit(room)"
          />
        }
      </div>
    }
  `,
  styles: [`
    .room-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .loading-state,
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 3rem 1rem;
      color: hsl(var(--muted-foreground));
      text-align: center;
    }

    .spinner {
      width: 2rem;
      height: 2rem;
      border: 3px solid hsl(var(--border));
      border-top-color: hsl(var(--primary));
      border-radius: 50%;
      animation: spin 1s linear infinite;
      margin-bottom: 1rem;
    }

    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }

    .empty-state p {
      margin: 0;
      font-size: 1rem;
    }

    .empty-state .hint {
      font-size: 0.875rem;
      margin-top: 0.5rem;
    }
  `],
})
export class RoomListComponent {
  readonly rooms = input<RoomResponse[]>([]);
  readonly loading = input<boolean>(false);

  readonly joinRoom = output<RoomResponse>();
  readonly watchRoom = output<RoomResponse>();
}
