import { Injectable, signal } from '@angular/core';
import { PlayerPosition } from '../../api/generated/signalr-types.generated';
import { Card } from '../models';

export interface CardFlyAnimation {
  card: Card;
  fromPosition: PlayerPosition;
  toPosition: 'center' | 'team1' | 'team2';
}

export interface TrickCollectAnimation {
  winner: PlayerPosition;
  cards: Card[];
}

/**
 * Simplified animation service that uses delays instead of animations.
 * The animation signals are kept for API compatibility but animations are disabled.
 */
@Injectable({
  providedIn: 'root',
})
export class AnimationQueueService {
  // Signals for component reactivity (kept for API compatibility)
  private readonly _cardFlying = signal<CardFlyAnimation | null>(null);
  private readonly _winnerHighlight = signal<PlayerPosition | null>(null);
  private readonly _trickCollecting = signal<TrickCollectAnimation | null>(null);
  private readonly _isAnimating = signal<boolean>(false);

  readonly cardFlying = this._cardFlying.asReadonly();
  readonly winnerHighlight = this._winnerHighlight.asReadonly();
  readonly trickCollecting = this._trickCollecting.asReadonly();
  readonly isAnimating = this._isAnimating.asReadonly();

  private wait(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  /**
   * Queue a card fly - replaced with a short delay
   */
  async flyCard(_animation: CardFlyAnimation): Promise<void> {
    await this.wait(50);
  }

  /**
   * Queue a winner highlight - replaced with a short delay
   */
  async highlightWinner(_winner: PlayerPosition): Promise<void> {
    await this.wait(100);
  }

  /**
   * Queue a trick collect - replaced with a short delay
   */
  async collectTrick(_animation: TrickCollectAnimation): Promise<void> {
    await this.wait(50);
  }

  /**
   * Queue a delay
   */
  async addDelay(ms: number): Promise<void> {
    await this.wait(ms);
  }

  /**
   * Animate a complete trick sequence - replaced with a short delay
   */
  async animateTrickComplete(_winner: PlayerPosition, _cards: Card[]): Promise<void> {
    await this.wait(150);
  }

  /**
   * Clear all animations
   */
  clear(): void {
    this._cardFlying.set(null);
    this._winnerHighlight.set(null);
    this._trickCollecting.set(null);
    this._isAnimating.set(false);
  }
}
