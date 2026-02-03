import { Injectable, inject, signal, computed, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  CardResponse,
  GameMode,
  PlayerPosition,
  Team,
  PendingActionType,
} from '../../api/generated/signalr-types.generated';
import { GameHubService } from '../../api/game-hub.service';
import { ApiService, GameStateResponse, PlayerStateResponse, RoomResponse, ValidAction, TrickResponse, NegotiationAction } from './api.service';
import { ClientSessionService } from './client-session.service';
import { environment } from '../../../environments/environment';
import { getTeam } from '../utils/position-utils';

export type GamePhase =
  | 'waiting'
  | 'cut'
  | 'negotiation'
  | 'playing'
  | 'dealSummary'
  | 'matchEnd';

export type MultiplierState = 'Normal' | 'Doubled' | 'Redoubled';

@Injectable({
  providedIn: 'root',
})
export class GameStateService {
  private readonly api = inject(ApiService);
  private readonly hub = inject(GameHubService);
  private readonly session = inject(ClientSessionService);
  private readonly destroyRef = inject(DestroyRef);

  // ─────────────────────────────────────────────────────────────────────────
  // Room State Signals
  // ─────────────────────────────────────────────────────────────────────────
  private readonly _currentRoom = signal<RoomResponse | null>(null);
  private readonly _gameId = signal<string | null>(null);
  private readonly _isCreator = signal<boolean>(false);

  readonly currentRoom = this._currentRoom.asReadonly();
  readonly gameId = this._gameId.asReadonly();
  readonly isCreator = this._isCreator.asReadonly();

  // ─────────────────────────────────────────────────────────────────────────
  // Game State Signals (from server)
  // ─────────────────────────────────────────────────────────────────────────
  private readonly _gameState = signal<GameStateResponse | null>(null);
  private readonly _playerState = signal<PlayerStateResponse | null>(null);
  private readonly _playerCardCounts = signal<Record<PlayerPosition, number> | null>(null);

  readonly gameState = this._gameState.asReadonly();
  readonly playerState = this._playerState.asReadonly();
  readonly playerCardCounts = this._playerCardCounts.asReadonly();

  // ─────────────────────────────────────────────────────────────────────────
  // Deal Summary State (transient, shown between deals)
  // ─────────────────────────────────────────────────────────────────────────
  private readonly _showingDealSummary = signal<boolean>(false);
  private readonly _dealSummary = signal<{
    gameMode: GameMode;
    team1CardPoints: number;
    team2CardPoints: number;
    team1MatchPointsEarned: number;
    team2MatchPointsEarned: number;
    team1TotalMatchPoints: number;
    team2TotalMatchPoints: number;
    wasSweep: boolean;
    sweepingTeam: Team | null;
  } | null>(null);

  readonly showingDealSummary = this._showingDealSummary.asReadonly();
  readonly dealSummary = this._dealSummary.asReadonly();

  // ─────────────────────────────────────────────────────────────────────────
  // Computed Signals: Derived State
  // ─────────────────────────────────────────────────────────────────────────

  /** Current high-level phase for UI rendering */
  readonly phase = computed<GamePhase>(() => {
    // Match end takes precedence
    if (this._gameState()?.isComplete) return 'matchEnd';

    // Deal summary overlay
    if (this._showingDealSummary()) return 'dealSummary';

    const room = this._currentRoom();
    const game = this._gameState();

    // No game started yet
    if (!game || room?.status === 'Waiting') return 'waiting';

    // Map server phase to UI phase
    switch (game.phase) {
      case 'AwaitingCut':
      case 'InitialDistribution':
        return 'cut';
      case 'Negotiation':
      case 'FinalDistribution':
        return 'negotiation';
      case 'Playing':
        return 'playing';
      case 'Completed':
        return game.isComplete ? 'matchEnd' : 'dealSummary';
      default:
        return 'waiting';
    }
  });

  /** My position in the game */
  readonly myPosition = computed(() => this.session.position());

