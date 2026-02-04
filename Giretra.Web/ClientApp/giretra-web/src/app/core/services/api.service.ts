import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { API_BASE_URL } from '../../app.config';
import {
  CardRank,
  CardResponse,
  CardSuit,
  GameMode,
  PlayerPosition,
  Team,
} from '../../api/generated/signalr-types.generated';

// ============================================================================
// API Response Types (matching WEB-API-SPEC.md)
// ============================================================================

export interface PlayerSlot {
  position: PlayerPosition;
  isOccupied: boolean;
  playerName: string | null;
  isAi: boolean;
}

export interface RoomResponse {
  roomId: string;
  name: string;
  status: 'Waiting' | 'Playing' | 'Completed';
  playerCount: number;
  watcherCount: number;
  playerSlots: PlayerSlot[];
  gameId: string | null;
  createdAt: string;
}

export interface RoomListResponse {
  rooms: RoomResponse[];
  totalCount: number;
}

export interface CreateRoomResponse {
  clientId: string;
  position: PlayerPosition;
  room: RoomResponse;
}

export interface JoinRoomResponse {
  clientId: string;
  position: PlayerPosition | null;
  room: RoomResponse;
}

export interface StartGameResponse {
  gameId: string;
  roomId: string;
}

export interface NegotiationAction {
  actionType: 'Announce' | 'Accept' | 'Double' | 'Redouble';
  player: PlayerPosition;
  mode: GameMode | null;
}

export interface ValidAction {
  actionType: 'Announce' | 'Accept' | 'Double' | 'Redouble';
  mode: GameMode | null;
}

export interface TrickResponse {
  leader: PlayerPosition;
  trickNumber: number;
  playedCards: Array<{ player: PlayerPosition; card: CardResponse }>;
  isComplete: boolean;
  winner: PlayerPosition | null;
}

export interface GameStateResponse {
  gameId: string;
  roomId: string;
  targetScore: number;
  team1MatchPoints: number;
  team2MatchPoints: number;
  dealer: PlayerPosition;
  phase: 'AwaitingCut' | 'InitialDistribution' | 'Negotiation' | 'FinalDistribution' | 'Playing' | 'Completed';
  completedDealsCount: number;
  gameMode: GameMode | null;
  multiplier: 'Normal' | 'Doubled' | 'Redoubled';
  currentTrick: TrickResponse | null;
  completedTricks: TrickResponse[];
  team1CardPoints: number;
  team2CardPoints: number;
  negotiationHistory: NegotiationAction[];
  currentBid: GameMode | null;
  isComplete: boolean;
  winner: Team | null;
  pendingActionType: 'Cut' | 'Negotiate' | 'PlayCard' | null;
  pendingActionPlayer: PlayerPosition | null;
}

export interface PlayerStateResponse {
  position: PlayerPosition;
  hand: CardResponse[];
  isYourTurn: boolean;
  pendingActionType: 'Cut' | 'Negotiate' | 'PlayCard' | null;
  validCards: CardResponse[] | null;
  validActions: ValidAction[] | null;
  gameState: GameStateResponse;
}

export interface WatcherStateResponse {
  gameState: GameStateResponse;
  playerCardCounts: Record<PlayerPosition, number>;
}

export interface ApiError {
  type: string;
  title: string;
  status: number;
  detail: string;
}

// ============================================================================
// API Service
// ============================================================================

