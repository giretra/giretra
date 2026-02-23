import { Component, input, output, computed, signal, effect } from '@angular/core';
import { RoomResponse, PlayerSlot } from '../../../../core/services/api.service';
import { PlayerPosition } from '../../../../api/generated/signalr-types.generated';
import { HlmButton } from '@spartan-ng/helm/button';
import { SeatAccessMode } from '../../../../api/generated/signalr-types.generated';
import { LucideAngularModule, LogIn, Eye, Bot, X, Lock, RotateCcw } from 'lucide-angular';

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
          @if (selecting()) {
            <!-- Seat picker mode -->
            <div class="seat-picker">
              <div class="picker-header">
                <span class="picker-title">Choose your seat</span>
                <button class="picker-cancel" (click)="cancelSelection()">
                  <i-lucide [img]="XIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                </button>
              </div>
              <div class="picker-compass">
                <!-- Top seat (North) -->
                <div class="picker-row">
                  <button
                    class="picker-seat"
                    [class.occupied]="getNorth().isOccupied"
                    [class.ai]="getNorth().isAi"
                    [class.invite-only]="!getNorth().isOccupied && isInviteOnly(getNorth())"
                    [class.team1]="!getNorth().isOccupied && !isInviteOnly(getNorth())"
                    [disabled]="getNorth().isOccupied || isInviteOnly(getNorth())"
                    (click)="selectSeat(PositionTop)"
                  >
                    @if (getNorth().isOccupied) {
                      @if (getNorth().isAi) {
                        <i-lucide [img]="BotIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                        <span class="picker-label">{{ getNorth().aiDisplayName ?? 'AI' }}</span>
                      } @else {
                        <span class="picker-initial">{{ getInitial(getNorth()) }}</span>
                        <span class="picker-label">{{ getNorth().playerName }}</span>
                      }
                    } @else if (isInviteOnly(getNorth())) {
                      <i-lucide [img]="LockIcon" [size]="14" [strokeWidth]="2" class="lock-icon"></i-lucide>
                      <span class="picker-label">Invite only</span>
                    } @else {
                      <span class="picker-pos">Top</span>
                      <span class="picker-team team1">Your Team</span>
                    }
                  </button>
                </div>
                <!-- Middle row: West + center + East -->
                <div class="picker-row middle">
                  <button
                    class="picker-seat"
                    [class.occupied]="getWest().isOccupied"
                    [class.ai]="getWest().isAi"
                    [class.invite-only]="!getWest().isOccupied && isInviteOnly(getWest())"
                    [class.team2]="!getWest().isOccupied && !isInviteOnly(getWest())"
                    [disabled]="getWest().isOccupied || isInviteOnly(getWest())"
                    (click)="selectSeat(PositionLeft)"
                  >
                    @if (getWest().isOccupied) {
                      @if (getWest().isAi) {
                        <i-lucide [img]="BotIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                        <span class="picker-label">{{ getWest().aiDisplayName ?? 'AI' }}</span>
                      } @else {
                        <span class="picker-initial">{{ getInitial(getWest()) }}</span>
                        <span class="picker-label">{{ getWest().playerName }}</span>
                      }
                    } @else if (isInviteOnly(getWest())) {
                      <i-lucide [img]="LockIcon" [size]="14" [strokeWidth]="2" class="lock-icon"></i-lucide>
                      <span class="picker-label">Invite only</span>
                    } @else {
                      <span class="picker-pos">Left</span>
                      <span class="picker-team team2">Opponents</span>
                    }
                  </button>
                  <div class="picker-center">
                    <span class="picker-you">You</span>
                  </div>
                  <button
                    class="picker-seat"
                    [class.occupied]="getEast().isOccupied"
                    [class.ai]="getEast().isAi"
                    [class.invite-only]="!getEast().isOccupied && isInviteOnly(getEast())"
                    [class.team2]="!getEast().isOccupied && !isInviteOnly(getEast())"
                    [disabled]="getEast().isOccupied || isInviteOnly(getEast())"
                    (click)="selectSeat(PositionRight)"
                  >
                    @if (getEast().isOccupied) {
                      @if (getEast().isAi) {
                        <i-lucide [img]="BotIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                        <span class="picker-label">{{ getEast().aiDisplayName ?? 'AI' }}</span>
                      } @else {
                        <span class="picker-initial">{{ getInitial(getEast()) }}</span>
                        <span class="picker-label">{{ getEast().playerName }}</span>
                      }
                    } @else if (isInviteOnly(getEast())) {
                      <i-lucide [img]="LockIcon" [size]="14" [strokeWidth]="2" class="lock-icon"></i-lucide>
                      <span class="picker-label">Invite only</span>
                    } @else {
                      <span class="picker-pos">Right</span>
                      <span class="picker-team team2">Opponents</span>
                    }
                  </button>
                </div>
                <!-- Bottom seat (South) -->
                <div class="picker-row">
                  <button
                    class="picker-seat"
                    [class.occupied]="getSouth().isOccupied"
                    [class.ai]="getSouth().isAi"
                    [class.invite-only]="!getSouth().isOccupied && isInviteOnly(getSouth())"
                    [class.team1]="!getSouth().isOccupied && !isInviteOnly(getSouth())"
                    [disabled]="getSouth().isOccupied || isInviteOnly(getSouth())"
                    (click)="selectSeat(PositionBottom)"
                  >
                    @if (getSouth().isOccupied) {
                      @if (getSouth().isAi) {
                        <i-lucide [img]="BotIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                        <span class="picker-label">{{ getSouth().aiDisplayName ?? 'AI' }}</span>
                      } @else {
                        <span class="picker-initial">{{ getInitial(getSouth()) }}</span>
                        <span class="picker-label">{{ getSouth().playerName }}</span>
                      }
                    } @else if (isInviteOnly(getSouth())) {
                      <i-lucide [img]="LockIcon" [size]="14" [strokeWidth]="2" class="lock-icon"></i-lucide>
                      <span class="picker-label">Invite only</span>
                    } @else {
                      <span class="picker-pos">Bottom</span>
                      <span class="picker-team team1">Your Team</span>
                    }
                  </button>
                </div>
              </div>
            </div>
          } @else {
            <!-- Normal compass preview -->
            <div class="compass">
              <!-- Top seat (North) -->
              <div class="compass-seat north" [class.occupied]="getNorth().isOccupied" [class.ai]="getNorth().isAi" [class.invite-only]="!getNorth().isOccupied && isInviteOnly(getNorth())">
                @if (getNorth().isOccupied) {
                  @if (getNorth().isAi) {
                    <i-lucide [img]="BotIcon" [size]="10" [strokeWidth]="2"></i-lucide>
                  } @else {
                    <span class="seat-letter">{{ getInitial(getNorth()) }}</span>
                  }
                } @else if (isInviteOnly(getNorth())) {
                  <i-lucide [img]="LockIcon" [size]="8" [strokeWidth]="2" class="lock-icon"></i-lucide>
                }
              </div>
              <!-- Middle row: West, center, East -->
              <div class="compass-row">
                <div class="compass-seat west" [class.occupied]="getWest().isOccupied" [class.ai]="getWest().isAi" [class.invite-only]="!getWest().isOccupied && isInviteOnly(getWest())">
                  @if (getWest().isOccupied) {
                    @if (getWest().isAi) {
                      <i-lucide [img]="BotIcon" [size]="10" [strokeWidth]="2"></i-lucide>
                    } @else {
                      <span class="seat-letter">{{ getInitial(getWest()) }}</span>
                    }
                  } @else if (isInviteOnly(getWest())) {
                    <i-lucide [img]="LockIcon" [size]="8" [strokeWidth]="2" class="lock-icon"></i-lucide>
                  }
                </div>
                <div class="compass-center">
                  <span class="player-count">{{ room().playerCount }}/4</span>
                </div>
                <div class="compass-seat east" [class.occupied]="getEast().isOccupied" [class.ai]="getEast().isAi" [class.invite-only]="!getEast().isOccupied && isInviteOnly(getEast())">
                  @if (getEast().isOccupied) {
                    @if (getEast().isAi) {
                      <i-lucide [img]="BotIcon" [size]="10" [strokeWidth]="2"></i-lucide>
                    } @else {
                      <span class="seat-letter">{{ getInitial(getEast()) }}</span>
                    }
                  } @else if (isInviteOnly(getEast())) {
                    <i-lucide [img]="LockIcon" [size]="8" [strokeWidth]="2" class="lock-icon"></i-lucide>
                  }
                </div>
              </div>
              <!-- Bottom seat (South) -->
              <div class="compass-seat south" [class.occupied]="getSouth().isOccupied" [class.ai]="getSouth().isAi" [class.invite-only]="!getSouth().isOccupied && isInviteOnly(getSouth())">
                @if (getSouth().isOccupied) {
                  @if (getSouth().isAi) {
                    <i-lucide [img]="BotIcon" [size]="10" [strokeWidth]="2"></i-lucide>
                  } @else {
                    <span class="seat-letter">{{ getInitial(getSouth()) }}</span>
                  }
                } @else if (isInviteOnly(getSouth())) {
                  <i-lucide [img]="LockIcon" [size]="8" [strokeWidth]="2" class="lock-icon"></i-lucide>
                }
              </div>
            </div>
          }
        </div>

        <!-- Bottom: action -->
        <div class="card-bottom">
          @if (!selecting()) {
            @if (room().status !== 'Completed') {
              @if (canRejoin()) {
                <button
                  hlmBtn
                  variant="default"
                  size="sm"
                  class="action-btn rejoin-btn"
                  (click)="rejoinClicked.emit()"
                >
                  <i-lucide [img]="RotateCcwIcon" [size]="14" [strokeWidth]="2"></i-lucide>
                  Rejoin
                </button>
              } @else if (isSeated()) {
                <span class="seated-label">Seated</span>
              } @else {
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
              }
            } @else {
              <span class="finished-label">Finished</span>
            }
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

    .compass-seat.invite-only {
      border-color: hsl(var(--gold) / 0.5);
      background: hsl(var(--gold) / 0.08);
    }

    .lock-icon {
      color: hsl(var(--gold) / 0.6);
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
      min-height: 2rem;
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

    .seated-label {
      font-size: 0.75rem;
      font-weight: 600;
      color: hsl(var(--primary));
    }

    .rejoin-btn {
      background: hsl(var(--gold) / 0.15);
      color: hsl(var(--gold));
      border: 1px solid hsl(var(--gold) / 0.3);
    }

    .rejoin-btn:hover {
      background: hsl(var(--gold) / 0.25);
    }

    /* ─── Seat Picker ─── */
    .seat-picker {
      width: 100%;
      animation: slideDown 0.2s ease;
    }

    @keyframes slideDown {
      from {
        opacity: 0;
        transform: translateY(-8px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    .picker-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.5rem;
    }

    .picker-title {
      font-size: 0.6875rem;
      font-weight: 600;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .picker-cancel {
      width: 1.5rem;
      height: 1.5rem;
      border-radius: 0.375rem;
      border: none;
      background: transparent;
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.15s ease;
    }

    .picker-cancel:hover {
      background: hsl(var(--muted) / 0.5);
      color: hsl(var(--foreground));
    }

    .picker-compass {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.375rem;
    }

    .picker-row {
      display: flex;
      justify-content: center;
      gap: 0.5rem;
    }

    .picker-row.middle {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }

    .picker-seat {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 0.125rem;
      padding: 0.375rem 0.5rem;
      min-width: 4rem;
      min-height: 2.75rem;
      background: hsl(var(--muted) / 0.15);
      border: 1.5px dashed hsl(var(--border));
      border-radius: 0.5rem;
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      transition: all 0.15s ease;
    }

    .picker-seat:not(:disabled):hover {
      transform: translateY(-1px);
    }

    .picker-seat.team1:not(:disabled):hover {
      border-color: hsl(var(--team1));
      border-style: solid;
      background: hsl(var(--team1) / 0.1);
      box-shadow: 0 0 12px hsl(var(--team1) / 0.2);
    }

    .picker-seat.team2:not(:disabled):hover {
      border-color: hsl(var(--team2));
      border-style: solid;
      background: hsl(var(--team2) / 0.1);
      box-shadow: 0 0 12px hsl(var(--team2) / 0.2);
    }

    .picker-seat.occupied {
      border-style: solid;
      border-color: hsl(var(--primary));
      background: hsl(var(--primary) / 0.1);
      cursor: default;
      opacity: 0.7;
    }

    .picker-seat.ai {
      border-color: hsl(var(--gold));
      background: hsl(var(--gold) / 0.1);
    }

    .picker-seat.invite-only {
      border-color: hsl(var(--gold) / 0.4);
      background: hsl(var(--gold) / 0.05);
      cursor: default;
      opacity: 0.7;
    }

    .picker-seat.ai i-lucide {
      color: hsl(var(--gold));
    }

    .picker-seat:disabled {
      cursor: default;
    }

    .picker-pos {
      font-size: 0.6875rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.02em;
    }

    .picker-team {
      font-size: 0.5625rem;
      font-weight: 500;
    }

    .picker-team.team1 {
      color: hsl(var(--team1));
    }

    .picker-team.team2 {
      color: hsl(var(--team2));
    }

    .picker-initial {
      font-size: 0.6875rem;
      font-weight: 700;
      color: hsl(var(--primary));
      text-transform: uppercase;
      line-height: 1;
    }

    .picker-label {
      font-size: 0.5625rem;
      font-weight: 500;
      color: hsl(var(--muted-foreground));
      max-width: 3.5rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .picker-center {
      width: 2.5rem;
      height: 2.75rem;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .picker-you {
      font-size: 0.625rem;
      font-weight: 700;
      color: hsl(var(--muted-foreground));
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
  `],
})
export class RoomCardComponent {
  readonly LogInIcon = LogIn;
  readonly EyeIcon = Eye;
  readonly BotIcon = Bot;
  readonly XIcon = X;
  readonly LockIcon = Lock;
  readonly RotateCcwIcon = RotateCcw;

  readonly PositionBottom = PlayerPosition.Bottom;
  readonly PositionLeft = PlayerPosition.Left;
  readonly PositionTop = PlayerPosition.Top;
  readonly PositionRight = PlayerPosition.Right;

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

  readonly statusClass = computed(() => {
    return this.room().status.toLowerCase();
  });

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

  getSlotTitle(slot: PlayerSlot): string {
    if (!slot.isOccupied) return `${slot.position} (Open)`;
    if (slot.isAi) return `${slot.position}: AI`;
    return `${slot.position}: ${slot.playerName}`;
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
