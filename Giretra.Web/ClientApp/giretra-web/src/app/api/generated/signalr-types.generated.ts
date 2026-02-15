/**
 * SignalR Hub types for Giretra game.
 * This file is manually maintained to match the server-side SignalR hub.
 *
 * Hub URL: /hubs/game
 */

// ============================================================================
// Enums (matching Giretra.Core)
// ============================================================================

export enum PlayerPosition {
  Bottom = 'Bottom',
  Left = 'Left',
  Top = 'Top',
  Right = 'Right',
}

export enum Team {
  Team1 = 'Team1',
  Team2 = 'Team2',
}

export enum PendingActionType {
  Cut = 'Cut',
  Negotiate = 'Negotiate',
  PlayCard = 'PlayCard',
  ContinueDeal = 'ContinueDeal',
  ContinueMatch = 'ContinueMatch',
}

export enum GameMode {
  ColourClubs = 'ColourClubs',
  ColourDiamonds = 'ColourDiamonds',
  ColourHearts = 'ColourHearts',
  ColourSpades = 'ColourSpades',
  SansAs = 'SansAs',
  ToutAs = 'ToutAs',
}

export enum CardRank {
  Seven = 'Seven',
  Eight = 'Eight',
  Nine = 'Nine',
  Ten = 'Ten',
  Jack = 'Jack',
  Queen = 'Queen',
  King = 'King',
  Ace = 'Ace',
}

export enum CardSuit {
  Clubs = 'Clubs',
  Diamonds = 'Diamonds',
  Hearts = 'Hearts',
  Spades = 'Spades',
}

export enum SeatAccessMode {
  Public = 'Public',
  InviteOnly = 'InviteOnly',
}

// ============================================================================
// Shared Types
// ============================================================================

export interface CardResponse {
  rank: CardRank;
  suit: CardSuit;
}

export interface PlayedCardResponse {
  player: PlayerPosition;
  card: CardResponse;
}

export interface TrickResponse {
  leader: PlayerPosition;
  trickNumber: number;
  playedCards: PlayedCardResponse[];
  isComplete: boolean;
  winner?: PlayerPosition;
}

export interface CardPointsBreakdownResponse {
  jacks: number;
  nines: number;
  aces: number;
  tens: number;
  kings: number;
  queens: number;
  lastTrickBonus: number;
  total: number;
}

// ============================================================================
// Hub Events (Server -> Client)
// ============================================================================

export interface PlayerJoinedEvent {
  roomId: string;
  playerName: string;
  position: PlayerPosition;
}

export interface PlayerLeftEvent {
  roomId: string;
  playerName: string;
  position: PlayerPosition;
}

export interface GameStartedEvent {
  roomId: string;
  gameId: string;
}

export interface DealStartedEvent {
  gameId: string;
  dealer: PlayerPosition;
  dealNumber: number;
}

export interface DealEndedEvent {
  gameId: string;
  gameMode: GameMode;
  team1CardPoints: number;
  team2CardPoints: number;
  team1MatchPointsEarned: number;
  team2MatchPointsEarned: number;
  team1TotalMatchPoints: number;
  team2TotalMatchPoints: number;
  wasSweep: boolean;
  sweepingTeam?: Team;
  team1Breakdown: CardPointsBreakdownResponse;
  team2Breakdown: CardPointsBreakdownResponse;
}

export interface YourTurnEvent {
  gameId: string;
  position: PlayerPosition;
  actionType: PendingActionType;
  timeoutAt: string;
}

export interface PlayerTurnEvent {
  gameId: string;
  position: PlayerPosition;
  actionType: PendingActionType;
  timeoutAt: string;
}

export interface CardPlayedEvent {
  gameId: string;
  player: PlayerPosition;
  card: CardResponse;
}

export interface TrickCompletedEvent {
  gameId: string;
  trick: TrickResponse;
  winner: PlayerPosition;
  team1CardPoints: number;
  team2CardPoints: number;
}

export interface MatchEndedEvent {
  gameId: string;
  winner: Team;
  team1MatchPoints: number;
  team2MatchPoints: number;
  totalDeals: number;
}

export interface PlayerKickedEvent {
  roomId: string;
  playerName: string;
  position: PlayerPosition;
}

export interface SeatModeChangedEvent {
  roomId: string;
  position: PlayerPosition;
  accessMode: SeatAccessMode;
}

// ============================================================================
// Hub Methods (Client -> Server)
// ============================================================================

export interface GameHubMethods {
  joinRoom(roomId: string, clientId: string): Promise<void>;
  leaveRoom(roomId: string, clientId: string): Promise<void>;
}

// ============================================================================
// Hub Event Handlers (Server -> Client)
// ============================================================================

export interface GameHubEvents {
  onPlayerJoined(callback: (event: PlayerJoinedEvent) => void): void;
  onPlayerLeft(callback: (event: PlayerLeftEvent) => void): void;
  onGameStarted(callback: (event: GameStartedEvent) => void): void;
  onDealStarted(callback: (event: DealStartedEvent) => void): void;
  onDealEnded(callback: (event: DealEndedEvent) => void): void;
  onYourTurn(callback: (event: YourTurnEvent) => void): void;
  onPlayerTurn(callback: (event: PlayerTurnEvent) => void): void;
  onCardPlayed(callback: (event: CardPlayedEvent) => void): void;
  onTrickCompleted(callback: (event: TrickCompletedEvent) => void): void;
  onMatchEnded(callback: (event: MatchEndedEvent) => void): void;
  onPlayerKicked(callback: (event: PlayerKickedEvent) => void): void;
  onSeatModeChanged(callback: (event: SeatModeChangedEvent) => void): void;
}

// ============================================================================
// SignalR Event Names (for use with HubConnection.on())
// ============================================================================

export const GameHubEventNames = {
  PlayerJoined: 'PlayerJoined',
  PlayerLeft: 'PlayerLeft',
  GameStarted: 'GameStarted',
  DealStarted: 'DealStarted',
  DealEnded: 'DealEnded',
  YourTurn: 'YourTurn',
  PlayerTurn: 'PlayerTurn',
  CardPlayed: 'CardPlayed',
  TrickCompleted: 'TrickCompleted',
  MatchEnded: 'MatchEnded',
  PlayerKicked: 'PlayerKicked',
  SeatModeChanged: 'SeatModeChanged',
} as const;

export type GameHubEventName = (typeof GameHubEventNames)[keyof typeof GameHubEventNames];
