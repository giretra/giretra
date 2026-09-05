import { Injectable, NgZone, OnDestroy, inject, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  IRetryPolicy,
  LogLevel,
  RetryContext,
} from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from '../core/services/auth.service';

export type ConnectionStatus = 'connected' | 'reconnecting' | 'disconnected';

/**
 * Reconnect schedule that never gives up. SignalR's default policy stops after
 * four attempts (about 45 s), which on a phone means any screen lock or cell
 * handover ends with a dead table and a manual Retry button. Quick attempts
 * first, then a steady cadence; the visibility handler below forces an
 * immediate attempt when the page comes back to the foreground, since browsers
 * throttle timers in background tabs.
 */
const RECONNECT_DELAYS_MS = [0, 1000, 2000, 5000];
const RECONNECT_STEADY_DELAY_MS = 10_000;
/** Delay before retrying when a fresh connection attempt itself fails. */
const RESUME_RETRY_DELAY_MS = 5000;

class EndlessRetryPolicy implements IRetryPolicy {
  nextRetryDelayInMilliseconds(context: RetryContext): number | null {
    return RECONNECT_DELAYS_MS[context.previousRetryCount] ?? RECONNECT_STEADY_DELAY_MS;
  }
}
import {
  AchievementsEarnedEvent,
  CardPlayedEvent,
  ChatHistoryResponse,
  ChatMessageEvent,
  ChatStatusChangedEvent,
  DealEndedEvent,
  DealStartedEvent,
  GameHubEventNames,
  GameStartedEvent,
  MatchAbandonedEvent,
  MatchEndedEvent,
  PendingFriendCountChangedEvent,
  PlayerJoinedEvent,
  PlayerKickedEvent,
  PlayerLeftEvent,
  PlayerTurnEvent,
  RoomIdleClosedEvent,
  RoomResetEvent,
  SeatModeChangedEvent,
  TrickCompletedEvent,
  YourTurnEvent,
} from './generated/signalr-types.generated';

@Injectable({
  providedIn: 'root',
})
export class GameHubService implements OnDestroy {
  private readonly ngZone = inject(NgZone);
  private readonly auth = inject(AuthService);
  private hubConnection: HubConnection | null = null;
  private hubUrl: string | null = null;
  /** Set by disconnect(); suppresses every automatic resume until the next connect(). */
  private manuallyDisconnected = false;
  private resumeInFlight: Promise<void> | null = null;
  private resumeRetryTimer: ReturnType<typeof setTimeout> | null = null;

  // Connection status
  private readonly _connectionStatus = signal<ConnectionStatus>('disconnected');
  readonly connectionStatus = this._connectionStatus.asReadonly();
  /** Emitted after the connection was re-established (automatically or by resume()). */
  readonly reconnected$ = new Subject<void>();
  /**
   * Emitted when the page returns to the foreground while the connection looks
   * healthy. A socket that sat in a background tab may be half-dead, or events
   * may have been dropped, so listeners should re-sync their state cheaply.
   */
  readonly resumed$ = new Subject<void>();

  constructor() {
    if (typeof document !== 'undefined') {
      document.addEventListener('visibilitychange', this.onVisibilityChange);
      window.addEventListener('online', this.onOnline);
      window.addEventListener('pageshow', this.onPageShow);
    }
  }

  // Event subjects
  readonly playerJoined$ = new Subject<PlayerJoinedEvent>();
  readonly playerLeft$ = new Subject<PlayerLeftEvent>();
  readonly gameStarted$ = new Subject<GameStartedEvent>();
  readonly dealStarted$ = new Subject<DealStartedEvent>();
  readonly dealEnded$ = new Subject<DealEndedEvent>();
  readonly yourTurn$ = new Subject<YourTurnEvent>();
  readonly playerTurn$ = new Subject<PlayerTurnEvent>();
  readonly cardPlayed$ = new Subject<CardPlayedEvent>();
  readonly trickCompleted$ = new Subject<TrickCompletedEvent>();
  readonly matchEnded$ = new Subject<MatchEndedEvent>();
  readonly matchAbandoned$ = new Subject<MatchAbandonedEvent>();
  readonly achievementsEarned$ = new Subject<AchievementsEarnedEvent>();
  readonly playerKicked$ = new Subject<PlayerKickedEvent>();
  readonly seatModeChanged$ = new Subject<SeatModeChangedEvent>();
  readonly roomIdleClosed$ = new Subject<RoomIdleClosedEvent>();
  readonly roomReset$ = new Subject<RoomResetEvent>();
  readonly roomsChanged$ = new Subject<void>();
  readonly pendingFriendCountChanged$ = new Subject<PendingFriendCountChangedEvent>();
  readonly chatMessageReceived$ = new Subject<ChatMessageEvent>();
  readonly chatStatusChanged$ = new Subject<ChatStatusChangedEvent>();

