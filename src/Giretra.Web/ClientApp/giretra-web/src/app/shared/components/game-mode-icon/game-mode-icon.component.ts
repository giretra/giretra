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
  templateUrl: './game-mode-icon.component.html',
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
