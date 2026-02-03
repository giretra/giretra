import { Injectable, signal } from '@angular/core';
import { Subject } from 'rxjs';
import { concatMap, delay } from 'rxjs/operators';
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

type AnimationItem =
  | { type: 'cardFly'; data: CardFlyAnimation }
  | { type: 'winnerHighlight'; data: { winner: PlayerPosition } }
  | { type: 'trickCollect'; data: TrickCollectAnimation }
  | { type: 'delay'; data: { ms: number } };

@Injectable({
  providedIn: 'root',
})
export class AnimationQueueService {
  private readonly queue$ = new Subject<AnimationItem>();

  // Signals for component reactivity
  private readonly _cardFlying = signal<CardFlyAnimation | null>(null);
  private readonly _winnerHighlight = signal<PlayerPosition | null>(null);
  private readonly _trickCollecting = signal<TrickCollectAnimation | null>(null);
  private readonly _isAnimating = signal<boolean>(false);

  readonly cardFlying = this._cardFlying.asReadonly();
  readonly winnerHighlight = this._winnerHighlight.asReadonly();
  readonly trickCollecting = this._trickCollecting.asReadonly();
  readonly isAnimating = this._isAnimating.asReadonly();

  constructor() {
    this.setupQueue();
  }

  private setupQueue(): void {
    this.queue$
      .pipe(
        concatMap((item) => this.processAnimation(item))
      )
      .subscribe();
  }

  private async processAnimation(item: AnimationItem): Promise<void> {
    this._isAnimating.set(true);

    switch (item.type) {
      case 'cardFly':
        this._cardFlying.set(item.data);
        await this.wait(300); // Animation duration
        this._cardFlying.set(null);
        break;

      case 'winnerHighlight':
        this._winnerHighlight.set(item.data.winner);
        await this.wait(1500); // Highlight duration
        this._winnerHighlight.set(null);
        break;

      case 'trickCollect':
        this._trickCollecting.set(item.data);
        await this.wait(500); // Collect animation duration
        this._trickCollecting.set(null);
        break;

      case 'delay':
        await this.wait(item.data.ms);
        break;
    }

    this._isAnimating.set(false);
  }

  private wait(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  /**
   * Queue a card fly animation
   */
  flyCard(animation: CardFlyAnimation): void {
    this.queue$.next({ type: 'cardFly', data: animation });
  }

  /**
   * Queue a winner highlight animation
   */
  highlightWinner(winner: PlayerPosition): void {
    this.queue$.next({ type: 'winnerHighlight', data: { winner } });
  }

  /**
   * Queue a trick collect animation
   */
  collectTrick(animation: TrickCollectAnimation): void {
    this.queue$.next({ type: 'trickCollect', data: animation });
  }

  /**
   * Queue a delay
   */
  addDelay(ms: number): void {
    this.queue$.next({ type: 'delay', data: { ms } });
  }

  /**
   * Animate a complete trick sequence:
   * 1. Highlight winner
   * 2. Collect cards
   */
  animateTrickComplete(winner: PlayerPosition, cards: Card[]): void {
    this.highlightWinner(winner);
    this.collectTrick({ winner, cards });
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
