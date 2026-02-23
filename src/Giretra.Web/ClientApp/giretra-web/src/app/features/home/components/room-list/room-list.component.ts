import { Component, input, output } from '@angular/core';
import { RoomResponse } from '../../../../core/services/api.service';
import { PlayerPosition } from '../../../../api/generated/signalr-types.generated';
import { RoomCardComponent } from '../room-card/room-card.component';
import { LucideAngularModule, Layers } from 'lucide-angular';

export interface JoinRoomEvent {
  room: RoomResponse;
  position: PlayerPosition;
}

@Component({
  selector: 'app-room-list',
  standalone: true,
  imports: [RoomCardComponent, LucideAngularModule],
  template: `
    @if (loading()) {
      <div class="state-container">
        <div class="spinner"></div>
        <span class="state-text">Looking for tables...</span>
      </div>
    } @else if (rooms().length === 0) {
      <div class="state-container empty">
        <div class="empty-icon">
          <i-lucide [img]="LayersIcon" [size]="28" [strokeWidth]="1.5"></i-lucide>
        </div>
        <p class="state-text">No tables open</p>
        <p class="state-hint">Create one and invite friends to play</p>
      </div>
    } @else {
      <div class="room-grid">
        @for (room of rooms(); track room.roomId) {
          <app-room-card
            [room]="room"
            (joinClicked)="joinRoom.emit({ room: room, position: $event })"
            (watchClicked)="watchRoom.emit(room)"
            (rejoinClicked)="rejoinRoom.emit(room)"
          />
        }
      </div>
    }
  `,
  styles: [`
    .room-grid {
      display: grid;
      grid-template-columns: 1fr;
      gap: 0.75rem;
    }

    @media (min-width: 540px) {
      .room-grid {
        grid-template-columns: repeat(2, 1fr);
      }
    }

    .state-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 3rem 1rem;
      color: hsl(var(--muted-foreground));
      text-align: center;
    }

    .spinner {
      width: 1.75rem;
      height: 1.75rem;
      border: 2.5px solid hsl(var(--border));
      border-top-color: hsl(var(--primary));
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
      margin-bottom: 0.75rem;
    }

    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }

    .empty-icon {
      width: 3.5rem;
      height: 3.5rem;
      border-radius: 50%;
      background: hsl(var(--muted) / 0.3);
      display: flex;
      align-items: center;
      justify-content: center;
      margin-bottom: 0.75rem;
      color: hsl(var(--muted-foreground));
    }

    .state-text {
      margin: 0;
      font-size: 0.9375rem;
      font-weight: 500;
    }

    .state-hint {
      margin: 0.25rem 0 0 0;
      font-size: 0.8125rem;
      opacity: 0.7;
    }
  `],
})
export class RoomListComponent {
  readonly LayersIcon = Layers;

  readonly rooms = input<RoomResponse[]>([]);
  readonly loading = input<boolean>(false);

  readonly joinRoom = output<JoinRoomEvent>();
  readonly watchRoom = output<RoomResponse>();
  readonly rejoinRoom = output<RoomResponse>();
}
