import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { HotToastService } from '@ngxpert/hot-toast';
import { TranslocoService } from '@jsverse/transloco';
import { API_BASE_URL } from '../../app.config';
import {
  CardRank,
  CardResponse,
  CardSuit,
  GameMode,
  PlayerPosition,
  SeatAccessMode,
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
  aiType: string | null;
  aiDisplayName: string | null;
  accessMode: SeatAccessMode;
  hasInvite: boolean;
  isCurrentUser: boolean;
}

export interface AiTypeInfo {
  name: string;
  displayName: string;
  difficulty: number;
  rating: number;
  pun: string | null;
  description: string | null;
  author: string | null;
}

export interface AiSeat {
  position: PlayerPosition;
  aiType: string;
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
  turnTimerSeconds: number;
  isOwner: boolean;
  isDisconnectedPlayer?: boolean;
  isRanked: boolean;
}

export interface InviteTokenResponse {
  position: PlayerPosition;
  token: string;
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
  pendingActionType: 'Cut' | 'Negotiate' | 'PlayCard' | 'ContinueDeal' | 'ContinueMatch' | null;
  pendingActionPlayer: PlayerPosition | null;
  pendingActionTimeoutAt: string | null;
}

export interface PlayerStateResponse {
  position: PlayerPosition;
  hand: CardResponse[];
  isYourTurn: boolean;
  pendingActionType: 'Cut' | 'Negotiate' | 'PlayCard' | 'ContinueDeal' | 'ContinueMatch' | null;
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
// Settings Response Types
// ============================================================================

export interface ProfileResponse {
  username: string;
  displayName: string;
  avatarUrl: string | null;
  eloRating: number;
  eloIsPublic: boolean;
  gamesPlayed: number;
  gamesWon: number;
  winStreak: number;
  bestWinStreak: number;
  createdAt: string;
}

export interface FriendResponse {
  userId: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
  friendsSince: string;
}

export interface FriendRequestResponse {
  friendshipId: string;
  userId: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
  sentAt: string;
}

export interface FriendsListResponse {
  friends: FriendResponse[];
  pendingReceived: FriendRequestResponse[];
  pendingSent: FriendRequestResponse[];
}

export interface BlockedUserResponse {
  blockId: string;
  userId: string;
  username: string;
  displayName: string;
  blockedAt: string;
}

export interface MatchHistoryPlayerResponse {
  displayName: string;
  position: PlayerPosition;
  team: Team;
  isWinner: boolean;
}

export interface MatchHistoryItemResponse {
  matchId: string;
  roomName: string;
  team1FinalScore: number;
  team2FinalScore: number;
  team: Team;
  position: PlayerPosition;
  isWinner: boolean;
  eloChange: number | null;
  totalDeals: number;
  wasAbandoned: boolean;
  durationSeconds: number | null;
  playedAt: string;
  players: MatchHistoryPlayerResponse[];
}

export interface MatchHistoryListResponse {
  matches: MatchHistoryItemResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface UserSearchResultResponse {
  userId: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
}

export interface UserSearchResponse {
  results: UserSearchResultResponse[];
}

export interface PendingCountResponse {
  count: number;
}

export interface LeaderboardPlayerEntry {
  playerId: string;
  rank: number;
  displayName: string;
  avatarUrl: string | null;
  rating: number;
  gamesPlayed: number;
  winRate: number;
}

export interface LeaderboardBotEntry {
  playerId: string;
  rank: number;
  displayName: string;
  rating: number;
  gamesPlayed: number;
  winRate: number;
  author: string | null;
  difficulty: number;
}

export interface LeaderboardResponse {
  players: LeaderboardPlayerEntry[];
  bots: LeaderboardBotEntry[];
  playerCount: number;
  botCount: number;
}

export interface PlayerProfileResponse {
  displayName: string;
  isBot: boolean;
  gamesPlayed: number;
  gamesWon: number;
  winStreak: number;
  bestWinStreak: number;
  // Human-only
  avatarUrl: string | null;
  eloRating: number | null;
  memberSince: string | null;
  // Bot-only
  description: string | null;
  author: string | null;
  pun: string | null;
  difficulty: number | null;
  botRating: number | null;
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
  private readonly toast = inject(HotToastService);
  private readonly transloco = inject(TranslocoService);

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

