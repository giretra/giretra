import { Component, input, computed, output } from '@angular/core';
import { GameMode, PlayerPosition } from '../../../../../api/generated/signalr-types.generated';
import { TrickResponse } from '../../../../../core/services/api.service';
import { toRelativePosition, RelativePosition } from '../../../../../core/utils/position-utils';
import { CardComponent } from '../../../../../shared/components/card/card.component';
import { Card } from '../../../../../core/models';

interface PositionedCard {
  card: Card;
  player: PlayerPosition;
  relativePosition: RelativePosition;
}

@Component({
  selector: 'app-trick-area',
  standalone: true,
  imports: [CardComponent],
  template: `
    <div
      class="trick-area"
      [class.clickable]="showingCompletedTrick()"
      (click)="onAreaClick()"
    >
      <!-- Card positions -->
      @for (pos of positions; track pos) {
        <div class="card-slot" [class]="pos">
          @if (getCardAtPosition(pos); as posCard) {
            <app-card
              [card]="posCard.card"
              [faceUp]="true"
              [gameMode]="gameMode()"
              [width]="64"
            />
          }
        </div>
      }

      <!-- Click to continue prompt -->
      @if (showingCompletedTrick()) {
        <div class="continue-prompt">
          Click to continue
        </div>
      }
    </div>
  `,
  styles: [`
    .trick-area {
      position: relative;
      width: 200px;
      height: 200px;
    }

    .trick-area.clickable {
      cursor: pointer;
    }

    .card-slot {
      position: absolute;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .card-slot.top {
      top: 0;
      left: 50%;
      transform: translateX(-50%);
    }

    .card-slot.bottom {
      bottom: 0;
      left: 50%;
      transform: translateX(-50%);
    }

    .card-slot.left {
      left: 0;
      top: 50%;
      transform: translateY(-50%);
    }

    .card-slot.right {
      right: 0;
      top: 50%;
      transform: translateY(-50%);
    }

    .continue-prompt {
      position: absolute;
      bottom: -24px;
      left: 50%;
      transform: translateX(-50%);
      font-size: 0.75rem;
      color: hsl(var(--muted-foreground));
      white-space: nowrap;
      animation: pulse 1.5s ease-in-out infinite;
    }

    @keyframes pulse {
      0%, 100% { opacity: 0.6; }
      50% { opacity: 1; }
    }
  `],
})
export class TrickAreaComponent {
  readonly currentTrick = input<TrickResponse | null>(null);
  readonly completedTrickToShow = input<TrickResponse | null>(null);
  readonly showingCompletedTrick = input<boolean>(false);
  readonly myPosition = input<PlayerPosition | null>(null);
  readonly gameMode = input<GameMode | null>(null);

  readonly dismissCompletedTrick = output<void>();

  readonly positions: RelativePosition[] = ['top', 'left', 'right', 'bottom'];

  /** Show completed trick if available, otherwise current trick */
  readonly displayedTrick = computed(() => {
    if (this.showingCompletedTrick()) {
      return this.completedTrickToShow();
    }
    return this.currentTrick();
  });

  readonly positionedCards = computed<PositionedCard[]>(() => {
    const trick = this.displayedTrick();
    const myPos = this.myPosition() ?? PlayerPosition.Bottom;

    if (!trick?.playedCards) return [];

    return trick.playedCards.map((pc) => ({
      card: pc.card,
      player: pc.player,
      relativePosition: toRelativePosition(pc.player, myPos),
    }));
  });

  getCardAtPosition(pos: RelativePosition): PositionedCard | null {
    return this.positionedCards().find((pc) => pc.relativePosition === pos) ?? null;
  }

  onAreaClick(): void {
    if (this.showingCompletedTrick()) {
      this.dismissCompletedTrick.emit();
    }
  }
}
