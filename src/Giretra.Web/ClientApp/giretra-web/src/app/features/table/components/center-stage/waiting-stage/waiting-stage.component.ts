import { Component, input, output, computed } from '@angular/core';
import { RoomResponse, PlayerSlot } from '../../../../../core/services/api.service';
import { PlayerPosition, SeatAccessMode } from '../../../../../api/generated/signalr-types.generated';
import { HlmButton } from '@spartan-ng/helm/button';
import { LucideAngularModule, Users, Lock, Unlock, Link, UserX } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';

@Component({
  selector: 'app-waiting-stage',
  standalone: true,
  imports: [HlmButton, LucideAngularModule, TranslocoDirective],
  template: `
    <div class="waiting-stage" *transloco="let t">
      <i-lucide [img]="UsersIcon" [size]="32" [strokeWidth]="1.5" class="users-icon"></i-lucide>

      <h2 class="title">{{ t('waiting.title') }}</h2>

      <!-- Seat indicator circles -->
      <div class="seat-indicators">
        @for (slot of seatSlots(); track slot.position) {
          <div class="seat-wrapper">
            <div class="seat-circle" [class.filled]="slot.isOccupied" [class.invite-only]="slot.accessMode === 'InviteOnly' && !slot.isOccupied" [title]="slot.position">
              @if (slot.isOccupied) {
                <span class="seat-initial">{{ slot.initial }}</span>
              } @else if (slot.accessMode === 'InviteOnly') {
                <i-lucide [img]="LockIcon" [size]="14" [strokeWidth]="2" class="lock-badge"></i-lucide>
              }
            </div>

            <!-- Owner controls for non-Bottom seats -->
            @if (isCreator() && !isWatcher() && slot.position !== 'Bottom') {
              <div class="seat-controls">
                @if (slot.isOccupied && !slot.isAi) {
                  <!-- Kick button -->
                  <button class="control-btn kick-btn" [title]="t('waiting.kickPlayer')" (click)="onKickPlayer(slot.position)">
                    <i-lucide [img]="UserXIcon" [size]="12" [strokeWidth]="2"></i-lucide>
                  </button>
                } @else if (!slot.isOccupied && !slot.isAi) {
                  <!-- Lock/unlock toggle -->
                  <button class="control-btn mode-btn" [title]="slot.accessMode === 'InviteOnly' ? t('waiting.setPublic') : t('waiting.setInviteOnly')" (click)="onToggleSeatMode(slot.position, slot.accessMode)">
                    @if (slot.accessMode === 'InviteOnly') {
                      <i-lucide [img]="UnlockIcon" [size]="12" [strokeWidth]="2"></i-lucide>
                    } @else {
                      <i-lucide [img]="LockIcon" [size]="12" [strokeWidth]="2"></i-lucide>
                    }
                  </button>
                  <!-- Invite link button (visible when invite-only) -->
                  @if (slot.accessMode === 'InviteOnly') {
                    <button class="control-btn invite-btn" [title]="t('waiting.copyInvite')" (click)="onGenerateInvite(slot.position)">
                      <i-lucide [img]="LinkIcon" [size]="12" [strokeWidth]="2"></i-lucide>
                    </button>
                  }
                }
              </div>
            }
          </div>
        }
      </div>

      <p class="count">{{ playerCount() }} / 4</p>

      @if (showStartButton()) {
        <button
          hlmBtn
          variant="default"
          class="start-button pulse-glow"
          (click)="startGame.emit()"
        >
          {{ t('waiting.startGame') }}
        </button>
        <p class="hint">{{ t('waiting.aiHint') }}</p>
      } @else if (isWatcher()) {
        <p class="hint">{{ t('waiting.hostHint') }}</p>
      } @else {
        <p class="hint">{{ t('waiting.morePlayersHint') }}</p>
      }
    </div>
  `,
  styles: [`
    .waiting-stage {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      padding: 2rem;
    }

    .users-icon {
      color: hsl(var(--muted-foreground));
      margin-bottom: 0.75rem;
    }

    .title {
      font-size: 1.25rem;
      font-weight: 600;
      color: hsl(var(--foreground));
      margin: 0 0 1rem 0;
    }

    .seat-indicators {
      display: flex;
      gap: 0.75rem;
      margin-bottom: 0.75rem;
    }

    .seat-wrapper {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.25rem;
    }

    .seat-circle {
      width: 2.5rem;
      height: 2.5rem;
      border-radius: 50%;
      border: 2px dashed hsl(var(--border));
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.2s ease;
    }

    .seat-circle.filled {
      border-style: solid;
      border-color: hsl(var(--primary));
      background: hsl(var(--primary) / 0.15);
    }

    .seat-circle.invite-only {
      border-color: hsl(var(--gold) / 0.6);
      background: hsl(var(--gold) / 0.08);
    }

    .seat-initial {
      font-size: 0.875rem;
      font-weight: 600;
      color: hsl(var(--primary));
      text-transform: uppercase;
    }

    .lock-badge {
      color: hsl(var(--gold) / 0.7);
    }

    .seat-controls {
      display: flex;
      gap: 0.125rem;
    }

    .control-btn {
      width: 1.25rem;
      height: 1.25rem;
      border-radius: 0.25rem;
      border: none;
      background: transparent;
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.15s ease;
      padding: 0;
    }

    .control-btn:hover {
      background: hsl(var(--muted) / 0.5);
      color: hsl(var(--foreground));
    }

    .kick-btn:hover {
      background: hsl(var(--destructive) / 0.15);
      color: hsl(var(--destructive));
    }

    .invite-btn:hover {
      background: hsl(var(--gold) / 0.15);
      color: hsl(var(--gold));
    }

    .count {
      font-size: 1.5rem;
      font-weight: 700;
      color: hsl(var(--primary));
      margin: 0 0 1.5rem 0;
    }

    .start-button {
      margin-bottom: 1rem;
      cursor: pointer;
      box-shadow: 0 4px 12px rgba(34, 197, 94, 0.3), 0 2px 4px rgba(0, 0, 0, 0.2);
      border: 1px solid rgba(255, 255, 255, 0.15);
      transition: all 0.2s ease;
      font-weight: 600;
      padding: 0.75rem 2rem;
    }

    .start-button:hover {
      transform: translateY(-2px) scale(1.02);
      box-shadow: 0 6px 20px rgba(34, 197, 94, 0.4), 0 4px 8px rgba(0, 0, 0, 0.3);
      filter: brightness(1.1);
    }

    .start-button:active {
      transform: translateY(0) scale(0.98);
      box-shadow: 0 2px 6px rgba(34, 197, 94, 0.2);
    }

    .pulse-glow {
      animation: pulseGlow 2s ease-in-out infinite;
    }

    @keyframes pulseGlow {
      0%, 100% {
        box-shadow: 0 4px 12px rgba(34, 197, 94, 0.3), 0 2px 4px rgba(0, 0, 0, 0.2);
      }
      50% {
        box-shadow: 0 4px 20px rgba(34, 197, 94, 0.5), 0 2px 8px rgba(0, 0, 0, 0.3);
      }
    }

    .hint {
      font-size: 0.875rem;
      color: hsl(var(--muted-foreground));
      margin: 0;
    }
  `],
})
export class WaitingStageComponent {
  readonly UsersIcon = Users;
  readonly LockIcon = Lock;
  readonly UnlockIcon = Unlock;
  readonly LinkIcon = Link;
  readonly UserXIcon = UserX;

