// types.ts — Type definitions for the Giretra bot API.
//
// These types mirror the JSON payloads exchanged between the game server and your bot.
// JSON uses camelCase property names which map directly to TypeScript conventions.
//
// You should NOT need to edit this file. All game logic belongs in bot.ts.

// ─── Cards ──────────────────────────────────────────────────────────

/**
 * Card ranks from lowest to highest.
 *
 * Strength order depends on game mode:
 * - Trump / AllTrumps: J > 9 > A > 10 > K > Q > 8 > 7
 * - Non-trump / NoTrumps: A > 10 > K > Q > J > 9 > 8 > 7
 */
export type CardRank = "Seven" | "Eight" | "Nine" | "Ten" | "Jack" | "Queen" | "King" | "Ace";

/** Card suits. Also determines Colour game modes (ColourClubs, ColourDiamonds, etc.). */
export type CardSuit = "Clubs" | "Diamonds" | "Hearts" | "Spades";

/**
 * A playing card with a rank and suit.
 *
 * Point values depend on game mode:
 *
 * | Card        | Trump/AllTrumps | Non-trump/NoTrumps |
 * |-------------|-----------------|-------------------|
 * | Jack        | 20              | 2                 |
 * | Nine        | 14              | 0                 |
 * | Ace         | 11              | 11                |
 * | Ten         | 10              | 10                |
 * | King        | 4               | 4                 |
 * | Queen       | 3               | 3                 |
 * | Eight,Seven | 0               | 0                 |
 */
export interface Card {
  rank: CardRank;
  suit: CardSuit;
}

// ─── Players ────────────────────────────────────────────────────────

/**
 * Seat positions around the table (clockwise: Bottom → Left → Top → Right).
 * Your bot is always `"Bottom"`.
 */
export type PlayerPosition = "Bottom" | "Left" | "Top" | "Right";

/**
 * Team1 = Bottom + Top (you and your partner).
 * Team2 = Left + Right (opponents).
 */
export type Team = "Team1" | "Team2";

/** A card played by a specific player in a trick. */
export interface PlayedCard {
  /** The player who played the card. */
  player: PlayerPosition;
  /** The card that was played. */
  card: Card;
}

// ─── Game Modes ─────────────────────────────────────────────────────

/**
 * Game modes ordered from lowest to highest bid.
 * During negotiation you can only announce a mode higher than the current bid.
 */
export type GameMode =
  | "ColourClubs"
  | "ColourDiamonds"
  | "ColourHearts"
  | "ColourSpades"
  | "NoTrumps"
  | "AllTrumps";

/**
 * Scoring multiplier for a deal.
 * None = x1, Doubled = x2, Redoubled = x4.
 */
export type MultiplierState = "None" | "Doubled" | "Redoubled";

// ─── Trick ──────────────────────────────────────────────────────────

/** State of a single trick (4 cards, one per player). */
export interface TrickState {
  /** The player who led this trick (played first). */
  leader: PlayerPosition;
  /** 1-based trick number within the deal (1–8). */
  trickNumber: number;
  /** Cards played so far in this trick (0–4 cards, in play order). */
  playedCards: PlayedCard[];
  /** True when all 4 cards have been played. */
  isComplete: boolean;
}

// ─── Hand State ─────────────────────────────────────────────────────

/**
 * State of the current deal's play phase (after negotiation).
 *
 * Total card points per mode: AllTrumps = 258, Colour = 162, NoTrumps = 130.
 */
export interface HandState {
  /** The game mode in effect for this deal. */
  gameMode: GameMode;
  /** Card points accumulated by Team1 (Bottom + Top) so far. */
  team1CardPoints: number;
  /** Card points accumulated by Team2 (Left + Right) so far. */
  team2CardPoints: number;
  /** Number of tricks won by Team1 so far (0–8). */
  team1TricksWon: number;
  /** Number of tricks won by Team2 so far (0–8). */
  team2TricksWon: number;
  /** The trick currently being played (null between tricks). */
  currentTrick: TrickState | null;
  /** All tricks completed so far in this deal. */
  completedTricks: TrickState[];
}

// ─── Negotiation ────────────────────────────────────────────────────

/** Announcement action from the negotiation history. */
export interface AnnouncementAction {
  type: "Announcement";
  /** The player who announced (present in history, absent in choices). */
  player?: PlayerPosition;
  /** The game mode being announced. */
  mode: GameMode;
}

/** Accept action from the negotiation history. */
export interface AcceptAction {
  type: "Accept";
  /** The player who accepted. */
  player?: PlayerPosition;
}

/** Double action from the negotiation history. */
export interface DoubleAction {
  type: "Double";
  /** The player who doubled. */
  player?: PlayerPosition;
  /** The mode being doubled. */
  targetMode: GameMode;
}

/** Redouble action from the negotiation history. */
export interface RedoubleAction {
  type: "Redouble";
  /** The player who redoubled. */
  player?: PlayerPosition;
  /** The mode being redoubled. */
  targetMode: GameMode;
}

/** A negotiation action from the history (includes the player who took it). */
export type NegotiationAction =
  | AnnouncementAction
  | AcceptAction
  | DoubleAction
  | RedoubleAction;

/**
 * A valid action you can choose during negotiation.
 * Same shape as {@link NegotiationAction} but without the player field
 * (the server knows who you are).
 */
export type NegotiationActionChoice =
  | { type: "Announcement"; mode: GameMode }
  | { type: "Accept" }
  | { type: "Double"; targetMode: GameMode }
  | { type: "Redouble"; targetMode: GameMode };

