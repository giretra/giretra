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
  // Completed Trick Display State (brief pause after trick ends)
  // ─────────────────────────────────────────────────────────────────────────
  private readonly _showingCompletedTrick = signal<boolean>(false);
  private readonly _completedTrickToShow = signal<TrickResponse | null>(null);
  private _completedTrickTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private readonly COMPLETED_TRICK_DELAY_MS = 3000;

  readonly showingCompletedTrick = this._showingCompletedTrick.asReadonly();
  readonly completedTrickToShow = this._completedTrickToShow.asReadonly();

  // ─────────────────────────────────────────────────────────────────────────
  // Computed Signals: Derived State
  // ─────────────────────────────────────────────────────────────────────────

  /** Current high-level phase for UI rendering */
  readonly phase = computed<GamePhase>(() => {
    const gameState = this._gameState();
    const room = this._currentRoom();
    const showingSummary = this._showingDealSummary();

    console.log('[GameState] phase computed:', {
      gameStateIsComplete: gameState?.isComplete,
      showingSummary,
      roomStatus: room?.status,
      gamePhase: gameState?.phase,
      hasGame: !!gameState,
    });

    // Match end takes precedence
    if (gameState?.isComplete) return 'matchEnd';

    // Deal summary overlay
    if (showingSummary) return 'dealSummary';

    // No game started yet
    if (!gameState || room?.status === 'Waiting') {
      console.log('[GameState] → phase = waiting (no game or room waiting)');
      return 'waiting';
    }

    // Map server phase to UI phase
    let result: GamePhase;
    switch (gameState.phase) {
      case 'AwaitingCut':
      case 'InitialDistribution':
        result = 'cut';
        break;
      case 'Negotiation':
      case 'FinalDistribution':
        result = 'negotiation';
        break;
      case 'Playing':
        result = 'playing';
        break;
      case 'Completed':
        result = gameState.isComplete ? 'matchEnd' : 'dealSummary';
        break;
      default:
        result = 'waiting';
    }
    console.log('[GameState] → phase =', result);
    return result;
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
    const isMyTurn = playerState?.isYourTurn ?? false;
    console.log('[GameState] isMyTurn computed:', {
      isYourTurn: playerState?.isYourTurn,
      pendingActionType: playerState?.pendingActionType,
      hasPlayerState: !!playerState,
    });
    return isMyTurn;
  });

  /** What action is pending for me? */
  readonly pendingActionType = computed<PendingActionType | null>(() => {
    const playerState = this._playerState();
    if (!playerState?.isYourTurn) {
      console.log('[GameState] pendingActionType: not my turn');
      return null;
    }

    let result: PendingActionType | null = null;
    switch (playerState.pendingActionType) {
      case 'Cut':
        result = PendingActionType.Cut;
        break;
      case 'Negotiate':
        result = PendingActionType.Negotiate;
        break;
      case 'PlayCard':
        result = PendingActionType.PlayCard;
        break;
      default:
        result = null;
    }
    console.log('[GameState] pendingActionType:', result, '(from:', playerState.pendingActionType, ')');
    return result;
  });

  /** My hand of cards */
  readonly hand = computed(() => {
    const hand = this._playerState()?.hand ?? [];
    console.log('[GameState] hand computed:', hand.length, 'cards');
    return hand;
  });

  /** Cards I can legally play */
  readonly validCards = computed(() => {
    const validCards = this._playerState()?.validCards ?? [];
    console.log('[GameState] validCards computed:', validCards.length, 'cards', validCards);
    return validCards;
  });

  /** Actions I can take during negotiation */
  readonly validActions = computed(() => {
    const validActions = this._playerState()?.validActions ?? [];
    console.log('[GameState] validActions computed:', validActions.length, 'actions', validActions);
    return validActions;
  });

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
    console.log('[GameState] enterRoom', { room, isCreator });
    this._currentRoom.set(room);
    this._isCreator.set(isCreator);
    this._gameId.set(room.gameId);

    // Connect to SignalR hub
    await this.hub.connect(environment.hubUrl);
    const clientId = this.session.clientId();
    console.log('[GameState] Joining room with clientId', clientId);
    if (clientId) {
      await this.hub.joinRoom(room.roomId, clientId);
      console.log('[GameState] Joined room successfully');
    }

    // If game is in progress, fetch state
    if (room.gameId) {
      console.log('[GameState] Game in progress, fetching state');
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

    console.log('[GameState] refreshState called', { gameId, clientId, isWatcher });

    if (!gameId) {
      console.log('[GameState] No gameId, skipping refresh');
      return;
    }

    try {
      if (isWatcher) {
        const watcherState = await this.api.getWatcherState(gameId).toPromise();
        console.log('[GameState] Watcher state received', watcherState);
        if (watcherState) {
          this._gameState.set(watcherState.gameState);
          this._playerCardCounts.set(watcherState.playerCardCounts);
        }
      } else if (clientId) {
        const playerState = await this.api.getPlayerState(gameId, clientId).toPromise();
        console.log('[GameState] Player state received', playerState);
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

  /**
   * Dismiss completed trick display early (user clicked to continue)
   */
  dismissCompletedTrick(): void {
    if (this._completedTrickTimeoutId) {
      clearTimeout(this._completedTrickTimeoutId);
      this._completedTrickTimeoutId = null;
    }
    this._showingCompletedTrick.set(false);
    this._completedTrickToShow.set(null);
    // Now refresh state to get the next trick
    this.refreshState();
  }

  // ─────────────────────────────────────────────────────────────────────────
  // SignalR Event Handlers
  // ─────────────────────────────────────────────────────────────────────────

  private setupHubListeners(): void {
    console.log('[GameState] Setting up hub listeners');

    // Player joined room
    this.hub.playerJoined$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: playerJoined', event);
      const room = this._currentRoom();
      if (room && room.roomId === event.roomId) {
        // Update room state - fetch fresh data
        this.api.getRoom(event.roomId).subscribe((r) => {
          console.log('[GameState] Refreshed room after playerJoined:', r);
          this._currentRoom.set(r);
        });
      }
    });

    // Player left room
    this.hub.playerLeft$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: playerLeft', event);
      const room = this._currentRoom();
      if (room && room.roomId === event.roomId) {
        this.api.getRoom(event.roomId).subscribe((r) => {
          console.log('[GameState] Refreshed room after playerLeft:', r);
          this._currentRoom.set(r);
        });
      }
    });

    // Game started
    this.hub.gameStarted$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: gameStarted', event);
      this._gameId.set(event.gameId);
      // Also refresh the room to get updated status
      const room = this._currentRoom();
      if (room) {
        this.api.getRoom(room.roomId).subscribe((r) => {
          console.log('[GameState] Refreshed room after gameStarted:', r);
          this._currentRoom.set(r);
        });
      }
      this.refreshState();
    });

    // Deal started
    this.hub.dealStarted$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: dealStarted', event);
      this.hideDealSummary();
      this.refreshState();
    });

    // Your turn
    this.hub.yourTurn$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: yourTurn', event);
      this.refreshState();
    });

    // Player turn (broadcast)
    this.hub.playerTurn$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: playerTurn', event);
      this.refreshState();
    });

    // Card played
    this.hub.cardPlayed$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: cardPlayed', event);
      this.refreshState();
    });

    // Trick completed
    this.hub.trickCompleted$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: trickCompleted', event);
      this.refreshState();
    });

    // Deal ended
    this.hub.dealEnded$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: dealEnded', event);
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
    this.hub.matchEnded$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: matchEnded', event);
      this.refreshState();
    });
  }
}