  readonly room = input<RoomResponse | null>(null);
  readonly isCreator = input<boolean>(false);
  readonly isWatcher = input<boolean>(false);

  readonly startGame = output<void>();
  readonly setSeatMode = output<{ position: PlayerPosition; accessMode: SeatAccessMode }>();
  readonly generateInvite = output<PlayerPosition>();
  readonly kickPlayer = output<PlayerPosition>();

  readonly playerCount = computed(() => this.room()?.playerCount ?? 0);

  readonly showStartButton = computed(() => {
    return this.isCreator() && !this.isWatcher();
  });

  readonly seatSlots = computed(() => {
    const room = this.room();
    if (!room?.playerSlots) return [];
    return room.playerSlots.map((slot) => ({
      position: slot.position,
      isOccupied: slot.isOccupied,
      isAi: slot.isAi,
      initial: slot.playerName ? slot.playerName.charAt(0).toUpperCase() : '',
      accessMode: slot.accessMode,
      hasInvite: slot.hasInvite,
    }));
  });

  onToggleSeatMode(position: PlayerPosition, currentMode: SeatAccessMode): void {
    const newMode = currentMode === SeatAccessMode.InviteOnly
      ? SeatAccessMode.Public
      : SeatAccessMode.InviteOnly;
    this.setSeatMode.emit({ position, accessMode: newMode });
  }

  onGenerateInvite(position: PlayerPosition): void {
    this.generateInvite.emit(position);
  }

  onKickPlayer(position: PlayerPosition): void {
    this.kickPlayer.emit(position);
  }
}
