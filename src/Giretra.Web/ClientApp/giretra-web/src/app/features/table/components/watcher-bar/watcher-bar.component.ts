import { Component, input } from '@angular/core';
import { PlayerPosition } from '../../../../api/generated/signalr-types.generated';
import { POSITIONS_CLOCKWISE } from '../../../../core/utils/position-utils';
import { TranslocoDirective } from '@jsverse/transloco';

@Component({
  selector: 'app-watcher-bar',
  standalone: true,
  imports: [TranslocoDirective],
  template: `
    <div class="watcher-bar" *transloco="let t">
      <span class="spectating-label">{{ t('watcher.spectating') }}</span>
      <div class="card-counts">
        @for (pos of positions; track pos) {
          <span class="count-item">
            <span class="position">{{ pos }}:</span>
            <span class="count">{{ getCount(pos) }}</span>
          </span>
        }
      </div>
    </div>
  `,
  styles: [`
    .watcher-bar {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
      padding: 0.5rem;
    }

    .spectating-label {
      font-size: 0.75rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: hsl(var(--muted-foreground));
    }

    .card-counts {
      display: flex;
      gap: 1rem;
      font-size: 0.875rem;
    }

    .count-item {
      display: flex;
      gap: 0.25rem;
    }

    .position {
      color: hsl(var(--muted-foreground));
    }

    .count {
      font-weight: 600;
      color: hsl(var(--foreground));
    }
  `],
})
export class WatcherBarComponent {
  readonly playerCardCounts = input<Record<PlayerPosition, number> | null>(null);

  readonly positions = POSITIONS_CLOCKWISE;

  getCount(position: PlayerPosition): number {
    return this.playerCardCounts()?.[position] ?? 0;
  }
}
