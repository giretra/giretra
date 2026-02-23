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
  CardPlayedContext,
  TrickCompletedContext,
  DealEndedContext,
  MatchEndedContext,
} from "./types";

export class Bot {
  constructor(public readonly matchId: string) {}

  /**
   * Called when it's your turn to cut the deck before a deal.
   * Return a position (6–26) and whether to cut from the top.
   */
  chooseCut(ctx: ChooseCutContext): CutResult {
    const position = 6 + Math.floor(Math.random() * 21); // 6..26
    const fromTop = Math.random() > 0.5;
    return { position, fromTop };
  }

  /**
   * Called during the negotiation (bidding) phase.
   * Pick one action from {@link ChooseNegotiationActionContext.validActions}.
   */
  chooseNegotiationAction(ctx: ChooseNegotiationActionContext): NegotiationActionChoice {
    return ctx.validActions[Math.floor(Math.random() * ctx.validActions.length)];
  }

  /**
   * Called when it's your turn to play a card.
   * Pick one card from {@link ChooseCardContext.validPlays}.
   */
  chooseCard(ctx: ChooseCardContext): Card {
    return ctx.validPlays[Math.floor(Math.random() * ctx.validPlays.length)];
  }

  /** Called when a new deal begins. */
  onDealStarted(ctx: DealStartedContext): void {}

  /** Called after any player (including you) plays a card. */
  onCardPlayed(ctx: CardPlayedContext): void {}

  /** Called when a trick is completed, with the winner. */
  onTrickCompleted(ctx: TrickCompletedContext): void {}

  /** Called when a deal ends, with scoring results. */
  onDealEnded(ctx: DealEndedContext): void {}

  /** Called when the match is over. */
  onMatchEnded(ctx: MatchEndedContext): void {}
}
