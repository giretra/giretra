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
          [style.z-index]="$index"
        >
          <app-card
            [card]="card"
            [faceUp]="true"
            [playable]="isPlayable(card)"
            [dimmed]="isDimmed(card)"
            [lifted]="isLifted(card)"
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
    }

    .card-wrapper {
      margin-left: -24px;
      transition: transform 0.15s ease;
    }

    .card-wrapper:first-child {
      margin-left: 0;
    }

    @media (min-width: 640px) {
      .card-wrapper {
        margin-left: -20px;
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
    // Responsive card size
    const count = this.cards().length;
    if (count > 6) return 56;
    return 64;
  });

  cardTrackBy(card: CardResponse, index: number): string {
    return `${card.rank}-${card.suit}`;
  }

  isPlayable(card: CardResponse): boolean {
    const interactive = this.interactive();
    if (!interactive) return false;
    const valid = this.validCards();
    const playable = valid.some((vc) => cardEquals(vc, card));
    return playable;
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