/**
 * Full state of the negotiation (bidding) phase.
 * Negotiation ends after 3 consecutive Accepts.
 */
export interface NegotiationState {
  /** The dealer for this deal. */
  dealer: PlayerPosition;
  /** Whose turn it is to act. */
  currentPlayer: PlayerPosition;
  /** The highest bid so far, or null if no one has announced yet. */
  currentBid: GameMode | null;
  /** The player who made the current highest bid. */
  currentBidder: PlayerPosition | null;
  /** Number of consecutive accepts (negotiation ends at 3). */
  consecutiveAccepts: number;
  /** Whether any double has occurred in this negotiation. */
  hasDoubleOccurred: boolean;
  /** Full history of all actions taken in this negotiation. */
  actions: NegotiationAction[];
  /** Which game modes have been doubled (mode → true/false). */
  doubledModes: Record<string, boolean>;
  /** Game modes that have been redoubled. */
  redoubledModes: string[];
  /** Each team's Colour announcement this deal (max one Colour per team). */
  teamColourAnnouncements: Record<string, string>;
}

// ─── Scoring ────────────────────────────────────────────────────────

/**
 * Result of a completed deal, including card points and match points awarded.
 *
 * Scoring thresholds: AllTrumps 129+/258, Colour 82+/162, NoTrumps 65+/130.
 * Match points: AllTrumps = 26 (split), NoTrumps = 52 (winner-takes-all), Colour = 16 (winner-takes-all).
 * Last trick bonus: +10 card points.
 */
export interface DealResult {
  /** The game mode that was played. */
  gameMode: GameMode;
  /** The multiplier (None/Doubled/Redoubled). */
  multiplier: MultiplierState;
  /** The team that made the winning bid. */
  announcerTeam: Team;
  /** Total card points earned by Team1. */
  team1CardPoints: number;
  /** Total card points earned by Team2. */
  team2CardPoints: number;
  /** Match points awarded to Team1. */
  team1MatchPoints: number;
  /** Match points awarded to Team2. */
  team2MatchPoints: number;
  /** Whether one team won all 8 tricks. */
  wasSweep: boolean;
  /** Which team swept (null if no sweep). */
  sweepingTeam: Team | null;
  /** Whether this deal results in an instant match win (Colour sweep). */
  isInstantWin: boolean;
}

// ─── Match State ────────────────────────────────────────────────────

/** Overall match state. First team to reach {@link targetScore} (150) match points wins. */
export interface MatchState {
  /** Match points needed to win (default 150). */
  targetScore: number;
  /** Team1's total match points across all deals. */
  team1MatchPoints: number;
  /** Team2's total match points across all deals. */
  team2MatchPoints: number;
  /** The current dealer position. */
  currentDealer: PlayerPosition;
  /** Whether the match is over. */
  isComplete: boolean;
  /** The winning team (null if match is still in progress). */
  winner: Team | null;
  /** Results of all completed deals in this match. */
  completedDeals: DealResult[];
}

// ─── Session (server-internal — not passed to bot methods) ──────────

export interface SessionRequest {
  position: PlayerPosition;
  matchId: string;
}

// ─── Bot Contexts (passed to your methods) ──────────────────────────

/** Context for {@link Bot.chooseCut}. */
export interface ChooseCutContext {
  /** Total number of cards in the deck (always 32). */
  deckSize: number;
  /** Current match state. */
  matchState: MatchState;
}

/** The cut decision: where to cut and from which end. */
export interface CutResult {
  /** Cut position (must be between 6 and 26 inclusive). */
  position: number;
  /** True to cut from the top of the deck, false from the bottom. */
  fromTop: boolean;
}

/** Context for {@link Bot.chooseNegotiationAction}. */
export interface ChooseNegotiationActionContext {
  /** Your current hand (5 cards during negotiation). */
  hand: Card[];
  /** Full negotiation state including bid history. */
  negotiationState: NegotiationState;
  /** Current match state. */
  matchState: MatchState;
  /** List of valid actions you can choose from. Pick exactly one. */
  validActions: NegotiationActionChoice[];
}

/** Context for {@link Bot.chooseCard}. */
export interface ChooseCardContext {
  /** Your current hand. */
  hand: Card[];
  /** Current play state (tricks, points, etc.). */
  handState: HandState;
  /** Current match state. */
  matchState: MatchState;
  /** List of cards you are allowed to play. Pick exactly one. */
  validPlays: Card[];
}

/** Notification: a new deal is starting. */
export interface DealStartedContext {
  matchState: MatchState;
}

/** Notification: a player (any, including you) played a card. */
export interface CardPlayedContext {
  /** The player who played the card. */
  player: PlayerPosition;
  /** The card that was played. */
  card: Card;
  /** Updated hand state after the card was played. */
  handState: HandState;
  matchState: MatchState;
}

/** Notification: a trick was completed. */
export interface TrickCompletedContext {
  /** The completed trick with all 4 cards. */
  completedTrick: TrickState;
  /** The player who won the trick. */
  winner: PlayerPosition;
  /** Updated hand state after the trick. */
  handState: HandState;
  matchState: MatchState;
}

/** Notification: a deal ended with scoring results. */
export interface DealEndedContext {
  /** Scoring result for the completed deal. */
  result: DealResult;
  /** Final hand state at end of deal. */
  handState: HandState;
  matchState: MatchState;
}

/** Notification: the match is over. */
export interface MatchEndedContext {
  /** Final match state (check {@link MatchState.winner}). */
  matchState: MatchState;
}
