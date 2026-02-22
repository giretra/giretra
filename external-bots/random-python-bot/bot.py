# bot.py — Pure game logic. This is the only file you need to edit.
# See bot_types.py for full type definitions.

import random

from bot_types import (
    ChooseCutContext,
    CutResult,
    ChooseNegotiationActionContext,
    NegotiationActionChoice,
    ChooseCardContext,
    Card,
)


def choose_cut(ctx: ChooseCutContext) -> CutResult:
    position = random.randint(6, 26)
    from_top = random.random() > 0.5
    return {"position": position, "fromTop": from_top}


def choose_negotiation_action(
    ctx: ChooseNegotiationActionContext,
) -> NegotiationActionChoice:
    return random.choice(ctx["validActions"])


def choose_card(ctx: ChooseCardContext) -> Card:
    return random.choice(ctx["validPlays"])


# Optional notification hooks — uncomment and import the context types you need:
# def on_deal_started(ctx: DealStartedContext) -> None: ...
# def on_card_played(ctx: CardPlayedContext) -> None: ...
# def on_trick_completed(ctx: TrickCompletedContext) -> None: ...
# def on_deal_ended(ctx: DealEndedContext) -> None: ...
# def on_match_ended(ctx: MatchEndedContext) -> None: ...