@Injectable({
  providedIn: 'root',
})
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  // ─────────────────────────────────────────────────────────────────────────
  // Rooms
  // ─────────────────────────────────────────────────────────────────────────

  listRooms(): Observable<RoomListResponse> {
    return this.http
      .get<RoomListResponse>(`${this.baseUrl}/api/rooms`)
      .pipe(catchError(this.handleError));
  }

  getRoom(roomId: string): Observable<RoomResponse> {
    return this.http
      .get<RoomResponse>(`${this.baseUrl}/api/rooms/${roomId}`)
      .pipe(catchError(this.handleError));
  }

  createRoom(name: string | null, creatorName: string, fillWithAi = false): Observable<CreateRoomResponse> {
    return this.http
      .post<CreateRoomResponse>(`${this.baseUrl}/api/rooms`, { name, creatorName, fillWithAi })
      .pipe(catchError(this.handleError));
  }

  deleteRoom(roomId: string, clientId: string): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl}/api/rooms/${roomId}?clientId=${clientId}`)
      .pipe(catchError(this.handleError));
  }

  joinRoom(
    roomId: string,
    displayName: string,
    preferredPosition?: PlayerPosition
  ): Observable<JoinRoomResponse> {
    return this.http
      .post<JoinRoomResponse>(`${this.baseUrl}/api/rooms/${roomId}/join`, {
        displayName,
        preferredPosition,
      })
      .pipe(catchError(this.handleError));
  }

  watchRoom(roomId: string, displayName: string): Observable<JoinRoomResponse> {
    return this.http
      .post<JoinRoomResponse>(`${this.baseUrl}/api/rooms/${roomId}/watch`, {
        displayName,
      })
      .pipe(catchError(this.handleError));
  }

  leaveRoom(roomId: string, clientId: string): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/api/rooms/${roomId}/leave`, { clientId })
      .pipe(catchError(this.handleError));
  }

  startGame(roomId: string, clientId: string): Observable<StartGameResponse> {
    return this.http
      .post<StartGameResponse>(`${this.baseUrl}/api/rooms/${roomId}/start`, { clientId })
      .pipe(catchError(this.handleError));
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Games
  // ─────────────────────────────────────────────────────────────────────────

  getGameState(gameId: string): Observable<GameStateResponse> {
    return this.http
      .get<GameStateResponse>(`${this.baseUrl}/api/games/${gameId}`)
      .pipe(catchError(this.handleError));
  }

  getPlayerState(gameId: string, clientId: string): Observable<PlayerStateResponse> {
    return this.http
      .get<PlayerStateResponse>(`${this.baseUrl}/api/games/${gameId}/player/${clientId}`)
      .pipe(catchError(this.handleError));
  }

  getWatcherState(gameId: string): Observable<WatcherStateResponse> {
    return this.http
      .get<WatcherStateResponse>(`${this.baseUrl}/api/games/${gameId}/watch`)
      .pipe(catchError(this.handleError));
  }

  submitCut(
    gameId: string,
    clientId: string,
    position: number,
    fromTop: boolean
  ): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/api/games/${gameId}/cut`, {
        clientId,
        position,
        fromTop,
      })
      .pipe(catchError(this.handleError));
  }

  submitNegotiation(
    gameId: string,
    clientId: string,
    actionType: 'Announce' | 'Accept' | 'Double' | 'Redouble',
    mode?: GameMode | null
  ): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/api/games/${gameId}/negotiate`, {
        clientId,
        actionType,
        mode,
      })
      .pipe(catchError(this.handleError));
  }

  playCard(
    gameId: string,
    clientId: string,
    rank: CardRank,
    suit: CardSuit
  ): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/api/games/${gameId}/play`, {
        clientId,
        rank,
        suit,
      })
      .pipe(catchError(this.handleError));
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Error Handling
  // ─────────────────────────────────────────────────────────────────────────

  private handleError(error: HttpErrorResponse): Observable<never> {
    let message = 'An error occurred';

    if (error.error && typeof error.error === 'object') {
      // Try different error response formats
      if ('detail' in error.error) {
        message = (error.error as ApiError).detail;
      } else if ('error' in error.error) {
        message = error.error.error;
      } else if ('message' in error.error) {
        message = error.error.message;
      }
    } else if (error.message) {
      message = error.message;
    }

    console.error('API Error:', error);
    console.error('Error message:', message);
    return throwError(() => new Error(message));
  }
}
