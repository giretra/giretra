import { Component, input, computed, output, HostListener } from '@angular/core';
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

const ROTATION_MAP: Record<RelativePosition, number> = {
  top: -3,
  left: -6,
  right: 5,
  bottom: 2,
};

@Component({
  selector: 'app-trick-area',
  standalone: true,
  imports: [CardComponent],
  template: `
    <div class="trick-area-wrapper">
      <!-- Card play area -->
      <div
        class="trick-area"
        [class.clickable]="showingCompletedTrick() && !isWatcher()"
        (click)="onAreaClick()"
      >
        <!-- Card positions -->
        @for (pos of positions; track pos) {
          <div
            class="card-slot"
            [class]="pos"
            [style.transform]="getSlotTransform(pos)"
          >
            @if (getCardAtPosition(pos); as posCard) {
              <div
                class="card-throw"
                [style.--rotation]="getRotation(posCard.relativePosition) + 'deg'"
              >
                <app-card
                  [card]="posCard.card"
                  [faceUp]="true"
                  [gameMode]="gameMode()"
                  [width]="80"
                />
              </div>
            }
          </div>
        }

        <!-- Click to continue prompt (hidden for watchers) -->
        @if (showingCompletedTrick() && !isWatcher()) {
          <div class="continue-prompt">
            Click or press Space to continue
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .trick-area-wrapper {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.75rem;
    }

    .trick-area {
      position: relative;
      width: 240px;
      height: 240px;
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

    .card-throw {
      transform: rotate(var(--rotation));
      filter: drop-shadow(0 4px 8px rgba(0, 0, 0, 0.4));
      animation: throwIn 0.2s ease-out;
    }

    @keyframes throwIn {
      from {
        opacity: 0;
        transform: rotate(var(--rotation)) scale(0.85);
      }
      to {
        opacity: 1;
        transform: rotate(var(--rotation)) scale(1);
      }
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

    /* Responsive adjustments */
    @media (max-width: 480px) {
      .trick-area {
        width: 190px;
        height: 190px;
      }
    }
  `],
})
export class TrickAreaComponent {
  readonly currentTrick = input<TrickResponse | null>(null);
  readonly completedTrickToShow = input<TrickResponse | null>(null);
  readonly showingCompletedTrick = input<boolean>(false);
  readonly isWatcher = input<boolean>(false);
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

  getRotation(pos: RelativePosition): number {
    return ROTATION_MAP[pos] ?? 0;
  }

  getSlotTransform(pos: RelativePosition): string {
    if (pos === 'top') return 'translateX(-50%)';
    if (pos === 'bottom') return 'translateX(-50%)';
    if (pos === 'left') return 'translateY(-50%)';
    if (pos === 'right') return 'translateY(-50%)';
    return '';
  }

  @HostListener('document:keydown.space', ['$event'])
  onSpacePress(event: Event): void {
    if (this.showingCompletedTrick() && !this.isWatcher()) {
      event.preventDefault();
      this.dismissCompletedTrick.emit();
    }
  }

  onAreaClick(): void {
    if (this.showingCompletedTrick() && !this.isWatcher()) {
      this.dismissCompletedTrick.emit();
    }
  }
}
