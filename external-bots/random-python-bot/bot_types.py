"""bot_types.py — Type definitions for the Giretra bot API.

These types mirror the JSON payloads exchanged between the game server and your bot.
JSON uses camelCase property names — Python TypedDict keys match the JSON keys directly.

You should NOT need to edit this file. All game logic belongs in bot.py.
"""

from typing import TypedDict, Literal, Optional, List, Dict, Union

# ─── Cards ──────────────────────────────────────────────────────────

CardRank = Literal["Seven", "Eight", "Nine", "Ten", "Jack", "Queen", "King", "Ace"]
"""Card ranks from lowest to highest.

Strength order depends on game mode:
- Trump / AllTrumps: J > 9 > A > 10 > K > Q > 8 > 7
- Non-trump / NoTrumps: A > 10 > K > Q > J > 9 > 8 > 7
"""

CardSuit = Literal["Clubs", "Diamonds", "Hearts", "Spades"]
"""Card suits. Also determines Colour game modes (ColourClubs, ColourDiamonds, etc.)."""


class Card(TypedDict):
    """A playing card with a rank and suit.

    Point values depend on game mode:

    =============  ================  ===================
    Card           Trump/AllTrumps   Non-trump/NoTrumps
    =============  ================  ===================
    Jack           20                2
    Nine           14                0
    Ace            11                11
    Ten            10                10
    King           4                 4
    Queen          3                 3
    Eight, Seven   0                 0
    =============  ================  ===================
    """

    rank: CardRank
    suit: CardSuit


# ─── Players ────────────────────────────────────────────────────────

PlayerPosition = Literal["Bottom", "Left", "Top", "Right"]
"""Seat positions around the table (clockwise: Bottom -> Left -> Top -> Right).
Your bot is always ``"Bottom"``.
"""

Team = Literal["Team1", "Team2"]
"""Team1 = Bottom + Top (you and your partner).
Team2 = Left + Right (opponents).
"""


class PlayedCard(TypedDict):
    """A card played by a specific player in a trick."""

    player: PlayerPosition
    """The player who played the card."""
    card: Card
    """The card that was played."""


# ─── Game Modes ─────────────────────────────────────────────────────

GameMode = Literal[
    "ColourClubs",
    "ColourDiamonds",
    "ColourHearts",
    "ColourSpades",
    "NoTrumps",
    "AllTrumps",
]
"""Game modes ordered from lowest to highest bid.
During negotiation you can only announce a mode higher than the current bid.
"""

MultiplierState = Literal["None", "Doubled", "Redoubled"]
"""Scoring multiplier for a deal. None = x1, Doubled = x2, Redoubled = x4."""

# ─── Trick ──────────────────────────────────────────────────────────


class TrickState(TypedDict):
    """State of a single trick (4 cards, one per player)."""

    leader: PlayerPosition
    """The player who led this trick (played first)."""
    trickNumber: int
    """1-based trick number within the deal (1-8)."""
    playedCards: List[PlayedCard]
    """Cards played so far in this trick (0-4 cards, in play order)."""
    isComplete: bool
    """True when all 4 cards have been played."""


# ─── Hand State ─────────────────────────────────────────────────────


class HandState(TypedDict):
    """State of the current deal's play phase (after negotiation).

    Total card points per mode: AllTrumps = 258, Colour = 162, NoTrumps = 130.
    """

    gameMode: GameMode
    """The game mode in effect for this deal."""
    team1CardPoints: int
    """Card points accumulated by Team1 (Bottom + Top) so far."""
    team2CardPoints: int
    """Card points accumulated by Team2 (Left + Right) so far."""
    team1TricksWon: int
    """Number of tricks won by Team1 so far (0-8)."""
    team2TricksWon: int
    """Number of tricks won by Team2 so far (0-8)."""
    currentTrick: Optional[TrickState]
    """The trick currently being played (None between tricks)."""
    completedTricks: List[TrickState]
    """All tricks completed so far in this deal."""


# ─── Negotiation ────────────────────────────────────────────────────


class AnnouncementAction(TypedDict):
    """Announcement action from the negotiation history."""

    type: Literal["Announcement"]
    player: PlayerPosition
    """The player who announced."""
    mode: GameMode
    """The game mode being announced."""


class AcceptAction(TypedDict):
    """Accept action from the negotiation history."""

    type: Literal["Accept"]
    player: PlayerPosition
    """The player who accepted."""


class DoubleAction(TypedDict):
    """Double action from the negotiation history."""

    type: Literal["Double"]
    player: PlayerPosition
    """The player who doubled."""
    targetMode: GameMode
    """The mode being doubled."""


class RedoubleAction(TypedDict):
    """Redouble action from the negotiation history."""

    type: Literal["Redouble"]
    player: PlayerPosition
    """The player who redoubled."""
    targetMode: GameMode
    """The mode being redoubled."""


NegotiationAction = Union[AnnouncementAction, AcceptAction, DoubleAction, RedoubleAction]
"""A negotiation action from the history (includes the player who took it)."""


class AnnouncementChoice(TypedDict):
    """Announce a game mode."""

    type: Literal["Announcement"]
    mode: GameMode


class AcceptChoice(TypedDict):
    """Accept the current bid."""

    type: Literal["Accept"]


class DoubleChoice(TypedDict):
    """Double a game mode."""

    type: Literal["Double"]
    targetMode: GameMode


class RedoubleChoice(TypedDict):
    """Redouble a game mode."""

    type: Literal["Redouble"]
    targetMode: GameMode


