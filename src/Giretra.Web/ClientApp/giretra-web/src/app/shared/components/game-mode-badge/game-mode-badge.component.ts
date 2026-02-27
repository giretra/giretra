import { Component, input, computed } from '@angular/core';
import { GameMode } from '../../../api/generated/signalr-types.generated';
import { GameModeIconComponent } from '../game-mode-icon/game-mode-icon.component';
import { TranslocoDirective } from '@jsverse/transloco';

@Component({
  selector: 'app-game-mode-badge',
  standalone: true,
  imports: [GameModeIconComponent, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
    @if (mode()) {
      <span class="badge" [class]="badgeClass()">
        <app-game-mode-icon [mode]="mode()" [size]="size()" />
        @if (modeTextKey(); as key) {
          <span class="mode-text">{{ t('game.modes.' + key) }}</span>
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
