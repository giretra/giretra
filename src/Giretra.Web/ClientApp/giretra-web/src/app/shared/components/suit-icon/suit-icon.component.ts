import { Component, input, computed } from '@angular/core';
import { CardSuit } from '../../../api/generated/signalr-types.generated';
import { getSuitSymbol, isRedSuit } from '../../../core/utils/card-utils';

@Component({
  selector: 'app-suit-icon',
  standalone: true,
  template: `
    <span
      class="suit-icon"
      [class.red]="isRed()"
      [class.black]="!isRed()"
      [style.font-size]="size()"
    >
      {{ symbol() }}
    </span>
  `,
  styles: [`
    .suit-icon {
      font-family: 'DejaVu Sans', 'Segoe UI Symbol', sans-serif;
      line-height: 1;
    }

    .red {
      color: #ef4444;
    }

    .black {
      color: #f8fafc;
    }
  `],
})
export class SuitIconComponent {
  readonly suit = input.required<CardSuit>();
  readonly size = input<string>('1.5rem');

  readonly symbol = computed(() => getSuitSymbol(this.suit()));
  readonly isRed = computed(() => isRedSuit(this.suit()));
}