NegotiationActionChoice = Union[AnnouncementChoice, AcceptChoice, DoubleChoice, RedoubleChoice]
"""A valid action you can choose during negotiation.
Same shape as NegotiationAction but without the player field
(the server knows who you are).
"""


class NegotiationState(TypedDict):
    """Full state of the negotiation (bidding) phase.
    Negotiation ends after 3 consecutive Accepts.
    """

    dealer: PlayerPosition
    """The dealer for this deal."""
    currentPlayer: PlayerPosition
    """Whose turn it is to act."""
    currentBid: Optional[GameMode]
    """The highest bid so far, or None if no one has announced yet."""
    currentBidder: Optional[PlayerPosition]
    """The player who made the current highest bid."""
    consecutiveAccepts: int
    """Number of consecutive accepts (negotiation ends at 3)."""
    hasDoubleOccurred: bool
    """Whether any double has occurred in this negotiation."""
    actions: List[NegotiationAction]
    """Full history of all actions taken in this negotiation."""
    doubledModes: Dict[str, bool]
    """Which game modes have been doubled (mode -> true/false)."""
    redoubledModes: List[str]
    """Game modes that have been redoubled."""
    teamColourAnnouncements: Dict[str, str]
    """Each team's Colour announcement this deal (max one Colour per team)."""


# ─── Scoring ────────────────────────────────────────────────────────


class DealResult(TypedDict):
    """Result of a completed deal, including card points and match points awarded.

    Scoring thresholds: AllTrumps 129+/258, Colour 82+/162, NoTrumps 65+/130.
    Match points: AllTrumps = 26 (split), NoTrumps = 52 (winner-takes-all),
    Colour = 16 (winner-takes-all). Last trick bonus: +10 card points.
    """

    gameMode: GameMode
    """The game mode that was played."""
    multiplier: MultiplierState
    """The multiplier (None/Doubled/Redoubled)."""
    announcerTeam: Team
    """The team that made the winning bid."""
    team1CardPoints: int
    """Total card points earned by Team1."""
    team2CardPoints: int
    """Total card points earned by Team2."""
    team1MatchPoints: int
    """Match points awarded to Team1."""
    team2MatchPoints: int
    """Match points awarded to Team2."""
    wasSweep: bool
    """Whether one team won all 8 tricks."""
    sweepingTeam: Optional[Team]
    """Which team swept (None if no sweep)."""
    isInstantWin: bool
    """Whether this deal results in an instant match win (Colour sweep)."""


# ─── Match State ────────────────────────────────────────────────────


class MatchState(TypedDict):
    """Overall match state. First team to reach ``targetScore`` (150) match points wins."""

    targetScore: int
    """Match points needed to win (default 150)."""
    team1MatchPoints: int
    """Team1's total match points across all deals."""
    team2MatchPoints: int
    """Team2's total match points across all deals."""
    currentDealer: PlayerPosition
    """The current dealer position."""
    isComplete: bool
    """Whether the match is over."""
    winner: Optional[Team]
    """The winning team (None if match is still in progress)."""
    completedDeals: List[DealResult]
    """Results of all completed deals in this match."""


# ─── Session (server-internal — not passed to bot methods) ──────────


class SessionRequest(TypedDict):
    position: PlayerPosition
    matchId: str


# ─── Bot Contexts (passed to your methods) ──────────────────────────


class ChooseCutContext(TypedDict):
    """Context for ``Bot.choose_cut``."""

    deckSize: int
    """Total number of cards in the deck (always 32)."""
    matchState: MatchState
    """Current match state."""


class CutResult(TypedDict):
    """The cut decision: where to cut and from which end."""

    position: int
    """Cut position (must be between 6 and 26 inclusive)."""
    fromTop: bool
    """True to cut from the top of the deck, False from the bottom."""


class ChooseNegotiationActionContext(TypedDict):
    """Context for ``Bot.choose_negotiation_action``."""

    hand: List[Card]
    """Your current hand (5 cards during negotiation)."""
    negotiationState: NegotiationState
    """Full negotiation state including bid history."""
    matchState: MatchState
    """Current match state."""
    validActions: List[NegotiationActionChoice]
    """List of valid actions you can choose from. Pick exactly one."""


class ChooseCardContext(TypedDict):
    """Context for ``Bot.choose_card``."""

    hand: List[Card]
    """Your current hand."""
    handState: HandState
    """Current play state (tricks, points, etc.)."""
    matchState: MatchState
    """Current match state."""
    validPlays: List[Card]
    """List of cards you are allowed to play. Pick exactly one."""


class DealStartedContext(TypedDict):
    """Notification: a new deal is starting."""

    matchState: MatchState


class CardPlayedContext(TypedDict):
    """Notification: a player (any, including you) played a card."""

    player: PlayerPosition
    """The player who played the card."""
    card: Card
    """The card that was played."""
    handState: HandState
    """Updated hand state after the card was played."""
    matchState: MatchState


class TrickCompletedContext(TypedDict):
    """Notification: a trick was completed."""

    completedTrick: TrickState
    """The completed trick with all 4 cards."""
    winner: PlayerPosition
    """The player who won the trick."""
    handState: HandState
    """Updated hand state after the trick."""
    matchState: MatchState


class DealEndedContext(TypedDict):
    """Notification: a deal ended with scoring results."""

    result: DealResult
    """Scoring result for the completed deal."""
    handState: HandState
    """Final hand state at end of deal."""
    matchState: MatchState


class MatchEndedContext(TypedDict):
    """Notification: the match is over."""

    matchState: MatchState
    """Final match state (check ``winner``)."""
