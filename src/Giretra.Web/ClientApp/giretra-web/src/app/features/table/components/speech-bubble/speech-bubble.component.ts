import { Component, input, computed, inject } from '@angular/core';
import { GameMode } from '../../../../api/generated/signalr-types.generated';
import { GameModeIconComponent } from '../../../../shared/components/game-mode-icon/game-mode-icon.component';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-speech-bubble',
  standalone: true,
  imports: [GameModeIconComponent],
  template: `
    <div class="speech-bubble" [class]="positionClass()">
      @if (actionType() === 'Announce' && mode()) {
        <app-game-mode-icon [mode]="mode()!" size="1.25rem" />
      } @else {
        <span class="text">{{ textDisplay() }}</span>
      }
    </div>
  `,
  styles: [`
    .speech-bubble {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: 0.375rem 0.625rem;
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 0.5rem;
      animation: scaleIn 0.15s ease;
      position: relative;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
    }

    @keyframes scaleIn {
      from {
        opacity: 0;
        transform: scale(0.85);
      }
      to {
        opacity: 1;
        transform: scale(1);
      }
    }

    .speech-bubble::after {
      content: '';
      position: absolute;
      width: 0;
      height: 0;
      border: 6px solid transparent;
    }

    .speech-bubble.bottom::after {
      bottom: -12px;
      left: 50%;
      transform: translateX(-50%);
      border-top-color: hsl(var(--border));
    }

    .speech-bubble.top::after {
      top: -12px;
      left: 50%;
      transform: translateX(-50%);
      border-bottom-color: hsl(var(--border));
    }

    .speech-bubble.left::after {
      left: -12px;
      top: 50%;
      transform: translateY(-50%);
      border-right-color: hsl(var(--border));
    }

    .speech-bubble.right::after {
      right: -12px;
      top: 50%;
      transform: translateY(-50%);
      border-left-color: hsl(var(--border));
    }

    .text {
      font-size: 0.875rem;
      font-weight: 600;
      color: hsl(var(--foreground));
    }
  `],
})
export class SpeechBubbleComponent {
  private readonly transloco = inject(TranslocoService);
  readonly actionType = input<string>('');
  readonly mode = input<GameMode | null>(null);
  readonly position = input<'top' | 'bottom' | 'left' | 'right'>('bottom');

  readonly positionClass = computed(() => this.position());

  readonly textDisplay = computed(() => {
    const action = this.actionType();
    switch (action) {
      case 'Accept':
        return this.transloco.translate('negotiation.accept');
      case 'Double':
        return '\u00d72';
      case 'Redouble':
        return '\u00d74';
      default:
        return action;
    }
  });
}
