import { Component, input, output, computed } from '@angular/core';
import { RoomResponse } from '../../../../../core/services/api.service';
import { HlmButton } from '@spartan-ng/helm/button';

@Component({
  selector: 'app-waiting-stage',
  standalone: true,
  imports: [HlmButton],
  template: `
    <div class="waiting-stage">
      <h2 class="title">Waiting for players</h2>
      <p class="count">{{ playerCount() }} / 4</p>

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
export class WaitingStageComponent {
  readonly room = input<RoomResponse | null>(null);
  readonly isCreator = input<boolean>(false);
  readonly isWatcher = input<boolean>(false);

  readonly startGame = output<void>();

  readonly playerCount = computed(() => this.room()?.playerCount ?? 0);

  readonly showStartButton = computed(() => {
    return this.isCreator() && !this.isWatcher();
  });
}
