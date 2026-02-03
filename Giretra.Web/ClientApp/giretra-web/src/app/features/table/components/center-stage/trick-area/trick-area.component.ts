import { Component, input, computed } from '@angular/core';
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
    <div class="trick-area">
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
    </div>
  `,
  styles: [`
    .trick-area {
      position: relative;
      width: 200px;
      height: 200px;
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
  `],
})
export class TrickAreaComponent {
  readonly currentTrick = input<TrickResponse | null>(null);
  readonly myPosition = input<PlayerPosition | null>(null);
  readonly gameMode = input<GameMode | null>(null);

  readonly positions: RelativePosition[] = ['top', 'left', 'right', 'bottom'];

  readonly positionedCards = computed<PositionedCard[]>(() => {
    const trick = this.currentTrick();
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
}
