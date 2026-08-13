import { Component, input, output, computed, inject, viewChild } from '@angular/core';
import { PlayerPosition } from '../../../../../api/generated/signalr-types.generated';
import { HlmButton } from '@spartan-ng/helm/button';
import { LucideAngularModule, Scissors, Info } from 'lucide-angular';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { CutDeck3dComponent, CutOutcome } from './cut-deck-3d.component';

@Component({
  selector: 'app-cut-stage',
  standalone: true,
  imports: [HlmButton, LucideAngularModule, TranslocoDirective, CutDeck3dComponent],
  template: `
    <div class="cut-stage" *transloco="let t">
      <div class="deck-wrap">
        <app-cut-deck-3d
          [interactive]="isMyTurn()"
          [cutOutcome]="cutOutcome()"
          (committed)="submitCut.emit($event)"
          (animationDone)="cutAnimationDone.emit()"
        />
        <button
          class="info-btn"
          type="button"
          [attr.aria-label]="t('cut.infoLabel')"
          [title]="t('cut.infoLabel')"
          (click)="infoClicked.emit()"
        >
          <i-lucide [img]="InfoIcon" [size]="16" [strokeWidth]="2"></i-lucide>
        </button>
      </div>

      @if (isMyTurn()) {
        <p class="hint">{{ t('cut.dragHint') }}</p>
        <button hlmBtn variant="secondary" class="quick-cut-button" (click)="deck().quickCut()">
          <i-lucide [img]="ScissorsIcon" [size]="16" [strokeWidth]="2"></i-lucide>
          {{ t('cut.justCut') }}
        </button>
      } @else {
        <p class="waiting-text">{{ waitingText() }}</p>
      }
    </div>
  `,
  styles: [`
    .cut-stage {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      padding: 1rem;
      gap: 0.75rem;
      width: 100%;
    }

    .deck-wrap {
      position: relative;
      width: 100%;
      max-width: 420px;
      display: flex;
      justify-content: center;
    }

    .info-btn {
      position: absolute;
      top: 8px;
      right: 8px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 28px;
      height: 28px;
      border-radius: 999px;
      border: none;
      background: rgba(0, 0, 0, 0.25);
      color: hsl(var(--muted-foreground));
      cursor: pointer;
      transition: color 0.15s ease, background 0.15s ease;
    }
    .info-btn:hover {
      color: hsl(var(--foreground));
      background: rgba(0, 0, 0, 0.4);
    }
    .info-btn:focus-visible {
      outline: 2px solid hsl(var(--ring));
      outline-offset: 2px;
    }

    .hint {
      margin: 0;
      font-size: 0.8rem;
      color: hsl(var(--muted-foreground));
    }

    .quick-cut-button {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      cursor: pointer;
    }

    .waiting-text {
      color: hsl(var(--muted-foreground));
      margin: 0;
    }
  `],
})
export class CutStageComponent {
  readonly ScissorsIcon = Scissors;
  readonly InfoIcon = Info;
  private readonly transloco = inject(TranslocoService);

  readonly activePlayer = input<PlayerPosition | null>(null);
  readonly myPosition = input<PlayerPosition | null>(null);
  readonly isWatcher = input<boolean>(false);
  readonly cutOutcome = input<CutOutcome | null>(null);

  /** Aimed cut position (6-26) chosen by the pinch. */
  readonly submitCut = output<number>();
  readonly cutAnimationDone = output<void>();
  readonly infoClicked = output<void>();

  readonly deck = viewChild.required(CutDeck3dComponent);

  readonly isMyTurn = computed(() => {
    if (this.isWatcher()) return false;
    return this.activePlayer() === this.myPosition();
  });

  readonly waitingText = computed(() => {
    const player = this.activePlayer();
    if (!player) return this.transloco.translate('cut.waiting');
    return this.transloco.translate('cut.playerCutting', { player });
  });
}
