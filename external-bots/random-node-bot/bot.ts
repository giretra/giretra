// bot.ts — Pure game logic. This is the only file you need to edit.
// See types.ts for full type definitions.
//
// One Bot instance is created per game session.
// You are always the Bottom player. Your teammate sits across (Top),
// and your opponents are Left and Right.

import {
  ChooseCutContext,
  CutResult,
  ChooseNegotiationActionContext,
  NegotiationActionChoice,
  ChooseCardContext,
  Card,
  DealStartedContext,
  NegotiationCompletedContext,
  CardPlayedContext,
  TrickCompletedContext,
  DealEndedContext,
  MatchEndedContext,
} from "./types";

export class Bot {
  private nextRandom: () => number;

  constructor(public readonly matchId: string, seed: number | null = null) {
    if (seed !== null) {
      // mulberry32 seeded PRNG
      let s = seed | 0;
      this.nextRandom = () => {
        s = (s + 0x6d2b79f5) | 0;
        let t = Math.imul(s ^ (s >>> 15), 1 | s);
        t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
      };
    } else {
      this.nextRandom = Math.random;
    }
  }

  /**
   * Called when it's your turn to cut the deck before a deal.
   * Return a position (6–26) and whether to cut from the top.
   */
  chooseCut(ctx: ChooseCutContext): CutResult {
    const position = 6 + Math.floor(this.nextRandom() * 21); // 6..26
    const fromTop = this.nextRandom() > 0.5;
    return { position, fromTop };
  }

  /**
   * Called during the negotiation (bidding) phase.
   * Pick one action from {@link ChooseNegotiationActionContext.validActions}.
   */
  chooseNegotiationAction(ctx: ChooseNegotiationActionContext): NegotiationActionChoice {
    return ctx.validActions[Math.floor(this.nextRandom() * ctx.validActions.length)];
  }

  /**
   * Called when it's your turn to play a card.
   * Pick one card from {@link ChooseCardContext.validPlays}.
   */
  chooseCard(ctx: ChooseCardContext): Card {
    return ctx.validPlays[Math.floor(this.nextRandom() * ctx.validPlays.length)];
  }

  /** Called when a new deal begins. */
  onDealStarted(ctx: DealStartedContext): void {}

  /** Called when negotiation completes, before trick-playing begins. */
  onNegotiationCompleted(ctx: NegotiationCompletedContext): void {}

  /** Called after any player (including you) plays a card. */
  onCardPlayed(ctx: CardPlayedContext): void {}

  /** Called when a trick is completed, with the winner. */
  onTrickCompleted(ctx: TrickCompletedContext): void {}

  /** Called when a deal ends, with scoring results. */
  onDealEnded(ctx: DealEndedContext): void {}

  /** Called when the match is over. */
  onMatchEnded(ctx: MatchEndedContext): void {}
}
