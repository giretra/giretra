import { Component, input, computed } from '@angular/core';
import { GameMode } from '../../../../api/generated/signalr-types.generated';
import { SuitIconComponent } from '../../../../shared/components/suit-icon/suit-icon.component';
import { CardSuit } from '../../../../api/generated/signalr-types.generated';

@Component({
  selector: 'app-speech-bubble',
  standalone: true,
  imports: [SuitIconComponent],
  template: `
    <div class="speech-bubble" [class]="positionClass()">
      @if (suitDisplay(); as suit) {
        <app-suit-icon [suit]="suit" size="1.25rem" />
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
      padding: 0.375rem 0.5rem;
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 0.5rem;
      animation: fadeIn 0.2s ease;
      position: relative;
    }

    @keyframes fadeIn {
      from {
        opacity: 0;
        transform: scale(0.9);
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
  readonly actionType = input<string>('');
  readonly mode = input<GameMode | null>(null);
  readonly position = input<'top' | 'bottom' | 'left' | 'right'>('bottom');

  private readonly modeToSuit: Record<string, CardSuit> = {
    [GameMode.ColourClubs]: CardSuit.Clubs,
    [GameMode.ColourDiamonds]: CardSuit.Diamonds,
    [GameMode.ColourHearts]: CardSuit.Hearts,
    [GameMode.ColourSpades]: CardSuit.Spades,
  };

  readonly positionClass = computed(() => this.position());

  readonly suitDisplay = computed(() => {
    const m = this.mode();
    if (m && this.modeToSuit[m]) {
      return this.modeToSuit[m];
    }
    return null;
  });

  readonly textDisplay = computed(() => {
    const action = this.actionType();
    const m = this.mode();

    switch (action) {
      case 'Accept':
        return 'Accept';
      case 'Double':
        return '\u00d72';
      case 'Redouble':
        return '\u00d74';
      case 'Announce':
        if (m === GameMode.SansAs) return 'Sans As';
        if (m === GameMode.ToutAs) return 'Tout As';
        return '';
      default:
        return action;
    }
  });
}
