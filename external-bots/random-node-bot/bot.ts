// bot.ts — Pure game logic. This is the only file you need to edit.
// See types.ts for full type definitions.

import {
  ChooseCutContext,
  CutResult,
  ChooseNegotiationActionContext,
  NegotiationActionChoice,
  ChooseCardContext,
  Card,
} from "./types";

export function chooseCut(ctx: ChooseCutContext): CutResult {
  const position = 6 + Math.floor(Math.random() * 21); // 6..26
  const fromTop = Math.random() > 0.5;
  return { position, fromTop };
}

export function chooseNegotiationAction(
  ctx: ChooseNegotiationActionContext
): NegotiationActionChoice {
  return ctx.validActions[Math.floor(Math.random() * ctx.validActions.length)];
}

export function chooseCard(ctx: ChooseCardContext): Card {
  return ctx.validPlays[Math.floor(Math.random() * ctx.validPlays.length)];
}

// Optional notification hooks — uncomment and import the context types you need:
// export function onDealStarted(ctx: DealStartedContext) {}
// export function onCardPlayed(ctx: CardPlayedContext) {}
// export function onTrickCompleted(ctx: TrickCompletedContext) {}
// export function onDealEnded(ctx: DealEndedContext) {}
// export function onMatchEnded(ctx: MatchEndedContext) {}
