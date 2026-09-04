import { Component, input, output, computed, signal, effect, inject } from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { RoomResponse, PlayerSlot } from '../../../../core/services/api.service';
import { PlayerPosition, SeatAccessMode } from '../../../../api/generated/signalr-types.generated';
import { getPositionTranslationKey } from '../../../../core/utils/position-utils';

interface SeatView {
  area: string;
  position: PlayerPosition;
  team: 1 | 2;
  labelKey: string;
  slot: PlayerSlot;
}

@Component({
  selector: 'app-room-card',
  standalone: true,
  imports: [TranslocoDirective, ButtonModule, TagModule],
  template: `
    <ng-container *transloco="let t">
    <article class="room" [class.completed]="room().status === 'Completed'" [class.selecting]="selecting()">
      <header class="room-head">
        <div class="room-title">
          <span class="room-name" [title]="room().name">{{ room().name }}</span>
          <span class="room-meta">
            <span><i class="pi pi-clock"></i>{{ room().turnTimerSeconds }}s</span>
            @if (room().isRanked) {
              <span><i class="pi pi-trophy"></i>{{ t('createForm.rated') }}</span>
            }
            @if (room().watcherCount > 0) {
              <span><i class="pi pi-eye"></i>{{ room().watcherCount }}</span>
            }
          </span>
        </div>
        <p-tag [value]="t(statusKey())" [severity]="statusSeverity()" [rounded]="true" />
      </header>

      <div class="compass">
        @for (seat of seats(); track seat.area) {
          <button
            type="button"
            class="seat"
            [style.grid-area]="seat.area"
            [class.occupied]="seat.slot.isOccupied"
            [class.ai]="seat.slot.isAi"
            [class.me]="seat.slot.isCurrentUser"
            [class.locked]="!seat.slot.isOccupied && isInviteOnly(seat.slot)"
            [class.team1]="seat.team === 1"
            [class.team2]="seat.team === 2"
            [class.pickable]="selecting() && canPick(seat.slot)"
            [disabled]="!selecting() || !canPick(seat.slot)"
            [title]="getSlotTitle(seat.slot)"
            (click)="selectSeat(seat.position)"
          >
            <span class="seat-avatar">
              @if (seat.slot.isOccupied) {
                @if (seat.slot.isAi) {
                  <i class="pi pi-microchip"></i>
                } @else {
                  {{ getInitial(seat.slot) }}
                }
              } @else if (isInviteOnly(seat.slot)) {
                <i class="pi pi-lock"></i>
              } @else if (selecting()) {
                <i class="pi pi-plus"></i>
              }
            </span>
            @if (selecting()) {
              <span class="seat-label">
                @if (seat.slot.isOccupied) {
                  {{ seat.slot.isAi ? (seat.slot.aiDisplayName ?? 'AI') : seat.slot.playerName }}
                } @else if (isInviteOnly(seat.slot)) {
                  {{ t('roomCard.inviteOnly') }}
                } @else {
                  {{ t(seat.labelKey) }}
                }
              </span>
              <span class="seat-team">{{ t(seat.team === 1 ? 'teams.yourTeam' : 'teams.opponents') }}</span>
            }
          </button>
        }
        <div class="compass-center">
          @if (selecting()) {
            <i class="pi pi-arrows-alt"></i>
          } @else {
            <span class="count">{{ room().playerCount }}<small>/4</small></span>
          }
        </div>
      </div>

      <footer class="room-foot">
        @if (selecting()) {
          <span class="foot-hint">{{ t('roomCard.chooseYourSeat') }}</span>
          <p-button [label]="t('common.cancel')" severity="secondary" [text]="true" size="small" (onClick)="cancelSelection()" />
        } @else if (room().status === 'Completed') {
          <span class="foot-hint">{{ t('roomCard.finished') }}</span>
        } @else if (canRejoin()) {
          <p-button [label]="t('roomCard.rejoin')" icon="pi pi-refresh" severity="warn" size="small" (onClick)="rejoinClicked.emit()" />
        } @else if (isSeated()) {
          @if (room().status === 'Playing') {
            <p-button [label]="t('roomCard.return')" icon="pi pi-refresh" size="small" (onClick)="rejoinClicked.emit()" />
          } @else {
            <p-tag [value]="t('roomCard.seated')" severity="success" icon="pi pi-check" [rounded]="true" />
          }
        } @else if (canJoin()) {
          <p-button [label]="t('roomCard.join')" icon="pi pi-sign-in" size="small" (onClick)="handleAction()" />
        } @else {
          <p-button [label]="t('roomCard.watch')" icon="pi pi-eye" severity="secondary" [outlined]="true" size="small" (onClick)="handleAction()" />
        }
      </footer>
    </article>
    </ng-container>
  `,
  styles: [`
    :host { display:block; }
    .room { display:flex; flex-direction:column; gap:0.75rem; height:100%; padding:1rem 1.25rem; border:1px solid var(--surface-border); border-radius:1rem; background:var(--p-surface-900); transition:border-color var(--transition-duration), box-shadow var(--transition-duration); }
    .room:hover { border-color:var(--p-surface-600); box-shadow:0 6px 24px rgba(0,0,0,0.18); }
    .room.completed { opacity:0.7; }
    .room-head { display:flex; align-items:flex-start; justify-content:space-between; gap:0.75rem; }
    .room-title { display:flex; flex-direction:column; gap:0.25rem; min-width:0; }
    .room-name { font-weight:600; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .room-meta { display:flex; flex-wrap:wrap; gap:0.625rem; font-size:0.75rem; color:var(--text-color-secondary); }
    .room-meta i { font-size:0.6875rem; margin-right:0.25rem; }

    .compass { display:grid; grid-template-columns:1fr auto 1fr; grid-template-areas:". n ." "w c e" ". s ."; align-items:center; justify-items:center; gap:0.25rem 0.5rem; padding:0.25rem 0; flex:1; }
    .compass-center { grid-area:c; display:flex; align-items:center; justify-content:center; min-width:3rem; color:var(--text-color-secondary); }
    .count { font-size:1.125rem; font-weight:700; color:var(--text-color); }
    .count small { font-size:0.75rem; font-weight:500; color:var(--text-color-secondary); }

    .seat { display:flex; flex-direction:column; align-items:center; gap:0.125rem; padding:0.25rem; border:none; background:transparent; color:inherit; border-radius:0.75rem; cursor:default; }
    .seat:disabled { cursor:default; }
    .seat-avatar { display:inline-flex; align-items:center; justify-content:center; width:2rem; height:2rem; border-radius:50%; border:2px dashed var(--p-surface-600); color:var(--text-color-secondary); font-size:0.8125rem; font-weight:700; background:transparent; transition:transform var(--transition-duration), border-color var(--transition-duration); }
    .seat-avatar i { font-size:0.8125rem; }
    .occupied .seat-avatar { border-style:solid; }
    .team1.occupied .seat-avatar { border-color:hsl(var(--team1)); background:hsl(var(--team1) / 0.18); color:hsl(var(--team1)); }
    .team2.occupied .seat-avatar { border-color:hsl(var(--team2)); background:hsl(var(--team2) / 0.18); color:hsl(var(--team2)); }
    .ai .seat-avatar { color:var(--text-color); }
    .me .seat-avatar { box-shadow:0 0 0 2px var(--p-surface-900), 0 0 0 4px var(--p-primary-color); }
    .locked .seat-avatar { border-style:solid; border-color:var(--p-surface-700); background:var(--p-surface-800); }
    .pickable { cursor:pointer; }
    .pickable .seat-avatar { border-color:var(--p-primary-400); color:var(--p-primary-400); }
    .pickable:hover .seat-avatar { transform:scale(1.08); background:color-mix(in srgb, var(--p-primary-color) 18%, transparent); }
    .pickable:focus-visible { outline:2px solid var(--p-primary-color); outline-offset:2px; }
    .seat-label { font-size:0.6875rem; font-weight:500; max-width:6rem; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .seat-team { font-size:0.625rem; color:var(--text-color-secondary); }
    .selecting .compass { gap:0.375rem 0.5rem; }

    .room-foot { display:flex; align-items:center; justify-content:space-between; gap:0.5rem; padding-top:0.5rem; border-top:1px solid var(--surface-border); min-height:2.75rem; }
    .room-foot > :only-child { margin-left:auto; }
    .foot-hint { font-size:0.8125rem; color:var(--text-color-secondary); }
  `],
})
export class RoomCardComponent {
  private readonly transloco = inject(TranslocoService);



