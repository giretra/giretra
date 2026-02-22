/**
 * Card model utilities - extends the generated SignalR types
 */
import { CardRank, CardSuit, CardResponse } from '../../api/generated/signalr-types.generated';

export interface Card extends CardResponse {
  display?: string;
}

export function cardEquals(a: Card | null | undefined, b: Card | null | undefined): boolean {
  if (!a || !b) return a === b;
  return a.rank === b.rank && a.suit === b.suit;
}

export function cardToString(card: Card): string {
  const rankSymbols: Record<CardRank, string> = {
    [CardRank.Seven]: '7',
    [CardRank.Eight]: '8',
    [CardRank.Nine]: '9',
    [CardRank.Ten]: '10',
    [CardRank.Jack]: 'J',
    [CardRank.Queen]: 'Q',
    [CardRank.King]: 'K',
    [CardRank.Ace]: 'A',
  };

  const suitSymbols: Record<CardSuit, string> = {
    [CardSuit.Clubs]: '\u2663',
    [CardSuit.Diamonds]: '\u2666',
    [CardSuit.Hearts]: '\u2665',
    [CardSuit.Spades]: '\u2660',
  };

  return `${rankSymbols[card.rank]}${suitSymbols[card.suit]}`;
}
