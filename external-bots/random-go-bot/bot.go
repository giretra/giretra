// bot.go — Pure game logic. This is the only file you need to edit.
// See types.go for full type definitions.
//
// One Bot instance is created per game session.
// You are always the Bottom player. Your teammate sits across (Top),
// and your opponents are Left and Right.

package main

import "math/rand/v2"

// Bot holds the state for a single game session.
type Bot struct {
	MatchID string
}

// NewBot creates a new Bot for the given match.
func NewBot(matchID string) *Bot {
	return &Bot{MatchID: matchID}
}

// ChooseCut is called when it's your turn to cut the deck before a deal.
// Return a position (6–26) and whether to cut from the top.
func (b *Bot) ChooseCut(ctx ChooseCutContext) CutResult {
	position := rand.IntN(21) + 6 // 6..26 inclusive
	fromTop := rand.IntN(2) == 0
	return CutResult{Position: position, FromTop: fromTop}
}

// ChooseNegotiationAction is called during the negotiation (bidding) phase.
// Pick one action from ctx.ValidActions.
func (b *Bot) ChooseNegotiationAction(ctx ChooseNegotiationActionContext) NegotiationActionChoice {
	return ctx.ValidActions[rand.IntN(len(ctx.ValidActions))]
}

// ChooseCard is called when it's your turn to play a card.
// Pick one card from ctx.ValidPlays.
func (b *Bot) ChooseCard(ctx ChooseCardContext) Card {
	return ctx.ValidPlays[rand.IntN(len(ctx.ValidPlays))]
}

// OnDealStarted is called when a new deal begins.
func (b *Bot) OnDealStarted(ctx DealStartedContext) {}

// OnCardPlayed is called after any player (including you) plays a card.
func (b *Bot) OnCardPlayed(ctx CardPlayedContext) {}

// OnTrickCompleted is called when a trick is completed, with the winner.
func (b *Bot) OnTrickCompleted(ctx TrickCompletedContext) {}

// OnDealEnded is called when a deal ends, with scoring results.
func (b *Bot) OnDealEnded(ctx DealEndedContext) {}

// OnMatchEnded is called when the match is over.
func (b *Bot) OnMatchEnded(ctx MatchEndedContext) {}