  createRoom(name: string | null, aiSeats: AiSeat[] = [], turnTimerSeconds?: number, inviteOnly = false, isRanked = true): Observable<CreateRoomResponse> {
    return this.http
      .post<CreateRoomResponse>(`${this.baseUrl}/api/rooms`, { name, aiSeats, turnTimerSeconds, inviteOnly, isRanked })
      .pipe(catchError(this.handleError));
  }

  getAiTypes(): Observable<AiTypeInfo[]> {
    return this.http
      .get<AiTypeInfo[]>(`${this.baseUrl}/api/ai-types`)
      .pipe(catchError(this.handleError));
  }

  deleteRoom(roomId: string): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl}/api/rooms/${roomId}`)
      .pipe(catchError(this.handleError));
  }

  joinRoom(
    roomId: string,
    preferredPosition?: PlayerPosition,
    inviteToken?: string
  ): Observable<JoinRoomResponse> {
    return this.http
      .post<JoinRoomResponse>(`${this.baseUrl}/api/rooms/${roomId}/join`, {
        preferredPosition,
        inviteToken,
      })
      .pipe(catchError(this.handleError));
  }

  watchRoom(roomId: string): Observable<JoinRoomResponse> {
    return this.http
      .post<JoinRoomResponse>(`${this.baseUrl}/api/rooms/${roomId}/watch`, {})
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

  rejoinRoom(roomId: string): Observable<JoinRoomResponse> {
    return this.http
      .post<JoinRoomResponse>(`${this.baseUrl}/api/rooms/${roomId}/rejoin`, {})
      .pipe(catchError(this.handleError));
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Seat Management
  // ─────────────────────────────────────────────────────────────────────────

  setSeatMode(roomId: string, position: PlayerPosition, accessMode: SeatAccessMode): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/api/rooms/${roomId}/seats/${position}/mode`, { position, accessMode })
      .pipe(catchError(this.handleError));
  }

  generateInvite(roomId: string, position: PlayerPosition): Observable<InviteTokenResponse> {
    return this.http
      .post<InviteTokenResponse>(`${this.baseUrl}/api/rooms/${roomId}/seats/${position}/invite`, {})
      .pipe(catchError(this.handleError));
  }

  kickPlayer(roomId: string, position: PlayerPosition): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/api/rooms/${roomId}/seats/${position}/kick`, {})
      .pipe(catchError(this.handleError));
  }

  getPlayerProfile(roomId: string, position: PlayerPosition): Observable<PlayerProfileResponse> {
    return this.http
      .get<PlayerProfileResponse>(`${this.baseUrl}/api/rooms/${roomId}/seats/${position}/profile`)
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

  submitContinueDeal(gameId: string, clientId: string): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/api/games/${gameId}/continue`, {
        clientId,
      })
      .pipe(catchError(this.handleError));
  }

  submitContinueMatch(gameId: string, clientId: string): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/api/games/${gameId}/continue-match`, {
        clientId,
      })
      .pipe(catchError(this.handleError));
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Current User
  // ─────────────────────────────────────────────────────────────────────────

  getMe(): Observable<{ displayName: string }> {
    return this.http
      .get<{ displayName: string }>(`${this.baseUrl}/api/me`)
      .pipe(catchError(this.handleError));
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Settings
  // ─────────────────────────────────────────────────────────────────────────

  getProfile(): Observable<ProfileResponse> {
    return this.http
      .get<ProfileResponse>(`${this.baseUrl}/api/settings/profile`)
      .pipe(catchError(this.handleError));
  }

  updateDisplayName(displayName: string): Observable<void> {
    return this.http
      .put<void>(`${this.baseUrl}/api/settings/profile/display-name`, { displayName })
      .pipe(catchError(this.handleError));
  }

  uploadAvatar(file: File): Observable<{ avatarUrl: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http
      .post<{ avatarUrl: string }>(`${this.baseUrl}/api/settings/profile/avatar`, formData)
      .pipe(catchError(this.handleError));
  }

  deleteAvatar(): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl}/api/settings/profile/avatar`)
      .pipe(catchError(this.handleError));
  }

  updateEloVisibility(isPublic: boolean): Observable<void> {
    return this.http
      .put<void>(`${this.baseUrl}/api/settings/profile/elo-visibility`, { isPublic })
      .pipe(catchError(this.handleError));
  }

  getFriends(): Observable<FriendsListResponse> {
    return this.http
      .get<FriendsListResponse>(`${this.baseUrl}/api/settings/friends`)
      .pipe(catchError(this.handleError));
  }

  getPendingFriendCount(): Observable<PendingCountResponse> {
    return this.http
      .get<PendingCountResponse>(`${this.baseUrl}/api/settings/friends/pending-count`)
      .pipe(catchError(this.handleError));
  }

  sendFriendRequest(username: string): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/api/settings/friends/request`, { username })
      .pipe(catchError(this.handleError));
  }

  acceptFriendRequest(friendshipId: string): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/api/settings/friends/${friendshipId}/accept`, {})
      .pipe(catchError(this.handleError));
  }

  declineFriendRequest(friendshipId: string): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/api/settings/friends/${friendshipId}/decline`, {})
      .pipe(catchError(this.handleError));
  }

  removeFriend(userId: string): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl}/api/settings/friends/${userId}`)
      .pipe(catchError(this.handleError));
  }

  searchUsers(query: string): Observable<UserSearchResponse> {
    return this.http
      .get<UserSearchResponse>(`${this.baseUrl}/api/settings/friends/search`, { params: { q: query } })
      .pipe(catchError(this.handleError));
  }

  getBlockedUsers(): Observable<BlockedUserResponse[]> {
    return this.http
      .get<BlockedUserResponse[]>(`${this.baseUrl}/api/settings/blocked`)
      .pipe(catchError(this.handleError));
  }

  blockUser(username: string, reason?: string): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/api/settings/blocked`, { username, reason })
      .pipe(catchError(this.handleError));
  }

  unblockUser(blockId: string): Observable<void> {
    return this.http
      .delete<void>(`${this.baseUrl}/api/settings/blocked/${blockId}`)
      .pipe(catchError(this.handleError));
  }

  getMatchHistory(page: number = 1, pageSize: number = 20): Observable<MatchHistoryListResponse> {
    return this.http
      .get<MatchHistoryListResponse>(`${this.baseUrl}/api/settings/matches`, { params: { page, pageSize } })
      .pipe(catchError(this.handleError));
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Leaderboard
  // ─────────────────────────────────────────────────────────────────────────

  getLeaderboard(): Observable<LeaderboardResponse> {
    return this.http
      .get<LeaderboardResponse>(`${this.baseUrl}/api/leaderboard`)
      .pipe(catchError(this.handleError));
  }

  getLeaderboardProfile(playerId: string): Observable<PlayerProfileResponse> {
    return this.http
      .get<PlayerProfileResponse>(`${this.baseUrl}/api/leaderboard/players/${playerId}`)
      .pipe(catchError(this.handleError));
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Error Handling
  // ─────────────────────────────────────────────────────────────────────────

  private handleError = (error: HttpErrorResponse): Observable<never> => {
    let message: string;

    if (error.status === 0) {
      // Network error — no response from server
      message = this.transloco.translate('errors.connectionIssue');
    } else if (error.status >= 500) {
      message = this.transloco.translate('errors.serverError');
    } else if (error.error && typeof error.error === 'object') {
      // Server validation/business errors (4xx)
      if ('detail' in error.error) {
        message = (error.error as ApiError).detail;
      } else if ('error' in error.error) {
        message = error.error.error;
      } else if ('message' in error.error) {
        message = error.error.message;
      } else {
        message = this.transloco.translate('errors.genericError');
      }
    } else if (error.message) {
      message = error.message;
    } else {
      message = this.transloco.translate('errors.genericError');
    }

    console.error('API Error:', error);
    this.toast.error(message);
    return throwError(() => new Error(message));
  };
}
