import { Component, input, output, computed } from '@angular/core';
import { CardResponse, GameMode } from '../../../api/generated/signalr-types.generated';
import { Card, cardEquals } from '../../../core/models';
import { CardComponent } from '../card/card.component';

@Component({
  selector: 'app-card-fan',
  standalone: true,
  imports: [CardComponent],
  template: `
    <div class="card-fan">
      @for (card of cards(); track cardTrackBy(card, $index)) {
        <div
          class="card-wrapper"
          [class.playable-card]="isPlayable(card)"
          [class.dimmed-card]="isDimmed(card)"
          [style.z-index]="$index"
          [style.transform]="getCardTransform($index)"
        >
          <app-card
            [card]="card"
            [faceUp]="true"
            [playable]="isPlayable(card)"
            [dimmed]="isDimmed(card)"
            [lifted]="false"
            [gameMode]="gameMode()"
            [width]="cardWidth()"
            (cardClicked)="onCardClicked($event)"
          />
        </div>
      }
    </div>
  `,
  styles: [`
    .card-fan {
      display: flex;
      justify-content: center;
      align-items: flex-end;
      padding: 0.5rem;
      gap: 0;
      position: relative;
    }

    .card-wrapper {
      margin-left: -36px;
      transform-origin: bottom center;
      transition: transform 0.15s ease, filter 0.15s ease;
    }

    .card-wrapper:first-child {
      margin-left: 0;
    }

    .card-wrapper.playable-card:hover {
      transform: translateY(-16px) scale(1.05) !important;
      z-index: 100 !important;
    }

    .card-wrapper.dimmed-card {
      filter: grayscale(0.3);
      opacity: 0.75;
    }

    @media (min-width: 640px) {
      .card-wrapper {
        margin-left: -24px;
      }
    }
  `],
})
export class CardFanComponent {
  readonly cards = input<CardResponse[]>([]);
  readonly validCards = input<CardResponse[]>([]);
  readonly gameMode = input<GameMode | null>(null);
  readonly interactive = input<boolean>(true);

  readonly cardSelected = output<Card>();

  readonly cardWidth = computed(() => {
    const count = this.cards().length;
    const isMobile = window.innerWidth < 640;
    if (isMobile) {
      if (count > 6) return 56;
      return 64;
    }
    if (count > 6) return 68;
    return 80;
  });

  private readonly rotationStep = computed(() => {
    const count = this.cards().length;
    if (count <= 1) return 0;
    // Spread of ~30 degrees total, distributed across cards
    const maxSpread = Math.min(30, count * 5);
    return maxSpread / (count - 1);
  });

  getCardTransform(index: number): string {
    const count = this.cards().length;
    if (count <= 1) return '';

    const step = this.rotationStep();
    const center = (count - 1) / 2;
    const offset = index - center;
    const rotation = offset * step;

    // Arc: cosine curve for vertical offset
    const maxLift = 12;
    const normalizedOffset = offset / ((count - 1) / 2 || 1);
    const lift = maxLift * (1 - Math.cos(normalizedOffset * (Math.PI / 2)));

    // Playable cards get lifted
    const card = this.cards()[index];
    const isPlayable = this.isPlayable(card);
    const playableLift = isPlayable ? -10 : 0;

    return `rotate(${rotation}deg) translateY(${lift + playableLift}px)`;
  }

  cardTrackBy(card: CardResponse, index: number): string {
    return `${card.rank}-${card.suit}`;
  }

  isPlayable(card: CardResponse): boolean {
    const interactive = this.interactive();
    if (!interactive) return false;
    const valid = this.validCards();
    return valid.some((vc) => cardEquals(vc, card));
  }

  isDimmed(card: CardResponse): boolean {
    if (!this.interactive()) return false;
    const valid = this.validCards();
    if (valid.length === 0) return false;
    return !valid.some((vc) => cardEquals(vc, card));
  }

  isLifted(card: CardResponse): boolean {
    return this.isPlayable(card);
  }

  onCardClicked(card: Card): void {
    const interactive = this.interactive();
    const validCards = this.validCards();
    const playable = this.isPlayable(card);

    console.log('[CardFan] onCardClicked:', {
      card: `${card.rank} of ${card.suit}`,
      interactive,
      validCardsCount: validCards.length,
      validCards: validCards.map(c => `${c.rank}${c.suit}`),
      playable,
    });

    if (playable) {
      console.log('[CardFan] Emitting cardSelected');
      this.cardSelected.emit(card);
    } else {
      console.log('[CardFan] Card not playable, ignoring click');
    }
  }
}
