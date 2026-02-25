import { Injectable, inject, signal, computed, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  CardResponse,
  CardPointsBreakdownResponse,
  GameMode,
  PlayerPosition,
  Team,
  PendingActionType,
} from '../../api/generated/signalr-types.generated';
import { GameHubService } from '../../api/game-hub.service';
import { ApiService, EloChangeResponse, GameStateResponse, PlayerStateResponse, RoomResponse, ValidAction, TrickResponse, NegotiationAction } from './api.service';
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

  // Kick state
  private readonly _wasKicked = signal<boolean>(false);
  readonly wasKicked = this._wasKicked.asReadonly();

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
    team1Breakdown: CardPointsBreakdownResponse;
    team2Breakdown: CardPointsBreakdownResponse;
  } | null>(null);

  readonly showingDealSummary = this._showingDealSummary.asReadonly();
  readonly dealSummary = this._dealSummary.asReadonly();

  // ─────────────────────────────────────────────────────────────────────────
  // Completed Trick Display State (brief pause after trick ends)
  // ─────────────────────────────────────────────────────────────────────────
  private readonly _showingCompletedTrick = signal<boolean>(false);
  private readonly _completedTrickToShow = signal<TrickResponse | null>(null);
  private readonly _isLastTrick = signal<boolean>(false);
  private _completedTrickTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private readonly COMPLETED_TRICK_DELAY_MS = 3000;

  // Pending deal summary to show after last trick is dismissed
  private _pendingDealSummary: {
    gameMode: GameMode;
    team1CardPoints: number;
    team2CardPoints: number;
    team1MatchPointsEarned: number;
    team2MatchPointsEarned: number;
    team1TotalMatchPoints: number;
    team2TotalMatchPoints: number;
    wasSweep: boolean;
    sweepingTeam: Team | null;
    team1Breakdown: CardPointsBreakdownResponse;
    team2Breakdown: CardPointsBreakdownResponse;
  } | null = null;
  private readonly _hasPendingDealSummary = signal(false);

  readonly showingCompletedTrick = this._showingCompletedTrick.asReadonly();
  readonly isLastTrick = this._isLastTrick.asReadonly();
  readonly completedTrickToShow = this._completedTrickToShow.asReadonly();

  // ─────────────────────────────────────────────────────────────────────────
  // Turn Timer State
  // ─────────────────────────────────────────────────────────────────────────
  private readonly _turnTimeoutAt = signal<Date | null>(null);
  readonly turnTimeoutAt = this._turnTimeoutAt.asReadonly();

  // ─────────────────────────────────────────────────────────────────────────
  // Idle Timeout State
  // ─────────────────────────────────────────────────────────────────────────
  private readonly _idleDeadline = signal<Date | null>(null);
  readonly idleDeadline = this._idleDeadline.asReadonly();

  private readonly _roomIdleClosed = signal<boolean>(false);
  readonly roomIdleClosed = this._roomIdleClosed.asReadonly();

  // ─────────────────────────────────────────────────────────────────────────
  // Computed Signals: Derived State
  // ─────────────────────────────────────────────────────────────────────────

  /** Current high-level phase for UI rendering */
  readonly phase = computed<GamePhase>(() => {
    const gameState = this._gameState();
    const room = this._currentRoom();
    const showingSummary = this._showingDealSummary();
    const showingCompletedTrick = this._showingCompletedTrick();
    const hasPendingDealSummary = this._hasPendingDealSummary();

    console.log('[GameState] phase computed:', {
      gameStateIsComplete: gameState?.isComplete,
      showingSummary,
      showingCompletedTrick,
      hasPendingDealSummary,
      roomStatus: room?.status,
      gamePhase: gameState?.phase,
      hasGame: !!gameState,
    });

    // Deal summary overlay takes highest priority — user must see scoring first
    if (showingSummary) return 'dealSummary';

    // If showing the last completed trick, stay in playing phase until dismissed
    if (showingCompletedTrick) {
      console.log('[GameState] → phase = playing (showing completed trick)');
      return 'playing';
    }

    // Match end — only after deal summary has been shown and dismissed
    if (gameState?.isComplete && !hasPendingDealSummary) return 'matchEnd';

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
        result = (gameState.isComplete && !hasPendingDealSummary) ? 'matchEnd' : 'dealSummary';
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
      case 'ContinueDeal':
        result = PendingActionType.ContinueDeal;
        break;
      case 'ContinueMatch':
        result = PendingActionType.ContinueMatch;
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

  /** Team 1 tricks won this deal */
  readonly team1TricksWon = computed(() => {
    const tricks = this.completedTricks();
    return tricks.filter(t =>
      t.winner === PlayerPosition.Bottom || t.winner === PlayerPosition.Top
    ).length;
  });

  /** Team 2 tricks won this deal */
  readonly team2TricksWon = computed(() => {
    const tricks = this.completedTricks();
    return tricks.filter(t =>
      t.winner === PlayerPosition.Left || t.winner === PlayerPosition.Right
    ).length;
  });

  /** Tricks won by player position */
  readonly tricksWonByPosition = computed(() => {
    const tricks = this.completedTricks();
    const counts: Record<PlayerPosition, number> = {
      [PlayerPosition.Bottom]: 0,
      [PlayerPosition.Top]: 0,
      [PlayerPosition.Left]: 0,
      [PlayerPosition.Right]: 0,
    };
    for (const trick of tricks) {
      if (trick.winner) {
        counts[trick.winner]++;
      }
    }
    return counts;
  });

  /** Match winner */
  readonly matchWinner = computed(() => this._gameState()?.winner ?? null);

  /** Elo change for the current player */
  readonly myEloChange = computed<EloChangeResponse | null>(() => {
    const pos = this.myPosition();
    const changes = this._gameState()?.eloChanges;
    if (!pos || !changes) return null;
    return changes[pos] ?? null;
  });

  /** Whether the current room is ranked */
  readonly isRanked = computed(() => this._currentRoom()?.isRanked ?? false);

  /** Is watcher mode? */
  readonly isWatcher = computed(() => this.session.isWatcher());

  /** Connection status (from hub) */
  readonly connectionStatus = computed(() => this.hub.connectionStatus());

  /** Is an action currently being submitted? */
  private readonly _isSubmittingAction = signal(false);
  readonly isSubmittingAction = this._isSubmittingAction.asReadonly();

  /** Mark action submission start */
  beginSubmit(): void {
    this._isSubmittingAction.set(true);
  }

  /** Mark action submission end */
  endSubmit(): void {
    this._isSubmittingAction.set(false);
  }

  constructor() {
    this.setupHubListeners();
    this.setupReconnectHandler();
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
    this._isCreator.set(room.isOwner ?? isCreator);
    this._gameId.set(room.gameId);
    this._idleDeadline.set(room.idleDeadline ? new Date(room.idleDeadline) : null);

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
    this._wasKicked.set(false);
    this._roomIdleClosed.set(false);
    this._showingDealSummary.set(false);
    this._dealSummary.set(null);
    this._hasPendingDealSummary.set(false);
    this._pendingDealSummary = null;
    this._turnTimeoutAt.set(null);
    this._idleDeadline.set(null);
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
          this._turnTimeoutAt.set(
            watcherState.gameState.pendingActionTimeoutAt
              ? new Date(watcherState.gameState.pendingActionTimeoutAt)
              : null
          );
        }
      } else if (clientId) {
        const playerState = await this.api.getPlayerState(gameId, clientId).toPromise();
        console.log('[GameState] Player state received', playerState);
        if (playerState) {
          this._playerState.set(playerState);
          this._gameState.set(playerState.gameState);
          this._turnTimeoutAt.set(
            playerState.gameState.pendingActionTimeoutAt
              ? new Date(playerState.gameState.pendingActionTimeoutAt)
              : null
          );
        }
      }
    } catch (e: any) {
      console.error('Failed to refresh game state', e);

      // If player state fetch failed (e.g. 404 — clientId not recognized after disconnect),
      // attempt to rejoin the room and retry
      if (!isWatcher && clientId) {
        await this.attemptRejoin();
      }
    }
  }

  /**
   * Attempt to rejoin the current room after a failed state refresh.
   * This handles the case where the player's clientId is no longer valid
   * (e.g., after a long disconnect where the grace period expired).
   */
  private async attemptRejoin(): Promise<void> {
    const room = this._currentRoom();
    const gameId = this._gameId();
    if (!room || room.status !== 'Playing' || !gameId) return;

    console.log('[GameState] Attempting rejoin for room', room.roomId);

    try {
      const response = await this.api.rejoinRoom(room.roomId).toPromise();
      if (!response) return;

      console.log('[GameState] Rejoin succeeded', response);

      // Update session with new clientId + position
      if (response.position) {
        this.session.joinRoom(room.roomId, response.clientId, response.position);
      }

      // Update room data
      this._currentRoom.set(response.room);
      this._gameId.set(response.room.gameId);

      // Re-join SignalR with new clientId
      try {
        await this.hub.joinRoom(room.roomId, response.clientId);
      } catch (e) {
        console.warn('[GameState] Failed to re-join SignalR after rejoin', e);
      }

      // Retry state refresh
      const newGameId = response.room.gameId;
      if (newGameId && response.clientId) {
        const playerState = await this.api.getPlayerState(newGameId, response.clientId).toPromise();
        if (playerState) {
          this._playerState.set(playerState);
          this._gameState.set(playerState.gameState);
          this._turnTimeoutAt.set(
            playerState.gameState.pendingActionTimeoutAt
              ? new Date(playerState.gameState.pendingActionTimeoutAt)
              : null
          );
        }
      }
    } catch (e) {
      console.warn('[GameState] Rejoin attempt failed', e);
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
    team1Breakdown: CardPointsBreakdownResponse;
    team2Breakdown: CardPointsBreakdownResponse;
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
    this._isLastTrick.set(false);

    // If there's a pending deal summary (last trick case), show it now
    if (this._pendingDealSummary) {
      this.showDealSummary(this._pendingDealSummary);
      this._pendingDealSummary = null;
      this._hasPendingDealSummary.set(false);
    } else {
      // Now refresh state to get the next trick
      this.refreshState();
    }
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
      this._idleDeadline.set(null);
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
      this._turnTimeoutAt.set(null);
      this.hideDealSummary();
      this.refreshState();
    });

    // Your turn
    this.hub.yourTurn$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: yourTurn', event);
      this._turnTimeoutAt.set(new Date(event.timeoutAt));
      this.refreshState();
    });

    // Player turn (broadcast)
    this.hub.playerTurn$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: playerTurn', event);
      this._turnTimeoutAt.set(new Date(event.timeoutAt));
      this.refreshState();
    });

    // Card played
    this.hub.cardPlayed$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: cardPlayed', event);
      this._turnTimeoutAt.set(null);
      this.refreshState();
    });

    // Trick completed - show cards briefly before clearing
    this.hub.trickCompleted$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: trickCompleted', event);
      this._turnTimeoutAt.set(null);

      // Clear any existing timeout
      if (this._completedTrickTimeoutId) {
        clearTimeout(this._completedTrickTimeoutId);
      }

      // Convert SignalR TrickResponse to API TrickResponse format
      const trick: TrickResponse = {
        leader: event.trick.leader,
        trickNumber: event.trick.trickNumber,
        playedCards: event.trick.playedCards,
        isComplete: event.trick.isComplete,
        winner: event.trick.winner ?? null,
      };

      // Detect if this is the last trick (trick 8)
      const isLastTrick = trick.trickNumber === 8;
      this._isLastTrick.set(isLastTrick);

      // Store the completed trick and show it
      this._completedTrickToShow.set(trick);
      this._showingCompletedTrick.set(true);

      // For non-last tricks, auto-dismiss after delay
      // For last trick, wait for user to click (no timeout)
      // Watchers always auto-dismiss (they don't interact)
      if (!isLastTrick || this.session.isWatcher()) {
        this._completedTrickTimeoutId = setTimeout(() => {
          this._completedTrickTimeoutId = null;
          this._showingCompletedTrick.set(false);
          this._completedTrickToShow.set(null);
          this.refreshState();
        }, this.COMPLETED_TRICK_DELAY_MS);
      }
    });

    // Deal ended
    this.hub.dealEnded$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: dealEnded', event);
      this._turnTimeoutAt.set(null);

      const summary = {
        gameMode: event.gameMode,
        team1CardPoints: event.team1CardPoints,
        team2CardPoints: event.team2CardPoints,
        team1MatchPointsEarned: event.team1MatchPointsEarned,
        team2MatchPointsEarned: event.team2MatchPointsEarned,
        team1TotalMatchPoints: event.team1TotalMatchPoints,
        team2TotalMatchPoints: event.team2TotalMatchPoints,
        wasSweep: event.wasSweep,
        sweepingTeam: event.sweepingTeam ?? null,
        team1Breakdown: event.team1Breakdown,
        team2Breakdown: event.team2Breakdown,
      };

      // If last trick is still showing, store the summary to show after user dismisses it
      if (this._isLastTrick() && this._showingCompletedTrick()) {
        this._pendingDealSummary = summary;
        this._hasPendingDealSummary.set(true);
      } else {
        this.showDealSummary(summary);
      }
    });

    // Match ended
    this.hub.matchEnded$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: matchEnded', event);
      this._turnTimeoutAt.set(null);
      this.refreshState();
      // Refresh room to pick up new idleDeadline (room resets to Waiting after match)
      const room = this._currentRoom();
      if (room) {
        this.api.getRoom(room.roomId).subscribe((r) => {
          this._currentRoom.set(r);
          this._idleDeadline.set(r.idleDeadline ? new Date(r.idleDeadline) : null);
        });
      }
    });

    // Player kicked
    this.hub.playerKicked$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: playerKicked', event);
      const room = this._currentRoom();
      if (room && room.roomId === event.roomId) {
        // If I was kicked (my position matches)
        const myPos = this.session.position();
        if (myPos && myPos === event.position) {
          this._wasKicked.set(true);
        }
        // Always refresh room data
        this.api.getRoom(event.roomId).subscribe((r) => {
          this._currentRoom.set(r);
        });
      }
    });

    // Seat mode changed
    this.hub.seatModeChanged$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: seatModeChanged', event);
      const room = this._currentRoom();
      if (room && room.roomId === event.roomId) {
        this.api.getRoom(event.roomId).subscribe((r) => {
          this._currentRoom.set(r);
        });
      }
    });

    // Room idle closed
    this.hub.roomIdleClosed$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      console.log('[GameState] Hub event: roomIdleClosed', event);
      const room = this._currentRoom();
      if (room && room.roomId === event.roomId) {
        this._roomIdleClosed.set(true);
      }
    });
  }

  private setupReconnectHandler(): void {
    this.hub.reconnected$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(async () => {
      console.log('[GameState] Hub reconnected — re-joining room and refreshing state');
      const roomId = this._currentRoom()?.roomId;
      const clientId = this.session.clientId();

      if (roomId && clientId) {
        try {
          await this.hub.joinRoom(roomId, clientId);
          console.log('[GameState] Re-joined room after reconnect');
        } catch (e) {
          console.warn('[GameState] Failed to re-join room after reconnect, attempting rejoin', e);
          // If joining fails, the clientId might be stale — try rejoin
          await this.attemptRejoin();
          return; // attemptRejoin already refreshes state
        }
      }

      await this.refreshState();
    });
  }
}
