import { Component, input, computed } from '@angular/core';
import { MultiplierState } from '../../../core/services/game-state.service';

@Component({
  selector: 'app-multiplier-badge',
  standalone: true,
  template: `
    @if (showBadge()) {
      <span class="badge" [class]="badgeClass()">
        {{ displayText() }}
      </span>
    }
  `,
  styles: [`
    .badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: 0.125rem 0.375rem;
      border-radius: 0.25rem;
      font-size: 0.75rem;
      font-weight: 700;
      text-transform: uppercase;
    }

    .doubled {
      background: hsl(45, 90%, 55%);
      color: hsl(220, 20%, 10%);
    }

    .redoubled {
      background: hsl(0, 72%, 51%);
      color: white;
    }
  `],
})
export class MultiplierBadgeComponent {
  readonly multiplier = input<MultiplierState>('Normal');

  readonly showBadge = computed(() => this.multiplier() !== 'Normal');

  readonly displayText = computed(() => {
    switch (this.multiplier()) {
      case 'Doubled':
        return '\u00d72';
      case 'Redoubled':
        return '\u00d74';
      default:
        return '';
    }
  });

  readonly badgeClass = computed(() => {
    switch (this.multiplier()) {
      case 'Doubled':
        return 'doubled';
      case 'Redoubled':
        return 'redoubled';
      default:
        return '';
    }
  });
}