  async connect(hubUrl: string): Promise<void> {
    this.hubUrl = hubUrl;
    this.manuallyDisconnected = false;
    this.clearResumeRetry();

    if (this.hubConnection?.state === HubConnectionState.Connected) {
      console.log('[Hub] Already connected');
      return;
    }

    await this.startNewConnection(hubUrl);
    this.ngZone.run(() => this._connectionStatus.set('connected'));
    console.log('[Hub] Connected successfully');
  }

  async disconnect(): Promise<void> {
    this.manuallyDisconnected = true;
    this.clearResumeRetry();
    if (this.hubConnection) {
      const connection = this.hubConnection;
      this.hubConnection = null;
      await connection.stop().catch(() => {});
    }
    this._connectionStatus.set('disconnected');
  }

  /**
   * Builds and starts a fresh connection, tearing down any previous one first.
   * Used both by the initial connect() and by resume(), which needs to abort a
   * reconnect loop whose timers were throttled while the page was hidden.
   */
  private async startNewConnection(hubUrl: string): Promise<void> {
    const previous = this.hubConnection;
    this.hubConnection = null;
    if (previous && previous.state !== HubConnectionState.Disconnected) {
      await previous.stop().catch(() => {});
    }

    console.log('[Hub] Connecting to', hubUrl);
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => this.auth.getToken() })
      .withAutomaticReconnect(new EndlessRetryPolicy())
      .configureLogging(LogLevel.Information)
      .build();
    this.hubConnection = connection;

    this.registerEventHandlers();
    this.registerConnectionHandlers();

    try {
      await connection.start();
    } catch (e) {
      if (this.hubConnection === connection) this.hubConnection = null;
      throw e;
    }
  }

  /**
   * Bring the connection back after the page was hidden, the network came back,
   * or the connection closed for good. Idempotent: concurrent callers share one
   * attempt. Does nothing after an explicit disconnect().
   */
  private resume(reason: string): Promise<void> {
    if (this.manuallyDisconnected || !this.hubUrl) return Promise.resolve();
    if (this.resumeInFlight) return this.resumeInFlight;

    const hubUrl = this.hubUrl;
    this.resumeInFlight = (async () => {
      this.clearResumeRetry();
      const state = this.hubConnection?.state;
      if (state === HubConnectionState.Connected) {
        console.log('[Hub] Page resumed while connected, asking listeners to re-sync');
        this.ngZone.run(() => this.resumed$.next());
        return;
      }
      if (state === HubConnectionState.Connecting) {
        // An initial connect() is still in progress; let it finish.
        return;
      }

      console.log('[Hub] Resuming connection', { reason, state });
      this.ngZone.run(() => this._connectionStatus.set('reconnecting'));
      try {
        await this.startNewConnection(hubUrl);
        this.ngZone.run(() => {
          this._connectionStatus.set('connected');
          this.reconnected$.next();
        });
        console.log('[Hub] Resumed successfully');
      } catch (e) {
        console.warn('[Hub] Resume failed, retrying shortly', e);
        this.scheduleResumeRetry();
      }
    })().finally(() => {
      this.resumeInFlight = null;
    });
    return this.resumeInFlight;
  }

  private scheduleResumeRetry(): void {
    this.clearResumeRetry();
    if (this.manuallyDisconnected) return;
    // Keep the "reconnecting" banner: an attempt is still coming.
    this.ngZone.run(() => this._connectionStatus.set('reconnecting'));
    this.resumeRetryTimer = setTimeout(() => {
      this.resumeRetryTimer = null;
      this.resume('retry');
    }, RESUME_RETRY_DELAY_MS);
  }

  private clearResumeRetry(): void {
    if (this.resumeRetryTimer) {
      clearTimeout(this.resumeRetryTimer);
      this.resumeRetryTimer = null;
    }
  }

  private readonly onVisibilityChange = (): void => {
    if (document.visibilityState === 'visible') {
      this.resume('visible');
    }
  };

  private readonly onOnline = (): void => {
    this.resume('online');
  };

  private readonly onPageShow = (event: PageTransitionEvent): void => {
    // Restored from the back/forward cache: the socket did not survive.
    if (event.persisted) this.resume('pageshow');
  };

  async joinRoom(roomId: string, clientId: string): Promise<void> {
    if (!this.hubConnection) {
      throw new Error('Not connected to hub');
    }
    await this.hubConnection.invoke('JoinRoom', roomId, clientId);
  }

  async leaveRoom(roomId: string, clientId: string): Promise<void> {
    if (!this.hubConnection) {
      throw new Error('Not connected to hub');
    }
    await this.hubConnection.invoke('LeaveRoom', roomId, clientId);
  }

  async joinLobby(): Promise<void> {
    if (!this.hubConnection) {
      throw new Error('Not connected to hub');
    }
    await this.hubConnection.invoke('JoinLobby');
  }

  async leaveLobby(): Promise<void> {
    if (!this.hubConnection) {
      throw new Error('Not connected to hub');
    }
    await this.hubConnection.invoke('LeaveLobby');
  }

  async sendChatMessage(roomId: string, clientId: string, content: string): Promise<void> {
    if (!this.hubConnection) {
      throw new Error('Not connected to hub');
    }
    await this.hubConnection.invoke('SendChatMessage', roomId, clientId, content);
  }

  async getChatHistory(roomId: string): Promise<ChatHistoryResponse> {
    if (!this.hubConnection) {
      throw new Error('Not connected to hub');
    }
    return await this.hubConnection.invoke('GetChatHistory', roomId);
  }

  get connectionState(): HubConnectionState {
    return this.hubConnection?.state ?? HubConnectionState.Disconnected;
  }

  ngOnDestroy(): void {
    if (typeof document !== 'undefined') {
      document.removeEventListener('visibilitychange', this.onVisibilityChange);
      window.removeEventListener('online', this.onOnline);
      window.removeEventListener('pageshow', this.onPageShow);
    }
    this.disconnect();
    this.reconnected$.complete();
    this.resumed$.complete();
    this.playerJoined$.complete();
    this.playerLeft$.complete();
    this.gameStarted$.complete();
    this.dealStarted$.complete();
    this.dealEnded$.complete();
    this.yourTurn$.complete();
    this.playerTurn$.complete();
    this.cardPlayed$.complete();
    this.trickCompleted$.complete();
    this.matchEnded$.complete();
    this.matchAbandoned$.complete();
    this.achievementsEarned$.complete();
    this.playerKicked$.complete();
    this.seatModeChanged$.complete();
    this.roomIdleClosed$.complete();
    this.roomReset$.complete();
    this.roomsChanged$.complete();
    this.pendingFriendCountChanged$.complete();
    this.chatMessageReceived$.complete();
    this.chatStatusChanged$.complete();
  }

  private registerConnectionHandlers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.onreconnecting((error) => {
      console.warn('[Hub] Reconnecting...', error);
      this.ngZone.run(() => this._connectionStatus.set('reconnecting'));
    });

    this.hubConnection.onreconnected((connectionId) => {
      console.log('[Hub] Reconnected with connectionId:', connectionId);
      this.ngZone.run(() => {
        this._connectionStatus.set('connected');
        this.reconnected$.next();
      });
    });

    const connection = this.hubConnection;
    connection.onclose((error) => {
      console.error('[Hub] Connection closed', error);
      // A stale connection being replaced by startNewConnection() must not
      // touch the status or trigger another resume.
      if (this.hubConnection !== connection) return;
      this.ngZone.run(() => this._connectionStatus.set('disconnected'));
      // With an endless retry policy this only happens for non-recoverable
      // closes; still try again from scratch rather than leaving a dead table.
      if (!this.manuallyDisconnected) this.scheduleResumeRetry();
    });
  }

  private registerEventHandlers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.on(GameHubEventNames.PlayerJoined, (event: PlayerJoinedEvent) => {
      console.log('[Hub] PlayerJoined', event);
      this.ngZone.run(() => this.playerJoined$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.PlayerLeft, (event: PlayerLeftEvent) => {
      console.log('[Hub] PlayerLeft', event);
      this.ngZone.run(() => this.playerLeft$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.GameStarted, (event: GameStartedEvent) => {
      console.log('[Hub] GameStarted', event);
      this.ngZone.run(() => this.gameStarted$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.DealStarted, (event: DealStartedEvent) => {
      console.log('[Hub] DealStarted', event);
      this.ngZone.run(() => this.dealStarted$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.DealEnded, (event: DealEndedEvent) => {
      console.log('[Hub] DealEnded', event);
      this.ngZone.run(() => this.dealEnded$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.YourTurn, (event: YourTurnEvent) => {
      console.log('[Hub] YourTurn', event);
      this.ngZone.run(() => this.yourTurn$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.PlayerTurn, (event: PlayerTurnEvent) => {
      console.log('[Hub] PlayerTurn', event);
      this.ngZone.run(() => this.playerTurn$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.CardPlayed, (event: CardPlayedEvent) => {
      console.log('[Hub] CardPlayed', event);
      this.ngZone.run(() => this.cardPlayed$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.TrickCompleted, (event: TrickCompletedEvent) => {
      console.log('[Hub] TrickCompleted', event);
      this.ngZone.run(() => this.trickCompleted$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.MatchEnded, (event: MatchEndedEvent) => {
      console.log('[Hub] MatchEnded', event);
      this.ngZone.run(() => this.matchEnded$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.MatchAbandoned, (event: MatchAbandonedEvent) => {
      console.log('[Hub] MatchAbandoned', event);
      this.ngZone.run(() => this.matchAbandoned$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.AchievementsEarned, (event: AchievementsEarnedEvent) => {
      console.log('[Hub] AchievementsEarned', event);
      this.ngZone.run(() => this.achievementsEarned$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.PlayerKicked, (event: PlayerKickedEvent) => {
      console.log('[Hub] PlayerKicked', event);
      this.ngZone.run(() => this.playerKicked$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.SeatModeChanged, (event: SeatModeChangedEvent) => {
      console.log('[Hub] SeatModeChanged', event);
      this.ngZone.run(() => this.seatModeChanged$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.RoomIdleClosed, (event: RoomIdleClosedEvent) => {
      console.log('[Hub] RoomIdleClosed', event);
      this.ngZone.run(() => this.roomIdleClosed$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.RoomReset, (event: RoomResetEvent) => {
      console.log('[Hub] RoomReset', event);
      this.ngZone.run(() => this.roomReset$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.RoomsChanged, () => {
      console.log('[Hub] RoomsChanged');
      this.ngZone.run(() => this.roomsChanged$.next());
    });

    this.hubConnection.on(GameHubEventNames.PendingFriendCountChanged, (event: PendingFriendCountChangedEvent) => {
      console.log('[Hub] PendingFriendCountChanged', event);
      this.ngZone.run(() => this.pendingFriendCountChanged$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.ChatMessageReceived, (event: ChatMessageEvent) => {
      console.log('[Hub] ChatMessageReceived', event);
      this.ngZone.run(() => this.chatMessageReceived$.next(event));
    });

    this.hubConnection.on(GameHubEventNames.ChatStatusChanged, (event: ChatStatusChangedEvent) => {
      console.log('[Hub] ChatStatusChanged', event);
      this.ngZone.run(() => this.chatStatusChanged$.next(event));
    });
  }
}
