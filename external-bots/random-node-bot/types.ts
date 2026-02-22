// types.ts — Type definitions for the Giretra bot API.

// ─── Cards ──────────────────────────────────────────────────────────

export type CardRank = "Seven" | "Eight" | "Nine" | "Ten" | "Jack" | "Queen" | "King" | "Ace";
export type CardSuit = "Clubs" | "Diamonds" | "Hearts" | "Spades";

export interface Card {
  rank: CardRank;
  suit: CardSuit;
}

// ─── Players ────────────────────────────────────────────────────────

export type PlayerPosition = "Bottom" | "Left" | "Top" | "Right";
export type Team = "Team1" | "Team2";

export interface PlayedCard {
  player: PlayerPosition;
  card: Card;
}

// ─── Game Modes ─────────────────────────────────────────────────────

export type GameMode =
  | "ColourClubs"
  | "ColourDiamonds"
  | "ColourHearts"
  | "ColourSpades"
  | "NoTrumps"
  | "AllTrumps";

export type MultiplierState = "None" | "Doubled" | "Redoubled";

// ─── Trick ──────────────────────────────────────────────────────────

export interface TrickState {
  leader: PlayerPosition;
  trickNumber: number;
  playedCards: PlayedCard[];
  isComplete: boolean;
}

// ─── Hand State ─────────────────────────────────────────────────────

export interface HandState {
  gameMode: GameMode;
  team1CardPoints: number;
  team2CardPoints: number;
  team1TricksWon: number;
  team2TricksWon: number;
  currentTrick: TrickState | null;
  completedTricks: TrickState[];
}

// ─── Negotiation ────────────────────────────────────────────────────

export interface AnnouncementAction {
  type: "Announcement";
  player?: PlayerPosition;
  mode: GameMode;
}

export interface AcceptAction {
  type: "Accept";
  player?: PlayerPosition;
}

export interface DoubleAction {
  type: "Double";
  player?: PlayerPosition;
  targetMode: GameMode;
}

export interface RedoubleAction {
  type: "Redouble";
  player?: PlayerPosition;
  targetMode: GameMode;
}

export type NegotiationAction =
  | AnnouncementAction
  | AcceptAction
  | DoubleAction
  | RedoubleAction;

/** Valid action choice (no player field — the server knows who you are). */
export type NegotiationActionChoice =
  | { type: "Announcement"; mode: GameMode }
  | { type: "Accept" }
  | { type: "Double"; targetMode: GameMode }
  | { type: "Redouble"; targetMode: GameMode };

export interface NegotiationState {
  dealer: PlayerPosition;
  currentPlayer: PlayerPosition;
  currentBid: GameMode | null;
  currentBidder: PlayerPosition | null;
  consecutiveAccepts: number;
  hasDoubleOccurred: boolean;
  actions: NegotiationAction[];
  doubledModes: Record<string, boolean>;
  redoubledModes: string[];
  teamColourAnnouncements: Record<string, string>;
}

// ─── Scoring ────────────────────────────────────────────────────────

export interface DealResult {
  gameMode: GameMode;
  multiplier: MultiplierState;
  announcerTeam: Team;
  team1CardPoints: number;
  team2CardPoints: number;
  team1MatchPoints: number;
  team2MatchPoints: number;
  wasSweep: boolean;
  sweepingTeam: Team | null;
  isInstantWin: boolean;
}

// ─── Match State ────────────────────────────────────────────────────

export interface MatchState {
  targetScore: number;
  team1MatchPoints: number;
  team2MatchPoints: number;
  currentDealer: PlayerPosition;
  isComplete: boolean;
  winner: Team | null;
  completedDeals: DealResult[];
}

// ─── Session ────────────────────────────────────────────────────────

export interface Session {
  position: PlayerPosition;
  matchId: string;
}

// ─── Bot Contexts (passed to your functions) ────────────────────────

export interface ChooseCutContext {
  deckSize: number;
  matchState: MatchState;
  session: Session;
}

export interface CutResult {
  position: number;
  fromTop: boolean;
}

export interface ChooseNegotiationActionContext {
  hand: Card[];
  negotiationState: NegotiationState;
  matchState: MatchState;
  validActions: NegotiationActionChoice[];
  session: Session;
}

export interface ChooseCardContext {
  hand: Card[];
  handState: HandState;
  matchState: MatchState;
  validPlays: Card[];
  session: Session;
}

export interface DealStartedContext {
  matchState: MatchState;
  session: Session;
}

export interface CardPlayedContext {
  player: PlayerPosition;
  card: Card;
  handState: HandState;
  matchState: MatchState;
  session: Session;
}

export interface TrickCompletedContext {
  completedTrick: TrickState;
  winner: PlayerPosition;
  handState: HandState;
  matchState: MatchState;
  session: Session;
}

export interface DealEndedContext {
  result: DealResult;
  handState: HandState;
  matchState: MatchState;
  session: Session;
}

export interface MatchEndedContext {
  matchState: MatchState;
  session: Session;
}
