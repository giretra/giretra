/**
 * Card utility functions for SVG rendering and display
 */
import { CardRank, CardSuit, GameMode } from '../../api/generated/signalr-types.generated';
import { Card } from '../models';

/**
 * Get the SVG href for a card from svg-cards.svg
 * Format: "assets/svg-cards.svg#suit_rank" (lowercase)
 * Examples: #heart_king, #spade_ace, #club_7
 */
export function getCardSvgHref(card: Card): string {
  const suitNames: Record<CardSuit, string> = {
    [CardSuit.Clubs]: 'club',
    [CardSuit.Diamonds]: 'diamond',
    [CardSuit.Hearts]: 'heart',
    [CardSuit.Spades]: 'spade',
  };

  const rankNames: Record<CardRank, string> = {
    [CardRank.Seven]: '7',
    [CardRank.Eight]: '8',
    [CardRank.Nine]: '9',
    [CardRank.Ten]: '10',
    [CardRank.Jack]: 'jack',
    [CardRank.Queen]: 'queen',
    [CardRank.King]: 'king',
    [CardRank.Ace]: 'ace',
  };

  return `assets/svg-cards.svg#${suitNames[card.suit]}_${rankNames[card.rank]}`;
}

/**
 * Get the SVG href for a card back
 */
export function getCardBackSvgHref(): string {
  return 'assets/svg-cards.svg#back';
}

/**
 * Check if a card's suit is red (hearts or diamonds)
 */
export function isRedSuit(suit: CardSuit): boolean {
  return suit === CardSuit.Hearts || suit === CardSuit.Diamonds;
}

/**
 * Get the trump suit from a game mode (null for SansAs/ToutAs)
 */
export function getTrumpSuit(gameMode: GameMode | null): CardSuit | null {
  if (!gameMode) return null;

  switch (gameMode) {
    case GameMode.ColourClubs:
      return CardSuit.Clubs;
    case GameMode.ColourDiamonds:
      return CardSuit.Diamonds;
    case GameMode.ColourHearts:
      return CardSuit.Hearts;
    case GameMode.ColourSpades:
      return CardSuit.Spades;
    default:
      return null;
  }
}

/**
 * Check if a card is trump in the current game mode
 */
export function isTrump(card: Card, gameMode: GameMode | null): boolean {
  if (!gameMode) return false;

  // In ToutAs, all cards use trump ranking but no "trump" suit
  if (gameMode === GameMode.ToutAs) return false;
  if (gameMode === GameMode.SansAs) return false;

  const trumpSuit = getTrumpSuit(gameMode);
  return trumpSuit === card.suit;
}

/**
 * Get suit symbol for display
 */
export function getSuitSymbol(suit: CardSuit): string {
  const symbols: Record<CardSuit, string> = {
    [CardSuit.Clubs]: '\u2663',
    [CardSuit.Diamonds]: '\u2666',
    [CardSuit.Hearts]: '\u2665',
    [CardSuit.Spades]: '\u2660',
  };
  return symbols[suit];
}

/**
 * Get rank display string
 */
export function getRankDisplay(rank: CardRank): string {
  const displays: Record<CardRank, string> = {
    [CardRank.Seven]: '7',
    [CardRank.Eight]: '8',
    [CardRank.Nine]: '9',
    [CardRank.Ten]: '10',
    [CardRank.Jack]: 'J',
    [CardRank.Queen]: 'Q',
    [CardRank.King]: 'K',
    [CardRank.Ace]: 'A',
  };
  return displays[rank];
}
