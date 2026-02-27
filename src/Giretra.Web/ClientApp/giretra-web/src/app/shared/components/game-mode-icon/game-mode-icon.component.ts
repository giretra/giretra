import { Component, input, computed } from '@angular/core';
import { CardSuit, GameMode } from '../../../api/generated/signalr-types.generated';

@Component({
  selector: 'app-game-mode-icon',
  standalone: true,
  host: {
    '[style.color]': 'hostColor()',
    '[style.display]': '"inline-flex"',
    '[style.align-items]': '"center"',
    '[style.line-height]': '"1"',
  },
  template: `
    @switch (resolvedMode()) {
      @case ('ColourClubs') {
        <svg [style.height]="size()" viewBox="0 0 24 24" role="img" aria-label="Clubs">
          <circle cx="12" cy="5.5" r="3.2" fill="currentColor"/>
          <circle cx="7.2" cy="11.2" r="3.2" fill="currentColor"/>
          <circle cx="16.8" cy="11.2" r="3.2" fill="currentColor"/>
          <path d="M10.5 10.5L10.2 17.5h3.6l-.3-7z" fill="currentColor"/>
          <path d="M8.5 19.5h7l-.5-2h-6z" fill="currentColor"/>
        </svg>
      }
      @case ('ColourDiamonds') {
        <svg [style.height]="size()" viewBox="0 0 24 24" role="img" aria-label="Diamonds">
          <path d="M12 1.5L3.5 12 12 22.5 20.5 12z" fill="#e63d4a"/>
        </svg>
      }
      @case ('ColourHearts') {
        <svg [style.height]="size()" viewBox="0 0 24 24" role="img" aria-label="Hearts">
          <path d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z" fill="#e63d4a"/>
        </svg>
      }
      @case ('ColourSpades') {
        <svg [style.height]="size()" viewBox="0 0 24 24" role="img" aria-label="Spades">
          <path d="M12 2C9.2 5.4 3 9.5 3 14.2c0 2.6 2.1 4.5 4.7 4.5 1.5 0 2.8-.6 3.7-1.6l.6-.7.6.7c.9 1 2.2 1.6 3.7 1.6 2.6 0 4.7-1.9 4.7-4.5C21 9.5 14.8 5.4 12 2z" fill="currentColor"/>
          <path d="M8.5 19.5h7l-.5-2h-6z" fill="currentColor"/>
          <rect x="10.2" y="16.5" width="3.6" height="3" fill="currentColor"/>
        </svg>
      }
      @case ('NoTrumps') {
        <svg [style.height]="size()" viewBox="0 0 24 24" role="img" aria-label="No Trumps">
          <path d="M3.5 17l1.8-9L9 11.5 12 4l3 7.5 3.7-3.5 1.8 9z" fill="none" stroke="#2d864d" stroke-width="1.4" stroke-linejoin="round" stroke-linecap="round"/>
          <rect x="3.5" y="17" width="17" height="2.5" rx="0.8" fill="#2d864d"/>
          <text x="12" y="16" text-anchor="middle" font-family="-apple-system, BlinkMacSystemFont, 'Helvetica Neue', sans-serif" font-weight="800" font-size="11" fill="#2d864d">A</text>
        </svg>
      }
      @case ('AllTrumps') {
        <svg [style.height]="size()" viewBox="0 0 24 24" role="img" aria-label="All Trumps">
          <circle cx="12" cy="12" r="10" fill="#e63d4a"/>
          <text x="12" y="16.5" text-anchor="middle" font-family="Georgia,serif" font-weight="700" font-size="14" fill="#fff">J</text>
        </svg>
      }
    }
  `,
})
export class GameModeIconComponent {
  readonly mode = input<GameMode | null>(null);
  readonly suit = input<CardSuit | null>(null);
  readonly size = input<string>('1.5rem');
  readonly variant = input<'dark' | 'light'>('dark');

  private static readonly suitToMode: Record<string, GameMode> = {
    [CardSuit.Clubs]: GameMode.ColourClubs,
    [CardSuit.Diamonds]: GameMode.ColourDiamonds,
    [CardSuit.Hearts]: GameMode.ColourHearts,
    [CardSuit.Spades]: GameMode.ColourSpades,
  };

  readonly resolvedMode = computed(() => {
    const m = this.mode();
    if (m) return m;
    const s = this.suit();
    if (s) return GameModeIconComponent.suitToMode[s] ?? null;
    return null;
  });

  readonly hostColor = computed(() => {
    const m = this.resolvedMode();
    if (m === GameMode.ColourClubs || m === GameMode.ColourSpades) {
      return this.variant() === 'dark' ? '#fff' : '#14181f';
    }
    return undefined;
  });
}
