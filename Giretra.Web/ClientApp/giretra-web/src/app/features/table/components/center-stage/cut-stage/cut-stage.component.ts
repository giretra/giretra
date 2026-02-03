import { Component, input, output, computed } from '@angular/core';
import { PlayerPosition } from '../../../../../api/generated/signalr-types.generated';
import { HlmButton } from '@spartan-ng/helm/button';

@Component({
  selector: 'app-cut-stage',
  standalone: true,
  imports: [HlmButton],
  template: `
    <div class="cut-stage">
      <!-- Deck visual -->
      <div class="deck">
        <div class="deck-cards">
          @for (i of deckLayers; track i) {
            <div class="deck-card" [style.transform]="'translateY(' + (i * -2) + 'px)'"></div>
          }
        </div>
      </div>

      @if (isMyTurn()) {
        <button
          hlmBtn
          variant="default"
          class="cut-button"
          (click)="submitCut.emit()"
        >
          Cut the Deck
        </button>
      } @else {
        <p class="waiting-text">{{ waitingText() }}</p>
      }
    </div>
  `,
  styles: [`
    .cut-stage {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      padding: 1rem;
    }

    .deck {
      margin-bottom: 1.5rem;
    }

    .deck-cards {
      position: relative;
      width: 80px;
      height: 112px;
    }

    .deck-card {
      position: absolute;
      width: 80px;
      height: 112px;
      background: linear-gradient(135deg, hsl(220, 20%, 35%), hsl(220, 20%, 25%));
      border: 2px solid hsl(220, 20%, 45%);
      border-radius: 8px;
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
    }

    .cut-button {
      padding: 0.75rem 2rem;
    }

    .waiting-text {
      color: hsl(var(--muted-foreground));
      margin: 0;
    }
  `],
})
export class CutStageComponent {
  readonly activePlayer = input<PlayerPosition | null>(null);
  readonly myPosition = input<PlayerPosition | null>(null);
  readonly isWatcher = input<boolean>(false);

  readonly submitCut = output<void>();

  readonly deckLayers = [0, 1, 2, 3, 4];

  readonly isMyTurn = computed(() => {
    if (this.isWatcher()) return false;
    return this.activePlayer() === this.myPosition();
  });

  readonly waitingText = computed(() => {
    const player = this.activePlayer();
    if (!player) return 'Waiting...';
    return `${player} is cutting...`;
  });
}
