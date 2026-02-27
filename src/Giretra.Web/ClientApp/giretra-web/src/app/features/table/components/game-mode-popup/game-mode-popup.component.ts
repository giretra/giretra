import { Component, input, output } from '@angular/core';
import { GameMode } from '../../../../api/generated/signalr-types.generated';
import { GameModeIconComponent } from '../../../../shared/components/game-mode-icon/game-mode-icon.component';
import { TranslocoDirective } from '@jsverse/transloco';

@Component({
  selector: 'app-game-mode-popup',
  standalone: true,
  imports: [GameModeIconComponent, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
      <div class="popup-backdrop" (click)="dismissed.emit()"></div>
      <div class="popup-container" (click)="dismissed.emit()">
        <div class="popup">
          <div class="popup-label">{{ t('gameModePopup.playing') }}</div>
          <div class="popup-mode">
            <app-game-mode-icon [mode]="gameMode()" size="2.5rem" />
            <span class="mode-name">{{ t('game.modes.' + modeKey()) }}</span>
          </div>
          @if (multiplier() !== 'Normal') {
            <div class="popup-multiplier" [class.redoubled]="multiplier() === 'Redoubled'">
              {{ multiplier() === 'Doubled' ? t('negotiation.double') : t('negotiation.redouble') }}
            </div>
          }
        </div>
      </div>
    </ng-container>
  `,
  styles: [
    `
      :host {
        display: contents;
      }

      .popup-backdrop {
        position: fixed;
        inset: 0;
        z-index: 200;
      }

      .popup-container {
        position: fixed;
        inset: 0;
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 201;
        pointer-events: none;
      }

      .popup {
        pointer-events: auto;
        background: hsl(var(--card));
        border: 1px solid hsl(var(--border));
        border-radius: 1rem;
        padding: 1.25rem 2rem;
        text-align: center;
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.5rem;
        animation: popIn 0.3s ease;
        box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
        cursor: pointer;
      }

      @keyframes popIn {
        from {
          opacity: 0;
          transform: scale(0.8);
        }
        to {
          opacity: 1;
          transform: scale(1);
        }
      }

      .popup-label {
        font-size: 0.75rem;
        font-weight: 600;
        text-transform: uppercase;
        letter-spacing: 0.08em;
        color: hsl(var(--muted-foreground));
      }

      .popup-mode {
        display: flex;
        align-items: center;
        gap: 0.625rem;
      }

      .mode-name {
        font-size: 1.5rem;
        font-weight: 700;
        color: hsl(var(--foreground));
      }

      .popup-multiplier {
        font-size: 0.875rem;
        font-weight: 700;
        padding: 0.125rem 0.625rem;
        border-radius: 9999px;
        background: hsl(var(--destructive) / 0.15);
        color: hsl(var(--destructive));
        border: 1px solid hsl(var(--destructive) / 0.3);
      }

      .popup-multiplier.redoubled {
        background: hsl(var(--destructive) / 0.25);
        border-color: hsl(var(--destructive) / 0.5);
      }
    `,
  ],
})
export class GameModePopupComponent {
  readonly gameMode = input.required<GameMode>();
  readonly multiplier = input<'Normal' | 'Doubled' | 'Redoubled'>('Normal');

  readonly dismissed = output<void>();

  protected readonly modeKey = (): string => {
    switch (this.gameMode()) {
      case GameMode.ColourClubs:
        return 'clubs';
      case GameMode.ColourDiamonds:
        return 'diamonds';
      case GameMode.ColourHearts:
        return 'hearts';
      case GameMode.ColourSpades:
        return 'spades';
      case GameMode.NoTrumps:
        return 'noTrumps';
      case GameMode.AllTrumps:
        return 'allTrumps';
      default:
        return 'noTrumps';
    }
  };
}
