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