  readonly room = input.required<RoomResponse>();

  readonly joinClicked = output<PlayerPosition>();
  readonly watchClicked = output<void>();
  readonly rejoinClicked = output<void>();

  readonly selecting = signal(false);

  readonly isSeated = computed(() => {
    return this.room().playerSlots.some(s => s.isCurrentUser);
  });

  readonly canJoin = computed(() => {
    const r = this.room();
    if (r.status !== 'Waiting') return false;
    if (this.isSeated()) return false;
    // Only count public, unoccupied seats as joinable
    return r.playerSlots.some(s => !s.isOccupied && s.accessMode === SeatAccessMode.Public);
  });

  readonly canRejoin = computed(() => {
    const r = this.room();
    return r.status === 'Playing' && !!r.isDisconnectedPlayer;
  });

  readonly statusKey = computed(() => `roomCard.status${this.room().status}`);

  readonly statusSeverity = computed<'success' | 'warn' | 'secondary'>(() => {
    switch (this.room().status) {
      case 'Waiting': return 'success';
      case 'Playing': return 'warn';
      default: return 'secondary';
    }
  });

  // Slots are ordered Bottom, Left, Top, Right; Bottom + Top form the viewer's team.
  readonly seats = computed<SeatView[]>(() => [
    { area: 'n', position: PlayerPosition.Top, team: 1, labelKey: 'positions.top', slot: this.getNorth() },
    { area: 'w', position: PlayerPosition.Left, team: 2, labelKey: 'positions.left', slot: this.getWest() },
    { area: 'e', position: PlayerPosition.Right, team: 2, labelKey: 'positions.right', slot: this.getEast() },
    { area: 's', position: PlayerPosition.Bottom, team: 1, labelKey: 'positions.bottom', slot: this.getSouth() },
  ]);

