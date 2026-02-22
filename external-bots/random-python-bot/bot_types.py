# types.py — Type definitions for the Giretra bot API.

from typing import TypedDict, Literal, Optional, List, Dict, Union

# ─── Cards ──────────────────────────────────────────────────────────

CardRank = Literal["Seven", "Eight", "Nine", "Ten", "Jack", "Queen", "King", "Ace"]
CardSuit = Literal["Clubs", "Diamonds", "Hearts", "Spades"]


class Card(TypedDict):
    rank: CardRank
    suit: CardSuit


# ─── Players ────────────────────────────────────────────────────────

PlayerPosition = Literal["Bottom", "Left", "Top", "Right"]
Team = Literal["Team1", "Team2"]


class PlayedCard(TypedDict):
    player: PlayerPosition
    card: Card


# ─── Game Modes ─────────────────────────────────────────────────────

GameMode = Literal[
    "ColourClubs",
    "ColourDiamonds",
    "ColourHearts",
    "ColourSpades",
    "NoTrumps",
    "AllTrumps",
]

MultiplierState = Literal["None", "Doubled", "Redoubled"]

# ─── Trick ──────────────────────────────────────────────────────────


class TrickState(TypedDict):
    leader: PlayerPosition
    trickNumber: int
    playedCards: List[PlayedCard]
    isComplete: bool


# ─── Hand State ─────────────────────────────────────────────────────


class HandState(TypedDict):
    gameMode: GameMode
    team1CardPoints: int
    team2CardPoints: int
    team1TricksWon: int
    team2TricksWon: int
    currentTrick: Optional[TrickState]
    completedTricks: List[TrickState]


# ─── Negotiation ────────────────────────────────────────────────────


class AnnouncementAction(TypedDict):
    type: Literal["Announcement"]
    player: PlayerPosition
    mode: GameMode


class AcceptAction(TypedDict):
    type: Literal["Accept"]
    player: PlayerPosition


class DoubleAction(TypedDict):
    type: Literal["Double"]
    player: PlayerPosition
    targetMode: GameMode


class RedoubleAction(TypedDict):
    type: Literal["Redouble"]
    player: PlayerPosition
    targetMode: GameMode


NegotiationAction = Union[AnnouncementAction, AcceptAction, DoubleAction, RedoubleAction]


class AnnouncementChoice(TypedDict):
    type: Literal["Announcement"]
    mode: GameMode


class AcceptChoice(TypedDict):
    type: Literal["Accept"]


class DoubleChoice(TypedDict):
    type: Literal["Double"]
    targetMode: GameMode


class RedoubleChoice(TypedDict):
    type: Literal["Redouble"]
    targetMode: GameMode


NegotiationActionChoice = Union[AnnouncementChoice, AcceptChoice, DoubleChoice, RedoubleChoice]
"""Valid action choice (no player field — the server knows who you are)."""


class NegotiationState(TypedDict):
    dealer: PlayerPosition
    currentPlayer: PlayerPosition
    currentBid: Optional[GameMode]
    currentBidder: Optional[PlayerPosition]
    consecutiveAccepts: int
    hasDoubleOccurred: bool
    actions: List[NegotiationAction]
    doubledModes: Dict[str, bool]
    redoubledModes: List[str]
    teamColourAnnouncements: Dict[str, str]


# ─── Scoring ────────────────────────────────────────────────────────


class DealResult(TypedDict):
    gameMode: GameMode
    multiplier: MultiplierState
    announcerTeam: Team
    team1CardPoints: int
    team2CardPoints: int
    team1MatchPoints: int
    team2MatchPoints: int
    wasSweep: bool
    sweepingTeam: Optional[Team]
    isInstantWin: bool


# ─── Match State ────────────────────────────────────────────────────


class MatchState(TypedDict):
    targetScore: int
    team1MatchPoints: int
    team2MatchPoints: int
    currentDealer: PlayerPosition
    isComplete: bool
    winner: Optional[Team]
    completedDeals: List[DealResult]


# ─── Session ────────────────────────────────────────────────────────


class Session(TypedDict):
    position: PlayerPosition
    matchId: str


# ─── Bot Contexts (passed to your functions) ────────────────────────


class ChooseCutContext(TypedDict):
    deckSize: int
    matchState: MatchState
    session: Session


class CutResult(TypedDict):
    position: int
    fromTop: bool


class ChooseNegotiationActionContext(TypedDict):
    hand: List[Card]
    negotiationState: NegotiationState
    matchState: MatchState
    validActions: List[NegotiationActionChoice]
    session: Session


class ChooseCardContext(TypedDict):
    hand: List[Card]
    handState: HandState
    matchState: MatchState
    validPlays: List[Card]
    session: Session


class DealStartedContext(TypedDict):
    matchState: MatchState
    session: Session


class CardPlayedContext(TypedDict):
    player: PlayerPosition
    card: Card
    handState: HandState
    matchState: MatchState
    session: Session


class TrickCompletedContext(TypedDict):
    completedTrick: TrickState
    winner: PlayerPosition
    handState: HandState
    matchState: MatchState
    session: Session


class DealEndedContext(TypedDict):
    result: DealResult
    handState: HandState
    matchState: MatchState
    session: Session


class MatchEndedContext(TypedDict):
    matchState: MatchState
    session: Session
