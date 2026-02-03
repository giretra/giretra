import { Component, input, output, computed, OnInit, DoCheck, inject } from '@angular/core';
import { RoomResponse } from '../../../../../core/services/api.service';
import { ClientSessionService } from '../../../../../core/services/client-session.service';
import { HlmButton } from '@spartan-ng/helm/button';

@Component({
  selector: 'app-waiting-stage',
  standalone: true,
  imports: [HlmButton],
  template: `
    <div class="waiting-stage">
      <h2 class="title">Waiting for players</h2>
      <p class="count">{{ playerCount() }} / 4</p>

      <!-- Debug info - remove later -->
      <p class="debug" style="font-size: 0.7rem; color: gray;">
        isCreator: {{ isCreator() }} | isWatcher: {{ isWatcher() }} | clientId: {{ debugClientId() }}
      </p>

      @if (showStartButton()) {
        <button
          hlmBtn
          variant="default"
          class="start-button"
          (click)="startGame.emit()"
        >
          Start Game
        </button>
        <p class="hint">Empty slots will be filled by AI</p>
      } @else if (isWatcher()) {
        <p class="hint">Waiting for host to start...</p>
      } @else {
        <p class="hint">Waiting for more players...</p>
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

    .title {
      font-size: 1.25rem;
      font-weight: 600;
      color: hsl(var(--foreground));
      margin: 0 0 0.5rem 0;
    }

    .count {
      font-size: 2rem;
      font-weight: 700;
      color: hsl(var(--primary));
      margin: 0 0 1.5rem 0;
    }

    .start-button {
      margin-bottom: 1rem;
    }

    .hint {
      font-size: 0.875rem;
      color: hsl(var(--muted-foreground));
      margin: 0;
    }
  `],
})
export class WaitingStageComponent implements OnInit, DoCheck {
  private readonly session = inject(ClientSessionService);

  readonly room = input<RoomResponse | null>(null);
  readonly isCreator = input<boolean>(false);
  readonly isWatcher = input<boolean>(false);

  readonly startGame = output<void>();

  readonly playerCount = computed(() => this.room()?.playerCount ?? 0);

  readonly showStartButton = computed(() => {
    return this.isCreator() && !this.isWatcher();
  });

  // Debug: show clientId to help diagnose issues
  readonly debugClientId = computed(() => {
    const id = this.session.clientId();
    return id ? `${id.slice(0, 12)}...` : 'none';
  });

  private _lastLoggedState: string = '';

  ngOnInit(): void {
    console.log('[WaitingStage] Component initialized');
  }

  ngDoCheck(): void {
    const room = this.room();
    const state = {
      roomId: room?.roomId,
      roomStatus: room?.status,
      playerCount: room?.playerCount,
      gameId: room?.gameId,
      slots: room?.playerSlots?.map(s => ({
        position: s.position,
        occupied: s.isOccupied,
        name: s.playerName,
        isAi: s.isAi,
      })),
      isCreator: this.isCreator(),
      isWatcher: this.isWatcher(),
      showStartButton: this.showStartButton(),
    };
    const stateKey = JSON.stringify(state);
    if (stateKey !== this._lastLoggedState) {
      console.log('[WaitingStage] State changed:', state);
      this._lastLoggedState = stateKey;
    }
  }
}
