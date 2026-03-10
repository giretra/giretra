# bot.py — Pure game logic. This is the only file you need to edit.
# See bot_types.py for full type definitions.
#
# One Bot instance is created per game session.
# You are always the Bottom player. Your teammate sits across (Top),
# and your opponents are Left and Right.

import random

from bot_types import (
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
)


class Bot:
    def __init__(self, match_id: str, seed: int | None = None) -> None:
        self.match_id = match_id
        self.rng = random.Random(seed)

    def choose_cut(self, ctx: ChooseCutContext) -> CutResult:
        """Called when it's your turn to cut the deck before a deal.

        Return a position (6–26) and whether to cut from the top.
        """
        position = self.rng.randint(6, 26)
        from_top = self.rng.random() > 0.5
        return {"position": position, "fromTop": from_top}

    def choose_negotiation_action(
        self, ctx: ChooseNegotiationActionContext
    ) -> NegotiationActionChoice:
        """Called during the negotiation (bidding) phase.

        Pick one action from ``ctx["validActions"]``.
        """
        return self.rng.choice(ctx["validActions"])

    def choose_card(self, ctx: ChooseCardContext) -> Card:
        """Called when it's your turn to play a card.

        Pick one card from ``ctx["validPlays"]``.
        """
        return self.rng.choice(ctx["validPlays"])

    def on_deal_started(self, ctx: DealStartedContext) -> None:
        """Called when a new deal begins."""

    def on_negotiation_completed(self, ctx: NegotiationCompletedContext) -> None:
        """Called when negotiation completes, before trick-playing begins."""

    def on_card_played(self, ctx: CardPlayedContext) -> None:
        """Called after any player (including you) plays a card."""

    def on_trick_completed(self, ctx: TrickCompletedContext) -> None:
        """Called when a trick is completed, with the winner."""

    def on_deal_ended(self, ctx: DealEndedContext) -> None:
        """Called when a deal ends, with scoring results."""

    def on_match_ended(self, ctx: MatchEndedContext) -> None:
        """Called when the match is over."""
