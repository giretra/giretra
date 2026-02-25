import { Component, input, computed } from '@angular/core';
import { CardSuit, GameMode } from '../../../api/generated/signalr-types.generated';
import { getTrumpSuit, isRedSuit, getSuitSymbol } from '../../../core/utils/card-utils';
import { SuitIconComponent } from '../suit-icon/suit-icon.component';
import { ShieldIconComponent } from '../shield-icon/shield-icon.component';
import { TranslocoDirective } from '@jsverse/transloco';

@Component({
  selector: 'app-game-mode-badge',
  standalone: true,
  imports: [SuitIconComponent, ShieldIconComponent, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
    @if (mode()) {
      <span class="badge" [class]="badgeClass()">
        @if (trumpSuit(); as suit) {
          <app-suit-icon [suit]="suit" [size]="size()" />
        } @else if (shieldType(); as shield) {
          <app-shield-icon [type]="shield" [size]="size()" />
          <span class="mode-text">{{ t('game.modes.' + modeTextKey()) }}</span>
        }
      </span>
    }
    </ng-container>
  `,
  styles: [`
    .badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.25rem;
      padding: 0.25rem 0.5rem;
      border-radius: 0.375rem;
      font-weight: 600;
      background: hsl(var(--secondary));
    }

    .mode-text {
      font-size: 0.875rem;
      text-transform: uppercase;
      letter-spacing: 0.025em;
    }

    .no-trumps {
      color: hsl(210, 40%, 96%);
    }

    .all-trumps {
      color: hsl(45, 90%, 55%);
    }
  `],
})
export class GameModeBadgeComponent {
  readonly mode = input<GameMode | null>(null);
  readonly size = input<string>('1.25rem');

  readonly trumpSuit = computed(() => {
    const m = this.mode();
    return m ? getTrumpSuit(m) : null;
  });

  readonly shieldType = computed<'no-trumps' | 'all-trumps' | null>(() => {
    const m = this.mode();
    if (m === GameMode.NoTrumps) return 'no-trumps';
    if (m === GameMode.AllTrumps) return 'all-trumps';
    return null;
  });

  readonly modeTextKey = computed(() => {
    const m = this.mode();
    if (m === GameMode.NoTrumps) return 'noTrumps';
    if (m === GameMode.AllTrumps) return 'allTrumps';
    return '';
  });

  readonly badgeClass = computed(() => {
    const m = this.mode();
    if (m === GameMode.NoTrumps) return 'no-trumps';
    if (m === GameMode.AllTrumps) return 'all-trumps';
    return '';
  });
}