  /** My team */
  readonly myTeam = computed(() => {
    const pos = this.myPosition();
    return pos ? getTeam(pos) : null;
  });

  /** Is it my turn? */
  readonly isMyTurn = computed(() => {
    const playerState = this._playerState();
    return playerState?.isYourTurn ?? false;
  });

  /** What action is pending for me? */
  readonly pendingActionType = computed<PendingActionType | null>(() => {
    const playerState = this._playerState();
    if (!playerState?.isYourTurn) return null;

    switch (playerState.pendingActionType) {
      case 'Cut':
        return PendingActionType.Cut;
      case 'Negotiate':
        return PendingActionType.Negotiate;
      case 'PlayCard':
        return PendingActionType.PlayCard;
      default:
        return null;
    }
  });

  /** My hand of cards */
  readonly hand = computed(() => this._playerState()?.hand ?? []);

  /** Cards I can legally play */
  readonly validCards = computed(() => this._playerState()?.validCards ?? []);

  /** Actions I can take during negotiation */
  readonly validActions = computed(() => this._playerState()?.validActions ?? []);

  /** Current trick being played */
  readonly currentTrick = computed(() => this._gameState()?.currentTrick ?? null);

  /** Who is the active player? */
  readonly activePlayer = computed(() => this._gameState()?.pendingActionPlayer ?? null);

  /** Current game mode (trump) */
  readonly gameMode = computed(() => this._gameState()?.gameMode ?? null);

  /** Multiplier state */
  readonly multiplier = computed<MultiplierState>(() => {
    const m = this._gameState()?.multiplier;
    return (m as MultiplierState) ?? 'Normal';
  });

  /** Team 1 match points */
  readonly team1MatchPoints = computed(() => this._gameState()?.team1MatchPoints ?? 0);

  /** Team 2 match points */
  readonly team2MatchPoints = computed(() => this._gameState()?.team2MatchPoints ?? 0);

  /** Team 1 card points this deal */
  readonly team1CardPoints = computed(() => this._gameState()?.team1CardPoints ?? 0);

  /** Team 2 card points this deal */
  readonly team2CardPoints = computed(() => this._gameState()?.team2CardPoints ?? 0);

  /** Current deal number */
  readonly dealNumber = computed(() => (this._gameState()?.completedDealsCount ?? 0) + 1);

  /** Dealer position */
  readonly dealer = computed(() => this._gameState()?.dealer ?? null);

  /** Negotiation history */
  readonly negotiationHistory = computed(() => this._gameState()?.negotiationHistory ?? []);

  /** Completed tricks */
  readonly completedTricks = computed(() => this._gameState()?.completedTricks ?? []);

  /** Match winner */
  readonly matchWinner = computed(() => this._gameState()?.winner ?? null);

  /** Is watcher mode? */
  readonly isWatcher = computed(() => this.session.isWatcher());