  private readonly availableSeats = computed(() => {
    return this.room().playerSlots.filter(s => !s.isOccupied && s.accessMode === SeatAccessMode.Public);
  });

  constructor() {
    // Auto-cancel selection if room becomes full (e.g. via poll update)
    effect(() => {
      if (this.selecting() && this.availableSeats().length === 0) {
        this.selecting.set(false);
      }
    });
  }

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

  isInviteOnly(slot: PlayerSlot): boolean {
    return slot.accessMode === SeatAccessMode.InviteOnly;
  }

  canPick(slot: PlayerSlot): boolean {
    return !slot.isOccupied && !this.isInviteOnly(slot);
  }

  getSlotTitle(slot: PlayerSlot): string {
    const pos = this.transloco.translate(getPositionTranslationKey(slot.position));
    if (!slot.isOccupied) return `${pos} (Open)`;
    if (slot.isAi) return `${pos}: AI`;
    return `${pos}: ${slot.playerName}`;
  }

  handleAction(): void {
    if (this.canJoin()) {
      const available = this.availableSeats();
      if (available.length === 1) {
        // Only 1 seat left — skip picker, join directly
        this.joinClicked.emit(available[0].position);
      } else {
        this.selecting.set(true);
      }
    } else {
      this.watchClicked.emit();
    }
  }

  selectSeat(position: PlayerPosition): void {
    this.selecting.set(false);
    this.joinClicked.emit(position);
  }

  cancelSelection(): void {
    this.selecting.set(false);
  }
}
