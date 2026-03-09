import { Component, input, computed, output } from '@angular/core';
import { CardSuit, GameMode } from '../../../api/generated/signalr-types.generated';
import { Card } from '../../../core/models';
import { getCardSvgHref, getCardBackSvgHref, isTrump } from '../../../core/utils/card-utils';

@Component({
  selector: 'app-card',
  standalone: true,
  host: {
    '[class.fluid]': 'width() == null',
  },
  template: `
    <div
      class="card-container"
      [class.fluid]="width() == null"
      [class.face-up]="faceUp()"
      [class.face-down]="!faceUp()"
      [class.playable]="playable()"
      [class.dimmed]="dimmed()"
      [class.lifted]="lifted()"
      [class.trump]="showTrumpGlow()"
      [class.focused]="focused()"
      [class.clickable]="playable() && faceUp()"
      [style.width.px]="width()"
      [style.height.px]="height()"
      (click)="handleClick()"
    >
      <svg
        [attr.viewBox]="'0 0 ' + svgWidth + ' ' + svgHeight"
        [style.width.px]="width()"
        [style.height.px]="height()"
        class="card-svg"
      >
        <use [attr.href]="svgHref()" />
      </svg>
    </div>
  `,
  styles: [`
    :host {
      display: inline-block;
    }

    :host(.fluid) {
      display: block;
    }

    .card-container.fluid {
      aspect-ratio: 169.075 / 244.64;
    }

    .fluid .card-svg {
      width: 100%;
      height: 100%;
    }

    .card-container {
      position: relative;
      border-radius: 8px;
      transition: transform 0.15s ease, filter 0.15s ease, box-shadow 0.15s ease;
      cursor: default;
      user-select: none;
    }

    .card-svg {
      display: block;
      border-radius: 8px;
      background: white;
    }

    .face-down .card-svg {
      background: hsl(220, 20%, 30%);
    }

    .clickable {
      cursor: pointer;
    }

    .playable {
      filter: brightness(1);
    }

    .playable:hover {
      transform: translateY(-4px);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
    }

    .lifted {
      transform: translateY(-12px);
    }

    .lifted:hover {
      transform: translateY(-16px);
    }

    .dimmed {
      filter: brightness(0.65);
      opacity: 0.75;
    }

    .trump {
      box-shadow: 0 0 8px 2px hsl(var(--gold));
    }

    .trump.lifted {
      box-shadow: 0 0 12px 3px hsl(var(--gold));
    }

    @media (min-width: 640px) {
      .focused {
        outline: 2px solid hsl(var(--primary));
        outline-offset: 2px;
      }
    }
  `],
})
export class CardComponent {
  // Inputs
  readonly card = input<Card | null>(null);
  readonly faceUp = input<boolean>(true);
  readonly playable = input<boolean>(false);
  readonly dimmed = input<boolean>(false);
  readonly lifted = input<boolean>(false);
  readonly focused = input<boolean>(false);
  readonly trumpSuit = input<CardSuit | null>(null);
  readonly gameMode = input<GameMode | null>(null);
  readonly width = input<number | null>(96);

  // Outputs
  readonly cardClicked = output<Card>();

  // SVG card dimensions (standard playing card ratio ~2.5:3.5)
  readonly svgWidth = 169.075;
  readonly svgHeight = 244.64;

  // Computed
  readonly height = computed(() => {
    const w = this.width();
    return w != null ? Math.round(w * (this.svgHeight / this.svgWidth)) : null;
  });

  readonly svgHref = computed(() => {
    const c = this.card();
    if (!c || !this.faceUp()) {
      return getCardBackSvgHref();
    }
    return getCardSvgHref(c);
  });

  readonly showTrumpGlow = computed(() => {
    const c = this.card();
    const mode = this.gameMode();
    if (!c || !mode || !this.faceUp()) return false;
    return isTrump(c, mode);
  });

  handleClick(): void {
    const c = this.card();
    if (c && this.playable() && this.faceUp()) {
      this.cardClicked.emit(c);
    }
  }
}