  constructor() {
    this.setupHubListeners();
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Public Methods
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Initialize state for a room
   */
  async enterRoom(room: RoomResponse, isCreator: boolean): Promise<void> {
    this._currentRoom.set(room);
    this._isCreator.set(isCreator);
    this._gameId.set(room.gameId);

    // Connect to SignalR hub
    await this.hub.connect(environment.hubUrl);
    const clientId = this.session.clientId();
    if (clientId) {
      await this.hub.joinRoom(room.roomId, clientId);
    }

    // If game is in progress, fetch state
    if (room.gameId) {
      await this.refreshState();
    }
  }

  /**
   * Leave current room and clean up
   */
  async leaveRoom(): Promise<void> {
    const roomId = this._currentRoom()?.roomId;
    const clientId = this.session.clientId();

    if (roomId && clientId) {
      try {
        await this.hub.leaveRoom(roomId, clientId);
      } catch (e) {
        console.warn('Failed to leave room via hub', e);
      }
    }

    await this.hub.disconnect();

    this._currentRoom.set(null);
    this._gameId.set(null);
    this._gameState.set(null);
    this._playerState.set(null);
    this._playerCardCounts.set(null);
    this._isCreator.set(false);
    this._showingDealSummary.set(false);
    this._dealSummary.set(null);
  }

  /**
   * Refresh game/player state from server
   */
  async refreshState(): Promise<void> {
    const gameId = this._gameId();
    const clientId = this.session.clientId();
    const isWatcher = this.session.isWatcher();

    if (!gameId) return;

    try {
      if (isWatcher) {
        const watcherState = await this.api.getWatcherState(gameId).toPromise();
        if (watcherState) {
          this._gameState.set(watcherState.gameState);
          this._playerCardCounts.set(watcherState.playerCardCounts);
        }
      } else if (clientId) {
        const playerState = await this.api.getPlayerState(gameId, clientId).toPromise();
        if (playerState) {
          this._playerState.set(playerState);
          this._gameState.set(playerState.gameState);
        }
      }
    } catch (e) {
      console.error('Failed to refresh game state', e);
    }
  }

  /**
   * Update room data (for player join/leave events)
   */
  updateRoom(room: RoomResponse): void {
    this._currentRoom.set(room);
  }

  /**
   * Set game ID when game starts
   */
  setGameId(gameId: string): void {
    this._gameId.set(gameId);
  }

  /**
   * Show deal summary overlay
   */
  showDealSummary(summary: {
    gameMode: GameMode;
    team1CardPoints: number;
    team2CardPoints: number;
    team1MatchPointsEarned: number;
    team2MatchPointsEarned: number;
    team1TotalMatchPoints: number;
    team2TotalMatchPoints: number;
    wasSweep: boolean;
    sweepingTeam: Team | null;
  }): void {
    this._dealSummary.set(summary);
    this._showingDealSummary.set(true);
  }

  /**
   * Hide deal summary overlay
   */
  hideDealSummary(): void {
    this._showingDealSummary.set(false);
    this._dealSummary.set(null);
  }

  // ─────────────────────────────────────────────────────────────────────────
  // SignalR Event Handlers
  // ─────────────────────────────────────────────────────────────────────────

  private setupHubListeners(): void {
    // Player joined room
    this.hub.playerJoined$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      const room = this._currentRoom();
      if (room && room.roomId === event.roomId) {
        // Update room state - fetch fresh data
        this.api.getRoom(event.roomId).subscribe((r) => this._currentRoom.set(r));
      }
    });

    // Player left room
    this.hub.playerLeft$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      const room = this._currentRoom();
      if (room && room.roomId === event.roomId) {
        this.api.getRoom(event.roomId).subscribe((r) => this._currentRoom.set(r));
      }
    });

    // Game started
    this.hub.gameStarted$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      this._gameId.set(event.gameId);
      this.refreshState();
    });

    // Deal started
    this.hub.dealStarted$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.hideDealSummary();
      this.refreshState();
    });

    // Your turn
    this.hub.yourTurn$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.refreshState();
    });

    // Player turn (broadcast)
    this.hub.playerTurn$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.refreshState();
    });

    // Card played
    this.hub.cardPlayed$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.refreshState();
    });

    // Trick completed
    this.hub.trickCompleted$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.refreshState();
    });

    // Deal ended
    this.hub.dealEnded$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      this.showDealSummary({
        gameMode: event.gameMode,
        team1CardPoints: event.team1CardPoints,
        team2CardPoints: event.team2CardPoints,
        team1MatchPointsEarned: event.team1MatchPointsEarned,
        team2MatchPointsEarned: event.team2MatchPointsEarned,
        team1TotalMatchPoints: event.team1TotalMatchPoints,
        team2TotalMatchPoints: event.team2TotalMatchPoints,
        wasSweep: event.wasSweep,
        sweepingTeam: event.sweepingTeam ?? null,
      });
    });

    // Match ended
    this.hub.matchEnded$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.refreshState();
    });
  }
}
