import { Component, input, output } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { SkeletonModule } from 'primeng/skeleton';
import { RoomResponse } from '../../../../core/services/api.service';
import { PlayerPosition } from '../../../../api/generated/signalr-types.generated';
import { RoomCardComponent } from '../room-card/room-card.component';

export interface JoinRoomEvent {
  room: RoomResponse;
  position: PlayerPosition;
}

@Component({
  selector: 'app-room-list',
  standalone: true,
  imports: [RoomCardComponent, TranslocoDirective, SkeletonModule],
  template: `
    <ng-container *transloco="let t">
      @if (loading()) {
        <div class="room-grid" aria-busy="true" [attr.aria-label]="t('roomList.lookingForTables')">
          @for (i of [0, 1, 2]; track i) {
            <p-skeleton height="12rem" borderRadius="16px" />
          }
        </div>
      } @else if (rooms().length === 0) {
        <div class="g-empty">
          <span class="g-empty-icon"><i class="pi pi-table"></i></span>
          <span class="g-empty-title">{{ t('roomList.noTables') }}</span>
          <span class="g-empty-hint">{{ t('roomList.noTablesHint') }}</span>
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
    </ng-container>
  `,
  styles: [`
    .room-grid { display:grid; grid-template-columns:repeat(auto-fill, minmax(17rem, 1fr)); gap:1rem; }
  `],
})
export class RoomListComponent {
  readonly rooms = input<RoomResponse[]>([]);
  readonly loading = input<boolean>(false);

  readonly joinRoom = output<JoinRoomEvent>();
  readonly watchRoom = output<RoomResponse>();
  readonly rejoinRoom = output<RoomResponse>();
}
